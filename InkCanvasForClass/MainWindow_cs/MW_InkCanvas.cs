using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;
using System.Windows.Media;
using iNKORE.UI.WPF.Helpers;

namespace Ink_Canvas {

    public class IccStroke : Stroke {

        public IccStroke(StylusPointCollection stylusPoints, DrawingAttributes drawingAttributes)
            : base(stylusPoints, drawingAttributes) { }

        public static Guid StrokeShapeTypeGuid = new Guid("6537b29c-557f-487f-800b-cb30a8f1de78");
        public static Guid StrokeIsShapeGuid = new Guid("40eff5db-9346-4e42-bd46-7b0eb19d0018");

        public StylusPointCollection RawStylusPointCollection { get; set; }

        public MainWindow.ShapeDrawingHelper.ArrowLineConfig ArrowLineConfig { get; set; } =
            new MainWindow.ShapeDrawingHelper.ArrowLineConfig();

        /// <summary>
        /// 根据这个属性判断当前 Stroke 是否是原始输入
        /// </summary>
        public bool IsRawStylusPoints = true;

        /// <summary>
        /// 根据这个属性决定在绘制 Stroke 时是否需要在直线形状中，在两点构成直线上分布点，用于墨迹的范围框选。
        /// </summary>
        public bool IsDistributePointsOnLineShape = true;

        /// <summary>
        /// 指示该墨迹是否来自一个完整墨迹被擦除后的一部分墨迹，仅用于形状墨迹。
        /// </summary>
        public bool IsErasedStrokePart = false;

        // 自定义的墨迹渲染
        protected override void DrawCore(DrawingContext drawingContext,
            DrawingAttributes drawingAttributes) {
            if (!(this.ContainsPropertyData(StrokeIsShapeGuid) &&
                  (bool)this.GetPropertyData(StrokeIsShapeGuid) == true)) {
                base.DrawCore(drawingContext, drawingAttributes);
                return;
            }

            if ((int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.DashedLine ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.Line ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.DottedLine ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.ArrowOneSide ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.ArrowTwoSide) {
                if (StylusPoints.Count < 2) {
                    base.DrawCore(drawingContext, drawingAttributes);
                    return;
                }

                var pts = new List<Point>(this.StylusPoints.ToPoints());
                if (IsDistributePointsOnLineShape && (
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.DashedLine ||
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.Line ||
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.DottedLine) && IsRawStylusPoints) {
                    IsRawStylusPoints = false;
                    RawStylusPointCollection = StylusPoints.Clone();
                    var pointList = new List<Point> { new Point(StylusPoints[0].X, StylusPoints[0].Y) };
                    pointList.AddRange(MainWindow.ShapeDrawingHelper.DistributePointsOnLine(new Point(StylusPoints[0].X, StylusPoints[0].Y),new Point(StylusPoints[1].X, StylusPoints[1].Y)));
                    pointList.Add(new Point(StylusPoints[1].X, StylusPoints[1].Y));
                    StylusPoints = new StylusPointCollection(pointList);
                }
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open()) {
                    ctx.BeginFigure(pts[0], false , false);
                    pts.RemoveAt(0);
                    ctx.PolyLineTo(pts,true, true);
                }
                var pen = new Pen(new SolidColorBrush(DrawingAttributes.Color),
                    (drawingAttributes.Width + drawingAttributes.Height) / 2) {
                    DashCap = PenLineCap.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };
                if ((int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)MainWindow.ShapeDrawingType.Line && 
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)MainWindow.ShapeDrawingType.ArrowOneSide && 
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)MainWindow.ShapeDrawingType.ArrowTwoSide)
                    pen.DashStyle = (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.DottedLine ? DashStyles.Dot : DashStyles.Dash;
                
                if ((int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)MainWindow.ShapeDrawingType.ArrowOneSide && IsRawStylusPoints) {
                    IsRawStylusPoints = false;
                    pts = new List<Point>(this.StylusPoints.ToPoints());
                    RawStylusPointCollection = StylusPoints.Clone();
                    double w = ArrowLineConfig.ArrowWidth, h = ArrowLineConfig.ArrowHeight;
                    var theta = Math.Atan2(pts[0].Y - pts[1].Y, pts[0].X - pts[1].X);
                    var sint = Math.Sin(theta);
                    var cost = Math.Cos(theta);
                    var pointList = new List<Point> {
                        new Point(pts[0].X, pts[0].Y),
                    };
                    if (IsDistributePointsOnLineShape) pointList.AddRange(MainWindow.ShapeDrawingHelper.DistributePointsOnLine(new Point(pts[0].X, pts[0].Y),new Point(pts[1].X, pts[1].Y)));
                    pointList.AddRange(new List<Point> {
                        new Point(pts[1].X, pts[1].Y),
                        new Point(pts[1].X + (w * cost - h * sint), pts[1].Y + (w * sint + h * cost)),
                        new Point(pts[1].X, pts[1].Y),
                        new Point(pts[1].X + (w * cost + h * sint), pts[1].Y - (h * cost - w * sint)),
                    });
                    StylusPoints = new StylusPointCollection(pointList);
                    var _pts = new List<Point>(this.StylusPoints.ToPoints());
                    using (StreamGeometryContext ctx = geometry.Open()) {
                        ctx.BeginFigure(_pts[0], false , false);
                        _pts.RemoveAt(0);
                        ctx.PolyLineTo(_pts,true, true);
                    }
                    drawingContext.DrawGeometry(new SolidColorBrush(DrawingAttributes.Color),pen, geometry);
                    return;

                }
                drawingContext.DrawGeometry(new SolidColorBrush(Colors.Transparent),pen, geometry);
            }
            
        }
    }

    class CustomDynamicRenderer : DynamicRenderer {

        private Point prevPoint;

        private List<StylusPoint> pointsList = new List<StylusPoint>();

        private void ClearPointsList() {
            pointsList.Clear();
        }

        private void PushPoint(StylusPoint point) {
            pointsList.Add(point);
            if (pointsList.Count > 15) {
                pointsList.RemoveRange(0,pointsList.Count - 15);
            }
        }

        protected override void OnStylusDown(RawStylusInput rawStylusInput) {
            prevPoint = new Point(double.NegativeInfinity, double.NegativeInfinity);
            ClearPointsList();
            var pts = rawStylusInput.GetStylusPoints();
            if (pts.Count == 1) {
                PushPoint(pts.Single());
            } else if (pts.Count > 1) {
                PushPoint(pts[pts.Count-1]);
            }
            Trace.WriteLine(pointsList.Count);
            base.OnStylusDown(rawStylusInput);
        }

        protected override void OnStylusMove(RawStylusInput rawStylusInput) {
            base.OnStylusMove(rawStylusInput);
            var pts = rawStylusInput.GetStylusPoints();
            if (pts.Count == 1) {
                PushPoint(pts.Single());
            } else if (pts.Count > 1) {
                PushPoint(pts[pts.Count-1]);
            }
            Trace.WriteLine(pointsList.Count);
        }

        protected override void OnDraw(DrawingContext drawingContext,
            StylusPointCollection stylusPoints,
            Geometry geometry, Brush fillBrush) {
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
                }
                else {
                    for (var i = 0; i <= n; i++) {
                        var point = new StylusPoint();

                        point.PressureFactor = (float)(0.4 * (n - i) / n + pressure);
                        point.X = pointsList[i].X;
                        point.Y = pointsList[i].Y;
                        sp.Add(point);
                    }
                }

                var da = DrawingAttributes.Clone();
                da.Width *= 0.75;
                da.Height *= 0.75;
                var stk = new Stroke(sp,da);
                stk.Draw(drawingContext);
            }
            catch { }
            
        }
    }

    public class IccInkCanvas : InkCanvas {

        CustomDynamicRenderer customDynamicRenderer = new CustomDynamicRenderer();

        public IccInkCanvas() {
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
                this.Strokes.Add(e.Stroke);
            } else {
                this.Strokes.Remove(e.Stroke);
                this.Strokes.Add(customStroke);
            }

            InkCanvasStrokeCollectedEventArgs args =
                new InkCanvasStrokeCollectedEventArgs(customStroke);
            base.OnStrokeCollected(args);
        }

        public event EventHandler<RoutedEventArgs> DeleteKeyCommandFired;
    }
}