using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;
using System.Windows.Media;
using InkCanvasForClass.IccInkCanvas.Settings;

namespace InkCanvasForClass.IccInkCanvas {
    class CustomDynamicRenderer : DynamicRenderer {

        private class StylusProcessedCallbackData {
            public int StylusDeviceID;
            public int TabletDeviceID;
        }

        private Point prevPoint;

        private IccInkCanvas _inkCanvas;

        private List<StylusPoint> pointsList = new List<StylusPoint>();

        private StylusProcessedCallbackData _stylusProcessedCallbackData;

        public InputtingDeviceType InputType = InputtingDeviceType.None;

        private void ClearPointsList() {
            pointsList.Clear();
        }

        private void PushPoint(StylusPoint point) {
            pointsList.Add(point);
            if (pointsList.Count > 15) {
                pointsList.RemoveRange(0,pointsList.Count - 15);
            }
        }

        public CustomDynamicRenderer(IccInkCanvas iccInkCanvas) {
            _inkCanvas = iccInkCanvas;
        }

        protected override void OnStylusDown(RawStylusInput rawStylusInput) {
            prevPoint = new Point(double.NegativeInfinity, double.NegativeInfinity);
            ClearPointsList();

            // 判断当前设备类型
            var styDevice = rawStylusInput.StylusDeviceId;
            var tabletDevice = rawStylusInput.TabletDeviceId;

            var pts = rawStylusInput.GetStylusPoints();
            if (pts.Count == 1) {
                PushPoint(pts.Single());
            } else if (pts.Count > 1) {
                PushPoint(pts[pts.Count-1]);
            }

            base.OnStylusDown(rawStylusInput);

            _stylusProcessedCallbackData = new StylusProcessedCallbackData() {
                StylusDeviceID = styDevice,
                TabletDeviceID = tabletDevice,
            };
        }

        protected override void OnStylusDownProcessed(object callbackData, bool targetVerified) {
            var data = _stylusProcessedCallbackData;
            var tabletDevices = Tablet.TabletDevices;
            if (data.StylusDeviceID == 0 && data.TabletDeviceID == 0) InputType = InputtingDeviceType.Mouse;
            foreach (TabletDevice tabletDevice in tabletDevices) {
                if (tabletDevice.Id == data.TabletDeviceID && tabletDevice.Type == TabletDeviceType.Stylus)
                    InputType = InputtingDeviceType.Stylus;
                if (tabletDevice.Id == data.TabletDeviceID && tabletDevice.Type == TabletDeviceType.Touch)
                    InputType = InputtingDeviceType.Touch;
            }
            
            base.OnStylusDownProcessed(callbackData, targetVerified);
        }

        protected override void OnStylusMove(RawStylusInput rawStylusInput) {
            base.OnStylusMove(rawStylusInput);
            var pts = rawStylusInput.GetStylusPoints();
            if (pts.Count == 1) {
                PushPoint(pts.Single());
            } else if (pts.Count > 1) {
                if (InputType != InputtingDeviceType.Stylus) PushPoint(pts[pts.Count-1]);
            }
        }

        protected override void OnStylusUpProcessed(object callbackData, bool targetVerified) {
            //InputType = InputtingDeviceType.None;
            // TODO: 触摸支持
            base.OnStylusUpProcessed(callbackData, targetVerified);
        }

        private void DrawBeautifulNibStroke(DrawingContext drawingContext) {
            try {
                var sp = new StylusPointCollection();
                var n = pointsList.Count - 1;
                var pressure = 0.1;
                var x = 10;
                if (n == 1) return;
                if (n >= x) {
                    for (var i = 0; i < n - x; i++) {
                        var point = new StylusPoint();

                        point.PressureFactor = (float)0.5;
                        point.X = pointsList[i].X;
                        point.Y = pointsList[i].Y;
                        sp.Add(point);
                    }

                    for (var i = n - x; i <= n; i++) {
                        var point = new StylusPoint();

                        point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                        point.X = pointsList[i].X;
                        point.Y = pointsList[i].Y;
                        sp.Add(point);
                    }
                } else {
                    for (var i = 0; i <= n; i++) {
                        var point = new StylusPoint();

                        point.PressureFactor = (float)(0.4 * (n - i) / n + pressure);
                        point.X = pointsList[i].X;
                        point.Y = pointsList[i].Y;
                        sp.Add(point);
                    }
                }

                var da = DrawingAttributes.Clone();
                da.Width -= 0.5;
                da.Height -= 0.5;
                var stk = new Stroke(sp, da);
                stk.Draw(drawingContext);
            } catch {}
        }

        private void DrawSolidNibStroke(DrawingContext drawingContext) {
            try {
                var sp = new StylusPointCollection();
                foreach (var pt in pointsList) {
                    var point = new StylusPoint();
                    point.PressureFactor = (float)0.5;
                    point.X = pt.X;
                    point.Y = pt.Y;
                    sp.Add(point);
                }
                var da = DrawingAttributes.Clone();
                da.Width -= 0.5;
                da.Height -= 0.5;
                var stk = new Stroke(sp, da);
                stk.Draw(drawingContext);
            }
            catch {}
        }

        private void DrawSolidNibStrokeForStylus(StylusPointCollection stylusPoints, DrawingContext drawingContext) {
            try {
                var sp = new StylusPointCollection();
                foreach (var pt in stylusPoints) {
                    var point = new StylusPoint();
                    point.PressureFactor = (float)0.5;
                    point.X = pt.X;
                    point.Y = pt.Y;
                    sp.Add(point);
                }
                var da = DrawingAttributes.Clone();
                var stk = new Stroke(sp, da);
                stk.Draw(drawingContext);
            }
            catch {}
        }

        protected override void OnDraw(DrawingContext drawingContext,
            StylusPointCollection stylusPoints,
            Geometry geometry, Brush fillBrush) {
            if (_inkCanvas.BoardSettings.StrokeNibStyle == StrokeNibStyle.Beautiful && InputType != InputtingDeviceType.Stylus) {
                DrawBeautifulNibStroke(drawingContext);
            } else if (_inkCanvas.BoardSettings.StrokeNibStyle == StrokeNibStyle.Solid && InputType != InputtingDeviceType.Stylus) {
                DrawSolidNibStroke(drawingContext);
            } else if (_inkCanvas.BoardSettings.StrokeNibStyle == StrokeNibStyle.Solid && InputType == InputtingDeviceType.Stylus) {
                DrawSolidNibStrokeForStylus(stylusPoints, drawingContext);
            } else if (_inkCanvas.BoardSettings.StrokeNibStyle == StrokeNibStyle.Default) {
                base.OnDraw(drawingContext, stylusPoints, geometry, fillBrush);
            } else {
                base.OnDraw(drawingContext, stylusPoints, geometry, fillBrush);
            }
            
        }
    }

    internal class IccInkCanvas : InkCanvas {

        CustomDynamicRenderer customDynamicRenderer;

        public BoardSettings BoardSettings { get; set; } = new BoardSettings();

        public IccInkCanvas() {
            customDynamicRenderer = new CustomDynamicRenderer(this);
            DynamicRenderer = customDynamicRenderer;

            // 通过反射移除InkCanvas自带的默认 Delete按键事件
            var commandBindingsField =
                typeof(CommandManager).GetField("_classCommandBindings", BindingFlags.NonPublic | BindingFlags.Static);
            var bnds = commandBindingsField.GetValue(null) as HybridDictionary;
            var inkCanvasBindings = bnds[typeof(InkCanvas)] as CommandBindingCollection;
            var enumerator = inkCanvasBindings.GetEnumerator();
            while (enumerator.MoveNext()) {
                var item = (CommandBinding)enumerator.Current;
                if (item.Command == ApplicationCommands.Delete) {
                    var executedField =
                        typeof(CommandBinding).GetField("Executed", BindingFlags.NonPublic | BindingFlags.Instance);
                    var canExecuteField =
                        typeof(CommandBinding).GetField("CanExecute", BindingFlags.NonPublic | BindingFlags.Instance);
                    executedField.SetValue(item, new ExecutedRoutedEventHandler((sender, args) => { }));
                    canExecuteField.SetValue(item, new CanExecuteRoutedEventHandler((sender, args) => { }));
                }
            }

            // 为IccInkCanvas注册自定义的 Delete按键Command并Invoke OnDeleteCommandFired。
            CommandManager.RegisterClassCommandBinding(typeof(IccInkCanvas), new CommandBinding(ApplicationCommands.Delete,
                (sender, args) => {
                    DeleteKeyCommandFired?.Invoke(this, new RoutedEventArgs());
                }, (sender, args) => {
                    args.CanExecute = GetSelectedStrokes().Count != 0;
                }));
        }

        protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e) {
            IccStroke customStroke = new IccStroke(e.Stroke.StylusPoints, e.Stroke.DrawingAttributes);
            if (e.Stroke is IccStroke) {
                Strokes.Add(e.Stroke);
            } else {
                Strokes.Remove(e.Stroke);
                Strokes.Add(customStroke);
            }

            if (BoardSettings.StrokeNibStyle == StrokeNibStyle.Beautiful && customDynamicRenderer.InputType != InputtingDeviceType.Stylus) {
                try {
                    var stylusPoints = new StylusPointCollection();
                    var n = customStroke.StylusPoints.Count - 1;
                    var pressure = 0.1;
                    var x = 10;
                    if (n == 1) return;
                    if (n >= x) {
                        for (var i = 0; i < n - x; i++) {
                            var point = new StylusPoint();

                            point.PressureFactor = (float)0.5;
                            point.X = customStroke.StylusPoints[i].X;
                            point.Y = customStroke.StylusPoints[i].Y;
                            stylusPoints.Add(point);
                        }

                        for (var i = n - x; i <= n; i++) {
                            var point = new StylusPoint();

                            point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                            point.X = customStroke.StylusPoints[i].X;
                            point.Y = customStroke.StylusPoints[i].Y;
                            stylusPoints.Add(point);
                        }
                    }
                    else {
                        for (var i = 0; i <= n; i++) {
                            var point = new StylusPoint();

                            point.PressureFactor = (float)(0.4 * (n - i) / n + pressure);
                            point.X = customStroke.StylusPoints[i].X;
                            point.Y = customStroke.StylusPoints[i].Y;
                            stylusPoints.Add(point);
                        }
                    }

                    customStroke.StylusPoints = stylusPoints;
                } catch { }
            } else if (BoardSettings.StrokeNibStyle == StrokeNibStyle.Solid) {
                try {
                    var sp = new StylusPointCollection();
                    foreach (var pt in customStroke.StylusPoints) {
                        var point = new StylusPoint();
                        point.PressureFactor = (float)0.5;
                        point.X = pt.X;
                        point.Y = pt.Y;
                        sp.Add(point);
                    }
                    customStroke.StylusPoints = sp;
                } catch { }
            }

            InkCanvasStrokeCollectedEventArgs args =
                new InkCanvasStrokeCollectedEventArgs(customStroke);
            base.OnStrokeCollected(args);

            Trace.WriteLine("dfsffdsdfdfsfds");
        }

        public event EventHandler<RoutedEventArgs> DeleteKeyCommandFired;

        public InputtingDeviceType InputtingDeviceType { get; private set; }
    }
}
