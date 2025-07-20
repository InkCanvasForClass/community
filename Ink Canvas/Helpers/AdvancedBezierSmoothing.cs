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
    /// 高级贝塞尔曲线平滑算法
    /// 用于解决墨迹闪烁问题，提供更平滑的笔迹效果
    /// </summary>
    public class AdvancedBezierSmoothing
    {
        /// <summary>
        /// 平滑强度 (0.0 - 1.0)
        /// </summary>
        public double SmoothingStrength { get; set; } = 0.6;

        /// <summary>
        /// 张力参数 (0.0 - 1.0)
        /// </summary>
        public double Tension { get; set; } = 0.5;

        /// <summary>
        /// 是否启用自适应平滑
        /// </summary>
        public bool EnableAdaptiveSmoothing { get; set; } = true;

        /// <summary>
        /// 最小点间距阈值
        /// </summary>
        public double MinPointDistance { get; set; } = 2.0;

        /// <summary>
        /// 最大点间距阈值
        /// </summary>
        public double MaxPointDistance { get; set; } = 50.0;

        /// <summary>
        /// 对笔画进行高级贝塞尔曲线平滑处理
        /// </summary>
        /// <param name="stroke">原始笔画</param>
        /// <returns>平滑后的笔画</returns>
        public Stroke SmoothStroke(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count < 3)
                return stroke;

            var originalPoints = stroke.StylusPoints.ToList();
            var smoothedPoints = new List<StylusPoint>();

            // 第一步：点过滤和重采样
            var filteredPoints = FilterAndResamplePoints(originalPoints);

            // 第二步：计算控制点
            var controlPoints = CalculateControlPoints(filteredPoints);

            // 第三步：生成平滑曲线点
            var curvePoints = GenerateCurvePoints(filteredPoints, controlPoints);

            // 第四步：创建新的笔画
            var newStylusPoints = new StylusPointCollection(curvePoints);
            var smoothedStroke = new Stroke(newStylusPoints)
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };

            return smoothedStroke;
        }

        /// <summary>
        /// 过滤和重采样点
        /// </summary>
        private List<StylusPoint> FilterAndResamplePoints(List<StylusPoint> points)
        {
            var filteredPoints = new List<StylusPoint>();
            
            if (points.Count == 0) return filteredPoints;

            // 添加第一个点
            filteredPoints.Add(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                var currentPoint = points[i];
                var lastPoint = filteredPoints[filteredPoints.Count - 1];

                double distance = GetDistance(lastPoint.ToPoint(), currentPoint.ToPoint());

                // 如果距离太近，跳过
                if (distance < MinPointDistance)
                    continue;

                // 如果距离太远，插入中间点
                if (distance > MaxPointDistance)
                {
                    int segments = (int)(distance / MaxPointDistance) + 1;
                    for (int j = 1; j < segments; j++)
                    {
                        double ratio = (double)j / segments;
                        var interpolatedPoint = InterpolatePoint(lastPoint, currentPoint, ratio);
                        filteredPoints.Add(interpolatedPoint);
                    }
                }

                filteredPoints.Add(currentPoint);
            }

            return filteredPoints;
        }

        /// <summary>
        /// 计算贝塞尔曲线的控制点
        /// </summary>
        private List<Point> CalculateControlPoints(List<StylusPoint> points)
        {
            var controlPoints = new List<Point>();
            
            if (points.Count < 2) return controlPoints;

            for (int i = 0; i < points.Count; i++)
            {
                Point currentPoint = points[i].ToPoint();
                Point controlPoint;

                if (i == 0)
                {
                    // 第一个点的控制点
                    Point nextPoint = points[i + 1].ToPoint();
                    controlPoint = new Point(
                        currentPoint.X + (nextPoint.X - currentPoint.X) * Tension * 0.5,
                        currentPoint.Y + (nextPoint.Y - currentPoint.Y) * Tension * 0.5
                    );
                }
                else if (i == points.Count - 1)
                {
                    // 最后一个点的控制点
                    Point prevPoint = points[i - 1].ToPoint();
                    controlPoint = new Point(
                        currentPoint.X + (currentPoint.X - prevPoint.X) * Tension * 0.5,
                        currentPoint.Y + (currentPoint.Y - prevPoint.Y) * Tension * 0.5
                    );
                }
                else
                {
                    // 中间点的控制点
                    Point prevPoint = points[i - 1].ToPoint();
                    Point nextPoint = points[i + 1].ToPoint();

                    // 计算切线方向
                    double tangentX = (nextPoint.X - prevPoint.X) * 0.5;
                    double tangentY = (nextPoint.Y - prevPoint.Y) * 0.5;

                    // 应用张力参数
                    controlPoint = new Point(
                        currentPoint.X + tangentX * Tension,
                        currentPoint.Y + tangentY * Tension
                    );
                }

                controlPoints.Add(controlPoint);
            }

            return controlPoints;
        }

        /// <summary>
        /// 生成曲线点
        /// </summary>
        private List<StylusPoint> GenerateCurvePoints(List<StylusPoint> points, List<Point> controlPoints)
        {
            var curvePoints = new List<StylusPoint>();
            
            if (points.Count < 2) return curvePoints;

            // 为每个线段生成贝塞尔曲线点
            for (int i = 0; i < points.Count - 1; i++)
            {
                var startPoint = points[i];
                var endPoint = points[i + 1];
                var startControl = controlPoints[i];
                var endControl = controlPoints[i + 1];

                // 计算自适应步长
                double distance = GetDistance(startPoint.ToPoint(), endPoint.ToPoint());
                int steps = Math.Max(3, Math.Min(20, (int)(distance / 5.0)));

                // 生成贝塞尔曲线点
                for (int j = 0; j <= steps; j++)
                {
                    double t = (double)j / steps;
                    var curvePoint = CalculateBezierPoint(startPoint.ToPoint(), startControl, endControl, endPoint.ToPoint(), t);
                    
                    // 插值压感值
                    float pressure = InterpolatePressure(startPoint.PressureFactor, endPoint.PressureFactor, t);
                    
                    var stylusPoint = new StylusPoint(curvePoint.X, curvePoint.Y, pressure);
                    curvePoints.Add(stylusPoint);
                }
            }

            return curvePoints;
        }

        /// <summary>
        /// 计算贝塞尔曲线上的点
        /// </summary>
        private Point CalculateBezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            Point point = new Point();
            point.X = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
            point.Y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

            return point;
        }

        /// <summary>
        /// 计算两点间距离
        /// </summary>
        private double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }

        /// <summary>
        /// 插值两点间的点
        /// </summary>
        private StylusPoint InterpolatePoint(StylusPoint p1, StylusPoint p2, double ratio)
        {
            return new StylusPoint(
                p1.X + (p2.X - p1.X) * ratio,
                p1.Y + (p2.Y - p1.Y) * ratio,
                InterpolatePressure(p1.PressureFactor, p2.PressureFactor, ratio)
            );
        }

        /// <summary>
        /// 插值压感值
        /// </summary>
        private float InterpolatePressure(float p1, float p2, double ratio)
        {
            return (float)(p1 + (p2 - p1) * ratio);
        }

        /// <summary>
        /// 应用自适应平滑
        /// </summary>
        private void ApplyAdaptiveSmoothing(List<StylusPoint> points)
        {
            if (!EnableAdaptiveSmoothing || points.Count < 3)
                return;

            // 计算笔迹的速度变化
            var speeds = new List<double>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1].ToPoint();
                var curr = points[i].ToPoint();
                var next = points[i + 1].ToPoint();

                double speed1 = GetDistance(prev, curr);
                double speed2 = GetDistance(curr, next);
                double avgSpeed = (speed1 + speed2) / 2.0;

                speeds.Add(avgSpeed);
            }

            // 根据速度调整平滑强度
            if (speeds.Count > 0)
            {
                double avgSpeed = speeds.Average();
                double maxSpeed = speeds.Max();
                double minSpeed = speeds.Min();

                // 速度变化越大，平滑强度越小
                double speedVariation = (maxSpeed - minSpeed) / avgSpeed;
                SmoothingStrength = Math.Max(0.1, Math.Min(0.9, SmoothingStrength * (1.0 - speedVariation * 0.3)));
            }
        }
    }
} 