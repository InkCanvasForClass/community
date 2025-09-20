using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 改进的异步硬件加速墨迹平滑处理器，使用优化的三次贝塞尔曲线拟合
    /// </summary>
    public class AsyncAdvancedBezierSmoothing
    {
        private readonly SemaphoreSlim _processingSemaphore;
        private readonly ConcurrentDictionary<Stroke, CancellationTokenSource> _processingTasks;
        private readonly Dispatcher _uiDispatcher;

        public AsyncAdvancedBezierSmoothing(Dispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
            _processingSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            _processingTasks = new ConcurrentDictionary<Stroke, CancellationTokenSource>();
        }

        public double SmoothingStrength { get; set; } = 0.4; // 适中的平滑强度
        public double ResampleInterval { get; set; } = 2.5; // 适中的重采样间隔
        public int InterpolationSteps { get; set; } = 12; // 增加插值步数提高精度
        public bool UseHardwareAcceleration { get; set; } = true;
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
        public bool UseAdaptiveInterpolation { get; set; } = true; // 自适应插值
        public double CurveTension { get; set; } = 0.3; // 曲线张力参数

        /// <summary>
        /// 异步平滑笔画
        /// </summary>
        public async Task<Stroke> SmoothStrokeAsync(Stroke originalStroke,
            Action<Stroke, Stroke> onCompleted = null,
            CancellationToken cancellationToken = default)
        {
            if (originalStroke == null || originalStroke.StylusPoints.Count < 2)
                return originalStroke;

            // 取消之前对同一笔画的处理
            if (_processingTasks.TryGetValue(originalStroke, out var existingCts))
            {
                existingCts.Cancel();
                _processingTasks.TryRemove(originalStroke, out _);
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTasks[originalStroke] = cts;

            try
            {
                await _processingSemaphore.WaitAsync(cts.Token);

                var smoothedStroke = await Task.Run(() =>
                    ProcessStrokeInternal(originalStroke, cts.Token), cts.Token);

                // 在UI线程上执行回调
                if (onCompleted != null && !cts.Token.IsCancellationRequested)
                {
                    await _uiDispatcher.InvokeAsync(() => onCompleted(originalStroke, smoothedStroke));
                }

                return smoothedStroke;
            }
            catch (OperationCanceledException)
            {
                return originalStroke;
            }
            finally
            {
                _processingSemaphore.Release();
                _processingTasks.TryRemove(originalStroke, out _);
                cts.Dispose();
            }
        }

        private Stroke ProcessStrokeInternal(Stroke stroke, CancellationToken cancellationToken)
        {
            var originalPoints = stroke.StylusPoints.ToArray();

            // 如果点数太少，直接返回原始笔画
            if (originalPoints.Length < 3)
                return stroke;

            cancellationToken.ThrowIfCancellationRequested();

            // 使用改进的贝塞尔曲线拟合
            var smoothedPoints = ApplyImprovedBezierSmoothing(originalPoints);

            System.Diagnostics.Debug.WriteLine($"AsyncAdvancedBezierSmoothing: 原始点数={originalPoints.Length}, 平滑后点数={smoothedPoints.Length}");

            cancellationToken.ThrowIfCancellationRequested();

            // 放宽点数限制
            if (smoothedPoints.Length > originalPoints.Length * 3.0)
            {
                System.Diagnostics.Debug.WriteLine($"AsyncAdvancedBezierSmoothing: 点数过多，进行重采样");
                // 如果点数增加太多，进行重采样
                smoothedPoints = ResampleEquidistantOptimized(smoothedPoints, ResampleInterval);
            }

            // 进一步放宽最终检查
            if (smoothedPoints.Length > originalPoints.Length * 2.5)
            {
                System.Diagnostics.Debug.WriteLine($"AsyncAdvancedBezierSmoothing: 重采样后点数仍然过多，返回原始笔画");
                // 如果仍然太多点，使用原始笔画
                return stroke;
            }

            // 创建平滑后的笔画
            var smoothedStroke = new Stroke(new StylusPointCollection(smoothedPoints))
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };

            System.Diagnostics.Debug.WriteLine($"AsyncAdvancedBezierSmoothing: 成功创建平滑笔画");
            return smoothedStroke;
        }

        /// <summary>
        /// 改进的贝塞尔曲线平滑处理
        /// </summary>
        private StylusPoint[] ApplyImprovedBezierSmoothing(StylusPoint[] points)
        {
            if (points.Length < 6) return points; // 5次贝塞尔需要6个点

            var result = new List<StylusPoint>();

            // 添加第一个点
            result.Add(points[0]);

            // 使用5次贝塞尔曲线，每次移动1个点确保连续性
            for (int i = 0; i < points.Length - 5; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];
                var p4 = points[i + 4];
                var p5 = points[i + 5];

                // 计算5次贝塞尔的控制点
                var controlPoints = CalculateQuinticControlPoints(p0, p1, p2, p3, p4, p5);

                // 生成插值点
                if (i == 0)
                {
                    // 第一个窗口：生成更多插值点
                    for (int j = 1; j <= 4; j++)
                    {
                        double t = (double)j / 5;
                        var bezierPoint = CalculateQuinticBezierPoint(p0, controlPoints, p5, t);
                        result.Add(bezierPoint);
                    }
                }
                else
                {
                    // 后续窗口：只生成最后一个插值点，避免重复
                    double t = 4.0 / 5.0; // 只取最后一个插值点
                    var bezierPoint = CalculateQuinticBezierPoint(p0, controlPoints, p5, t);
                    result.Add(bezierPoint);
                }
            }

            // 添加最后一个点
            result.Add(points[points.Length - 1]);

            System.Diagnostics.Debug.WriteLine($"ApplyImprovedBezierSmoothing: 原始点数={points.Length}, 生成点数={result.Count}");

            // 使用更宽松的去重
            return RemoveDuplicatePointsLoose(result.ToArray());
        }

        /// <summary>
        /// 5次贝塞尔曲线控制点计算
        /// </summary>
        private (Point cp1, Point cp2, Point cp3, Point cp4) CalculateQuinticControlPoints(
            StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3, StylusPoint p4, StylusPoint p5)
        {
            // 计算控制点距离（基于相邻点距离）
            double dist1 = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            double dist2 = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            double dist3 = Math.Sqrt((p4.X - p3.X) * (p4.X - p3.X) + (p4.Y - p3.Y) * (p4.Y - p3.Y));
            double dist4 = Math.Sqrt((p5.X - p4.X) * (p5.X - p4.X) + (p5.Y - p4.Y) * (p5.Y - p4.Y));

            // 使用更小的控制点距离，产生超平滑的曲线
            double controlDist1 = dist1 * 0.15;
            double controlDist2 = dist2 * 0.15;
            double controlDist3 = dist3 * 0.15;
            double controlDist4 = dist4 * 0.15;

            // 计算控制点方向 - 使用更平滑的方向计算
            double dir1X = p2.X - p0.X;
            double dir1Y = p2.Y - p0.Y;
            double dir2X = p3.X - p1.X;
            double dir2Y = p3.Y - p1.Y;
            double dir3X = p4.X - p2.X;
            double dir3Y = p4.Y - p2.Y;
            double dir4X = p5.X - p3.X;
            double dir4Y = p5.Y - p3.Y;

            // 归一化方向
            NormalizeVector(ref dir1X, ref dir1Y);
            NormalizeVector(ref dir2X, ref dir2Y);
            NormalizeVector(ref dir3X, ref dir3Y);
            NormalizeVector(ref dir4X, ref dir4Y);

            // 计算控制点
            var cp1 = new Point(p1.X + dir1X * controlDist1, p1.Y + dir1Y * controlDist1);
            var cp2 = new Point(p2.X + dir2X * controlDist2, p2.Y + dir2Y * controlDist2);
            var cp3 = new Point(p3.X - dir3X * controlDist3, p3.Y - dir3Y * controlDist3);
            var cp4 = new Point(p4.X - dir4X * controlDist4, p4.Y - dir4Y * controlDist4);

            return (cp1, cp2, cp3, cp4);
        }

        /// <summary>
        /// 归一化向量
        /// </summary>
        private void NormalizeVector(ref double x, ref double y)
        {
            double length = Math.Sqrt(x * x + y * y);
            if (length > 0)
            {
                x /= length;
                y /= length;
            }
        }

        /// <summary>
        /// 5次贝塞尔曲线点计算
        /// </summary>
        private StylusPoint CalculateQuinticBezierPoint(StylusPoint p0, (Point cp1, Point cp2, Point cp3, Point cp4) controlPoints, StylusPoint p5, double t)
        {
            double oneMinusT = 1 - t;
            double oneMinusT2 = oneMinusT * oneMinusT;
            double oneMinusT3 = oneMinusT2 * oneMinusT;
            double oneMinusT4 = oneMinusT3 * oneMinusT;
            double oneMinusT5 = oneMinusT4 * oneMinusT;

            double t2 = t * t;
            double t3 = t2 * t;
            double t4 = t3 * t;
            double t5 = t4 * t;

            // 5次贝塞尔曲线公式
            double x = oneMinusT5 * p0.X +
                      5 * oneMinusT4 * t * controlPoints.cp1.X +
                      10 * oneMinusT3 * t2 * controlPoints.cp2.X +
                      10 * oneMinusT2 * t3 * controlPoints.cp3.X +
                      5 * oneMinusT * t4 * controlPoints.cp4.X +
                      t5 * p5.X;

            double y = oneMinusT5 * p0.Y +
                      5 * oneMinusT4 * t * controlPoints.cp1.Y +
                      10 * oneMinusT3 * t2 * controlPoints.cp2.Y +
                      10 * oneMinusT2 * t3 * controlPoints.cp3.Y +
                      5 * oneMinusT * t4 * controlPoints.cp4.Y +
                      t5 * p5.Y;

            // 压力插值 - 使用线性插值
            float pressure = (float)((1 - t) * p0.PressureFactor + t * p5.PressureFactor);

            return new StylusPoint(x, y, Math.Max(pressure, 0.1f));
        }

        /// <summary>
        /// 简化的控制点计算 
        /// </summary>
        private (Point cp1, Point cp2) CalculateSimpleControlPoints(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 计算控制点距离（基于线段长度）
            double dist1 = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            double dist2 = Math.Sqrt((p3.X - p2.X) * (p3.X - p2.X) + (p3.Y - p2.Y) * (p3.Y - p2.Y));

            // 使用更小的控制点距离，产生更平滑的曲线
            double controlDist1 = dist1 * 0.2; // 进一步减少控制点影响
            double controlDist2 = dist2 * 0.2;

            // 计算控制点方向 - 使用更平滑的方向计算
            double dir1X = p2.X - p0.X; // 使用更远的点计算方向
            double dir1Y = p2.Y - p0.Y;
            double dir2X = p3.X - p1.X;
            double dir2Y = p3.Y - p1.Y;

            // 归一化方向
            double len1 = Math.Sqrt(dir1X * dir1X + dir1Y * dir1Y);
            double len2 = Math.Sqrt(dir2X * dir2X + dir2Y * dir2Y);

            if (len1 > 0)
            {
                dir1X /= len1;
                dir1Y /= len1;
            }

            if (len2 > 0)
            {
                dir2X /= len2;
                dir2Y /= len2;
            }

            // 计算控制点
            var cp1 = new Point(
                p1.X + dir1X * controlDist1,
                p1.Y + dir1Y * controlDist1
            );

            var cp2 = new Point(
                p2.X - dir2X * controlDist2,
                p2.Y - dir2Y * controlDist2
            );

            return (cp1, cp2);
        }

        /// <summary>
        /// 宽松的去重算法
        /// </summary>
        private StylusPoint[] RemoveDuplicatePointsLoose(StylusPoint[] points)
        {
            if (points.Length < 2) return points;

            var result = new List<StylusPoint>();
            result.Add(points[0]);

            double minDistance = 0.1; // 非常小的距离阈值，几乎不去重

            for (int i = 1; i < points.Length; i++)
            {
                var lastPoint = result[result.Count - 1];
                var currentPoint = points[i];

                // 计算距离
                double distance = Math.Sqrt(
                    (currentPoint.X - lastPoint.X) * (currentPoint.X - lastPoint.X) +
                    (currentPoint.Y - lastPoint.Y) * (currentPoint.Y - lastPoint.Y));

                // 如果距离足够大，添加这个点
                if (distance >= minDistance)
                {
                    result.Add(currentPoint);
                }
            }

            System.Diagnostics.Debug.WriteLine($"RemoveDuplicatePointsLoose: 输入点数={points.Length}, 输出点数={result.Count}");
            return result.ToArray();
        }

        /// <summary>
        /// 计算贝塞尔曲线上的点
        /// </summary>
        private StylusPoint CalculateBezierPoint(StylusPoint p0, Point cp1, Point cp2, StylusPoint p3, double t)
        {
            double x = Math.Pow(1 - t, 3) * p0.X +
                      3 * Math.Pow(1 - t, 2) * t * cp1.X +
                      3 * (1 - t) * Math.Pow(t, 2) * cp2.X +
                      Math.Pow(t, 3) * p3.X;

            double y = Math.Pow(1 - t, 3) * p0.Y +
                      3 * Math.Pow(1 - t, 2) * t * cp1.Y +
                      3 * (1 - t) * Math.Pow(t, 2) * cp2.Y +
                      Math.Pow(t, 3) * p3.Y;

            // 压力插值
            float pressure = (float)(Math.Pow(1 - t, 3) * p0.PressureFactor +
                                   3 * Math.Pow(1 - t, 2) * t * p0.PressureFactor +
                                   3 * (1 - t) * Math.Pow(t, 2) * p3.PressureFactor +
                                   Math.Pow(t, 3) * p3.PressureFactor);

            return new StylusPoint(x, y, Math.Max(pressure, 0.1f));
        }

        /// <summary>
        /// 计算改进的控制点
        /// </summary>
        private (Point cp1, Point cp2) CalculateImprovedControlPoints(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 计算切线方向
            var tangent1 = new Vector(p1.X - p0.X, p1.Y - p0.Y);
            var tangent2 = new Vector(p3.X - p2.X, p3.Y - p2.Y);

            // 归一化切线
            if (tangent1.Length > 0) tangent1.Normalize();
            if (tangent2.Length > 0) tangent2.Normalize();

            // 计算控制点距离（基于点间距离）
            double dist1 = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            double dist2 = Math.Sqrt((p3.X - p2.X) * (p3.X - p2.X) + (p3.Y - p2.Y) * (p3.Y - p2.Y));

            double controlDist1 = dist1 * CurveTension;
            double controlDist2 = dist2 * CurveTension;

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
        /// 自适应插值步数计算
        /// </summary>
        private int CalculateAdaptiveSteps(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 基于曲线长度和复杂度计算步数
            double totalLength = 0;
            totalLength += Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            totalLength += Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            totalLength += Math.Sqrt((p3.X - p2.X) * (p3.X - p2.X) + (p3.Y - p2.Y) * (p3.Y - p2.Y));

            // 计算曲率（简化版本）
            double curvature = CalculateCurvature(p0, p1, p2, p3);

            // 基于长度和曲率计算步数
            int baseSteps = Math.Max(8, Math.Min(20, (int)(totalLength / 10)));
            int curvatureSteps = (int)(curvature * 10);

            return Math.Max(InterpolationSteps, Math.Min(24, baseSteps + curvatureSteps));
        }

        /// <summary>
        /// 计算曲率（简化版本）
        /// </summary>
        private double CalculateCurvature(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 计算三个向量的角度变化
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
        /// 去除重复和过近的点
        /// </summary>
        private StylusPoint[] RemoveDuplicatePoints(StylusPoint[] points)
        {
            if (points.Length < 2) return points;

            var result = new List<StylusPoint>();
            result.Add(points[0]);

            double minDistance = 0.3; // 进一步减少最小距离阈值，保留更多平滑点

            for (int i = 1; i < points.Length; i++)
            {
                var lastPoint = result[result.Count - 1];
                var currentPoint = points[i];

                // 计算距离
                double distance = Math.Sqrt(
                    (currentPoint.X - lastPoint.X) * (currentPoint.X - lastPoint.X) +
                    (currentPoint.Y - lastPoint.Y) * (currentPoint.Y - lastPoint.Y));

                // 如果距离足够大，添加这个点
                if (distance >= minDistance)
                {
                    result.Add(currentPoint);
                }
            }

            System.Diagnostics.Debug.WriteLine($"RemoveDuplicatePoints: 输入点数={points.Length}, 输出点数={result.Count}");
            return result.ToArray();
        }

        /// <summary>
        /// 使用控制点的三次贝塞尔曲线计算
        /// </summary>
        private StylusPoint CubicBezierWithControlPoints((Point cp1, Point cp2) controlPoints, double t, StylusPoint p0, StylusPoint p3)
        {
            var p1 = controlPoints.cp1;
            var p2 = controlPoints.cp2;

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

            double x = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
            double y = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;

            // 插值压力值
            float pressure = (float)(p0.PressureFactor * u + p3.PressureFactor * t);
            pressure = Math.Max(pressure, 0.1f);

            return new StylusPoint(x, y, pressure);
        }

        /// <summary>
        /// 硬件加速的向量化指数平滑
        /// </summary>
        private StylusPoint[] ApplyExponentialSmoothingVectorized(StylusPoint[] points, double alpha)
        {
            if (points.Length == 0) return points;

            var result = new StylusPoint[points.Length];
            result[0] = points[0];

            double lastX = points[0].X;
            double lastY = points[0].Y;
            float lastPressure = points[0].PressureFactor;
            double oneMinusAlpha = 1.0 - alpha;

            // 向量化处理，减少分支预测失败
            for (int i = 1; i < points.Length; i++)
            {
                var p = points[i];
                lastX = alpha * p.X + oneMinusAlpha * lastX;
                lastY = alpha * p.Y + oneMinusAlpha * lastY;
                lastPressure = (float)(alpha * p.PressureFactor + oneMinusAlpha * lastPressure);
                lastPressure = Math.Max(lastPressure, 0.1f); // 避免分支
                result[i] = new StylusPoint(lastX, lastY, lastPressure);
            }
            return result;
        }

        /// <summary>
        /// 优化的等距重采样
        /// </summary>
        private StylusPoint[] ResampleEquidistantOptimized(StylusPoint[] points, double interval)
        {
            if (points.Length == 0) return points;

            var result = new List<StylusPoint>(points.Length) { points[0] };
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

        /// <summary>
        /// 硬件加速的贝塞尔曲线拟合
        /// </summary>
        private StylusPoint[] SlidingBezierFitHardwareAccelerated(StylusPoint[] points, int window, int steps)
        {
            if (points.Length < window) return points;

            var result = new List<StylusPoint>(points.Length * steps / window);

            // 使用并行处理加速计算
            var segments = new List<StylusPoint[]>();

            Parallel.For(0, points.Length - window + 1, i =>
            {
                var segmentPoints = new StylusPoint[steps];
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];

                for (int j = 0; j < steps; j++)
                {
                    double t = (double)j / steps;
                    segmentPoints[j] = CubicBezierOptimized(p0, p1, p2, p3, t);
                }

                lock (segments)
                {
                    segments.Add(segmentPoints);
                }
            });

            // 合并结果
            foreach (var segment in segments)
            {
                result.AddRange(segment);
            }

            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        /// <summary>
        /// 优化的单线程贝塞尔拟合
        /// </summary>
        private StylusPoint[] SlidingBezierFitOptimized(StylusPoint[] points, int window, int steps)
        {
            if (points.Length < window) return points;

            var result = new List<StylusPoint>(points.Length * steps / window);

            for (int i = 0; i <= points.Length - window; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];
                var p3 = points[i + 3];

                for (int j = 0; j < steps; j++)
                {
                    double t = (double)j / steps;
                    result.Add(CubicBezierOptimized(p0, p1, p2, p3, t));
                }
            }

            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        /// <summary>
        /// 优化的三次贝塞尔曲线计算
        /// </summary>
        private StylusPoint CubicBezierOptimized(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3, double t)
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

            double x = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
            double y = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
            float pressure = (float)(p1.PressureFactor * u + p2.PressureFactor * t);
            pressure = Math.Max(pressure, 0.1f);

            return new StylusPoint(x, y, pressure);
        }

        /// <summary>
        /// 兼容性方法：传统指数平滑
        /// </summary>
        private StylusPoint[] ApplyExponentialSmoothing(StylusPoint[] points, double alpha)
        {
            if (points.Length == 0) return points;

            var result = new StylusPoint[points.Length];
            result[0] = points[0];

            double lastX = points[0].X;
            double lastY = points[0].Y;
            float lastPressure = points[0].PressureFactor;

            for (int i = 1; i < points.Length; i++)
            {
                var p = points[i];
                lastX = alpha * p.X + (1 - alpha) * lastX;
                lastY = alpha * p.Y + (1 - alpha) * lastY;
                lastPressure = (float)(alpha * p.PressureFactor + (1 - alpha) * lastPressure);
                lastPressure = Math.Max(lastPressure, 0.1f);
                result[i] = new StylusPoint(lastX, lastY, lastPressure);
            }
            return result;
        }

        /// <summary>
        /// 取消所有正在进行的处理任务
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var kvp in _processingTasks)
            {
                kvp.Value.Cancel();
            }
            _processingTasks.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            CancelAllTasks();
            _processingSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// 原有的同步版本（保持向后兼容）
    /// </summary>
    public class AdvancedBezierSmoothing
    {
        public double SmoothingStrength { get; set; } = 0.6; 
        public double ResampleInterval { get; set; } = 2.0; 
        public int InterpolationSteps { get; set; } = 12; 

        public Stroke SmoothStroke(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine($"AdvancedBezierSmoothing: 笔画点数不足，跳过平滑 (点数: {stroke?.StylusPoints.Count ?? 0})");
                return stroke;
            }

            System.Diagnostics.Debug.WriteLine($"AdvancedBezierSmoothing: 开始平滑处理，原始点数: {stroke.StylusPoints.Count}");

            var originalPoints = stroke.StylusPoints.ToArray();

            // 使用真正的贝塞尔曲线平滑
            var smoothedPoints = ApplyCubicBezierSmoothing(originalPoints);

            System.Diagnostics.Debug.WriteLine($"AdvancedBezierSmoothing: 平滑完成，平滑后点数: {smoothedPoints.Length}");

            // 检查点数是否合理
            if (smoothedPoints.Length > originalPoints.Length * 10.0)
            {
                System.Diagnostics.Debug.WriteLine($"AdvancedBezierSmoothing: 点数增加过多，返回原始笔画 (原始:{originalPoints.Length}, 平滑后:{smoothedPoints.Length})");
                return stroke; // 如果点数增加太多，返回原始笔画
            }

            var smoothedStroke = new Stroke(new StylusPointCollection(smoothedPoints))
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };
            
            System.Diagnostics.Debug.WriteLine($"AdvancedBezierSmoothing: 创建平滑笔画成功");
            return smoothedStroke;
        }

        /// <summary>
        /// 三次贝塞尔曲线平滑 
        /// </summary>
        private StylusPoint[] ApplyCubicBezierSmoothing(StylusPoint[] points)
        {
            if (points.Length < 4) return points;

            var result = new List<StylusPoint>();
            result.Add(points[0]);

            // 使用更保守的窗口大小和插值
            int windowSize = Math.Min(4, points.Length);
            int stepSize = Math.Max(1, points.Length / 10); // 根据点数动态调整步长

            for (int i = 0; i <= points.Length - windowSize; i += stepSize)
            {
                if (i + windowSize - 1 >= points.Length) break;

                var p0 = points[i];
                var p1 = points[Math.Min(i + 1, points.Length - 1)];
                var p2 = points[Math.Min(i + 2, points.Length - 1)];
                var p3 = points[Math.Min(i + windowSize - 1, points.Length - 1)];

                // 计算控制点
                var controlPoints = CalculateControlPoints(p0, p1, p2, p3);

                // 只生成2-3个插值点
                int steps = 2;
                
                // 生成贝塞尔曲线点
                for (int j = 1; j <= steps; j++)
                {
                    double t = (double)j / steps;
                    var bezierPoint = CalculateBezierPoint(p0, controlPoints.cp1, controlPoints.cp2, p3, t);
                    result.Add(bezierPoint);
                }
            }

            result.Add(points[points.Length - 1]);
            
            // 去重处理
            return RemoveDuplicatePoints(result.ToArray());
        }

        /// <summary>
        /// 去除重复点
        /// </summary>
        private StylusPoint[] RemoveDuplicatePoints(StylusPoint[] points)
        {
            if (points.Length <= 1) return points;

            var result = new List<StylusPoint> { points[0] };
            double minDistance = 1.0; // 最小距离阈值

            for (int i = 1; i < points.Length; i++)
            {
                var lastPoint = result[result.Count - 1];
                var currentPoint = points[i];
                
                double distance = Math.Sqrt(Math.Pow(currentPoint.X - lastPoint.X, 2) + 
                                          Math.Pow(currentPoint.Y - lastPoint.Y, 2));
                
                if (distance > minDistance)
                {
                    result.Add(currentPoint);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 计算控制点
        /// </summary>
        private (Point cp1, Point cp2) CalculateControlPoints(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3)
        {
            // 计算切线方向
            var tangent1 = new Vector(p1.X - p0.X, p1.Y - p0.Y);
            var tangent2 = new Vector(p3.X - p2.X, p3.Y - p2.Y);

            // 归一化切线
            if (tangent1.Length > 0) tangent1.Normalize();
            if (tangent2.Length > 0) tangent2.Normalize();

            // 计算控制点距离
            double dist1 = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            double dist2 = Math.Sqrt((p3.X - p2.X) * (p3.X - p2.X) + (p3.Y - p2.Y) * (p3.Y - p2.Y));

            double controlDist1 = dist1 * SmoothingStrength;
            double controlDist2 = dist2 * SmoothingStrength;

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
        /// 计算贝塞尔曲线上的点
        /// </summary>
        private StylusPoint CalculateBezierPoint(StylusPoint p0, Point cp1, Point cp2, StylusPoint p3, double t)
        {
            double x = Math.Pow(1 - t, 3) * p0.X +
                      3 * Math.Pow(1 - t, 2) * t * cp1.X +
                      3 * (1 - t) * Math.Pow(t, 2) * cp2.X +
                      Math.Pow(t, 3) * p3.X;

            double y = Math.Pow(1 - t, 3) * p0.Y +
                      3 * Math.Pow(1 - t, 2) * t * cp1.Y +
                      3 * (1 - t) * Math.Pow(t, 2) * cp2.Y +
                      Math.Pow(t, 3) * p3.Y;

            // 压力插值
            float pressure = (float)(Math.Pow(1 - t, 3) * p0.PressureFactor +
                                   3 * Math.Pow(1 - t, 2) * t * p0.PressureFactor +
                                   3 * (1 - t) * Math.Pow(t, 2) * p3.PressureFactor +
                                   Math.Pow(t, 3) * p3.PressureFactor);

            return new StylusPoint(x, y, Math.Max(pressure, 0.1f));
        }

        /// <summary>
        /// 轻度指数平滑
        /// </summary>
        private List<StylusPoint> ApplyLightExponentialSmoothing(List<StylusPoint> points, double alpha)
        {
            var result = new List<StylusPoint>();
            if (points.Count == 0) return result;

            result.Add(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                var prev = result[result.Count - 1];
                var curr = points[i];

                double x = alpha * curr.X + (1 - alpha) * prev.X;
                double y = alpha * curr.Y + (1 - alpha) * prev.Y;
                float pressure = (float)(alpha * curr.PressureFactor + (1 - alpha) * prev.PressureFactor);
                pressure = Math.Max(pressure, 0.1f);

                result.Add(new StylusPoint(x, y, pressure));
            }
            return result;
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

        private List<StylusPoint> SlidingBezierFit(List<StylusPoint> points, int window = 4, int steps = 48) // 从24增加到48
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

    /// <summary>
    /// 性能监控器
    /// </summary>
    public class InkSmoothingPerformanceMonitor
    {
        private readonly Queue<TimeSpan> _processingTimes = new Queue<TimeSpan>();
        private readonly object _lock = new object();
        private const int MaxSamples = 100;

        public void RecordProcessingTime(TimeSpan time)
        {
            lock (_lock)
            {
                _processingTimes.Enqueue(time);
                if (_processingTimes.Count > MaxSamples)
                    _processingTimes.Dequeue();
            }
        }

        public double GetAverageProcessingTimeMs()
        {
            lock (_lock)
            {
                return _processingTimes.Count > 0 ?
                    _processingTimes.Average(t => t.TotalMilliseconds) : 0;
            }
        }

        public double GetMaxProcessingTimeMs()
        {
            lock (_lock)
            {
                return _processingTimes.Count > 0 ?
                    _processingTimes.Max(t => t.TotalMilliseconds) : 0;
            }
        }

        public int GetSampleCount()
        {
            lock (_lock)
            {
                return _processingTimes.Count;
            }
        }
    }
}