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
            
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality); 
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased); 
            RenderOptions.SetCachingHint(this, CachingHint.Cache); 
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

        public IReadOnlyList<DrawingVisual> Visuals => _visuals;
    }

    /// <summary>
    /// 用于显示笔迹的类 
    /// </summary>
    public class StrokeVisual
    {
        private int _lastDrawnPointCount = 0; 
        private const int INCREMENTAL_DRAW_THRESHOLD = 2; 
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
        private void DrawSegmentToNewVisual(int startIndex, int endIndex)
        {
            if (Stroke == null || Stroke.StylusPoints.Count == 0 || _visualCanvas == null) return;
            if (startIndex >= endIndex || startIndex < 0 || endIndex > Stroke.StylusPoints.Count) return;

            var points = Stroke.StylusPoints;
            var drawingAttributes = Stroke.DrawingAttributes;
            
            // 创建新的DrawingVisual用于绘制这个点段
            var segmentVisual = new DrawingVisual();
            
            RenderOptions.SetBitmapScalingMode(segmentVisual, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(segmentVisual, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(segmentVisual, CachingHint.Cache);

            using (var dc = segmentVisual.RenderOpen())
            {
                var pen = new Pen(new SolidColorBrush(drawingAttributes.Color), drawingAttributes.Width);
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.LineJoin = PenLineJoin.Round;

                // 绘制指定范围内的点段
                if (endIndex - startIndex >= 2)
                {
                    // 多个点，绘制线段
                    for (int i = startIndex; i < endIndex - 1 && i < points.Count - 1; i++)
                    {
                        var startPoint = new Point(points[i].X, points[i].Y);
                        var endPoint = new Point(points[i + 1].X, points[i + 1].Y);
                        dc.DrawLine(pen, startPoint, endPoint);
                    }
                }
                else if (endIndex - startIndex == 1 && startIndex < points.Count)
                {
                    // 只有一个点，绘制圆点
                    var brush = new SolidColorBrush(drawingAttributes.Color);
                    var point = points[startIndex];
                    dc.DrawEllipse(brush, null, new Point(point.X, point.Y), 
                        drawingAttributes.Width / 2, drawingAttributes.Height / 2);
                }
            }

            // 将新的DrawingVisual添加到VisualCanvas中
            _visualCanvas.AddVisual(segmentVisual);
        }

        /// <summary>
        /// 重新画出笔迹
        /// </summary>
        public void Redraw()
        {
            if (Stroke == null || _visualCanvas == null) return;

            var currentPointCount = Stroke.StylusPoints.Count;
            if (currentPointCount == 0) return;

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
                _visualCanvas.Clear();
            }
            _lastDrawnPointCount = 0;
            Redraw();
        }

        private readonly DrawingAttributes _drawingAttributes;

        public static implicit operator Stroke(StrokeVisual v)
        {
            throw new NotImplementedException();
        }
    }
}
