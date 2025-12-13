using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private StrokeCollection newStrokes = new StrokeCollection();
        private List<Circle> circles = new List<Circle>();
        private const double LINE_STRAIGHTEN_THRESHOLD = 0.20;

        private List<RectangleGuideLine> rectangleGuideLines = new List<RectangleGuideLine>();
        private const double RECTANGLE_ENDPOINT_THRESHOLD = 30.0;
        private const double RECTANGLE_ANGLE_THRESHOLD = 15.0;

        private class RectangleGuideLine
        {
            public Stroke OriginalStroke { get; set; }
            public Point StartPoint { get; set; }
            public Point EndPoint { get; set; }
            public DateTime CreatedTime { get; set; }
            public double Angle { get; set; }
            public bool IsHorizontal { get; set; }
            public bool IsVertical { get; set; }

            public RectangleGuideLine(Stroke stroke, Point start, Point end)
            {
                OriginalStroke = stroke;
                StartPoint = start;
                EndPoint = end;
                CreatedTime = DateTime.Now;

                // 计算角度
                double deltaX = end.X - start.X;
                double deltaY = end.Y - start.Y;
                Angle = Math.Atan2(deltaY, deltaX);

                // 判断是否为水平或垂直线
                double angleDegrees = Math.Abs(Angle * 180.0 / Math.PI);
                IsHorizontal = angleDegrees < RECTANGLE_ANGLE_THRESHOLD || angleDegrees > (180 - RECTANGLE_ANGLE_THRESHOLD);
                IsVertical = Math.Abs(angleDegrees - 90) < RECTANGLE_ANGLE_THRESHOLD;
            }
        }

        private void inkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            // 检查是否启用墨迹渐隐功能
            if (Settings.Canvas.EnableInkFade)
            {
                // 获取墨迹的起点和终点
                var startPoint = e.Stroke.StylusPoints.Count > 0 ? e.Stroke.StylusPoints[0].ToPoint() : new Point();
                var endPoint = e.Stroke.StylusPoints.Count > 0 ? e.Stroke.StylusPoints[e.Stroke.StylusPoints.Count - 1].ToPoint() : new Point();

                if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }

                // 添加到墨迹渐隐管理器
                if (_inkFadeManager != null)
                {
                    _inkFadeManager.AddFadingStroke(e.Stroke, startPoint, endPoint);
                }
                else
                {
                    LogHelper.WriteLogToFile("StrokeCollected: 墨迹渐隐管理器为空，无法添加墨迹", LogHelper.LogType.Error);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink)
                        {
                            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                        }

                        if (inkCanvas.Strokes.Contains(e.Stroke))
                        {
                            inkCanvas.Strokes.Remove(e.Stroke);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"延迟移除墨迹时出错: {ex}", LogHelper.LogType.Error);
                    }
                }), DispatcherPriority.Background);

                return;
            }

            // 标记是否进行了直线拉直
            bool wasStraightened = false;

            if (Settings.Canvas.FitToCurve) drawingAttributes.FitToCurve = false;

            try
            {
                inkCanvas.Opacity = 1;

                if (Settings.Canvas.DisablePressure)
                {
                    var uniformPoints = new StylusPointCollection();
                    foreach (StylusPoint point in e.Stroke.StylusPoints)
                    {
                        StylusPoint newPoint = new StylusPoint(point.X, point.Y, 0.5f); // 统一压感值为0.5
                        uniformPoints.Add(newPoint);
                    }
                    e.Stroke.StylusPoints = uniformPoints;
                }
                else if (Settings.Canvas.EnablePressureTouchMode)
                {
                    bool isTouchInput = true;
                    foreach (StylusPoint point in e.Stroke.StylusPoints)
                    {
                        if ((point.PressureFactor > 0.501 || point.PressureFactor < 0.5) && point.PressureFactor != 0)
                        {
                            isTouchInput = false;
                            break;
                        }
                    }

                    if (isTouchInput)
                    {
                        switch (Settings.Canvas.InkStyle)
                        {
                            case 1:
                                if (penType == 0)
                                    try
                                    {
                                        var stylusPoints = new StylusPointCollection();
                                        var n = e.Stroke.StylusPoints.Count - 1;

                                        for (var i = 0; i <= n; i++)
                                        {
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
                                    try
                                    {
                                        var stylusPoints = new StylusPointCollection();
                                        var n = e.Stroke.StylusPoints.Count - 1;
                                        var pressure = 0.1;
                                        var x = 10;
                                        if (n == 1) return;
                                        if (n >= x)
                                        {
                                            for (var i = 0; i < n - x; i++)
                                            {
                                                var point = new StylusPoint();

                                                point.PressureFactor = (float)0.5;
                                                point.X = e.Stroke.StylusPoints[i].X;
                                                point.Y = e.Stroke.StylusPoints[i].Y;
                                                stylusPoints.Add(point);
                                            }

                                            for (var i = n - x; i <= n; i++)
                                            {
                                                var point = new StylusPoint();

                                                point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                                                point.X = e.Stroke.StylusPoints[i].X;
                                                point.Y = e.Stroke.StylusPoints[i].Y;
                                                stylusPoints.Add(point);
                                            }
                                        }
                                        else
                                        {
                                            for (var i = 0; i <= n; i++)
                                            {
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

                if (Settings.InkToShape.IsInkToShapeEnabled)
                {
                    // 检查是否启用了直线自动拉直功能
                    if (Settings.Canvas.AutoStraightenLine && IsPotentialStraightLine(e.Stroke))
                    {
                        Point endpoint1, endpoint2;
                        bool shouldStraighten = TryGetStraightLineEndpoints(e.Stroke, out endpoint1, out endpoint2);

                        if (shouldStraighten)
                        {
                            Point startPoint = endpoint1;
                            Point endPoint = endpoint2;

                            // 只有当确定要拉直线条时，才检查端点吸附
                            if (Settings.Canvas.LineEndpointSnapping)
                            {
                                // 只有在启用了形状识别（矩形或三角形）时才执行端点吸附
                                if (Settings.InkToShape.IsInkToShapeRectangle || Settings.InkToShape.IsInkToShapeTriangle)
                                {
                                    Point[] snappedPoints = GetSnappedEndpoints(startPoint, endPoint);
                                    if (snappedPoints != null)
                                    {
                                        startPoint = snappedPoints[0];
                                        endPoint = snappedPoints[1];
                                    }
                                }
                            }

                            // 创建直线
                            StylusPointCollection straightLinePoints = CreateStraightLine(startPoint, endPoint);
                            Stroke straightStroke = new Stroke(straightLinePoints)
                            {
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
                            if (newStrokes.Contains(e.Stroke))
                            {
                                newStrokes.Remove(e.Stroke);
                                newStrokes.Add(straightStroke);
                            }

                            wasStraightened = true; // 标记已进行直线拉直
                        }
                    }
                }

                if (Settings.InkToShape.IsInkToShapeEnabled && !Environment.Is64BitProcess)
                {
                    void InkToShapeProcess()
                    {
                        try
                        {
                            newStrokes.Add(e.Stroke);
                            if (newStrokes.Count > 4) newStrokes.RemoveAt(0);
                            for (var i = 0; i < newStrokes.Count; i++)
                                if (!inkCanvas.Strokes.Contains(newStrokes[i]))
                                    newStrokes.RemoveAt(i--);

                            for (var i = 0; i < circles.Count; i++)
                                if (!inkCanvas.Strokes.Contains(circles[i].Stroke))
                                    circles.RemoveAt(i);

                            // 处理矩形参考线系统
                            ProcessRectangleGuideLines(e.Stroke);

                            var strokeReco = new StrokeCollection();
                            var result = InkRecognizeHelper.RecognizeShape(newStrokes);
                            for (var i = newStrokes.Count - 1; i >= 0; i--)
                            {
                                strokeReco.Add(newStrokes[i]);
                                var newResult = InkRecognizeHelper.RecognizeShape(strokeReco);
                                if (newResult.InkDrawingNode.GetShapeName() == "Circle" ||
                                    newResult.InkDrawingNode.GetShapeName() == "Ellipse")
                                {
                                    result = newResult;
                                    break;
                                }
                                //Label.Visibility = Visibility.Visible;
                                //Label.Content = circles.Count.ToString() + "\n" + newResult.InkDrawingNode.GetShapeName();
                            }

                            if (result.InkDrawingNode.GetShapeName() == "Circle" &&
                                Settings.InkToShape.IsInkToShapeRounded)
                            {
                                var shape = result.InkDrawingNode.GetShape();
                                if (shape.Width > 75)
                                {
                                    foreach (var circle in circles)
                                        //判断是否画同心圆
                                        if (Math.Abs(result.Centroid.X - circle.Centroid.X) / shape.Width < 0.12 &&
                                            Math.Abs(result.Centroid.Y - circle.Centroid.Y) / shape.Width < 0.12)
                                        {
                                            result.Centroid = circle.Centroid;
                                            break;
                                        }
                                        else
                                        {
                                            var d = (result.Centroid.X - circle.Centroid.X) *
                                                    (result.Centroid.X - circle.Centroid.X) +
                                                    (result.Centroid.Y - circle.Centroid.Y) *
                                                    (result.Centroid.Y - circle.Centroid.Y);
                                            d = Math.Sqrt(d);
                                            //判断是否画外切圆
                                            var x = shape.Width / 2.0 + circle.R - d;
                                            if (Math.Abs(x) / shape.Width < 0.1)
                                            {
                                                var sinTheta = (result.Centroid.Y - circle.Centroid.Y) / d;
                                                var cosTheta = (result.Centroid.X - circle.Centroid.X) / d;
                                                var newX = result.Centroid.X + x * cosTheta;
                                                var newY = result.Centroid.Y + x * sinTheta;
                                                result.Centroid = new Point(newX, newY);
                                            }

                                            //判断是否画外切圆
                                            x = Math.Abs(circle.R - shape.Width / 2.0) - d;
                                            if (Math.Abs(x) / shape.Width < 0.1)
                                            {
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
                                    var stroke = new Stroke(point)
                                    {
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
                                     Settings.InkToShape.IsInkToShapeRounded)
                            {
                                var shape = result.InkDrawingNode.GetShape();
                                //var shape1 = result.InkDrawingNode.GetShape();
                                //shape1.Fill = Brushes.Gray;
                                //Canvas.Children.Add(shape1);
                                var p = result.InkDrawingNode.HotPoints;
                                var a = GetDistance(p[0], p[2]) / 2; //长半轴
                                var b = GetDistance(p[1], p[3]) / 2; //短半轴
                                if (a < b)
                                {
                                    var t = a;
                                    a = b;
                                    b = t;
                                }

                                result.Centroid = new Point((p[0].X + p[2].X) / 2, (p[0].Y + p[2].Y) / 2);
                                var needRotation = true;

                                if (shape.Width > 75 || (shape.Height > 75 && p.Count == 4))
                                {
                                    var iniP = new Point(result.Centroid.X - shape.Width / 2,
                                        result.Centroid.Y - shape.Height / 2);
                                    var endP = new Point(result.Centroid.X + shape.Width / 2,
                                        result.Centroid.Y + shape.Height / 2);

                                    foreach (var circle in circles)
                                        //判断是否画同心椭圆
                                        if (Math.Abs(result.Centroid.X - circle.Centroid.X) / a < 0.2 &&
                                            Math.Abs(result.Centroid.Y - circle.Centroid.Y) / a < 0.2)
                                        {
                                            result.Centroid = circle.Centroid;
                                            iniP = new Point(result.Centroid.X - shape.Width / 2,
                                                result.Centroid.Y - shape.Height / 2);
                                            endP = new Point(result.Centroid.X + shape.Width / 2,
                                                result.Centroid.Y + shape.Height / 2);

                                            //再判断是否与圆相切
                                            if (Math.Abs(a - circle.R) / a < 0.2)
                                            {
                                                if (shape.Width >= shape.Height)
                                                {
                                                    iniP.X = result.Centroid.X - circle.R;
                                                    endP.X = result.Centroid.X + circle.R;
                                                    iniP.Y = result.Centroid.Y - b;
                                                    endP.Y = result.Centroid.Y + b;
                                                }
                                                else
                                                {
                                                    iniP.Y = result.Centroid.Y - circle.R;
                                                    endP.Y = result.Centroid.Y + circle.R;
                                                    iniP.X = result.Centroid.X - a;
                                                    endP.X = result.Centroid.X + a;
                                                }
                                            }

                                            break;
                                        }
                                        else if (Math.Abs(result.Centroid.X - circle.Centroid.X) / a < 0.2)
                                        {
                                            var sinTheta = Math.Abs(circle.Centroid.Y - result.Centroid.Y) /
                                                           circle.R;
                                            var cosTheta = Math.Sqrt(1 - sinTheta * sinTheta);
                                            var newA = circle.R * cosTheta;
                                            if (circle.R * sinTheta / circle.R < 0.9 && a / b > 2 &&
                                                Math.Abs(newA - a) / newA < 0.3)
                                            {
                                                iniP.X = circle.Centroid.X - newA;
                                                endP.X = circle.Centroid.X + newA;
                                                iniP.Y = result.Centroid.Y - newA / 5;
                                                endP.Y = result.Centroid.Y + newA / 5;

                                                var topB = endP.Y - iniP.Y;

                                                SetNewBackupOfStroke();
                                                _currentCommitType = CommitReason.ShapeRecognition;
                                                inkCanvas.Strokes.Remove(result.InkDrawingNode.Strokes);
                                                newStrokes = new StrokeCollection();

                                                var _pointList = GenerateEllipseGeometry(iniP, endP, false);
                                                var _point = new StylusPointCollection(_pointList);
                                                var _stroke = new Stroke(_point)
                                                {
                                                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                                };
                                                var _dashedLineStroke =
                                                    GenerateDashedLineEllipseStrokeCollection(iniP, endP, true, false);
                                                var strokes = new StrokeCollection {
                                                    _stroke,
                                                    _dashedLineStroke
                                                };
                                                inkCanvas.Strokes.Add(strokes);
                                                _currentCommitType = CommitReason.UserInput;
                                                return;
                                            }
                                        }
                                        else if (Math.Abs(result.Centroid.Y - circle.Centroid.Y) / a < 0.2)
                                        {
                                            var cosTheta = Math.Abs(circle.Centroid.X - result.Centroid.X) /
                                                           circle.R;
                                            var sinTheta = Math.Sqrt(1 - cosTheta * cosTheta);
                                            var newA = circle.R * sinTheta;
                                            if (circle.R * sinTheta / circle.R < 0.9 && a / b > 2 &&
                                                Math.Abs(newA - a) / newA < 0.3)
                                            {
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
                                    var stroke = new Stroke(point)
                                    {
                                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                                    };

                                    if (needRotation)
                                    {
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
                                     Settings.InkToShape.IsInkToShapeTriangle)
                            {
                                var shape = result.InkDrawingNode.GetShape();
                                var p = result.InkDrawingNode.HotPoints;
                                if ((Math.Max(Math.Max(p[0].X, p[1].X), p[2].X) -
                                     Math.Min(Math.Min(p[0].X, p[1].X), p[2].X) >= 100 ||
                                     Math.Max(Math.Max(p[0].Y, p[1].Y), p[2].Y) -
                                     Math.Min(Math.Min(p[0].Y, p[1].Y), p[2].Y) >= 100) &&
                                    result.InkDrawingNode.HotPoints.Count == 3)
                                {
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
                                    var stroke = new Stroke(GenerateFakePressureTriangle(point))
                                    {
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
                                     Settings.InkToShape.IsInkToShapeRectangle)
                            {
                                var shape = result.InkDrawingNode.GetShape();
                                var p = result.InkDrawingNode.HotPoints;
                                if ((Math.Max(Math.Max(Math.Max(p[0].X, p[1].X), p[2].X), p[3].X) -
                                     Math.Min(Math.Min(Math.Min(p[0].X, p[1].X), p[2].X), p[3].X) >= 100 ||
                                     Math.Max(Math.Max(Math.Max(p[0].Y, p[1].Y), p[2].Y), p[3].Y) -
                                     Math.Min(Math.Min(Math.Min(p[0].Y, p[1].Y), p[2].Y), p[3].Y) >= 100) &&
                                    result.InkDrawingNode.HotPoints.Count == 4)
                                {
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
                                    var stroke = new Stroke(GenerateFakePressureRectangle(point))
                                    {
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

                try
                {
                    if (e.Stroke.StylusPoints.Count > 3)
                    {
                        var random = new Random();
                        var _speed = GetPointSpeed(
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint(),
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint(),
                            e.Stroke.StylusPoints[random.Next(0, e.Stroke.StylusPoints.Count - 1)].ToPoint());

                        RandWindow.randSeed = (int)(_speed * 100000 * 1000);
                    }
                }
                catch { }

                switch (Settings.Canvas.InkStyle)
                {
                    case 1:
                        if (penType == 0)
                            try
                            {
                                var stylusPoints = new StylusPointCollection();
                                var n = e.Stroke.StylusPoints.Count - 1;
                                var s = "";

                                for (var i = 0; i <= n; i++)
                                {
                                    var speed = GetPointSpeed(e.Stroke.StylusPoints[Math.Max(i - 1, 0)].ToPoint(),
                                        e.Stroke.StylusPoints[i].ToPoint(),
                                        e.Stroke.StylusPoints[Math.Min(i + 1, n)].ToPoint());
                                    s += speed + "\t";
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
                            try
                            {
                                var stylusPoints = new StylusPointCollection();
                                var n = e.Stroke.StylusPoints.Count - 1;
                                var pressure = 0.1;
                                var x = 10;
                                if (n == 1) return;
                                if (n >= x)
                                {
                                    for (var i = 0; i < n - x; i++)
                                    {
                                        var point = new StylusPoint();

                                        point.PressureFactor = (float)0.5;
                                        point.X = e.Stroke.StylusPoints[i].X;
                                        point.Y = e.Stroke.StylusPoints[i].Y;
                                        stylusPoints.Add(point);
                                    }

                                    for (var i = n - x; i <= n; i++)
                                    {
                                        var point = new StylusPoint();

                                        point.PressureFactor = (float)((0.5 - pressure) * (n - i) / x + pressure);
                                        point.X = e.Stroke.StylusPoints[i].X;
                                        point.Y = e.Stroke.StylusPoints[i].Y;
                                        stylusPoints.Add(point);
                                    }
                                }
                                else
                                {
                                    for (var i = 0; i <= n; i++)
                                    {
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
            Debug.WriteLine($"墨迹平滑检查: UseAdvancedBezierSmoothing={Settings.Canvas.UseAdvancedBezierSmoothing}, wasStraightened={wasStraightened}");
            Debug.WriteLine($"异步平滑设置: UseAsyncInkSmoothing={Settings.Canvas.UseAsyncInkSmoothing}, _inkSmoothingManager={_inkSmoothingManager != null}");

            if (Settings.Canvas.UseAdvancedBezierSmoothing && !wasStraightened)
            {
                try
                {
                    Debug.WriteLine($"开始墨迹平滑处理: 原始点数={e.Stroke.StylusPoints.Count}, 直线拉直={wasStraightened}");

                    // 检查原始笔画是否仍然存在于画布中
                    if (inkCanvas.Strokes.Contains(e.Stroke))
                    {
                        // 使用新的异步墨迹平滑管理器
                        if (Settings.Canvas.UseAsyncInkSmoothing && _inkSmoothingManager != null)
                        {
                            Debug.WriteLine("使用异步墨迹平滑");
                            // 异步处理
                            _ = ProcessStrokeAsync(e.Stroke);
                        }
                        else
                        {
                            // 同步处理（向后兼容）
                            var smoothedStroke = _inkSmoothingManager?.SmoothStroke(e.Stroke) ?? e.Stroke;

                            if (smoothedStroke != e.Stroke)
                            {
                                // 替换原始笔画
                                SetNewBackupOfStroke();
                                _currentCommitType = CommitReason.ShapeRecognition;
                                inkCanvas.Strokes.Remove(e.Stroke);
                                inkCanvas.Strokes.Add(smoothedStroke);
                                _currentCommitType = CommitReason.UserInput;
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("原始笔画不在画布中，跳过平滑处理");
                    }
                }
                catch (Exception ex)
                {
                    // 如果高级平滑失败，回退到原始笔画
                    Debug.WriteLine($"高级贝塞尔曲线平滑失败: {ex.Message}");
                }
            }
            else if (Settings.Canvas.FitToCurve && !wasStraightened)
            {
                drawingAttributes.FitToCurve = true;
            }
        }

        /// <summary>
        /// 异步处理笔画平滑
        /// </summary>
        private async Task ProcessStrokeAsync(Stroke originalStroke)
        {
            try
            {
                Debug.WriteLine($"异步平滑开始: 原始点数={originalStroke.StylusPoints.Count}");
                await _inkSmoothingManager.SmoothStrokeAsync(originalStroke, (original, smoothed) =>
                {
                    Debug.WriteLine($"异步平滑完成: 原始点数={original.StylusPoints.Count}, 平滑后点数={smoothed.StylusPoints.Count}");
                    Debug.WriteLine($"墨迹比较: smoothed != original = {smoothed != original}");
                    Debug.WriteLine($"画布包含原始墨迹: {inkCanvas.Strokes.Contains(original)}");

                    // 在UI线程上执行笔画替换
                    if (inkCanvas.Strokes.Contains(original) && smoothed != original)
                    {
                        Debug.WriteLine("异步替换原始笔画为平滑后的笔画");
                        SetNewBackupOfStroke();
                        _currentCommitType = CommitReason.ShapeRecognition;
                        inkCanvas.Strokes.Remove(original);
                        inkCanvas.Strokes.Add(smoothed);
                        _currentCommitType = CommitReason.UserInput;
                    }
                    else
                    {
                        Debug.WriteLine($"异步平滑后的笔画与原始笔画相同，未进行替换 (contains={inkCanvas.Strokes.Contains(original)}, different={smoothed != original})");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"异步墨迹平滑失败: {ex.Message}");
            }
        }

        // New method: Checks if a stroke is potentially a straight line
        private bool IsPotentialStraightLine(Stroke stroke)
        {
            // 确保有足够的点来进行线条分析
            if (stroke.StylusPoints.Count < 5)
                return false;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);
            // 分辨率自适应阈值
            double adaptiveThreshold = Settings.Canvas.AutoStraightenLineThreshold * GetResolutionScale();
            // 线条必须足够长才考虑拉直，使用自适应阈值
            if (lineLength < adaptiveThreshold)
                return false;

            // 新增：检查墨迹复杂度，避免将复杂图形拉直
            if (IsComplexShape(stroke))
                return false;

            // 新增：检查是否为明显的曲线
            if (IsObviousCurve(stroke))
                return false;

            // 获取用户设置的灵敏度值，确保使用正确的设置
            double sensitivity = Settings.InkToShape.LineStraightenSensitivity;

            // 输出当前灵敏度值
            Debug.WriteLine($"IsPotentialStraightLine - sensitivity: {sensitivity}, length: {lineLength}");

            // 将灵敏度转换为阈值：灵敏度0.05-2.0映射到阈值0.01-0.4
            double quickThreshold = Math.Max(0.01, sensitivity * 0.2); // 确保最小阈值为0.01

            Debug.WriteLine($"使用快速检查阈值: {quickThreshold}");

            // 快速检查：计算几个关键点与直线的距离
            if (stroke.StylusPoints.Count >= 10)
            {
                List<Point> checkPoints;
                
                // 使用采样点进行更准确的判断
                if (Settings.Canvas.HighPrecisionLineStraighten)
                {
                    var allPoints = stroke.StylusPoints.Select(p => p.ToPoint()).ToList();
                    checkPoints = SamplePointsByDistance(allPoints, 10.0);
                    Debug.WriteLine($"高精度模式快速检查：原始点数={allPoints.Count}, 采样点数={checkPoints.Count}");
                }
                else
                {
                    // 取中点和1/4、3/4位置的点
                    int quarterIdx = stroke.StylusPoints.Count / 4;
                    int midIdx = stroke.StylusPoints.Count / 2;
                    int threeQuarterIdx = quarterIdx * 3;

                    checkPoints = new List<Point>
                    {
                        stroke.StylusPoints[quarterIdx].ToPoint(),
                        stroke.StylusPoints[midIdx].ToPoint(),
                        stroke.StylusPoints[threeQuarterIdx].ToPoint()
                    };
                }

                // 计算所有检查点与直线的平均偏差
                double totalDeviation = 0;
                double maxDeviation = 0;
                int validPointCount = 0;

                foreach (Point checkPoint in checkPoints)
                {
                    double deviation = DistanceFromLineToPoint(start, end, checkPoint);
                    totalDeviation += deviation;
                    maxDeviation = Math.Max(maxDeviation, deviation);
                    validPointCount++;
                }

                if (validPointCount > 0)
                {
                    double avgDeviation = totalDeviation / validPointCount;
                    // 使用相对偏差：偏差与线长的比例，并使用灵敏度进行调整
                    double quickRelativeThreshold = lineLength * quickThreshold;

                    // 使用平均偏差和最大偏差的综合判断
                    double deviationThreshold = Settings.Canvas.HighPrecisionLineStraighten 
                        ? Math.Max(avgDeviation, maxDeviation * 0.7) // 高精度模式更严格
                        : maxDeviation;

                    // 记录检测到的偏差
                    Debug.WriteLine($"Deviations: avg={avgDeviation:F2}, max={maxDeviation:F2}, threshold={quickRelativeThreshold:F2}, highPrecision={Settings.Canvas.HighPrecisionLineStraighten}");

                    if (deviationThreshold > quickRelativeThreshold)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 检查墨迹是否为复杂形状
        /// </summary>
        private bool IsComplexShape(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 10) return false;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);

            // 计算墨迹的实际路径长度
            double actualLength = 0;
            for (int i = 1; i < stroke.StylusPoints.Count; i++)
            {
                Point p1 = stroke.StylusPoints[i - 1].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();
                actualLength += GetDistance(p1, p2);
            }

            // 如果实际路径长度远大于直线距离，说明是复杂形状
            double complexityRatio = actualLength / Math.Max(lineLength, 1);
            if (complexityRatio > 2.5) // 实际路径是直线距离的2.5倍以上
            {
                Debug.WriteLine($"检测到复杂形状：复杂度比率 = {complexityRatio:F2}");
                return true;
            }

            // 检查方向变化次数
            int directionChanges = CountDirectionChanges(stroke);
            int maxAllowedChanges = Math.Max(3, stroke.StylusPoints.Count / 20); // 动态阈值
            if (directionChanges > maxAllowedChanges)
            {
                Debug.WriteLine($"检测到复杂形状：方向变化次数 = {directionChanges}，阈值 = {maxAllowedChanges}");
                return true;
            }

            // 检查是否有明显的回环或重叠
            if (HasSignificantLoops(stroke))
            {
                Debug.WriteLine("检测到复杂形状：存在明显回环");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否为明显的曲线（如圆弧、抛物线等）
        /// </summary>
        private bool IsObviousCurve(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 10) return false;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);

            // 检查曲率一致性
            if (HasConsistentCurvature(stroke))
            {
                Debug.WriteLine("检测到明显曲线：曲率一致");
                return true;
            }

            // 检查中点偏移（对圆弧特别有效）
            int midIndex = stroke.StylusPoints.Count / 2;
            Point midPoint = stroke.StylusPoints[midIndex].ToPoint();
            double midDeviation = DistanceFromLineToPoint(start, end, midPoint);

            // 如果中点偏移超过线长的15%，且偏移方向一致，可能是圆弧
            if (midDeviation > lineLength * 0.15)
            {
                // 检查偏移方向的一致性
                if (IsConsistentArcDirection(stroke))
                {
                    Debug.WriteLine($"检测到明显曲线：中点偏移 = {midDeviation:F2}，线长 = {lineLength:F2}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 计算方向变化次数
        /// </summary>
        private int CountDirectionChanges(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 3) return 0;

            int changes = 0;
            double lastAngle = 0;
            bool hasLastAngle = false;

            for (int i = 1; i < stroke.StylusPoints.Count - 1; i++)
            {
                Point p1 = stroke.StylusPoints[i - 1].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();
                Point p3 = stroke.StylusPoints[i + 1].ToPoint();

                // 计算角度变化
                double angle1 = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double angle2 = Math.Atan2(p3.Y - p2.Y, p3.X - p2.X);
                double angleDiff = Math.Abs(angle2 - angle1);

                // 处理角度跨越问题
                if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;

                // 如果角度变化超过30度，认为是方向变化
                if (angleDiff > Math.PI / 6) // 30度
                {
                    if (hasLastAngle && Math.Abs(angleDiff - lastAngle) > Math.PI / 12) // 15度
                    {
                        changes++;
                    }
                    lastAngle = angleDiff;
                    hasLastAngle = true;
                }
            }

            return changes;
        }

        /// <summary>
        /// 检查是否有明显的回环
        /// </summary>
        private bool HasSignificantLoops(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 20) return false;

            // 检查起点和终点是否接近（可能是闭合图形）
            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double startEndDistance = GetDistance(start, end);

            // 计算平均点间距
            double totalDistance = 0;
            for (int i = 1; i < stroke.StylusPoints.Count; i++)
            {
                Point p1 = stroke.StylusPoints[i - 1].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();
                totalDistance += GetDistance(p1, p2);
            }
            double avgPointDistance = totalDistance / (stroke.StylusPoints.Count - 1);

            // 如果起点和终点很接近，可能是闭合图形
            if (startEndDistance < avgPointDistance * 5)
            {
                return true;
            }

            // 检查是否有点重复经过相似区域
            int overlapCount = 0;
            double overlapThreshold = avgPointDistance * 3;

            for (int i = 0; i < stroke.StylusPoints.Count - 10; i += 5)
            {
                Point p1 = stroke.StylusPoints[i].ToPoint();
                for (int j = i + 10; j < stroke.StylusPoints.Count; j += 5)
                {
                    Point p2 = stroke.StylusPoints[j].ToPoint();
                    if (GetDistance(p1, p2) < overlapThreshold)
                    {
                        overlapCount++;
                        if (overlapCount > 3) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查曲率是否一致（用于识别圆弧等规则曲线）
        /// </summary>
        private bool HasConsistentCurvature(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 15) return false;

            List<double> curvatures = new List<double>();

            // 计算每个点的曲率
            for (int i = 2; i < stroke.StylusPoints.Count - 2; i++)
            {
                Point p1 = stroke.StylusPoints[i - 2].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();
                Point p3 = stroke.StylusPoints[i + 2].ToPoint();

                double curvature = CalculateCurvature(p1, p2, p3);
                if (!double.IsNaN(curvature) && !double.IsInfinity(curvature))
                {
                    curvatures.Add(Math.Abs(curvature));
                }
            }

            if (curvatures.Count < 5) return false;

            // 计算曲率的标准差
            double avgCurvature = curvatures.Average();
            double variance = curvatures.Select(c => Math.Pow(c - avgCurvature, 2)).Average();
            double stdDev = Math.Sqrt(variance);

            // 如果曲率变化很小且平均曲率不为零，可能是规则曲线
            return avgCurvature > 0.001 && stdDev / avgCurvature < 0.5;
        }

        /// <summary>
        /// 检查圆弧方向是否一致
        /// </summary>
        private bool IsConsistentArcDirection(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 10) return false;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();

            int positiveDeviations = 0;
            int negativeDeviations = 0;

            // 检查多个点相对于直线的偏移方向
            for (int i = 1; i < stroke.StylusPoints.Count - 1; i += Math.Max(1, stroke.StylusPoints.Count / 10))
            {
                Point p = stroke.StylusPoints[i].ToPoint();
                double signedDistance = SignedDistanceFromLineToPoint(start, end, p);

                if (Math.Abs(signedDistance) > 5) // 忽略很小的偏移
                {
                    if (signedDistance > 0) positiveDeviations++;
                    else negativeDeviations++;
                }
            }

            // 如果大部分点都在直线的同一侧，说明是一致的弧形
            int totalSignificantDeviations = positiveDeviations + negativeDeviations;
            if (totalSignificantDeviations < 3) return false;

            double consistency = Math.Max(positiveDeviations, negativeDeviations) / (double)totalSignificantDeviations;
            return consistency > 0.8; // 80%的点在同一侧
        }

        /// <summary>
        /// 计算三点的曲率
        /// </summary>
        private double CalculateCurvature(Point p1, Point p2, Point p3)
        {
            // 使用三点计算曲率的公式
            double a = GetDistance(p1, p2);
            double b = GetDistance(p2, p3);
            double c = GetDistance(p1, p3);

            if (a == 0 || b == 0 || c == 0) return 0;

            // 使用海伦公式计算面积
            double s = (a + b + c) / 2;
            double area = Math.Sqrt(s * (s - a) * (s - b) * (s - c));

            // 曲率 = 4 * 面积 / (a * b * c)
            return 4 * area / (a * b * c);
        }

        /// <summary>
        /// 计算点到直线的有符号距离
        /// </summary>
        private double SignedDistanceFromLineToPoint(Point lineStart, Point lineEnd, Point point)
        {
            // 使用叉积计算有符号距离
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lineLength = Math.Sqrt(dx * dx + dy * dy);

            if (lineLength == 0) return 0;

            return ((lineEnd.Y - lineStart.Y) * point.X - (lineEnd.X - lineStart.X) * point.Y +
                    lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X) / lineLength;
        }

        private bool TryGetStraightLineEndpoints(Stroke stroke, out Point endpoint1, out Point endpoint2)
        {
            endpoint1 = new Point();
            endpoint2 = new Point();

            var points = stroke.StylusPoints.Select(p => p.ToPoint()).ToList();
            if (points.Count < 10)
            {
                return false;
            }

            List<Point> workingPoints = points;
            if (Settings.Canvas.HighPrecisionLineStraighten)
            {
                workingPoints = SamplePointsByDistance(points, 10.0);
                Debug.WriteLine($"高精度模式：原始点数={points.Count}, 采样后点数={workingPoints.Count}");
            }

            // 使用总最小二乘法(TLS/PCA)进行直线拟合
            int n = workingPoints.Count - 8;
            if (n < 1)
            {
                // 如果采样后点数太少，回退到原始方法
                n = points.Count - 8;
                workingPoints = points;
            }

            List<Point> filteredPoints = new List<Point>();

            // 收集过滤后的点（跳过前 4 个和后 4 个点，用于计算直线方向）
            int skipCount = Math.Min(4, n / 2); // 确保跳过数量不超过一半
            for (int i = skipCount; i < n + skipCount && i < workingPoints.Count; i++)
            {
                filteredPoints.Add(workingPoints[i]);
            }

            // 计算中心点（使用过滤后的点）
            double centerX = 0, centerY = 0;
            foreach (Point p in filteredPoints)
            {
                centerX += p.X;
                centerY += p.Y;
            }
            centerX /= filteredPoints.Count;
            centerY /= filteredPoints.Count;

            // 计算协方差矩阵（使用过滤后的点）
            double covXX = 0, covYY = 0, covXY = 0;
            foreach (Point p in filteredPoints)
            {
                double dx = p.X - centerX;
                double dy = p.Y - centerY;
                covXX += dx * dx;
                covYY += dy * dy;
                covXY += dx * dy;
            }

            // 计算特征值和特征向量
            double trace = covXX + covYY;
            double determinant = covXX * covYY - covXY * covXY;
            double discriminant = Math.Sqrt(trace * trace - 4 * determinant);

            double eigenvalue1 = (trace + discriminant) / 2;
            double eigenvalue2 = (trace - discriminant) / 2;

            // 最大特征值对应的特征向量即为直线方向
            double directionX, directionY;
            if (Math.Abs(covXY) > 1e-10)
            {
                directionX = covXY;
                directionY = eigenvalue1 - covXX;
                // 归一化
                double length = Math.Sqrt(directionX * directionX + directionY * directionY);
                directionX /= length;
                directionY /= length;
            }
            else
            {
                // 如果协方差为 0，则是水平或垂直直线
                directionX = (covXX >= covYY) ? 1 : 0;
                directionY = (covXX >= covYY) ? 0 : 1;
            }

            // 计算解释方差比例（拟合优度）
            double totalVariance = eigenvalue1 + eigenvalue2;
            double explainedVarianceRatio = (totalVariance > 1e-10) ?
                Math.Max(eigenvalue1, eigenvalue2) / totalVariance : 1d;

            // 使用所有点计算端点
            double minProjection = double.MaxValue;
            double maxProjection = double.MinValue;

            // 计算所有点在直线方向上的投影
            List<Point> pointsForProjection = Settings.Canvas.HighPrecisionLineStraighten ? workingPoints : points;
            foreach (Point p in pointsForProjection)
            {
                // 相对于过滤点中心的投影
                double projection = (p.X - centerX) * directionX + (p.Y - centerY) * directionY;
                minProjection = Math.Min(minProjection, projection);
                maxProjection = Math.Max(maxProjection, projection);
            }

            // 计算端点坐标
            endpoint1 = new Point(
                centerX + minProjection * directionX,
                centerY + minProjection * directionY
            );

            endpoint2 = new Point(
                centerX + maxProjection * directionX,
                centerY + maxProjection * directionY
            );

            // 使用解释方差比例作为判断条件
            double threshold = 0.998 + Settings.InkToShape.LineNormalizationThreshold / 500;
            return explainedVarianceRatio > threshold;
        }

        // New method: Determines if a stroke should be straightened into a line 
        private bool ShouldStraightenLine(Stroke stroke)
        {
            // 分辨率自适应阈值
            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);
            double adaptiveThreshold = Settings.Canvas.AutoStraightenLineThreshold * GetResolutionScale();
            
            // 如果线条太短，不进行拉直处理
            if (lineLength < adaptiveThreshold)
            {
                Debug.WriteLine($"线条太短: {lineLength} < {adaptiveThreshold}");
                return false;
            }

            // 检查复杂度
            if (IsComplexShape(stroke))
            {
                Debug.WriteLine("拒绝拉直：检测到复杂形状");
                return false;
            }

            Point endpoint1, endpoint2;
            bool shouldStraighten = TryGetStraightLineEndpoints(stroke, out endpoint1, out endpoint2);
            
            if (shouldStraighten)
            {
                Debug.WriteLine($"接受拉直：判断为直线，解释方差比例满足阈值");
            }
            else
            {
                Debug.WriteLine($"拒绝拉直：判断不满足直线条件");
            }

            return shouldStraighten;
        }

        /// <summary>
        /// 计算墨迹的直线度评分（0-1，1表示完美直线）
        /// </summary>
        private double CalculateStraightnessScore(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 3) return 0;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();
            double lineLength = GetDistance(start, end);

            if (lineLength == 0) return 0;

            // 1. 计算偏差评分（基于点到直线的距离）
            double totalDeviation = 0;
            double maxDeviation = 0;
            int pointCount = 0;

            foreach (StylusPoint sp in stroke.StylusPoints)
            {
                Point p = sp.ToPoint();
                double deviation = DistanceFromLineToPoint(start, end, p);
                totalDeviation += deviation;
                maxDeviation = Math.Max(maxDeviation, deviation);
                pointCount++;
            }

            double avgDeviation = totalDeviation / pointCount;

            // 偏差评分：基于平均偏差和最大偏差
            double deviationScore = Math.Max(0, 1 - (avgDeviation / (lineLength * 0.05)) - (maxDeviation / (lineLength * 0.1)));

            // 2. 计算方向一致性评分
            double directionScore = CalculateDirectionConsistency(stroke);

            // 3. 计算路径效率评分（实际路径长度 vs 直线距离）
            double actualLength = 0;
            for (int i = 1; i < stroke.StylusPoints.Count; i++)
            {
                Point p1 = stroke.StylusPoints[i - 1].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();
                actualLength += GetDistance(p1, p2);
            }
            double efficiencyScore = Math.Max(0, Math.Min(1, lineLength / actualLength));

            // 4. 计算端点连接度评分（起点到终点的直接性）
            double endpointScore = 1.0; // 默认满分，因为我们已经有了起点和终点

            // 综合评分（加权平均）
            double finalScore = (deviationScore * 0.4 + directionScore * 0.3 + efficiencyScore * 0.2 + endpointScore * 0.1);

            Debug.WriteLine($"直线度评分详情: 偏差={deviationScore:F3}, 方向={directionScore:F3}, 效率={efficiencyScore:F3}, 综合={finalScore:F3}");

            return Math.Max(0, Math.Min(1, finalScore));
        }

        /// <summary>
        /// 计算方向一致性评分
        /// </summary>
        private double CalculateDirectionConsistency(Stroke stroke)
        {
            if (stroke.StylusPoints.Count < 5) return 1.0;

            Point start = stroke.StylusPoints.First().ToPoint();
            Point end = stroke.StylusPoints.Last().ToPoint();

            // 目标方向
            double targetAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);

            double totalAngleDifference = 0;
            int segmentCount = 0;

            // 计算每个线段与目标方向的角度差
            for (int i = 1; i < stroke.StylusPoints.Count; i++)
            {
                Point p1 = stroke.StylusPoints[i - 1].ToPoint();
                Point p2 = stroke.StylusPoints[i].ToPoint();

                double segmentLength = GetDistance(p1, p2);
                if (segmentLength < 2) continue; // 忽略太短的线段

                double segmentAngle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double angleDiff = Math.Abs(segmentAngle - targetAngle);

                // 处理角度跨越问题
                if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;

                totalAngleDifference += angleDiff;
                segmentCount++;
            }

            if (segmentCount == 0) return 1.0;

            double avgAngleDifference = totalAngleDifference / segmentCount;

            // 将角度差转换为评分（0-1）
            // 0度差 = 1分，90度差 = 0分
            double directionScore = Math.Max(0, 1 - (avgAngleDifference / (Math.PI / 2)));

            return directionScore;
        }

        // New method: Creates a straight line stroke between two points
        private StylusPointCollection CreateStraightLine(Point start, Point end)
        {
            StylusPointCollection points = new StylusPointCollection();

            // 根据是否启用压感触屏模式决定如何设置压感
            // 如果未启用压感触屏模式，则使用均匀粗细
            if (!Settings.Canvas.EnablePressureTouchMode || Settings.Canvas.DisablePressure ||
                Settings.InkToShape.IsInkToShapeNoFakePressureRectangle || penType == 1)
            {
                // 使用均匀粗细（所有点压感值都是0.5f）
                points.Add(new StylusPoint(start.X, start.Y, 0.5f));

                // 可以添加一些额外的中间点使线条更平滑（均匀粗细）
                double distance = GetDistance(start, end);
                if (distance > 100)
                {
                    // 对于较长的线条，添加几个中间点
                    for (int i = 1; i < 3; i++)
                    {
                        double ratio = i / 3.0;
                        Point midPoint = new Point(
                            start.X + (end.X - start.X) * ratio,
                            start.Y + (end.Y - start.Y) * ratio);
                        points.Add(new StylusPoint(midPoint.X, midPoint.Y, 0.5f));
                    }
                }

                points.Add(new StylusPoint(end.X, end.Y, 0.5f));
            }
            else
            {
                // 启用了压感触屏模式，使用变化的粗细（原有行为）
                points.Add(new StylusPoint(start.X, start.Y, 0.4f));

                // 添加中点，压感值较高，使线条中间较粗
                Point midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                points.Add(new StylusPoint(midPoint.X, midPoint.Y, 0.8f));

                points.Add(new StylusPoint(end.X, end.Y, 0.4f));
            }

            return points;
        }

        /// <summary>
        /// 高精度模式
        /// </summary>
        private List<Point> SamplePointsByDistance(List<Point> points, double sampleInterval = 10.0)
        {
            if (points == null || points.Count < 2)
                return points;

            List<Point> sampledPoints = new List<Point>();
            sampledPoints.Add(points[0]); // 总是包含起点

            double accumulatedDistance = 0;
            Point lastSampledPoint = points[0];

            for (int i = 1; i < points.Count; i++)
            {
                double segmentDistance = GetDistance(lastSampledPoint, points[i]);
                accumulatedDistance += segmentDistance;

                // 当累积距离达到采样间隔时，添加当前点
                if (accumulatedDistance >= sampleInterval)
                {
                    sampledPoints.Add(points[i]);
                    lastSampledPoint = points[i];
                    accumulatedDistance = 0; // 重置累积距离
                }
            }

            // 总是包含终点（如果还没有包含）
            if (sampledPoints.Count == 0 || GetDistance(sampledPoints.Last(), points.Last()) > 1.0)
            {
                sampledPoints.Add(points.Last());
            }

            return sampledPoints;
        }

        // New method: Gets distance from point to a line defined by two points
        private double DistanceFromLineToPoint(Point lineStart, Point lineEnd, Point point)
        {
            // Calculate distance from point to line defined by lineStart and lineEnd
            double lineLength = GetDistance(lineStart, lineEnd);
            if (lineLength == 0) return GetDistance(point, lineStart);

            // Calculate the cross product to get the perpendicular distance
            double distance = Math.Abs((lineEnd.Y - lineStart.Y) * point.X -
                                      (lineEnd.X - lineStart.X) * point.Y +
                                      lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X) / lineLength;
            return distance;
        }

        /// <summary>
        /// 判断一个 stroke 是否是直线（排除虚线和点线）
        /// </summary>
        /// <param name="stroke">要检查的 stroke</param>
        /// <returns>如果是直线返回 true，否则返回 false</returns>
        private bool IsStraightLine(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count == 0)
                return false;

            int pointCount = stroke.StylusPoints.Count;

            if (pointCount == 1)
                return false;

            // 最简单的直线：只有2个点
            if (pointCount == 2)
            {
                Point p1 = stroke.StylusPoints[0].ToPoint();
                Point p2 = stroke.StylusPoints[1].ToPoint();
                double lineLength = GetDistance(p1, p2);
                
                if (lineLength < 10)
                    return false;

                return true;
            }

            if (pointCount > 3)
                return false;

            // 对于3个点的情况，检查它们是否基本在一条直线上
            if (pointCount == 3)
            {
                Point p1 = stroke.StylusPoints[0].ToPoint();
                Point p2 = stroke.StylusPoints[1].ToPoint();
                Point p3 = stroke.StylusPoints[2].ToPoint();

                double totalLength = GetDistance(p1, p3);
                if (totalLength < 10)
                    return false;

                // 计算点到直线的距离
                // 使用 p1 和 p3 作为直线端点，检查 p2 是否在这条直线上
                double distance = DistanceFromLineToPoint(p1, p3, p2);

                // 如果点到直线的距离相对于线段长度很小，认为是直线
                // 使用相对误差阈值（比如 1%）
                if (totalLength > 0 && distance / totalLength < 0.01)
                    return true;

                return false;
            }

            return false;
        }

        // New method: Attempts to snap endpoints to existing stroke endpoints
        private Point[] GetSnappedEndpoints(Point start, Point end)
        {
            if (!Settings.Canvas.LineEndpointSnapping)
                return null;

            bool startSnapped = false;
            bool endSnapped = false;
            Point snappedStart = start;
            Point snappedEnd = end;

            // 使用设置中的吸附距离阈值
            double snapThreshold = Settings.Canvas.LineEndpointSnappingThreshold;

            // Check all strokes in canvas for potential snap points
            foreach (Stroke stroke in inkCanvas.Strokes)
            {
                if (stroke.StylusPoints.Count == 0) continue;

                // 只对直线进行端点吸附，跳过虚线和点线
                if (!IsStraightLine(stroke))
                    continue;

                // Get stroke endpoints
                Point strokeStart = stroke.StylusPoints.First().ToPoint();
                Point strokeEnd = stroke.StylusPoints.Last().ToPoint();

                // Check if start point should snap to an endpoint
                if (!startSnapped)
                {
                    if (GetDistance(start, strokeStart) < snapThreshold)
                    {
                        snappedStart = strokeStart;
                        startSnapped = true;
                    }
                    else if (GetDistance(start, strokeEnd) < snapThreshold)
                    {
                        snappedStart = strokeEnd;
                        startSnapped = true;
                    }
                }

                // Check if end point should snap to an endpoint
                if (!endSnapped)
                {
                    if (GetDistance(end, strokeStart) < snapThreshold)
                    {
                        snappedEnd = strokeStart;
                        endSnapped = true;
                    }
                    else if (GetDistance(end, strokeEnd) < snapThreshold)
                    {
                        snappedEnd = strokeEnd;
                        endSnapped = true;
                    }
                }

                // If both endpoints are snapped, we're done
                if (startSnapped && endSnapped) break;
            }

            // Return snapped points if any snapping occurred
            if (startSnapped || endSnapped)
            {
                return new[] { snappedStart, snappedEnd };
            }

            return null;
        }

        private void SetNewBackupOfStroke()
        {
            lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            var whiteboardIndex = CurrentWhiteboardIndex;
            if (currentMode == 0) whiteboardIndex = 0;

            strokeCollections[whiteboardIndex] = lastTouchDownStrokeCollection;
        }

        public double GetDistance(Point point1, Point point2)
        {
            return Math.Sqrt((point1.X - point2.X) * (point1.X - point2.X) +
                             (point1.Y - point2.Y) * (point1.Y - point2.Y));
        }

        public double GetPointSpeed(Point point1, Point point2, Point point3)
        {
            return (Math.Sqrt((point1.X - point2.X) * (point1.X - point2.X) +
                              (point1.Y - point2.Y) * (point1.Y - point2.Y))
                    + Math.Sqrt((point3.X - point2.X) * (point3.X - point2.X) +
                                (point3.Y - point2.Y) * (point3.Y - point2.Y)))
                   / 20;
        }

        public Point[] FixPointsDirection(Point p1, Point p2)
        {
            if (Math.Abs(p1.X - p2.X) / Math.Abs(p1.Y - p2.Y) > 8)
            {
                //水平
                var x = Math.Abs(p1.Y - p2.Y) / 2;
                if (p1.Y > p2.Y)
                {
                    p1.Y -= x;
                    p2.Y += x;
                }
                else
                {
                    p1.Y += x;
                    p2.Y -= x;
                }
            }
            else if (Math.Abs(p1.Y - p2.Y) / Math.Abs(p1.X - p2.X) > 8)
            {
                //垂直
                var x = Math.Abs(p1.X - p2.X) / 2;
                if (p1.X > p2.X)
                {
                    p1.X -= x;
                    p2.X += x;
                }
                else
                {
                    p1.X += x;
                    p2.X -= x;
                }
            }

            return new Point[2] { p1, p2 };
        }

        public StylusPointCollection GenerateFakePressureTriangle(StylusPointCollection points)
        {
            if (Settings.InkToShape.IsInkToShapeNoFakePressureTriangle || penType == 1)
            {
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
            else
            {
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

        public StylusPointCollection GenerateFakePressureRectangle(StylusPointCollection points)
        {
            if (Settings.InkToShape.IsInkToShapeNoFakePressureRectangle || penType == 1)
            {
                return points;
            }

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

        public Point GetCenterPoint(Point point1, Point point2)
        {
            return new Point((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
        }

        public StylusPoint GetCenterPoint(StylusPoint point1, StylusPoint point2)
        {
            return new StylusPoint((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
        }

        // 分辨率自适应：以1080P为基准，返回当前分辨率下的阈值倍数
        private double GetResolutionScale()
        {
            // 以1920x1080为基准
            double baseWidth = 1920.0;
            double baseHeight = 1080.0;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            // 取宽高平均缩放，防止极端比例
            double scaleW = screenWidth / baseWidth;
            double scaleH = screenHeight / baseHeight;
            return (scaleW + scaleH) / 2.0;
        }

        #region 矩形参考线系统

        /// <summary>
        /// 处理矩形参考线系统
        /// </summary>
        private void ProcessRectangleGuideLines(Stroke newStroke)
        {
            // 只有启用矩形识别时才处理
            if (!Settings.InkToShape.IsInkToShapeRectangle) return;

            // 检查新笔画是否为直线
            if (!IsPotentialStraightLine(newStroke)) return;

            Point startPoint = newStroke.StylusPoints[0].ToPoint();
            Point endPoint = newStroke.StylusPoints[newStroke.StylusPoints.Count - 1].ToPoint();

            // 创建新的参考线
            var newGuideLine = new RectangleGuideLine(newStroke, startPoint, endPoint);

            // 清理过期的参考线（超过30秒的）
            CleanupExpiredGuideLines();

            // 添加新参考线
            rectangleGuideLines.Add(newGuideLine);

            // 检查是否可以构成矩形
            CheckForRectangleFormation();
        }

        /// <summary>
        /// 清理过期的参考线
        /// </summary>
        private void CleanupExpiredGuideLines()
        {
            var expireTime = DateTime.Now.AddSeconds(-30); // 30秒过期
            for (int i = rectangleGuideLines.Count - 1; i >= 0; i--)
            {
                var guideLine = rectangleGuideLines[i];
                if (guideLine.CreatedTime < expireTime || !inkCanvas.Strokes.Contains(guideLine.OriginalStroke))
                {
                    rectangleGuideLines.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 检查是否可以构成矩形
        /// </summary>
        private void CheckForRectangleFormation()
        {
            if (rectangleGuideLines.Count < 4) return;

            // 尝试找到四条能构成矩形的直线
            var rectangleLines = FindRectangleLines();
            if (rectangleLines != null && rectangleLines.Count == 4)
            {
                // 创建矩形并替换原有直线
                CreateRectangleFromLines(rectangleLines);
            }
        }

        /// <summary>
        /// 寻找能构成矩形的四条直线
        /// </summary>
        private List<RectangleGuideLine> FindRectangleLines()
        {
            // 按时间排序，优先考虑最近绘制的直线
            var sortedLines = rectangleGuideLines.OrderByDescending(l => l.CreatedTime).ToList();

            // 尝试不同的四条直线组合
            for (int i = 0; i < sortedLines.Count - 3; i++)
            {
                for (int j = i + 1; j < sortedLines.Count - 2; j++)
                {
                    for (int k = j + 1; k < sortedLines.Count - 1; k++)
                    {
                        for (int l = k + 1; l < sortedLines.Count; l++)
                        {
                            var lines = new List<RectangleGuideLine> { sortedLines[i], sortedLines[j], sortedLines[k], sortedLines[l] };
                            if (CanFormRectangle(lines))
                            {
                                return lines;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 判断四条直线是否能构成矩形
        /// </summary>
        private bool CanFormRectangle(List<RectangleGuideLine> lines)
        {
            if (lines.Count != 4) return false;

            // 分类水平线和垂直线
            var horizontalLines = lines.Where(l => l.IsHorizontal).ToList();
            var verticalLines = lines.Where(l => l.IsVertical).ToList();

            // 必须有2条水平线和2条垂直线
            if (horizontalLines.Count != 2 || verticalLines.Count != 2) return false;

            // 检查端点相交关系
            return CheckEndpointConnections(horizontalLines, verticalLines);
        }

        /// <summary>
        /// 检查端点相交关系
        /// </summary>
        private bool CheckEndpointConnections(List<RectangleGuideLine> horizontalLines, List<RectangleGuideLine> verticalLines)
        {
            // 收集所有端点
            var allEndpoints = new List<Point>();
            foreach (var line in horizontalLines.Concat(verticalLines))
            {
                allEndpoints.Add(line.StartPoint);
                allEndpoints.Add(line.EndPoint);
            }

            // 检查是否有4个相交点（允许一定误差）
            var intersectionPoints = new List<Point>();

            foreach (var hLine in horizontalLines)
            {
                foreach (var vLine in verticalLines)
                {
                    var intersection = GetLineIntersection(hLine, vLine);
                    if (intersection.HasValue)
                    {
                        // 检查交点是否在两条线段的端点附近
                        if (IsPointNearLineEndpoints(intersection.Value, hLine) &&
                            IsPointNearLineEndpoints(intersection.Value, vLine))
                        {
                            intersectionPoints.Add(intersection.Value);
                        }
                    }
                }
            }

            // 需要有4个交点才能构成矩形
            return intersectionPoints.Count >= 4;
        }

        /// <summary>
        /// 计算两条直线的交点
        /// </summary>
        private Point? GetLineIntersection(RectangleGuideLine line1, RectangleGuideLine line2)
        {
            double x1 = line1.StartPoint.X, y1 = line1.StartPoint.Y;
            double x2 = line1.EndPoint.X, y2 = line1.EndPoint.Y;
            double x3 = line2.StartPoint.X, y3 = line2.StartPoint.Y;
            double x4 = line2.EndPoint.X, y4 = line2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return null; // 平行线

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

            double intersectionX = x1 + t * (x2 - x1);
            double intersectionY = y1 + t * (y2 - y1);

            return new Point(intersectionX, intersectionY);
        }

        /// <summary>
        /// 检查点是否在直线端点附近
        /// </summary>
        private bool IsPointNearLineEndpoints(Point point, RectangleGuideLine line)
        {
            double distToStart = GetDistance(point, line.StartPoint);
            double distToEnd = GetDistance(point, line.EndPoint);

            return distToStart <= RECTANGLE_ENDPOINT_THRESHOLD || distToEnd <= RECTANGLE_ENDPOINT_THRESHOLD;
        }

        /// <summary>
        /// 从四条直线创建矩形
        /// </summary>
        private void CreateRectangleFromLines(List<RectangleGuideLine> lines)
        {
            try
            {
                // 计算矩形的四个角点
                var corners = CalculateRectangleCorners(lines);
                if (corners == null || corners.Count != 4) return;

                // 创建矩形笔画
                var pointList = new List<Point>(corners) { corners[0] }; // 闭合矩形
                var point = new StylusPointCollection(pointList);
                var rectangleStroke = new Stroke(GenerateFakePressureRectangle(point))
                {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };

                // 移除原有的四条直线
                SetNewBackupOfStroke();
                _currentCommitType = CommitReason.ShapeRecognition;

                foreach (var line in lines)
                {
                    if (inkCanvas.Strokes.Contains(line.OriginalStroke))
                    {
                        inkCanvas.Strokes.Remove(line.OriginalStroke);
                    }
                }

                // 添加新的矩形
                inkCanvas.Strokes.Add(rectangleStroke);
                _currentCommitType = CommitReason.UserInput;

                // 清理参考线
                foreach (var line in lines)
                {
                    rectangleGuideLines.Remove(line);
                }

                // 清空新笔画集合，避免重复处理
                newStrokes = new StrokeCollection();

                Debug.WriteLine("成功创建矩形参考线矩形");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建矩形时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算矩形的四个角点
        /// </summary>
        private List<Point> CalculateRectangleCorners(List<RectangleGuideLine> lines)
        {
            var horizontalLines = lines.Where(l => l.IsHorizontal).ToList();
            var verticalLines = lines.Where(l => l.IsVertical).ToList();

            if (horizontalLines.Count != 2 || verticalLines.Count != 2) return null;

            var corners = new List<Point>();

            // 计算四个交点
            foreach (var hLine in horizontalLines)
            {
                foreach (var vLine in verticalLines)
                {
                    var intersection = GetLineIntersection(hLine, vLine);
                    if (intersection.HasValue)
                    {
                        corners.Add(intersection.Value);
                    }
                }
            }

            if (corners.Count != 4) return null;

            // 按顺序排列角点（顺时针或逆时针）
            return SortRectangleCorners(corners);
        }

        /// <summary>
        /// 按顺序排列矩形角点
        /// </summary>
        private List<Point> SortRectangleCorners(List<Point> corners)
        {
            if (corners.Count != 4) return corners;

            // 计算中心点
            double centerX = corners.Average(p => p.X);
            double centerY = corners.Average(p => p.Y);
            var center = new Point(centerX, centerY);

            // 按角度排序
            return corners.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToList();
        }

        #endregion
    }
}