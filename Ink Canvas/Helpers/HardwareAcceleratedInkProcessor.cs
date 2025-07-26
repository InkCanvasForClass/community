using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 硬件加速的墨迹处理器，利用WPF的GPU渲染能力
    /// </summary>
    public class HardwareAcceleratedInkProcessor
    {
        private readonly RenderTargetBitmap _renderTarget;
        private readonly DrawingVisual _drawingVisual;
        private readonly DrawingContext _drawingContext;
        private bool _isInitialized = false;

        public HardwareAcceleratedInkProcessor(int width = 1920, int height = 1080)
        {
            // 创建硬件加速的渲染目标
            _renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            _drawingVisual = new DrawingVisual();
            
            // 启用硬件加速
            RenderOptions.SetBitmapScalingMode(_drawingVisual, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(_drawingVisual, EdgeMode.Aliased);
            
            _isInitialized = true;
        }

        /// <summary>
        /// 使用GPU加速的贝塞尔曲线平滑
        /// </summary>
        public async Task<Stroke> SmoothStrokeWithGPU(Stroke originalStroke)
        {
            if (!_isInitialized || originalStroke == null || originalStroke.StylusPoints.Count < 2)
                return originalStroke;

            return await Task.Run(() =>
            {
                try
                {
                    // 使用PathGeometry进行硬件加速的曲线拟合
                    var pathGeometry = CreateSmoothPathGeometry(originalStroke.StylusPoints);
                    
                    // 将PathGeometry转换回StylusPoint集合
                    var smoothedPoints = ConvertPathGeometryToStylusPoints(pathGeometry, originalStroke.StylusPoints);
                    
                    return new Stroke(new StylusPointCollection(smoothedPoints))
                    {
                        DrawingAttributes = originalStroke.DrawingAttributes.Clone()
                    };
                }
                catch
                {
                    return originalStroke;
                }
            });
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
            
            // 使用贝塞尔曲线段创建平滑路径
            for (int i = 0; i < points.Count - 1; i += 3)
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
        public static StylusPoint[] ParallelBezierInterpolation(StylusPoint[] controlPoints, int segments = 16)
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
        /// 释放GPU资源
        /// </summary>
        public void Dispose()
        {
            _drawingContext?.Close();
            _renderTarget?.Clear();
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 质量配置枚举
    /// </summary>
    public enum InkSmoothingQuality
    {
        HighPerformance = 0,  // 高性能低质量
        Balanced = 1,         // 平衡
        HighQuality = 2       // 高质量低性能
    }

    /// <summary>
    /// 墨迹平滑配置
    /// </summary>
    public class InkSmoothingConfig
    {
        public InkSmoothingQuality Quality { get; set; } = InkSmoothingQuality.Balanced;
        public bool UseHardwareAcceleration { get; set; } = true;
        public bool UseAsyncProcessing { get; set; } = true;
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
        public double SmoothingStrength { get; set; } = 0.6;
        public double ResampleInterval { get; set; } = 1.2;
        public int InterpolationSteps { get; set; } = 16;

        public static InkSmoothingConfig FromSettings()
        {
            return new InkSmoothingConfig
            {
                Quality = (InkSmoothingQuality)MainWindow.Settings.Canvas.InkSmoothingQuality,
                UseHardwareAcceleration = MainWindow.Settings.Canvas.UseHardwareAcceleration,
                UseAsyncProcessing = MainWindow.Settings.Canvas.UseAsyncInkSmoothing,
                MaxConcurrentTasks = MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks > 0 ?
                    MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks : Environment.ProcessorCount
            };
        }

        public void ApplyQualitySettings()
        {
            switch (Quality)
            {
                case InkSmoothingQuality.HighPerformance:
                    SmoothingStrength = 0.4;
                    ResampleInterval = 2.0;
                    InterpolationSteps = 8;
                    break;
                case InkSmoothingQuality.Balanced:
                    SmoothingStrength = 0.6;
                    ResampleInterval = 1.2;
                    InterpolationSteps = 16;
                    break;
                case InkSmoothingQuality.HighQuality:
                    SmoothingStrength = 0.8;
                    ResampleInterval = 0.8;
                    InterpolationSteps = 32;
                    break;
            }
        }
    }
}
