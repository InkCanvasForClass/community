using System;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public class VisualCanvas : FrameworkElement
    {
        protected override Visual GetVisualChild(int index)
        {
            return Visual;
        }

        protected override int VisualChildrenCount => 1;

        public VisualCanvas(DrawingVisual visual)
        {
            Visual = visual;
            AddVisualChild(visual);
        }

        public DrawingVisual Visual { get; }
    }

    /// <summary>
    /// 用于显示笔迹的类 
    /// </summary>
    public class StrokeVisual : DrawingVisual
    {
        private bool _needsRedraw = true;
        private int _lastPointCount = 0;
        private const int REDRAW_THRESHOLD = 3;

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
        ///     创建显示笔迹的类
        /// </summary>
        /// <param name="drawingAttributes"></param>
        public StrokeVisual(DrawingAttributes drawingAttributes)
        {
            _drawingAttributes = drawingAttributes;

            // 启用硬件加速
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(this, CachingHint.Cache);
        }

        /// <summary>
        /// 设置或获取显示的笔迹
        /// </summary>
        public Stroke Stroke { set; get; }

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
                _lastPointCount = 1;
            }
            else
            {
                Stroke.StylusPoints.Add(point);
                _lastPointCount++;
            }

            // 标记需要重绘
            _needsRedraw = true;
        }

        /// <summary>
        /// 重新画出笔迹
        /// </summary>
        public void Redraw()
        {
            if (!_needsRedraw || Stroke == null) return;

            if (_lastPointCount % REDRAW_THRESHOLD != 0 && _lastPointCount > REDRAW_THRESHOLD)
            {
                return;
            }

            try
            {
                using (var dc = RenderOpen())
                {
                    Stroke.Draw(dc);
                }
                _needsRedraw = false;
            }
            catch { }
        }

        /// <summary>
        /// 强制重绘
        /// </summary>
        public void ForceRedraw()
        {
            _needsRedraw = true;
            Redraw();
        }

        private readonly DrawingAttributes _drawingAttributes;

        public static implicit operator Stroke(StrokeVisual v)
        {
            throw new NotImplementedException();
        }
    }
}
