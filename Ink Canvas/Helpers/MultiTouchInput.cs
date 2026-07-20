using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public class VisualCanvas : FrameworkElement
    {
        private readonly List<DrawingVisual> _visuals = new List<DrawingVisual>();

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visuals.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _visuals[index];
        }

        protected override int VisualChildrenCount => _visuals.Count;

        public VisualCanvas()
        {
            CacheMode = new BitmapCache();

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(this, CachingHint.Cache);
        }

        public void AddVisual(DrawingVisual visual)
        {
            if (visual == null) return;
            _visuals.Add(visual);
            AddVisualChild(visual);
        }

        public void RemoveVisual(DrawingVisual visual)
        {
            if (visual == null) return;
            if (!_visuals.Remove(visual)) return;
            RemoveVisualChild(visual);
        }

        public void Clear()
        {
            foreach (var visual in _visuals)
            {
                RemoveVisualChild(visual);
            }
            _visuals.Clear();
        }

        public IReadOnlyList<DrawingVisual> Visuals => _visuals;
    }

    /// <summary>
    /// 用于显示笔迹的类
    /// </summary>
    public class StrokeVisual
    {
        private int _lastCommittedPointCount = 0;
        private const int COMMIT_POINT_THRESHOLD = 24;
        private DrawingVisual _activeVisual;
        private VisualCanvas _visualCanvas;

        /// <summary>
        ///     创建显示笔迹的类
        /// </summary>
        public StrokeVisual() : this(new DrawingAttributes
        {
            Color = Colors.Red,
            //FitToCurve = true,
            Width = 3,
            Height = 3
        })
        {
        }

        /// <summary>
        /// 创建显示笔迹的类
        /// </summary>
        /// <param name="drawingAttributes"></param>
        public StrokeVisual(DrawingAttributes drawingAttributes)
        {
            _drawingAttributes = drawingAttributes;
        }

        /// <summary>
        /// 设置或获取显示的笔迹
        /// </summary>
        public Stroke Stroke { set; get; }

        /// <summary>
        /// 设置关联的VisualCanvas
        /// </summary>
        public void SetVisualCanvas(VisualCanvas visualCanvas)
        {
            _visualCanvas = visualCanvas;
        }

        /// <summary>
        /// 在笔迹中添加点
        /// </summary>
        /// <param name="point"></param>
        public void Add(StylusPoint point)
        {
            if (Stroke == null)
            {
                var collection = new StylusPointCollection { point };
                Stroke = new Stroke(collection) { DrawingAttributes = _drawingAttributes };
            }
            else
            {
                Stroke.StylusPoints.Add(point);
            }
        }

        /// <summary>
        /// 绘制点段到新的DrawingVisual
        /// </summary>
        private static double PressureToVisualScale(float pressureFactor, bool ignorePressure)
        {
            if (ignorePressure)
                return 1.0;
            // 与 WPF 墨迹观感接近：0.5 为标称，压低变细、抬高变粗（预览此前固定 Pen 宽，等同忽略压感）
            return Math.Max(0.22, Math.Min(2.1, 0.42 + 1.16 * pressureFactor));
        }

        private DrawingVisual CreateDrawingVisual()
        {
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(visual, CachingHint.Cache);
            return visual;
        }

        private void DrawSegment(DrawingVisual visual, int startIndex, int endIndex)
        {
            if (Stroke == null || Stroke.StylusPoints.Count == 0 || visual == null) return;
            if (startIndex >= endIndex || startIndex < 0 || endIndex > Stroke.StylusPoints.Count) return;

            var points = Stroke.StylusPoints;
            var drawingAttributes = Stroke.DrawingAttributes;
            var ignorePressure = drawingAttributes.IgnorePressure;

            using (var dc = visual.RenderOpen())
            {
                if (endIndex - startIndex >= 2)
                {
                    for (int i = startIndex; i < endIndex - 1 && i < points.Count - 1; i++)
                    {
                        var startPoint = new Point(points[i].X, points[i].Y);
                        var endPoint = new Point(points[i + 1].X, points[i + 1].Y);
                        var s0 = PressureToVisualScale(points[i].PressureFactor, ignorePressure);
                        var s1 = PressureToVisualScale(points[i + 1].PressureFactor, ignorePressure);
                        var thickness = Math.Max(0.35, (drawingAttributes.Width * s0 + drawingAttributes.Width * s1) / 2.0);
                        var pen = new Pen(new SolidColorBrush(drawingAttributes.Color), thickness)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round,
                            LineJoin = PenLineJoin.Round
                        };
                        dc.DrawLine(pen, startPoint, endPoint);
                    }
                }
                else if (endIndex - startIndex == 1 && startIndex < points.Count)
                {
                    var brush = new SolidColorBrush(drawingAttributes.Color);
                    var point = points[startIndex];
                    var s = PressureToVisualScale(point.PressureFactor, ignorePressure);
                    dc.DrawEllipse(brush, null, new Point(point.X, point.Y),
                        drawingAttributes.Width * s / 2, drawingAttributes.Height * s / 2);
                }
            }
        }

        private void CommitActiveVisual(int currentPointCount)
        {
            if (currentPointCount <= _lastCommittedPointCount + 1) return;

            var committedVisual = CreateDrawingVisual();
            var startIndex = _lastCommittedPointCount == 0 ? 0 : _lastCommittedPointCount - 1;
            DrawSegment(committedVisual, startIndex, currentPointCount);
            _visualCanvas.AddVisual(committedVisual);
            _lastCommittedPointCount = currentPointCount;
        }

        /// <summary>
        /// 重新画出笔迹
        /// </summary>
        public void Redraw()
        {
            if (Stroke == null || _visualCanvas == null) return;

            var currentPointCount = Stroke.StylusPoints.Count;
            if (currentPointCount == 0) return;

            try
            {
                if (_activeVisual == null)
                {
                    _activeVisual = CreateDrawingVisual();
                    _visualCanvas.AddVisual(_activeVisual);
                }

                var activeStartIndex = _lastCommittedPointCount == 0 ? 0 : _lastCommittedPointCount - 1;
                DrawSegment(_activeVisual, activeStartIndex, currentPointCount);

                if (currentPointCount - _lastCommittedPointCount >= COMMIT_POINT_THRESHOLD)
                {
                    _visualCanvas.RemoveVisual(_activeVisual);
                    _activeVisual = null;
                    CommitActiveVisual(currentPointCount);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 强制重绘。当点数回退（如停顿拉直替换 StylusPoints）时清除全部视觉重建；
        /// 否则只清除活跃区段，保留已提交的视觉缓存。
        /// </summary>
        public void ForceRedraw()
        {
            if (Stroke == null || _visualCanvas == null) return;

            var currentPointCount = Stroke.StylusPoints.Count;

            // 点数回退（笔画被替换/缩短），必须清除全部已提交视觉重建
            if (currentPointCount < _lastCommittedPointCount)
            {
                _visualCanvas.Clear();
                _activeVisual = null;
                _lastCommittedPointCount = 0;
            }
            else if (_activeVisual != null)
            {
                _visualCanvas.RemoveVisual(_activeVisual);
                _activeVisual = null;
            }

            Redraw();
        }

        private readonly DrawingAttributes _drawingAttributes;

    }
}
