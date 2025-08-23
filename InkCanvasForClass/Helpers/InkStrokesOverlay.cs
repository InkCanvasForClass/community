using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Remoting.Contexts;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Pen = System.Windows.Media.Pen;

namespace Ink_Canvas.Helpers
{
    
    public class InkStrokesOverlay : FrameworkElement
    {
        private VisualCollection _children;
        private ImprovedDrawingVisual _layer = new ImprovedDrawingVisual();
        private StrokeCollection cachedStrokeCollection = new StrokeCollection();
        private DrawingGroup cachedDrawingGroup = new DrawingGroup();
        private bool isCached = false;
        private DrawingContext context;

        public class ImprovedDrawingVisual: DrawingVisual {
            public ImprovedDrawingVisual() {
                CacheMode = new BitmapCache() {
                    EnableClearType = false,
                    RenderAtScale = 1,
                    SnapsToDevicePixels = false
                };
            }
        }

        public InkStrokesOverlay()
        {
            _children = new VisualCollection(this) {
                _layer // 初始化DrawingVisual
            };
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException();
            return _children[index];
        }

        public DrawingContext Open() {
            context = _layer.RenderOpen();
            return context;
        }

        public void Close() {
            context.Close();
        }

        public void DrawStrokes(StrokeCollection strokes, Matrix? matrixTransform, bool isOneTimeDrawing = true) {
            if (isOneTimeDrawing) {
                context = _layer.RenderOpen();
            }

            if (matrixTransform != null) context.PushTransform(new MatrixTransform((Matrix)matrixTransform));

            if (strokes.Count != 0) {
                if (!isCached || (isCached && !strokes.Equals(cachedStrokeCollection))) {
                    cachedStrokeCollection = strokes;
                    cachedDrawingGroup = new DrawingGroup();
                    var gp_context = cachedDrawingGroup.Open();
                    var stks_cloned = strokes.Clone();
                    foreach (var stroke in stks_cloned) {
                        stroke.DrawingAttributes.Width += 2;
                        stroke.DrawingAttributes.Height += 2;
                    }
                    stks_cloned.Draw(gp_context);
                    foreach (var ori_stk in strokes) {
                        var geo = ori_stk.GetGeometry();
                        gp_context.DrawGeometry(new SolidColorBrush(Colors.White),null,geo);

                    }
                    gp_context.Close();
                }
            }

            context.DrawDrawing(cachedDrawingGroup);

            if (matrixTransform != null) context.Pop();

            if (isOneTimeDrawing) {
                context.Close();
            }
        }
    }
}
