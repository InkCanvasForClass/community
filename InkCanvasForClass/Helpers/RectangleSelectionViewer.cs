using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ink_Canvas.Helpers
{
    
    public class RectangleSelectionViewer : FrameworkElement
    {
        private VisualCollection _children;
        private DrawingVisual _layer = new DrawingVisual();
        private Pen defaultPen = new Pen();
        private Pen lassoPen = new Pen();

        public RectangleSelectionViewer()
        {
            _children = new VisualCollection(this) {
                _layer // 初始化DrawingVisual
            };
            defaultPen.Thickness = 2;
            defaultPen.Brush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            defaultPen.DashStyle = DashStyles.Dash;

            lassoPen.Thickness = 6;
            lassoPen.Brush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            lassoPen.DashStyle = new DashStyle(new double[]{0,2},0);
            lassoPen.DashCap = PenLineCap.Round;
            lassoPen.StartLineCap = PenLineCap.Round;
            lassoPen.EndLineCap = PenLineCap.Round;
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException();
            return _children[index];
        }

        public void DrawSelectionBox(Rect rect) {
            DrawingContext context = _layer.RenderOpen();
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(78, 96, 165, 250)), defaultPen, rect, 4, 4);
            context.Close();
        }

        public void DrawLassoLine(PointCollection pts) {
            DrawingContext context = _layer.RenderOpen();
            if (pts.Count > 2) {
                StreamGeometry geometry = new StreamGeometry();
                var _pts = pts.Clone();
                _pts.RemoveAt(0);
                using (StreamGeometryContext ctx = geometry.Open()) {
                    ctx.BeginFigure(pts[0], true , false);
                    ctx.PolyLineTo(_pts,true, true);
                }
                context.DrawGeometry(new SolidColorBrush(Colors.Transparent), lassoPen, geometry);
            } else if (pts.Count == 2) {
                context.DrawLine(defaultPen, pts[0], pts[1]);
            } else if (pts.Count == 1) {
                context.DrawLine(defaultPen, pts[0], pts[0]);
            }
            context.Close();
        }

        public void ClearDrawing() {
            DrawingContext context = _layer.RenderOpen();
            context.Close();
        }
    }
}
