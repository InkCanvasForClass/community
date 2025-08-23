using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Helpers;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {

        public bool isUsingGeometryEraser = false;
        private IncrementalStrokeHitTester hitTester = null;

        public double eraserWidth = 64;
        public bool isEraserCircleShape = false;
        public bool isUsingStrokesEraser = false;

        private Matrix scaleMatrix = new Matrix();

        private void EraserOverlay_Loaded(object sender, RoutedEventArgs e) {
            var bd = (Border)sender;
            bd.StylusDown += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Border)o).CaptureStylus();
                EraserOverlay_PointerDown(sender);
            });
            bd.StylusUp += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Border)o).ReleaseStylusCapture();
                EraserOverlay_PointerUp(sender);
            });
            bd.StylusMove += ((o, args) => {
                e.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetPosition(Main_Grid));
            });
            bd.MouseDown += ((o, args) => {
                ((Border)o).CaptureMouse();
                EraserOverlay_PointerDown(sender);
            });
            bd.MouseUp += ((o, args) => {
                ((Border)o).ReleaseMouseCapture();
                EraserOverlay_PointerUp(sender);
            });
            bd.MouseMove += ((o, args) => {
                EraserOverlay_PointerMove(sender, args.GetPosition(Main_Grid));
            });
            bd.StylusButtonUp += (o, args) => {
                Trace.WriteLine("ButtonUp!!!!!");
            };
        }

        private void EraserOverlay_PointerDown(object sender) {
            if (isUsingGeometryEraser) return;

            // lock
            isUsingGeometryEraser = true;

            // caculate height
            var _h = eraserWidth * 56 / 38;

            // init hittester
            hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(new RectangleStylusShape(
                eraserWidth, _h));
            hitTester.StrokeHit += EraserGeometry_StrokeHit;

            // eraser bitmap cache
            EraserOverlay_DrawingVisual.CacheMode = new BitmapCache();

            // caculate scale matrix
            var scaleX = eraserWidth / 38;
            var scaleY = _h / 56;
            scaleMatrix = new Matrix();
            scaleMatrix.ScaleAt(scaleX,scaleY,0,0);
        }

        private void EraserOverlay_PointerUp(object sender) {
            if (!isUsingGeometryEraser) return;

            // unlock
            isUsingGeometryEraser = false;

            // release capture
            ((Border)sender).ReleaseMouseCapture();

            // hide eraser feedback
            var ct = EraserOverlay_DrawingVisual.DrawingVisual.RenderOpen();
            ct.DrawRectangle(new SolidColorBrush(Colors.Transparent),null,new Rect(0,0,ActualWidth,ActualHeight));
            ct.Close();

            // end hittest
            hitTester.EndHitTesting();

            // commit stroke erased history
            // 我有受虐倾向，被这个bug硬控10秒钟，请大家嘲笑我
            if (ReplacedStroke != null || AddedStroke != null) {
                timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }
        }

        private void EraserGeometry_StrokeHit(object sender,
            StrokeHitEventArgs args) {
            StrokeCollection eraseResult =
                args.GetPointEraseResults();
            StrokeCollection strokesToReplace = new StrokeCollection {
                args.HitStroke
            };

            // replace the old stroke with the new one.
            var filtered_2replace = strokesToReplace.Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
            var filtered2Replace = filtered_2replace as Stroke[] ?? filtered_2replace.ToArray();
            if (!filtered2Replace.Any()) return;
            var filtered_result = eraseResult.Where(stroke=>!stroke.ContainsPropertyData(IsLockGuid));
            var filteredResult = filtered_result as Stroke[] ?? filtered_result.ToArray();
            if (filteredResult.Any()) {
                inkCanvas.Strokes.Replace(new StrokeCollection(filtered2Replace), new StrokeCollection(filteredResult));
            } else {
                inkCanvas.Strokes.Remove(new StrokeCollection(filtered2Replace));
            }
        }

        private void EraserOverlay_PointerMove(object sender, Point pt) {
            if (!isUsingGeometryEraser) return;

            if (isUsingStrokesEraser) {
                var _filtered = inkCanvas.Strokes.HitTest(pt).Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
                var filtered = _filtered as Stroke[] ?? _filtered.ToArray();
                if (!filtered.Any()) return;
                inkCanvas.Strokes.Remove(new StrokeCollection(filtered));
            } else {
                // draw eraser feedback
                var ct = EraserOverlay_DrawingVisual.DrawingVisual.RenderOpen();
                var mt = scaleMatrix;
                var _h = eraserWidth * 56 / 38;
                mt.Translate(pt.X - eraserWidth / 2, pt.Y - _h / 2);
                ct.PushTransform(new MatrixTransform(mt));
                ct.DrawDrawing(FindResource(isEraserCircleShape?"EraserCircleDrawingGroup":"EraserDrawingGroup") as DrawingGroup);
                ct.Pop();
                ct.Close();

                // add point to hittester
                hitTester.AddPoint(pt);
            }
            
        }
    }
}
