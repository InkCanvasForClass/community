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

            // VisualCanvas 不应拦截输入事件，绘制层仅用于显示笔迹
            IsHitTestVisible = false;

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(this, CachingHint.Cache);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Do not return Infinity; clamp to finite values
            double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
            return new Size(w, h);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        public void AddVisual(DrawingVisual visual)
        {
            if (visual == null) return;
            _visuals.Add(visual);
            AddVisualChild(visual);
        }
        public void Clear()
        {
            foreach (var visual in _visuals)
            {
                RemoveVisualChild(visual);
            }
            _visuals.Clear();
        }

        public void RemoveVisual(DrawingVisual visual)
        {
            if (visual == null) return;
            // remove from list first, then remove visual child (protected)
            if (_visuals.Remove(visual))
            {
                RemoveVisualChild(visual);
            }
        }

        public IReadOnlyList<DrawingVisual> Visuals => _visuals;
    }

    /// <summary>
    /// 用于显示笔迹的类 
    /// </summary>
    public class StrokeVisual
    {
        private int _lastDrawnPointCount = 0;
        private const int INCREMENTAL_DRAW_THRESHOLD = 1;
        private VisualCanvas _visualCanvas;
        private Pen _cachedPen;
        private SolidColorBrush _cachedBrush;
        private double _cachedWidth = -1;
        private Color _cachedColor = Colors.Transparent;
        private DrawingVisual _segmentVisual;
        private DrawingGroup _drawingGroup;
        // 绘制分块与节流配置
        private const int MAX_DRAWINGGROUP_CHILDREN = 64;
        private const int REDRAW_THROTTLE_MS = 16; // 限制到约 60 FPS 最大负载
        private long _lastRedrawTimestamp = 0;
        private readonly List<DrawingVisual> _segmentVisuals = new List<DrawingVisual>();

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

        private void UpdateCachedDrawingTools()
        {
            if (Stroke == null) return;
            var da = Stroke.DrawingAttributes;
            if (da == null) return;

            if (_cachedBrush == null || _cachedColor != da.Color)
            {
                _cachedBrush = new SolidColorBrush(da.Color);
                _cachedBrush.Freeze();
                _cachedColor = da.Color;
            }

            if (_cachedPen == null || Math.Abs(_cachedWidth - da.Width) > 0.01 || _cachedColor != da.Color)
            {
                _cachedPen = new Pen(_cachedBrush, da.Width);
                _cachedPen.StartLineCap = PenLineCap.Round;
                _cachedPen.EndLineCap = PenLineCap.Round;
                _cachedPen.LineJoin = PenLineJoin.Round;
                _cachedPen.Freeze();
                _cachedWidth = da.Width;
            }
        }

        /// <summary>
        /// 设置关联的VisualCanvas
        /// </summary>
        public void SetVisualCanvas(VisualCanvas visualCanvas)
        {
            _visualCanvas = visualCanvas;
            if (_visualCanvas != null && _segmentVisual == null)
            {
                // 创建首个绘制块
                _segmentVisual = new DrawingVisual();
                _segmentVisuals.Add(_segmentVisual);
                _drawingGroup = new DrawingGroup();
                using (var dc = _segmentVisual.RenderOpen())
                {
                    dc.DrawDrawing(_drawingGroup);
                }
                _visualCanvas.AddVisual(_segmentVisual);
            }
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
        private void DrawSegmentToNewVisual(int startIndex, int endIndex)
        {
            if (Stroke == null || Stroke.StylusPoints.Count == 0 || _visualCanvas == null) return;
            if (startIndex >= endIndex || startIndex < 0 || endIndex > Stroke.StylusPoints.Count) return;

            var points = Stroke.StylusPoints;
            var drawingAttributes = Stroke.DrawingAttributes;

            // 使用单个 DrawingVisual 与 DrawingGroup 来追加绘制，减少大量 Visual 分配
            if (_visualCanvas == null) return;

            UpdateCachedDrawingTools();

            // 构造当前段的几何并加入 DrawingGroup
            if (endIndex - startIndex >= 2)
            {
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    var first = new Point(points[startIndex].X, points[startIndex].Y);
                    ctx.BeginFigure(first, false, false);
                    for (int i = startIndex + 1; i < endIndex && i < points.Count; i++)
                    {
                        ctx.LineTo(new Point(points[i].X, points[i].Y), true, false);
                    }
                }
                geom.Freeze();
                var gd = new GeometryDrawing(_cachedBrush, _cachedPen, geom);
                _drawingGroup.Children.Add(gd);
            }
            else if (endIndex - startIndex == 1 && startIndex < points.Count)
            {
                var p = points[startIndex];
                var eg = new EllipseGeometry(new Point(p.X, p.Y), drawingAttributes.Width / 2, drawingAttributes.Height / 2);
                eg.Freeze();
                var gd = new GeometryDrawing(_cachedBrush, null, eg);
                _drawingGroup.Children.Add(gd);
            }

            // 如果当前 DrawingGroup 太大，则创建新的分块以继续追加，避免单个 DrawingGroup 过大
            if (_drawingGroup.Children.Count >= MAX_DRAWINGGROUP_CHILDREN)
            {
                // 新建绘制块和绘制组（首次将新组绑定到新的 DrawingVisual）
                _segmentVisual = new DrawingVisual();
                _segmentVisuals.Add(_segmentVisual);
                _drawingGroup = new DrawingGroup();
                using (var dc = _segmentVisual.RenderOpen())
                {
                    dc.DrawDrawing(_drawingGroup);
                }
                _visualCanvas.AddVisual(_segmentVisual);
                return;
            }

            // 对于当前分块，直接在 DrawingGroup 中追加 GeometryDrawing 即可，避免每次通过 RenderOpen 重绘整个分块
            if (_segmentVisual == null)
            {
                _segmentVisual = new DrawingVisual();
                _segmentVisuals.Add(_segmentVisual);
                using (var dc = _segmentVisual.RenderOpen())
                {
                    dc.DrawDrawing(_drawingGroup);
                }
                _visualCanvas.AddVisual(_segmentVisual);
            }
        }

        /// <summary>
        /// 重新画出笔迹
        /// </summary>
        public void Redraw()
        {
            if (Stroke == null || _visualCanvas == null) return;

            var currentPointCount = Stroke.StylusPoints.Count;
            if (currentPointCount == 0) return;

            // 简单节流：限制重绘频率
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastRedrawTimestamp != 0 && now - _lastRedrawTimestamp < REDRAW_THROTTLE_MS)
            {
                return;
            }
            _lastRedrawTimestamp = now;

            // 计算新增的点数
            int newPointCount = currentPointCount - _lastDrawnPointCount;

            // 如果新增点数达到阈值，才进行增量绘制
            if (newPointCount >= INCREMENTAL_DRAW_THRESHOLD || _lastDrawnPointCount == 0)
            {
                try
                {
                    if (_lastDrawnPointCount == 0)
                    {
                        // 首次绘制：绘制所有点
                        DrawSegmentToNewVisual(0, currentPointCount);
                        _lastDrawnPointCount = currentPointCount;
                    }
                    else
                    {
                        // 从上次绘制的最后一个点开始
                        int startIndex = Math.Max(0, _lastDrawnPointCount - 1);
                        DrawSegmentToNewVisual(startIndex, currentPointCount);
                        _lastDrawnPointCount = currentPointCount;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 强制重绘
        /// </summary>
        public void ForceRedraw()
        {
            if (_visualCanvas != null)
            {
                // 移除所有分块视觉
                foreach (var sv in _segmentVisuals.ToArray())
                {
                    _visualCanvas.RemoveVisual(sv);
                }
                _segmentVisuals.Clear();
                _segmentVisual = null;
            }
            _drawingGroup = new DrawingGroup();
            _lastDrawnPointCount = 0;
            _lastRedrawTimestamp = 0;
            Redraw();
        }

        private readonly DrawingAttributes _drawingAttributes;

        public static implicit operator Stroke(StrokeVisual v)
        {
            throw new NotImplementedException();
        }
    }
}
