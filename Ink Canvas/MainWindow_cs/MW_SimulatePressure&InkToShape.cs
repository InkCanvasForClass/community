using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private StrokeCollection newStrokes = new StrokeCollection();
        private List<Circle> circles = new List<Circle>();
        private const double LINE_STRAIGHTEN_THRESHOLD = 0.20; // 默认灵敏度阈值，与UI默认值对应

        private void inkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e) {
            // 标记是否进行了直线拉直
            bool wasStraightened = false;
            
            // 禁用原有的FitToCurve，使用新的高级贝塞尔曲线平滑
            if (Settings.Canvas.FitToCurve == true) drawingAttributes.FitToCurve = false;

            try {
                inkCanvas.Opacity = 1;
                
                // 应用屏蔽压感功能 - 如果启用，所有笔画都使用统一粗细
                if (Settings.Canvas.DisablePressure) {
                    var uniformPoints = new StylusPointCollection();
                    foreach (StylusPoint point in e.Stroke.StylusPoints) {
                        StylusPoint newPoint = new StylusPoint(point.X, point.Y, 0.5f); // 统一压感值为0.5
                        uniformPoints.Add(newPoint);
                    }
                    e.Stroke.StylusPoints = uniformPoints;
                }
                // 应用压感触屏模式 - 如果启用并且检测到触屏输入
                else if (Settings.Canvas.EnablePressureTouchMode) {
                    bool isTouchInput = true;
                    foreach (StylusPoint point in e.Stroke.StylusPoints) {
                        // 检测是否为压感笔输入（压感笔的PressureFactor不等于0.5或0）
                        if ((point.PressureFactor > 0.501 || point.PressureFactor < 0.5) && point.PressureFactor != 0) {
                            isTouchInput = false;
                            break;
                        }
                    }

                    // 如果是触屏输入，则应用模拟压感
                    if (isTouchInput) {
                        switch (Settings.Canvas.InkStyle) {
                            case 1:
                                if (penType == 0)
                                    try {
                                        var stylusPoints = new StylusPointCollection();
                                        var n = e.Stroke.StylusPoints.Count - 1;

                                        for (var i = 0; i <= n; i++) {
                                            var speed = GetPointSpeed(e.Stroke.StylusPoints[Math.Max(i - 1, 0)].ToPoint(),
                                                e.Stroke.StylusPoints[i].ToPoint(),
                                                e.Stroke.StylusPoints[Math.Min(i + 1, n)].ToPoint());
                                            var point = new StylusPoint();
                                            if (speed >= 0.25)
                                                point.PressureFactor = (float)(0.5 - 0.3 * (Math.Min(speed, 1.5) - 0.3) / 1.2);
                                            else if (speed >= 0.05)
                                                point.PressureFactor = (float)0.5;
                                            else
                                                point.PressureFactor = (float)(0.5 + 0.4 * (0.05 - speed) / 0.05);

                                            point.X = e.Stroke.StylusPoints[i].X;
                                            point.Y = e.Stroke.StylusPoints[i].Y;
                                            stylusPoints.Add(point);
                                        }

                                        e.Stroke.StylusPoints = stylusPoints;
                                    }
                                    catch { }
                                break;
                            case 0:
                                if (penType == 0)
                                    try {
                                        var stylusPoints = new StylusPointCollection();
                                        var n = e.Stroke.StylusPoints.Count - 1;
                                        var pressure = 0.1;
                                        var x = 10;
                                        if (n == 1) return;
                                        if (n >= x) {
                                            for (var i = 0; i < n - x; i++) {
                                                var point = new StylusPoint();

                                                point.PressureFactor = (float)0.5;
                                                point.X = e.Stroke.StylusPoints[i].X;
                                                point.Y = e.Stroke.StylusPoints[i].Y;
                                                stylusPoints.Add(point);
                                            }

                                            for (var i = n - x; i <= n; i++) {
                                                var point = new StylusPoint();

                                                point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                                                point.X = e.Stroke.StylusPoints[i].X;
                                                point.Y = e.Stroke.StylusPoints[i].Y;
                                                stylusPoints.Add(point);
                                            }
                                        }
                                        else {
                                            for (var i = 0; i <= n; i++) {
                                                var point = new StylusPoint();

                                                point.PressureFactor = (float)(0.4 * (n - i) / n + pressure);
                                                point.X = e.Stroke.StylusPoints[i].X;
                                                point.Y = e.Stroke.StylusPoints[i].Y;
                                                stylusPoints.Add(point);
                                            }
                                        }

                                        e.Stroke.StylusPoints = stylusPoints;
                                    }
                                    catch { }
                                break;
                        }
                    }
                }
                
                            // Apply line straightening and endpoint snapping if ink-to-shape is enabled
            
            if (Settings.InkToShape.IsInkToShapeEnabled) {
                    // 检查是否启用了直线自动拉直功能
                    if (Settings.Canvas.AutoStraightenLine && IsPotentialStraightLine(e.Stroke)) {
                        // Get start and end points of the stroke
                        Point startPoint = e.Stroke.StylusPoints[0].ToPoint();
                        Point endPoint = e.Stroke.StylusPoints[e.Stroke.StylusPoints.Count - 1].ToPoint();
                        
                        // 先完成所有直线判定，再考虑端点吸附
                        // 读取实际的灵敏度设置值
                        double sensitivity = Settings.InkToShape.LineStraightenSensitivity;
                        System.Diagnostics.Debug.WriteLine($"当前灵敏度值: {sensitivity}");
                        
                        // 判断是否应该拉直线条
                        bool shouldStraighten = ShouldStraightenLine(e.Stroke);
                        
                        // 输出一些调试信息，帮助理解灵敏度设置的效果
                        System.Diagnostics.Debug.WriteLine($"LineStraightenSensitivity: {Settings.InkToShape.LineStraightenSensitivity}, ShouldStraighten: {shouldStraighten}");
                        
                        // 只有当确定要拉直线条时，才检查端点吸附
                        if (shouldStraighten && Settings.Canvas.LineEndpointSnapping) {
                            // 只有在启用了形状识别（矩形或三角形）时才执行端点吸附
                            if (Settings.InkToShape.IsInkToShapeRectangle || Settings.InkToShape.IsInkToShapeTriangle) {
                                Point[] snappedPoints = GetSnappedEndpoints(startPoint, endPoint);
                                if (snappedPoints != null) {
                                    startPoint = snappedPoints[0];
                                    endPoint = snappedPoints[1];
                                }
                            }
                        }

                        // 如果确定要拉直，则创建直线
                        if (shouldStraighten) {
                            StylusPointCollection straightLinePoints = CreateStraightLine(startPoint, endPoint);
                            Stroke straightStroke = new Stroke(straightLinePoints) {
                                DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                            };
                            
                            // Replace the original stroke with the straightened one
                            SetNewBackupOfStroke();
                            _currentCommitType = CommitReason.ShapeRecognition;
                            inkCanvas.Strokes.Remove(e.Stroke);
                            inkCanvas.Strokes.Add(straightStroke);
                            _currentCommitType = CommitReason.UserInput;
                            
                            // We can't modify e.Stroke directly, but we need to update newStrokes
                            // to ensure proper shape recognition for the straightened line
                            if (newStrokes.Contains(e.Stroke)) {
                                newStrokes.Remove(e.Stroke);
                                newStrokes.Add(straightStroke);
                            }
                            
                            wasStraightened = true; // 标记已进行直线拉直
                        }
                    }
                }

                if (Settings.InkToShape.IsInkToShapeEnabled && !Environment.Is64BitProcess) {
                    void InkToShapeProcess() {
                        try {
                            newStrokes.Add(e.Stroke);
                            if (newStrokes.Count > 4) newStrokes.RemoveAt(0);
                            for (var i = 0; i < newStrokes.Count; i++)
                                if (!inkCanvas.Strokes.Contains(newStrokes[i]))
                                    newStrokes.RemoveAt(i--);

                            for (var i = 0; i < circles.Count; i++)
                                if (!inkCanvas.Strokes.Contains(circles[i].Stroke))
                                    circles.RemoveAt(i);

                            var strokeReco = new StrokeCollection();
                            var result = InkRecognizeHelper.RecognizeShape(newStrokes);
                            for (var i = newStrokes.Count - 1; i >= 0; i--) {
                                strokeReco.Add(newStrokes[i]);
                                var newResult = InkRecognizeHelper.RecognizeShape(strokeReco);
                                if (newResult.InkDrawingNode.GetShapeName() == "Circle" ||
                                    newResult.InkDrawingNode.GetShapeName() == "Ellipse") {
                                    result = newResult;
                                    break;
                                }
                                //Label.Visibility = Visibility.Visible;
                                //Label.Content = circles.Count.ToString() + "\n" + newResult.InkDrawingNode.GetShapeName();
                            }

                            if (result.InkDrawingNode.GetShapeName() == "Circle" &&
                                Settings.InkToShape.IsInkToShapeRounded == true) {
                                var shape = result.InkDrawingNode.GetShape();
                                if (shape.Width > 75) {
                                    foreach (var circle in circles)
                                        //判断是否画同心圆
                                        if (Math.Abs(result.Centroid.X - circle.Centroid.X) / shape.Width < 0.12 &&
                                            Math.Abs(result.Centroid.Y - circle.Centroid.Y) / shape.Width < 0.12) {
                                            result.Centroid = circle.Centroid;
                                            break;
                                        }
                                        else {
                                            var d = (result.Centroid.X - circle.Centroid.X) *
                                                    (result.Centroid.X - circle.Centroid.X) +
                                                    (result.Centroid.Y - circle.Centroid.Y) *
                                                    (result.Centroid.Y - circle.Centroid.Y);
                                            d = Math.Sqrt(d);
                                            //判断是否画外切圆
                                            var x = shape.Width / 2.0 + circle.R - d;
                                            if (Math.Abs(x) / shape.Width < 0.1) {
                                                var sinTheta = (result.Centroid.Y - circle.Centroid.Y) / d;
                                                var cosTheta = (result.Centroid.X - circle.Centroid.X) / d;
                                                var newX = result.Centroid.X + x * cosTheta;
                                                var newY = result.Centroid.Y + x * sinTheta;
                                                result.Centroid = new Point(newX, newY);
                                            }

                                            //判断是否画外切圆
                                            x = Math.Abs(circle.R - shape.Width / 2.0) - d;
                                            if (Math.Abs(x) / shape.Width < 0.1) {
                                                var sinTheta = (result.Centroid.Y - circle.Centroid.Y) / d;
                                                var cosTheta = (result.Centroid.X - circle.Centroid.X) / d;
                                                var newX = result.Centroid.X + x * cosTheta;
                                                var newY = result.Centroid.Y + x * sinTheta;
                                                result.Centroid = new Point(newX, newY);
                                            }
                                        }

                                    var iniP = new Point(result.Centroid.X - shape.Width / 2,
                                        result.Centroid.Y - shape.Height / 2);
                                    var endP = new Point(result.Centroid.X + shape.Width / 2,
                                        result.Centroid.Y + shape.Height / 2);
                                    var pointList = GenerateEllipseGeometry(iniP, endP);
                                    var point = new StylusPointCollection(pointList);
                                    var stroke = new Stroke(point) {
                                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                    };
                                    circles.Add(new Circle(result.Centroid, shape.Width / 2.0, stroke));
                                    SetNewBackupOfStroke();
                                    _currentCommitType = CommitReason.ShapeRecognition;
                                    inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                    inkCanvas.Strokes.Add(stroke);
                                    _currentCommitType = CommitReason.UserInput;
                                    newStrokes = new StrokeCollection();
                                }
                            }
                            else if (result.InkDrawingNode.GetShapeName().Contains("Ellipse") &&
                                     Settings.InkToShape.IsInkToShapeRounded == true) {
                                var shape = result.InkDrawingNode.GetShape();
                                //var shape1 = result.InkDrawingNode.GetShape();
                                //shape1.Fill = Brushes.Gray;
                                //Canvas.Children.Add(shape1);
                                var p = result.InkDrawingNode.HotPoints;
                                var a = GetDistance(p[0], p[2]) / 2; //长半轴
                                var b = GetDistance(p[1], p[3]) / 2; //短半轴
                                if (a < b) {
                                    var t = a;
                                    a = b;
                                    b = t;
                                }

                                result.Centroid = new Point((p[0].X + p[2].X) / 2, (p[0].Y + p[2].Y) / 2);
                                var needRotation = true;

                                if (shape.Width > 75 || (shape.Height > 75 && p.Count == 4)) {
                                    var iniP = new Point(result.Centroid.X - shape.Width / 2,
                                        result.Centroid.Y - shape.Height / 2);
                                    var endP = new Point(result.Centroid.X + shape.Width / 2,
                                        result.Centroid.Y + shape.Height / 2);

                                    foreach (var circle in circles)
                                        //判断是否画同心椭圆
                                        if (Math.Abs(result.Centroid.X - circle.Centroid.X) / a < 0.2 &&
                                            Math.Abs(result.Centroid.Y - circle.Centroid.Y) / a < 0.2) {
                                            result.Centroid = circle.Centroid;
                                            iniP = new Point(result.Centroid.X - shape.Width / 2,
                                                result.Centroid.Y - shape.Height / 2);
                                            endP = new Point(result.Centroid.X + shape.Width / 2,
                                                result.Centroid.Y + shape.Height / 2);

                                            //再判断是否与圆相切
                                            if (Math.Abs(a - circle.R) / a < 0.2) {
                                                if (shape.Width >= shape.Height) {
                                                    iniP.X = result.Centroid.X - circle.R;
                                                    endP.X = result.Centroid.X + circle.R;
                                                    iniP.Y = result.Centroid.Y - b;
                                                    endP.Y = result.Centroid.Y + b;
                                                }
                                                else {
                                                    iniP.Y = result.Centroid.Y - circle.R;
                                                    endP.Y = result.Centroid.Y + circle.R;
                                                    iniP.X = result.Centroid.X - a;
                                                    endP.X = result.Centroid.X + a;
                                                }
                                            }

                                            break;
                                        }
                                        else if (Math.Abs(result.Centroid.X - circle.Centroid.X) / a < 0.2) {
                                            var sinTheta = Math.Abs(circle.Centroid.Y - result.Centroid.Y) /
                                                           circle.R;
                                            var cosTheta = Math.Sqrt(1 - sinTheta * sinTheta);
                                            var newA = circle.R * cosTheta;
                                            if (circle.R * sinTheta / circle.R < 0.9 && a / b > 2 &&
                                                Math.Abs(newA - a) / newA < 0.3) {
                                                iniP.X = circle.Centroid.X - newA;
                                                endP.X = circle.Centroid.X + newA;
                                                iniP.Y = result.Centroid.Y - newA / 5;
                                                endP.Y = result.Centroid.Y + newA / 5;

                                                var topB = endP.Y - iniP.Y;

                                                SetNewBackupOfStroke();
                                                _currentCommitType = CommitReason.ShapeRecognition;
                                                inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                                newStrokes = new StrokeCollection();

                                                var _pointList = GenerateEllipseGeometry(iniP, endP, false, true);
                                                var _point = new StylusPointCollection(_pointList);
                                                var _stroke = new Stroke(_point) {
                                                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                                };
                                                var _dashedLineStroke =
                                                    GenerateDashedLineEllipseStrokeCollection(iniP, endP, true, false);
                                                var strokes = new StrokeCollection() {
                                                    _stroke,
                                                    _dashedLineStroke
                                                };
                                                inkCanvas.Strokes.Add(strokes);
                                                _currentCommitType = CommitReason.UserInput;
                                                return;
                                            }
                                        }
                                        else if (Math.Abs(result.Centroid.Y - circle.Centroid.Y) / a < 0.2) {
                                            var cosTheta = Math.Abs(circle.Centroid.X - result.Centroid.X) /
                                                           circle.R;
                                            var sinTheta = Math.Sqrt(1 - cosTheta * cosTheta);
                                            var newA = circle.R * sinTheta;
                                            if (circle.R * sinTheta / circle.R < 0.9 && a / b > 2 &&
                                                Math.Abs(newA - a) / newA < 0.3) {
                                                iniP.X = result.Centroid.X - newA / 5;
                                                endP.X = result.Centroid.X + newA / 5;
                                                iniP.Y = circle.Centroid.Y - newA;
                                                endP.Y = circle.Centroid.Y + newA;
                                                needRotation = false;
                                            }
                                        }

                                    //纠正垂直与水平关系
                                    var newPoints = FixPointsDirection(p[0], p[2]);
                                    p[0] = newPoints[0];
                                    p[2] = newPoints[1];
                                    newPoints = FixPointsDirection(p[1], p[3]);
                                    p[1] = newPoints[0];
                                    p[3] = newPoints[1];

                                    var pointList = GenerateEllipseGeometry(iniP, endP);
                                    var point = new StylusPointCollection(pointList);
                                    var stroke = new Stroke(point) {
                                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                    };

                                    if (needRotation) {
                                        var m = new Matrix();
                                        var fe = e.Source as FrameworkElement;
                                        var tanTheta = (p[2].Y - p[0].Y) / (p[2].X - p[0].X);
                                        var theta = Math.Atan(tanTheta);
                                        m.RotateAt(theta * 180.0 / Math.PI, result.Centroid.X, result.Centroid.Y);
                                        stroke.Transform(m, false);
                                    }

                                    SetNewBackupOfStroke();
                                    _currentCommitType = CommitReason.ShapeRecognition;
                                    inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                    inkCanvas.Strokes.Add(stroke);
                                    _currentCommitType = CommitReason.UserInput;
                                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                                    newStrokes = new StrokeCollection();
                                }
                            }
                            else if (result.InkDrawingNode.GetShapeName().Contains("Triangle") &&
                                     Settings.InkToShape.IsInkToShapeTriangle == true) {
                                var shape = result.InkDrawingNode.GetShape();
                                var p = result.InkDrawingNode.HotPoints;
                                if ((Math.Max(Math.Max(p[0].X, p[1].X), p[2].X) -
                                     Math.Min(Math.Min(p[0].X, p[1].X), p[2].X) >= 100 ||
                                     Math.Max(Math.Max(p[0].Y, p[1].Y), p[2].Y) -
                                     Math.Min(Math.Min(p[0].Y, p[1].Y), p[2].Y) >= 100) &&
                                    result.InkDrawingNode.HotPoints.Count == 3) {
                                    //纠正垂直与水平关系
                                    var newPoints = FixPointsDirection(p[0], p[1]);
                                    p[0] = newPoints[0];
                                    p[1] = newPoints[1];
                                    newPoints = FixPointsDirection(p[0], p[2]);
                                    p[0] = newPoints[0];
                                    p[2] = newPoints[1];
                                    newPoints = FixPointsDirection(p[1], p[2]);
                                    p[1] = newPoints[0];
                                    p[2] = newPoints[1];

                                    var pointList = p.ToList();
                                    //pointList.Add(p[0]);
                                    var point = new StylusPointCollection(pointList);
                                    var stroke = new Stroke(GenerateFakePressureTriangle(point)) {
                                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                    };
                                    SetNewBackupOfStroke();
                                    _currentCommitType = CommitReason.ShapeRecognition;
                                    inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                    inkCanvas.Strokes.Add(stroke);
                                    _currentCommitType = CommitReason.UserInput;
                                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                                    newStrokes = new StrokeCollection();
                                }
                            }
                            else if ((result.InkDrawingNode.GetShapeName().Contains("Rectangle") ||
                                      result.InkDrawingNode.GetShapeName().Contains("Diamond") ||
                                      result.InkDrawingNode.GetShapeName().Contains("Parallelogram") ||
                                      result.InkDrawingNode.GetShapeName().Contains("Square") ||
                                      result.InkDrawingNode.GetShapeName().Contains("Trapezoid")) &&
                                     Settings.InkToShape.IsInkToShapeRectangle == true) {
                                var shape = result.InkDrawingNode.GetShape();
                                var p = result.InkDrawingNode.HotPoints;
                                if ((Math.Max(Math.Max(Math.Max(p[0].X, p[1].X), p[2].X), p[3].X) -
                                     Math.Min(Math.Min(Math.Min(p[0].X, p[1].X), p[2].X), p[3].X) >= 100 ||
                                     Math.Max(Math.Max(Math.Max(p[0].Y, p[1].Y), p[2].Y), p[3].Y) -
                                     Math.Min(Math.Min(Math.Min(p[0].Y, p[1].Y), p[2].Y), p[3].Y) >= 100) &&
                                    result.InkDrawingNode.HotPoints.Count == 4) {
                                    //纠正垂直与水平关系
                                    var newPoints = FixPointsDirection(p[0], p[1]);
                                    p[0] = newPoints[0];
                                    p[1] = newPoints[1];
                                    newPoints = FixPointsDirection(p[1], p[2]);
                                    p[1] = newPoints[0];
                                    p[2] = newPoints[1];
                                    newPoints = FixPointsDirection(p[2], p[3]);
                                    p[2] = newPoints[0];
                                    p[3] = newPoints[1];
                                    newPoints = FixPointsDirection(p[3], p[0]);
                                    p[3] = newPoints[0];
                                    p[0] = newPoints[1];

                                    var pointList = p.ToList();
                                    pointList.Add(p[0]);
                                    var point = new StylusPointCollection(pointList);
                                    var stroke = new Stroke(GenerateFakePressureRectangle(point)) {
                                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                    };
                                    SetNewBackupOfStroke();
                                    _currentCommitType = CommitReason.ShapeRecognition;
                                    inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                    inkCanvas.Strokes.Add(stroke);
                                    _currentCommitType = CommitReason.UserInput;
                                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                                    newStrokes = new StrokeCollection();
                                }
                            }
                        }
                        catch { }
                    }

                    InkToShapeProcess();
                }

                foreach (var stylusPoint in e.Stroke.StylusPoints)
                    //LogHelper.WriteLogToFile(stylusPoint.PressureFactor.ToString(), LogHelper.LogType.Info);
                    // 检查是否是压感笔书写
                    //if (stylusPoint.PressureFactor != 0.5 && stylusPoint.PressureFactor != 0)
                    if ((stylusPoint.PressureFactor > 0.501 || stylusPoint.PressureFactor < 0.5) &&
                        stylusPoint.PressureFactor != 0)
                        return;

                try {
                    if (e.Stroke.StylusPoints.Count > 3) {
                        var random = new Random();
                        var _speed = GetPointSpeed(
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint(),
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint(),
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint());

                        RandWindow.randSeed = (int)(_speed * 100000 * 1000);
                    }
                }
                catch { }

                switch (Settings.Canvas.InkStyle) {
                    case 1:
                        if (penType == 0)
                            try {
                                var stylusPoints = new StylusPointCollection();
                                var n = e.Stroke.StylusPoints.Count - 1;
                                var s = "";

                                for (var i = 0; i <= n; i++) {
                                    var speed = GetPointSpeed(e.Stroke.StylusPoints[Math.Max(i - 1, 0)].ToPoint(),
                                        e.Stroke.StylusPoints[i].ToPoint(),
                                        e.Stroke.StylusPoints[Math.Min(i + 1, n)].ToPoint());
                                    s += speed.ToString() + "\t";
                                    var point = new StylusPoint();
                                    if (speed >= 0.25)
                                        point.PressureFactor = (float)(0.5 - 0.3 * (Math.Min(speed, 1.5) - 0.3) / 1.2);
                                    else if (speed >= 0.05)
                                        point.PressureFactor = (float)0.5;
                                    else
                                        point.PressureFactor = (float)(0.5 + 0.4 * (0.05 - speed) / 0.05);

                                    point.X = e.Stroke.StylusPoints[i].X;
                                    point.Y = e.Stroke.StylusPoints[i].Y;
                                    stylusPoints.Add(point);
                                }

                                e.Stroke.StylusPoints = stylusPoints;
                            }
                            catch { }

                        break;
                    case 0:
                        if (penType == 0)
                            try {
                                var stylusPoints = new StylusPointCollection();
                                var n = e.Stroke.StylusPoints.Count - 1;
                                var pressure = 0.1;
                                var x = 10;
                                if (n == 1) return;
                                if (n >= x) {
                                    for (var i = 0; i < n - x; i++) {
                                        var point = new StylusPoint();

                                        point.PressureFactor = (float)0.5;
                                        point.X = e.Stroke.StylusPoints[i].X;
                                        point.Y = e.Stroke.StylusPoints[i].Y;
                                        stylusPoints.Add(point);
                                    }

                                    for (var i = n - x; i <= n; i++) {
                                        var point = new StylusPoint();

                                        point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                                        point.X = e.Stroke.StylusPoints[i].X;
                                        point.Y = e.Stroke.StylusPoints[i].Y;
                                        stylusPoints.Add(point);
                                    }
                                }
                                else {
                                    for (var i = 0; i <= n; i++) {
                                        var point = new StylusPoint();

                                        point.PressureFactor = (float)(0.4 * (n - i) / n + pressure);
                                        point.X = e.Stroke.StylusPoints[i].X;
                                        point.Y = e.Stroke.StylusPoints[i].Y;
                                        stylusPoints.Add(point);
                                    }
                                }

                                e.Stroke.StylusPoints = stylusPoints;
                            }
                            catch { }

                        break;
                }
            }
            catch { }

            // 应用高级贝塞尔曲线平滑（仅在未进行直线拉直时）
            if (Settings.Canvas.UseAdvancedBezierSmoothing && !wasStraightened)
            {
                try
                {
                    // 检查原始笔画是否仍然存在于画布中
                    if (inkCanvas.Strokes.Contains(e.Stroke))
                    {
                        var advancedSmoothing = new Helpers.AdvancedBezierSmoothing
                        {
                            SmoothingStrength = Settings.Canvas.AdvancedSmoothingStrength,
                            Tension = Settings.Canvas.AdvancedSmoothingTension,
                            EnableAdaptiveSmoothing = Settings.Canvas.EnableAdaptiveSmoothing,
                            ShakeCorrectionStrength = Settings.Canvas.ShakeCorrectionStrength,
                            VelocityWeightedSmoothingStrength = Settings.Canvas.VelocityWeightedSmoothingStrength,
                            TimeWeightedSmoothingStrength = Settings.Canvas.TimeWeightedSmoothingStrength,
                            CornerSmoothingStrength = Settings.Canvas.CornerSmoothingStrength,
                            PixelLevelPrecision = Settings.Canvas.PixelLevelPrecision
                        };

                        var smoothedStroke = advancedSmoothing.SmoothStroke(e.Stroke);
                        
                        // 替换原始笔画
                        SetNewBackupOfStroke();
                        _currentCommitType = CommitReason.ShapeRecognition;
                        inkCanvas.Strokes.Remove(e.Stroke);
                        inkCanvas.Strokes.Add(smoothedStroke);
                        _currentCommitType = CommitReason.UserInput;
                    }
                }
                catch (Exception ex)
                {
                    // 如果高级平滑失败，回退到原始笔画
                    System.Diagnostics.Debug.WriteLine($"高级贝塞尔曲线平滑失败: {ex.Message}");
                }
            }
            else if (Settings.Canvas.FitToCurve == true && !wasStraightened) 
            {
                drawingAttributes.FitToCurve = true;
            }
        }

        // New method: Checks if a stroke is potentially a straight line
        private bool IsPotentialStraightLine(Stroke stroke) {
            // 确保有足够的点来进行线条分析
            if (stroke.StylusPoints.Count < 5) 
                return false;
                
            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);
            
            // 线条必须足够长才考虑拉直，使用设置中的阈值
            if (lineLength < Settings.Canvas.AutoStraightenLineThreshold)
                return false;
                
            // 获取用户设置的灵敏度值，确保使用正确的设置
            double sensitivity = Settings.InkToShape.LineStraightenSensitivity;
            
            // 输出当前灵敏度值（调试用）
            System.Diagnostics.Debug.WriteLine($"IsPotentialStraightLine - sensitivity: {sensitivity}, length: {lineLength}");
            
            // 根据灵敏度调整快速检查阈值
            double quickThreshold;
            
            // 如果灵敏度超过1.0，使用更宽松的快速检查标准
            if (sensitivity > 1.0) {
                // 高灵敏度模式 - 使用更宽松的阈值
                quickThreshold = Math.Min(0.2 + (sensitivity - 1.0) * 0.3, 0.5); // 映射到0.2-0.5范围
            } else {
                // 常规灵敏度模式
                quickThreshold = Math.Min(sensitivity * 1.5, 0.20);
            }
            
            System.Diagnostics.Debug.WriteLine($"使用快速检查阈值: {quickThreshold}");
                
            // 快速检查：计算几个关键点与直线的距离
            if (stroke.StylusPoints.Count >= 10) {
                // 取中点和1/4、3/4位置的点，快速检查偏差
                int quarterIdx = stroke.StylusPoints.Count / 4;
                int midIdx = stroke.StylusPoints.Count / 2;
                int threeQuarterIdx = quarterIdx * 3;
                
                Point quarterPoint = stroke.StylusPoints[quarterIdx].ToPoint();
                Point midPoint = stroke.StylusPoints[midIdx].ToPoint();
                Point threeQuarterPoint = stroke.StylusPoints[threeQuarterIdx].ToPoint();
                
                double quarterDeviation = DistanceFromLineToPoint(start, end, quarterPoint);
                double midDeviation = DistanceFromLineToPoint(start, end, midPoint);
                double threeQuarterDeviation = DistanceFromLineToPoint(start, end, threeQuarterPoint);
                
                // 使用相对偏差：偏差与线长的比例，并使用灵敏度进行调整
                double quickRelativeThreshold = lineLength * quickThreshold;
                
                // 记录检测到的偏差（调试用）
                System.Diagnostics.Debug.WriteLine($"Deviations: q={quarterDeviation}, m={midDeviation}, tq={threeQuarterDeviation}, threshold={quickRelativeThreshold}");
                
                // 如果灵敏度超过1.5，则即使有一个点满足条件也认为可能是直线
                if (sensitivity > 1.5) {
                    // 超高灵敏度模式：只要有一个关键点偏差小，就认为可能是直线
                    if (quarterDeviation <= quickRelativeThreshold || 
                        midDeviation <= quickRelativeThreshold || 
                        threeQuarterDeviation <= quickRelativeThreshold) {
                        return true;
                    }
                } else {
                    // 常规判断：如果任一点偏离太大，直接排除
                    if (quarterDeviation > quickRelativeThreshold || 
                        midDeviation > quickRelativeThreshold || 
                        threeQuarterDeviation > quickRelativeThreshold) {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        // New method: Determines if a stroke should be straightened into a line
        private bool ShouldStraightenLine(Stroke stroke) {
            // Basic implementation: check if points roughly follow a straight line
            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            
            // Calculate max deviation from the straight line between start and end
            double maxDeviation = 0;
            double lineLength = GetDistance(start, end);
            
            // 如果线条太短，不进行拉直处理，使用设置中的阈值
            if (lineLength < Settings.Canvas.AutoStraightenLineThreshold) {
                // 显示调试信息 - 线条长度不足
                // MessageBox.Show($"线条太短: {lineLength} < {Settings.Canvas.AutoStraightenLineThreshold}", "调试信息");
                return false;
            }
            
            // 获取用户设置的灵敏度值，确保使用正确的值进行后续判断
            double sensitivity = Settings.InkToShape.LineStraightenSensitivity;
            
            // 输出详细的调试信息
            System.Diagnostics.Debug.WriteLine($"ShouldStraightenLine - sensitivity: {sensitivity}, length: {lineLength}");
            
            // 临时：显示调试消息框
            // MessageBox.Show($"灵敏度值: {sensitivity}", "调试信息");
            
            // 计算点与直线的偏差
            double totalDeviation = 0;
            int pointCount = 0;
            
            // 检查是否启用了高精度直线拉直
            bool useHighPrecision = Settings.Canvas.HighPrecisionLineStraighten;
            
            if (useHighPrecision) {
                System.Diagnostics.Debug.WriteLine("使用高精度直线拉直模式");
                
                // 高精度模式：每隔10像素取一个计数点
                double strokeLength = 0;
                double sampleInterval = 10.0; // 10像素间隔
                
                // 计算笔画的总长度，用于后续采样
                for (int i = 1; i < stroke.StylusPoints.Count; i++) {
                    Point p1 = stroke.StylusPoints[i-1].ToPoint();
                    Point p2 = stroke.StylusPoints[i].ToPoint();
                    strokeLength += GetDistance(p1, p2);
                }
                
                // 如果笔画太短，直接使用所有点
                if (strokeLength < sampleInterval * 5) {
                    foreach (StylusPoint sp in stroke.StylusPoints) {
                        Point p = sp.ToPoint();
                        double deviation = DistanceFromLineToPoint(start, end, p);
                        maxDeviation = Math.Max(maxDeviation, deviation);
                        totalDeviation += deviation;
                        pointCount++;
                    }
                } else {
                    // 使用等距采样点
                    double currentLength = 0;
                    double nextSampleAt = 0;
                    
                    // 总是包含起点
                    Point lastPoint = start;
                    double deviation = DistanceFromLineToPoint(start, end, lastPoint);
                    maxDeviation = Math.Max(maxDeviation, deviation);
                    totalDeviation += deviation;
                    pointCount++;
                    
                    // 采样中间点
                    for (int i = 1; i < stroke.StylusPoints.Count; i++) {
                        Point currentPoint = stroke.StylusPoints[i].ToPoint();
                        double segmentLength = GetDistance(lastPoint, currentPoint);
                        
                        // 如果这段线段跨越了下一个采样点
                        while (currentLength + segmentLength >= nextSampleAt) {
                            // 计算采样点在线段上的位置
                            double t = (nextSampleAt - currentLength) / segmentLength;
                            Point samplePoint = new Point(
                                lastPoint.X + t * (currentPoint.X - lastPoint.X),
                                lastPoint.Y + t * (currentPoint.Y - lastPoint.Y)
                            );
                            
                            // 计算采样点的偏差
                            deviation = DistanceFromLineToPoint(start, end, samplePoint);
                            maxDeviation = Math.Max(maxDeviation, deviation);
                            totalDeviation += deviation;
                            pointCount++;
                            
                            // 设置下一个采样点位置
                            nextSampleAt += sampleInterval;
                            
                            // 防止无限循环
                            if (nextSampleAt > strokeLength) break;
                        }
                        
                        currentLength += segmentLength;
                        lastPoint = currentPoint;
                    }
                    
                    // 总是包含终点
                    deviation = DistanceFromLineToPoint(start, end, end);
                    maxDeviation = Math.Max(maxDeviation, deviation);
                    totalDeviation += deviation;
                    pointCount++;
                }
            } else {
                // 原始模式：使用所有点
                foreach (StylusPoint sp in stroke.StylusPoints) {
                    Point p = sp.ToPoint();
                    double deviation = DistanceFromLineToPoint(start, end, p);
                    maxDeviation = Math.Max(maxDeviation, deviation);
                    totalDeviation += deviation;
                    pointCount++;
                }
            }
            
            // 计算平均偏差
            double avgDeviation = totalDeviation / pointCount;
            
            // 更详细的调试信息
            System.Diagnostics.Debug.WriteLine($"Max deviation: {maxDeviation}, Avg: {avgDeviation}, Threshold: {sensitivity * lineLength}, Points: {pointCount}");
            
            // 支持更广泛的灵敏度范围 (0.05-2.0)
            
            // 如果灵敏度高于1.0，使用更宽松的判断标准
            if (sensitivity > 1.0) {
                // 高灵敏度模式 - 允许更大的偏差
                double adjustedSensitivity = 0.5 + (sensitivity - 1.0) * 1.5; // 映射到0.5-2.0范围
                
                // 只判断平均偏差和相对偏差
                if (maxDeviation / lineLength < adjustedSensitivity && avgDeviation < lineLength * 0.1 * adjustedSensitivity) {
                    System.Diagnostics.Debug.WriteLine("接受拉直 (高灵敏度模式)");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine("拒绝拉直 (高灵敏度模式)");
                return false;
            }
            // 否则使用常规判断标准
            else {
                // 检查点分布的一致性 - 如果有些点偏离很大而其他点很接近直线，表明线条有明显弯曲
                double deviationVariance = 0;
                
                // 使用相同的高精度/原始模式来计算方差
                if (useHighPrecision) {
                    // 高精度模式：重新采样计算方差
                    double strokeLength = 0;
                    double sampleInterval = 10.0; // 10像素间隔
                    
                    // 计算笔画的总长度，用于后续采样
                    for (int i = 1; i < stroke.StylusPoints.Count; i++) {
                        Point p1 = stroke.StylusPoints[i-1].ToPoint();
                        Point p2 = stroke.StylusPoints[i].ToPoint();
                        strokeLength += GetDistance(p1, p2);
                    }
                    
                    // 如果笔画太短，直接使用所有点
                    if (strokeLength < sampleInterval * 5) {
                        foreach (StylusPoint sp in stroke.StylusPoints) {
                            Point p = sp.ToPoint();
                            double deviation = DistanceFromLineToPoint(start, end, p);
                            deviationVariance += Math.Pow(deviation - avgDeviation, 2);
                        }
                    } else {
                        // 使用等距采样点
                        double currentLength = 0;
                        double nextSampleAt = 0;
                        Point lastPoint = start;
                        
                        // 起点方差
                        double deviation = DistanceFromLineToPoint(start, end, lastPoint);
                        deviationVariance += Math.Pow(deviation - avgDeviation, 2);
                        
                        // 采样中间点
                        for (int i = 1; i < stroke.StylusPoints.Count; i++) {
                            Point currentPoint = stroke.StylusPoints[i].ToPoint();
                            double segmentLength = GetDistance(lastPoint, currentPoint);
                            
                            // 如果这段线段跨越了下一个采样点
                            while (currentLength + segmentLength >= nextSampleAt) {
                                // 计算采样点在线段上的位置
                                double t = (nextSampleAt - currentLength) / segmentLength;
                                Point samplePoint = new Point(
                                    lastPoint.X + t * (currentPoint.X - lastPoint.X),
                                    lastPoint.Y + t * (currentPoint.Y - lastPoint.Y)
                                );
                                
                                // 计算采样点的方差
                                deviation = DistanceFromLineToPoint(start, end, samplePoint);
                                deviationVariance += Math.Pow(deviation - avgDeviation, 2);
                                
                                // 设置下一个采样点位置
                                nextSampleAt += sampleInterval;
                                
                                // 防止无限循环
                                if (nextSampleAt > strokeLength) break;
                            }
                            
                            currentLength += segmentLength;
                            lastPoint = currentPoint;
                        }
                        
                        // 终点方差
                        deviation = DistanceFromLineToPoint(start, end, end);
                        deviationVariance += Math.Pow(deviation - avgDeviation, 2);
                    }
                } else {
                    // 原始模式：使用所有点计算方差
                    foreach (StylusPoint sp in stroke.StylusPoints) {
                        Point p = sp.ToPoint();
                        double deviation = DistanceFromLineToPoint(start, end, p);
                        deviationVariance += Math.Pow(deviation - avgDeviation, 2);
                    }
                }
                
                deviationVariance /= pointCount;
                
                // 输出更多调试信息
                System.Diagnostics.Debug.WriteLine($"Deviation variance: {deviationVariance}, Threshold: {sensitivity * lineLength * 0.05}");
                
                // 如果最大偏差超过线长的阈值比例，或者偏差方差较大（表示不均匀弯曲），则不拉直
                // 灵敏度越大，容许的偏差越大，更容易将线条识别为直线
                if ((maxDeviation / lineLength) > sensitivity) {
                    System.Diagnostics.Debug.WriteLine("拒绝拉直：最大偏差过大");
                    return false;
                }
                
                // 如果偏差方差大，说明线条弯曲不均匀
                // 灵敏度越大，容许的偏差方差越大
                if (deviationVariance > (sensitivity * lineLength * 0.05)) {
                    System.Diagnostics.Debug.WriteLine("拒绝拉直：偏差方差过大");
                    return false;
                }
                
                // 检查中点偏离情况 - 针对弧形线条特别有效
                if (stroke.StylusPoints.Count > 10) {
                    int midIndex = stroke.StylusPoints.Count / 2;
                    Point midPoint = stroke.StylusPoints[midIndex].ToPoint();
                    double midDeviation = DistanceFromLineToPoint(start, end, midPoint);
                    
                    // 输出中点偏差信息
                    System.Diagnostics.Debug.WriteLine($"Mid deviation: {midDeviation}, Threshold: {lineLength * sensitivity * 0.8}");
                    
                    // 如果中点偏离过大，不拉直
                    // 使用灵敏度作为判断基准，灵敏度越大，容许的中点偏离越大
                    if (midDeviation > (lineLength * sensitivity * 0.8)) {
                        System.Diagnostics.Debug.WriteLine("拒绝拉直：中点偏差过大");
                        return false;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("接受拉直");
                return true;
            }
        }
        
        // New method: Creates a straight line stroke between two points
        private StylusPointCollection CreateStraightLine(Point start, Point end) {
            StylusPointCollection points = new StylusPointCollection();
            
            // 根据是否启用压感触屏模式决定如何设置压感
            // 如果未启用压感触屏模式，则使用均匀粗细
            if (!Settings.Canvas.EnablePressureTouchMode || Settings.Canvas.DisablePressure || 
                Settings.InkToShape.IsInkToShapeNoFakePressureRectangle == true || penType == 1) {
                // 使用均匀粗细（所有点压感值都是0.5f）
                points.Add(new StylusPoint(start.X, start.Y, 0.5f));
                
                // 可以添加一些额外的中间点使线条更平滑（均匀粗细）
                double distance = GetDistance(start, end);
                if (distance > 100) {
                    // 对于较长的线条，添加几个中间点
                    for (int i = 1; i < 3; i++) {
                        double ratio = i / 3.0;
                        Point midPoint = new Point(
                            start.X + (end.X - start.X) * ratio,
                            start.Y + (end.Y - start.Y) * ratio);
                        points.Add(new StylusPoint(midPoint.X, midPoint.Y, 0.5f));
                    }
                }
                
                points.Add(new StylusPoint(end.X, end.Y, 0.5f));
            } else {
                // 启用了压感触屏模式，使用变化的粗细（原有行为）
                points.Add(new StylusPoint(start.X, start.Y, 0.4f));
                
                // 添加中点，压感值较高，使线条中间较粗
                Point midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                points.Add(new StylusPoint(midPoint.X, midPoint.Y, 0.8f));
                
                points.Add(new StylusPoint(end.X, end.Y, 0.4f));
            }
            
            return points;
        }
        
        // New method: Gets distance from point to a line defined by two points
        private double DistanceFromLineToPoint(Point lineStart, Point lineEnd, Point point) {
            // Calculate distance from point to line defined by lineStart and lineEnd
            double lineLength = GetDistance(lineStart, lineEnd);
            if (lineLength == 0) return GetDistance(point, lineStart);
            
            // Calculate the cross product to get the perpendicular distance
            double distance = Math.Abs((lineEnd.Y - lineStart.Y) * point.X - 
                                      (lineEnd.X - lineStart.X) * point.Y + 
                                      lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X) / lineLength;
            return distance;
        }
        
        // New method: Attempts to snap endpoints to existing stroke endpoints
        private Point[] GetSnappedEndpoints(Point start, Point end) {
            // 如果端点吸附功能关闭，直接返回null
            // 这里不再返回原始点，因为调用此方法的地方会判断返回值是否为null
            if (!Settings.Canvas.LineEndpointSnapping)
                return null;
                
            bool startSnapped = false;
            bool endSnapped = false;
            Point snappedStart = start;
            Point snappedEnd = end;
            
            // 使用设置中的吸附距离阈值
            double snapThreshold = Settings.Canvas.LineEndpointSnappingThreshold;
            
            // Check all strokes in canvas for potential snap points
            foreach (Stroke stroke in inkCanvas.Strokes) {
                if (stroke.StylusPoints.Count == 0) continue;
                
                // Get stroke endpoints
                Point strokeStart = stroke.StylusPoints.First().ToPoint();
                Point strokeEnd = stroke.StylusPoints.Last().ToPoint();
                
                // Check if start point should snap to an endpoint
                if (!startSnapped) {
                    if (GetDistance(start, strokeStart) < snapThreshold) {
                        snappedStart = strokeStart;
                        startSnapped = true;
                    } else if (GetDistance(start, strokeEnd) < snapThreshold) {
                        snappedStart = strokeEnd;
                        startSnapped = true;
                    }
                }
                
                // Check if end point should snap to an endpoint
                if (!endSnapped) {
                    if (GetDistance(end, strokeStart) < snapThreshold) {
                        snappedEnd = strokeStart;
                        endSnapped = true;
                    } else if (GetDistance(end, strokeEnd) < snapThreshold) {
                        snappedEnd = strokeEnd;
                        endSnapped = true;
                    }
                }
                
                // If both endpoints are snapped, we're done
                if (startSnapped && endSnapped) break;
            }
            
            // Return snapped points if any snapping occurred
            if (startSnapped || endSnapped) {
                return new Point[] { snappedStart, snappedEnd };
            }
            
            return null;
        }

        private void SetNewBackupOfStroke() {
            lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            var whiteboardIndex = CurrentWhiteboardIndex;
            if (currentMode == 0) whiteboardIndex = 0;

            strokeCollections[whiteboardIndex] = lastTouchDownStrokeCollection;
        }

        public double GetDistance(Point point1, Point point2) {
            return Math.Sqrt((point1.X - point2.X) * (point1.X - point2.X) +
                             (point1.Y - point2.Y) * (point1.Y - point2.Y));
        }

        public double GetPointSpeed(Point point1, Point point2, Point point3) {
            return (Math.Sqrt((point1.X - point2.X) * (point1.X - point2.X) +
                              (point1.Y - point2.Y) * (point1.Y - point2.Y))
                    + Math.Sqrt((point3.X - point2.X) * (point3.X - point2.X) +
                                (point3.Y - point2.Y) * (point3.Y - point2.Y)))
                   / 20;
        }

        public Point[] FixPointsDirection(Point p1, Point p2) {
            if (Math.Abs(p1.X - p2.X) / Math.Abs(p1.Y - p2.Y) > 8) {
                //水平
                var x = Math.Abs(p1.Y - p2.Y) / 2;
                if (p1.Y > p2.Y) {
                    p1.Y -= x;
                    p2.Y += x;
                }
                else {
                    p1.Y += x;
                    p2.Y -= x;
                }
            }
            else if (Math.Abs(p1.Y - p2.Y) / Math.Abs(p1.X - p2.X) > 8) {
                //垂直
                var x = Math.Abs(p1.X - p2.X) / 2;
                if (p1.X > p2.X) {
                    p1.X -= x;
                    p2.X += x;
                }
                else {
                    p1.X += x;
                    p2.X -= x;
                }
            }

            return new Point[2] { p1, p2 };
        }

        public StylusPointCollection GenerateFakePressureTriangle(StylusPointCollection points) {
            if (Settings.InkToShape.IsInkToShapeNoFakePressureTriangle == true || penType == 1) {
                var newPoint = new StylusPointCollection();
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y));
                var cPoint = GetCenterPoint(points[0], points[1]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y));
                cPoint = GetCenterPoint(points[1], points[2]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y));
                cPoint = GetCenterPoint(points[2], points[0]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y));
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y));
                return newPoint;
            }
            else {
                var newPoint = new StylusPointCollection();
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y, (float)0.4));
                var cPoint = GetCenterPoint(points[0], points[1]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y, (float)0.4));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y, (float)0.4));
                cPoint = GetCenterPoint(points[1], points[2]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y, (float)0.4));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y, (float)0.4));
                cPoint = GetCenterPoint(points[2], points[0]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y, (float)0.4));
                return newPoint;
            }
        }

        public StylusPointCollection GenerateFakePressureRectangle(StylusPointCollection points) {
            if (Settings.InkToShape.IsInkToShapeNoFakePressureRectangle == true || penType == 1) {
                return points;
            }
            else {
                var newPoint = new StylusPointCollection();
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y, (float)0.4));
                var cPoint = GetCenterPoint(points[0], points[1]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y, (float)0.4));
                newPoint.Add(new StylusPoint(points[1].X, points[1].Y, (float)0.4));
                cPoint = GetCenterPoint(points[1], points[2]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y, (float)0.4));
                newPoint.Add(new StylusPoint(points[2].X, points[2].Y, (float)0.4));
                cPoint = GetCenterPoint(points[2], points[3]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[3].X, points[3].Y, (float)0.4));
                newPoint.Add(new StylusPoint(points[3].X, points[3].Y, (float)0.4));
                cPoint = GetCenterPoint(points[3], points[0]);
                newPoint.Add(new StylusPoint(cPoint.X, cPoint.Y, (float)0.8));
                newPoint.Add(new StylusPoint(points[0].X, points[0].Y, (float)0.4));
                return newPoint;
            }
        }

        public Point GetCenterPoint(Point point1, Point point2) {
            return new Point((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
        }

        public StylusPoint GetCenterPoint(StylusPoint point1, StylusPoint point2) {
            return new StylusPoint((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
        }
    }
}