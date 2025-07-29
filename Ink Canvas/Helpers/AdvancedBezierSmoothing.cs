using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 异步硬件加速的墨迹平滑处理器
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

        public double SmoothingStrength { get; set; } = 0.3; // 大幅降低强度
        public double ResampleInterval { get; set; } = 3.0; // 大幅增加间隔减少点数
        public int InterpolationSteps { get; set; } = 8; // 从4增加到8，提高插值步数
        public bool UseHardwareAcceleration { get; set; } = true;
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;

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

            // 简化处理：只进行轻度平滑，避免点数爆炸
            var smoothedPoints = ApplyLightSmoothing(originalPoints);

            cancellationToken.ThrowIfCancellationRequested();

            // 确保点数不会过多
            if (smoothedPoints.Length > originalPoints.Length * 2)
            {
                // 如果点数增加太多，回退到原始笔画
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
        /// 轻度平滑处理，避免点数爆炸
        /// </summary>
        private StylusPoint[] ApplyLightSmoothing(StylusPoint[] points)
        {
            if (points.Length < 3) return points;

            var result = new List<StylusPoint>();
            result.Add(points[0]); // 保持第一个点

            // 简单的3点平均平滑
            for (int i = 1; i < points.Length - 1; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var next = points[i + 1];

                // 3点平均
                double x = (prev.X + curr.X + next.X) / 3.0;
                double y = (prev.Y + curr.Y + next.Y) / 3.0;
                float pressure = (prev.PressureFactor + curr.PressureFactor + next.PressureFactor) / 3.0f;

                result.Add(new StylusPoint(x, y, Math.Max(pressure, 0.1f)));
            }

            result.Add(points[points.Length - 1]); // 保持最后一个点

            return result.ToArray();
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