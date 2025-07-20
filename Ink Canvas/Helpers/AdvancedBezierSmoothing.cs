using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 适合手写/触摸的墨迹平滑方案：指数平滑+等距重采样+Catmull-Rom样条插值，防止自交和异常填充
    /// </summary>
    public class AdvancedBezierSmoothing
    {
        public double SmoothingStrength { get; set; } = 0.8;
        public double ResampleInterval { get; set; } = 0.8;
        public int InterpolationSteps { get; set; } = 64;

        public Stroke SmoothStroke(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count < 2)
                return stroke;
            var originalPoints = stroke.StylusPoints.ToList();
            var smoothedPoints = ApplyExponentialSmoothing(originalPoints, SmoothingStrength);
            var resampledPoints = ResampleEquidistant(smoothedPoints, ResampleInterval);
            var interpolatedPoints = SlidingBezierFit(resampledPoints, 4, 24);
            var finalPoints = ApplyExponentialSmoothing(interpolatedPoints, 0.5); // 二次平滑
            var ultraSmoothPoints = SlidingWindowSmooth(finalPoints, 7); // 滑动窗口平滑
            var smoothedStroke = new Stroke(new StylusPointCollection(ultraSmoothPoints))
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };
            return smoothedStroke;
        }

        private List<StylusPoint> ApplyExponentialSmoothing(List<StylusPoint> points, double alpha)
        {
            var result = new List<StylusPoint>();
            if (points.Count == 0) return result;
            result.Add(points[0]);
            double lastX = points[0].X;
            double lastY = points[0].Y;
            float lastPressure = points[0].PressureFactor;
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                lastX = alpha * p.X + (1 - alpha) * lastX;
                lastY = alpha * p.Y + (1 - alpha) * lastY;
                lastPressure = (float)(alpha * p.PressureFactor + (1 - alpha) * lastPressure);
                if (lastPressure < 0.1f) lastPressure = 0.1f;
                result.Add(new StylusPoint(lastX, lastY, lastPressure));
            }
            return result;
        }

        private List<StylusPoint> ResampleEquidistant(List<StylusPoint> points, double interval = 2.0)
        {
            var result = new List<StylusPoint>();
            if (points.Count == 0) return result;
            result.Add(points[0]);
            double accumulated = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var prev = result.Last();
                var curr = points[i];
                double dx = curr.X - prev.X;
                double dy = curr.Y - prev.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist + accumulated >= interval)
                {
                    double t = (interval - accumulated) / dist;
                    double x = prev.X + t * dx;
                    double y = prev.Y + t * dy;
                    float pressure = (float)(prev.PressureFactor * (1 - t) + curr.PressureFactor * t);
                    if (pressure < 0.1f) pressure = 0.1f;
                    var newPoint = new StylusPoint(x, y, pressure);
                    result.Add(newPoint);
                    accumulated = 0;
                    i--; // 重新处理当前点
                }
                else
                {
                    accumulated += dist;
                }
            }
            return result;
        }

        private List<StylusPoint> SlidingBezierFit(List<StylusPoint> points, int window = 4, int steps = 24)
        {
            var result = new List<StylusPoint>();
            if (points.Count < window) return points;
            for (int i = 0; i <= points.Count - window; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];
                for (int j = 0; j < steps; j++)
                {
                    double t = (double)j / steps;
                    var pt = CubicBezier(p0, p1, p2, p3, t);
                    result.Add(pt);
                }
            }
            // 保证最后一个点被包含
            result.Add(points.Last());
            return result;
        }

        private StylusPoint CubicBezier(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;
            double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
            double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
            float pressure = (float)(p1.PressureFactor * (1 - t) + p2.PressureFactor * t);
            if (pressure < 0.1f) pressure = 0.1f;
            return new StylusPoint(x, y, pressure);
        }

        private List<StylusPoint> SlidingWindowSmooth(List<StylusPoint> points, int window = 5)
        {
            var result = new List<StylusPoint>();
            int half = window / 2;
            for (int i = 0; i < points.Count; i++)
            {
                double sumX = 0, sumY = 0, sumP = 0;
                int count = 0;
                for (int j = Math.Max(0, i - half); j <= Math.Min(points.Count - 1, i + half); j++)
                {
                    sumX += points[j].X;
                    sumY += points[j].Y;
                    sumP += points[j].PressureFactor;
                    count++;
                }
                result.Add(new StylusPoint(sumX / count, sumY / count, (float)(sumP / count)));
            }
            return result;
        }
    }
} 