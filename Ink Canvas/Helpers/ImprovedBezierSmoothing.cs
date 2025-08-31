using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 改进的三次贝塞尔曲线平滑算法
    /// </summary>
    public class ImprovedBezierSmoothing
    {
        private readonly InkSmoothingConfig _config;

        public ImprovedBezierSmoothing(InkSmoothingConfig config = null)
        {
            _config = config ?? new InkSmoothingConfig();
        }

        /// <summary>
        /// 使用改进的贝塞尔曲线算法平滑笔画
        /// </summary>
        public Stroke SmoothStroke(Stroke originalStroke)
        {
            if (originalStroke == null || originalStroke.StylusPoints.Count < 3)
                return originalStroke;

            var originalPoints = originalStroke.StylusPoints.ToArray();
            
            // 预处理：去除噪声点
            var cleanedPoints = RemoveNoisePoints(originalPoints);
            
            // 使用改进的贝塞尔曲线拟合
            var smoothedPoints = ApplyCubicBezierSmoothing(cleanedPoints);
            
            // 后处理：重采样和优化
            var finalPoints = PostProcessPoints(smoothedPoints);

            return new Stroke(new StylusPointCollection(finalPoints))
            {
                DrawingAttributes = originalStroke.DrawingAttributes.Clone()
            };
        }

        /// <summary>
        /// 去除噪声点
        /// </summary>
        private StylusPoint[] RemoveNoisePoints(StylusPoint[] points)
        {
            if (points.Length < 3) return points;

            var result = new List<StylusPoint> { points[0] };
            double minDistance = _config.ResampleInterval * 0.5;

            for (int i = 1; i < points.Length - 1; i++)
            {
                var prev = result[result.Count - 1];
                var curr = points[i];
                var next = points[i + 1];

                // 计算到前一个点的距离
                double distToPrev = Math.Sqrt((curr.X - prev.X) * (curr.X - prev.X) + 
                                            (curr.Y - prev.Y) * (curr.Y - prev.Y));

                // 如果距离太近，跳过这个点
                if (distToPrev < minDistance)
                    continue;

                // 检查是否为异常点（与前后点形成锐角）
                if (IsOutlierPoint(prev, curr, next))
                    continue;

                result.Add(curr);
            }

            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        /// <summary>
        /// 检查是否为异常点
        /// </summary>
        private bool IsOutlierPoint(StylusPoint prev, StylusPoint curr, StylusPoint next)
        {
            var v1 = new Vector(curr.X - prev.X, curr.Y - prev.Y);
            var v2 = new Vector(next.X - curr.X, next.Y - curr.Y);

            if (v1.Length == 0 || v2.Length == 0) return false;

            v1.Normalize();
            v2.Normalize();

            double dotProduct = Vector.Multiply(v1, v2);
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, dotProduct)));

            // 如果角度小于30度，认为是异常点
            return angle < Math.PI / 6;
        }

        /// <summary>
        /// 应用三次贝塞尔曲线平滑
        /// </summary>
        private StylusPoint[] ApplyCubicBezierSmoothing(StylusPoint[] points)
        {
            if (points.Length < 4) return points;

            var result = new List<StylusPoint>();
            result.Add(points[0]);

            // 使用滑动窗口进行贝塞尔曲线拟合
            for (int i = 0; i <= points.Length - 4; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];

                // 计算控制点
                var controlPoints = CalculateOptimalControlPoints(p0, p1, p2, p3);

                // 计算插值步数
                int steps = CalculateInterpolationSteps(p0, p1, p2, p3);

                // 生成贝塞尔曲线点
                for (int j = 1; j <= steps; j++)
                {
                    double t = (double)j / steps;
                    var bezierPoint = CalculateBezierPoint(p0, controlPoints.cp1, controlPoints.cp2, p3, t);
                    result.Add(bezierPoint);
                }
            }

            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        /// <summary>
        /// 计算最优控制点
        /// </summary>
        private (Point cp1, Point cp2) CalculateOptimalControlPoints(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 计算切线方向
            var tangent1 = CalculateTangent(p0, p1, p2);
            var tangent2 = CalculateTangent(p1, p2, p3);

            // 计算控制点距离
            double dist1 = CalculateDistance(p0, p1);
            double dist2 = CalculateDistance(p2, p3);
            
            double controlDist1 = dist1 * _config.CurveTension;
            double controlDist2 = dist2 * _config.CurveTension;

            // 计算控制点
            var cp1 = new Point(
                p1.X + tangent1.X * controlDist1,
                p1.Y + tangent1.Y * controlDist1
            );

            var cp2 = new Point(
                p2.X - tangent2.X * controlDist2,
                p2.Y - tangent2.Y * controlDist2
            );

            return (cp1, cp2);
        }

        /// <summary>
        /// 计算切线方向
        /// </summary>
        private Vector CalculateTangent(StylusPoint p0, StylusPoint p1, StylusPoint p2)
        {
            var v1 = new Vector(p1.X - p0.X, p1.Y - p0.Y);
            var v2 = new Vector(p2.X - p1.X, p2.Y - p1.Y);

            // 如果向量长度为零，返回零向量
            if (v1.Length == 0 || v2.Length == 0)
                return new Vector(0, 0);

            v1.Normalize();
            v2.Normalize();

            // 返回平均方向
            var tangent = (v1 + v2) / 2;
            if (tangent.Length > 0)
                tangent.Normalize();

            return tangent;
        }

        /// <summary>
        /// 计算两点间距离
        /// </summary>
        private double CalculateDistance(StylusPoint p1, StylusPoint p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算插值步数
        /// </summary>
        private int CalculateInterpolationSteps(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            if (!_config.UseAdaptiveInterpolation)
                return _config.InterpolationSteps;

            // 计算曲线长度
            double totalLength = CalculateDistance(p0, p1) + CalculateDistance(p1, p2) + CalculateDistance(p2, p3);

            // 计算曲率
            double curvature = CalculateCurvature(p0, p1, p2, p3);

            // 基于长度和曲率计算步数
            int baseSteps = Math.Max(8, Math.Min(20, (int)(totalLength / 10)));
            int curvatureSteps = (int)(curvature * 15);

            return Math.Max(_config.InterpolationSteps, Math.Min(30, baseSteps + curvatureSteps));
        }

        /// <summary>
        /// 计算曲率
        /// </summary>
        private double CalculateCurvature(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            var v1 = new Vector(p1.X - p0.X, p1.Y - p0.Y);
            var v2 = new Vector(p2.X - p1.X, p2.Y - p1.Y);
            var v3 = new Vector(p3.X - p2.X, p3.Y - p2.Y);

            if (v1.Length == 0 || v2.Length == 0 || v3.Length == 0) return 0;

            v1.Normalize();
            v2.Normalize();
            v3.Normalize();

            // 计算角度变化
            double angle1 = Math.Acos(Math.Max(-1, Math.Min(1, Vector.Multiply(v1, v2))));
            double angle2 = Math.Acos(Math.Max(-1, Math.Min(1, Vector.Multiply(v2, v3))));

            return (angle1 + angle2) / Math.PI; // 归一化到0-1
        }

        /// <summary>
        /// 计算贝塞尔曲线上的点
        /// </summary>
        private StylusPoint CalculateBezierPoint(StylusPoint p0, Point cp1, Point cp2, StylusPoint p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            // 预计算系数
            double c0 = uuu;
            double c1 = 3 * uu * t;
            double c2 = 3 * u * tt;
            double c3 = ttt;

            double x = c0 * p0.X + c1 * cp1.X + c2 * cp2.X + c3 * p3.X;
            double y = c0 * p0.Y + c1 * cp1.Y + c2 * cp2.Y + c3 * p3.Y;

            // 插值压力值
            float pressure = (float)(p0.PressureFactor * u + p3.PressureFactor * t);
            pressure = Math.Max(pressure, 0.1f);

            return new StylusPoint(x, y, pressure);
        }

        /// <summary>
        /// 后处理点集
        /// </summary>
        private StylusPoint[] PostProcessPoints(StylusPoint[] points)
        {
            if (points.Length == 0) return points;

            // 如果点数过多，进行重采样
            if (points.Length > _config.MaxPointsPerStroke)
            {
                return ResamplePoints(points, _config.ResampleInterval);
            }

            return points;
        }

        /// <summary>
        /// 重采样点集
        /// </summary>
        private StylusPoint[] ResamplePoints(StylusPoint[] points, double interval)
        {
            var result = new List<StylusPoint> { points[0] };
            double accumulated = 0;

            for (int i = 1; i < points.Length; i++)
            {
                var prev = result[result.Count - 1];
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
                    pressure = Math.Max(pressure, 0.1f);

                    result.Add(new StylusPoint(x, y, pressure));
                    accumulated = 0;
                    i--; // 重新处理当前点
                }
                else
                {
                    accumulated += dist;
                }
            }

            return result.ToArray();
        }
    }
} 