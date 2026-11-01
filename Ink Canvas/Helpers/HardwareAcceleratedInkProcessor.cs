using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// CPU geometry smoothing. The legacy API name is retained for compatibility.
    /// </summary>
    public class HardwareAcceleratedInkProcessor : IDisposable
    {
        private bool _isInitialized;
        private readonly SemaphoreSlim _processingGate = new SemaphoreSlim(1, 1);

        public HardwareAcceleratedInkProcessor(int width = 1920, int height = 1080)
        {
            // Width/height remain in the public signature; smoothing never renders
            // into a bitmap, so an unused full-screen render target is unnecessary.
            _isInitialized = true;
        }

        /// <summary>
        /// Snapshot input on the calling thread and compute CPU geometry on a worker.
        /// The legacy method name is retained for compatibility.
        /// </summary>
        public async Task<Stroke> SmoothStrokeWithGPU(Stroke originalStroke)
        {
            if (!_isInitialized || originalStroke == null || originalStroke.StylusPoints.Count < 2)
                return originalStroke;

            // Take an immutable snapshot on the owning thread. Geometry construction
            // and flattening are CPU work even though the old method was called GPU.
            if (originalStroke.StylusPoints.Count > 32768 || !await _processingGate.WaitAsync(0))
                return originalStroke;

            try
            {
                // Fit and flatten the curve without accessing the source stroke on the worker.
                var points = originalStroke.StylusPoints.ToArray();
                var da = originalStroke.DrawingAttributes.Clone();
                var smoothedPoints = await Task.Run(() =>
                {
                    var local = new StylusPointCollection(points);
                    var pathGeometry = CreateSmoothPathGeometry(local);
                    return ConvertPathGeometryToStylusPoints(pathGeometry, local);
                });
                var result = new Stroke(new StylusPointCollection(smoothedPoints))
                {
                    DrawingAttributes = da
                };
                return result;
            }
            catch
            {
                return originalStroke;
            }
            finally { _processingGate.Release(); }
        }

        internal Stroke SmoothStrokeInline(Stroke originalStroke)
        {
            if (!_isInitialized || originalStroke == null || originalStroke.StylusPoints.Count < 2
                || originalStroke.StylusPoints.Count > 32768 || !_processingGate.Wait(0)) return originalStroke;
            try
            {
                var points = ConvertPathGeometryToStylusPoints(CreateSmoothPathGeometry(originalStroke.StylusPoints), originalStroke.StylusPoints);
                return new Stroke(new StylusPointCollection(points)) { DrawingAttributes = originalStroke.DrawingAttributes.Clone() };
            }
            catch { return originalStroke; }
            finally { _processingGate.Release(); }
        }

        /// <summary>
        /// 创建平滑的路径几何体
        /// </summary>
        private PathGeometry CreateSmoothPathGeometry(StylusPointCollection points)
        {
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();

            if (points.Count < 2) return pathGeometry;

            pathFigure.StartPoint = new Point(points[0].X, points[0].Y);

            // 使用贝塞尔曲线段创建平滑路径，增加插点密度
            for (int i = 0; i < points.Count - 1; i += 2)  // 从i+=3改为i+=2，增加插点密度
            {
                var p1 = i + 1 < points.Count ? new Point(points[i + 1].X, points[i + 1].Y) : pathFigure.StartPoint;
                var p2 = i + 2 < points.Count ? new Point(points[i + 2].X, points[i + 2].Y) : p1;
                var p3 = i + 3 < points.Count ? new Point(points[i + 3].X, points[i + 3].Y) : p2;

                var bezierSegment = new BezierSegment(p1, p2, p3, true);
                pathFigure.Segments.Add(bezierSegment);
            }

            pathGeometry.Figures.Add(pathFigure);
            return pathGeometry;
        }

        /// <summary>
        /// 将PathGeometry转换为StylusPoint集合
        /// </summary>
        private List<StylusPoint> ConvertPathGeometryToStylusPoints(PathGeometry pathGeometry, StylusPointCollection originalPoints)
        {
            var result = new List<StylusPoint>();
            var flattened = pathGeometry.GetFlattenedPathGeometry();

            foreach (var figure in flattened.Figures)
            {
                result.Add(new StylusPoint(figure.StartPoint.X, figure.StartPoint.Y, 0.5f));

                foreach (var segment in figure.Segments)
                {
                    if (segment is LineSegment lineSegment)
                    {
                        result.Add(new StylusPoint(lineSegment.Point.X, lineSegment.Point.Y, 0.5f));
                    }
                    else if (segment is PolyLineSegment polyLineSegment)
                    {
                        foreach (var point in polyLineSegment.Points)
                        {
                            result.Add(new StylusPoint(point.X, point.Y, 0.5f));
                        }
                    }
                }
            }

            // 保持原始压感信息
            InterpolatePressure(result, originalPoints);

            return result;
        }

        /// <summary>
        /// 插值压感信息
        /// </summary>
        private void InterpolatePressure(List<StylusPoint> smoothedPoints, StylusPointCollection originalPoints)
        {
            if (originalPoints.Count == 0 || smoothedPoints.Count == 0) return;

            for (int i = 0; i < smoothedPoints.Count; i++)
            {
                double ratio = (double)i / (smoothedPoints.Count - 1);
                int originalIndex = (int)(ratio * (originalPoints.Count - 1));
                originalIndex = Math.Max(0, Math.Min(originalIndex, originalPoints.Count - 1));

                var point = smoothedPoints[i];
                float pressure = originalPoints[originalIndex].PressureFactor;
                smoothedPoints[i] = new StylusPoint(point.X, point.Y, Math.Max(pressure, 0.1f));
            }
        }

        /// <summary>
        /// 使用GPU加速的并行贝塞尔计算
        /// </summary>
        public static StylusPoint[] ParallelBezierInterpolation(StylusPoint[] controlPoints, int segments = 32)
        {
            if (controlPoints.Length < 4) return controlPoints;

            var result = new StylusPoint[segments * (controlPoints.Length / 4)];

            Parallel.For(0, controlPoints.Length / 4, segmentIndex =>
            {
                var p0 = controlPoints[segmentIndex * 4];
                var p1 = controlPoints[segmentIndex * 4 + 1];
                var p2 = controlPoints[segmentIndex * 4 + 2];
                var p3 = controlPoints[segmentIndex * 4 + 3];

                for (int i = 0; i < segments; i++)
                {
                    double t = (double)i / (segments - 1);
                    result[segmentIndex * segments + i] = CubicBezierFast(p0, p1, p2, p3, t);
                }
            });

            return result;
        }

        /// <summary>
        /// 优化的三次贝塞尔曲线计算
        /// </summary>
        private static StylusPoint CubicBezierFast(StylusPoint p0, StylusPoint p1, StylusPoint p2, StylusPoint p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
            double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
            float pressure = (float)(p1.PressureFactor * u + p2.PressureFactor * t);

            return new StylusPoint(x, y, Math.Max(pressure, 0.1f));
        }

        /// <summary>
        /// Stop accepting work. In-flight computations retain their semaphore lease.
        /// </summary>
        public void Dispose()
        {
            _isInitialized = false;
        }
    }


}
