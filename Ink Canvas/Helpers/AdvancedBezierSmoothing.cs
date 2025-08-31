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

            cancellationToken.ThrowIfCancellationRequested();

            // 严格控制点数，避免产生过多点
            if (smoothedPoints.Length > originalPoints.Length * 2)
            {
                // 如果点数增加太多，进行重采样
                smoothedPoints = ResampleEquidistantOptimized(smoothedPoints, ResampleInterval);
            }

            // 最终检查：确保点数不会过多
            if (smoothedPoints.Length > originalPoints.Length * 1.5)
            {
                // 如果仍然太多点，使用原始笔画
                return stroke;
            }

            // 创建平滑后的笔画
            var smoothedStroke = new Stroke(new StylusPointCollection(smoothedPoints))
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };

            return smoothedStroke;
        }

        /// <summary>
        /// 改进的贝塞尔曲线平滑处理
        /// </summary>
        private StylusPoint[] ApplyImprovedBezierSmoothing(StylusPoint[] points)
        {
            if (points.Length < 4) return points;

            var result = new List<StylusPoint>();

            // 添加第一个点
            result.Add(points[0]);

            // 使用非重叠的窗口进行贝塞尔曲线拟合
            for (int i = 0; i < points.Length - 3; i += 3) // 每次移动3个点，避免重叠
            {
                var p0 = points[i];
                var p1 = points[Math.Min(i + 1, points.Length - 1)];
                var p2 = points[Math.Min(i + 2, points.Length - 1)];
                var p3 = points[Math.Min(i + 3, points.Length - 1)];

                // 计算改进的控制点
                var controlPoints = CalculateImprovedControlPoints(p0, p1, p2, p3);

                // 限制插值步数，避免点数爆炸
                int steps = Math.Min(UseAdaptiveInterpolation ?
                    CalculateAdaptiveSteps(p0, p1, p2, p3) : InterpolationSteps, 16);

                // 生成贝塞尔曲线点，但跳过第一个点避免重复
                for (int j = 1; j <= steps; j++)
                {
                    double t = (double)j / steps;
                    var bezierPoint = CubicBezierWithControlPoints(controlPoints, t, p0, p3);
                    result.Add(bezierPoint);
                }
            }

            // 添加最后一个点
            result.Add(points[points.Length - 1]);

            // 去重和优化点数
            return RemoveDuplicatePoints(result.ToArray());
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

            double minDistance = ResampleInterval * 0.5; // 最小距离阈值

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
        public double SmoothingStrength { get; set; } = 0.3;
        public double ResampleInterval { get; set; } = 3.0;
        public int InterpolationSteps { get; set; } = 8;

        public Stroke SmoothStroke(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count < 3)
                return stroke;

            var originalPoints = stroke.StylusPoints.ToList();

            // 简化处理：只进行轻度平滑
            var smoothedPoints = ApplyLightExponentialSmoothing(originalPoints, 0.2); // 很轻的平滑

            // 检查点数是否合理
            if (smoothedPoints.Count > originalPoints.Count * 1.5)
            {
                return stroke; // 如果点数增加太多，返回原始笔画
            }

            var smoothedStroke = new Stroke(new StylusPointCollection(smoothedPoints))
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };
            return smoothedStroke;
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