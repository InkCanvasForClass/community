using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasForClassX.Libraries
{
    /// <summary>
    /// 該控件提供一個基於<c>DrawingVisual</c>的最小的<c>StrokeCollection</c>渲染控件，用以替代重量級的<c>InkCanvas</c>控件
    /// </summary>
    public class InkProjector : FrameworkElement {
        private VisualCollection _children;
        private DrawingVisual _layer = new DrawingVisual();
        private StrokeCollection _strokes;
        private PerfectFreehandJint _perfectFreehandJint = new PerfectFreehandJint();

        public InkProjector()
        {
            _children = new VisualCollection(this) {
                _layer // 初始化DrawingVisual
            };
        }

        public StrokeCollection Strokes {
            get => _strokes;
            set {
                _strokes = value;
                DrawPerfectInk();
            }
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) {
            if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException();
            return _children[index];
        }

        private void DrawInk() {
            DrawingContext context = _layer.RenderOpen();
            _strokes.Draw(context);
            context.Close();
        }

        private void DrawPerfectInk() {
            DrawingContext context = _layer.RenderOpen();
            context.PushClip(new RectangleGeometry(new Rect(new Size(this.ActualWidth,this.ActualHeight))));
            foreach (var stroke in _strokes) {
                var stylusPtsList = new List<PerfectFreehandJint.StylusPointLite>();
                foreach (var strokeStylusPoint in stroke.StylusPoints)
                {
                    stylusPtsList.Add(new PerfectFreehandJint.StylusPointLite()
                    {
                        x = Math.Round(strokeStylusPoint.X, 2),
                        y = Math.Round(strokeStylusPoint.Y, 2),
                        pressure = strokeStylusPoint.PressureFactor,
                    });
                }
                context.DrawGeometry(new SolidColorBrush(Colors.Black), (System.Windows.Media.Pen)null, _perfectFreehandJint.GetGeometryStroke(stylusPtsList.ToArray(), new PerfectFreehandJint.StrokeOptions()
                {
                    size = 2,
                    thinning = 0.5,
                    smoothing = 0.5,
                    streamline = 0.2,
                    simulatePressure = true,
                    easing = (x) => 1 - (1 - x) * (1 - x),
                    last = true,
                    start = new PerfectFreehandJint.StrokeCapOptions()
                    {
                        cap = true,
                        taper = 0,
                        easing = (x) => 1 - (1 - x) * (1 - x),
                    },
                    end = new PerfectFreehandJint.StrokeCapOptions()
                    {
                        cap = true,
                        taper = 0,
                        easing = (x) => 1 - (1 - x) * (1 - x),
                    },
                }));
            }
            context.Pop();
            context.Close();
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            base.OnMouseLeave(e);
            Trace.WriteLine("Mouse Move");
        }
    }
}
