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
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        #region Multi-Touch

        private bool isInMultiTouchMode = false;

        private void BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isInMultiTouchMode) {
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.Children.Clear();
                isInMultiTouchMode = false;
            } else {
                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.Children.Clear();
                isInMultiTouchMode = true;
            }
        }

        private void MainWindow_TouchDown(object sender, TouchEventArgs e) {
            if (!isCursorHidden && Settings.Gesture.HideCursorWhenUsingTouchDevice) {
                System.Windows.Forms.Cursor.Hide();
                isCursorHidden = true;
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            if (!isHidingSubPanelsWhenInking) {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            // 不禁用手势橡皮
            if (!Settings.Gesture.DisableGestureEraser) {
                double boundWidth = e.GetTouchPoint(null).Bounds.Width;
                if ((Settings.Advanced.TouchMultiplier != 0 ||
                     !Settings.Advanced.IsSpecialScreen) //启用特殊屏幕且触摸倍数为 0 时禁用橡皮
                    && (boundWidth > BoundsWidth)) {
                    if (drawingShapeMode == 0 && forceEraser) return;
                    double EraserThresholdValue = Settings.Startup.IsEnableNibMode
                        ? Settings.Advanced.NibModeBoundsWidthThresholdValue
                        : Settings.Advanced.FingerModeBoundsWidthThresholdValue;
                    if (boundWidth > BoundsWidth * EraserThresholdValue) {
                        boundWidth *= (Settings.Startup.IsEnableNibMode
                            ? Settings.Advanced.NibModeBoundsWidthEraserSize
                            : Settings.Advanced.FingerModeBoundsWidthEraserSize);
                        if (Settings.Advanced.IsSpecialScreen) boundWidth *= Settings.Advanced.TouchMultiplier;
                        TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                        eraserWidth = boundWidth;
                        isEraserCircleShape = Settings.Canvas.EraserShapeType == 0;
                        isUsingStrokesEraser = false;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    } else {
                        isUsingStrokesEraser = true;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                } else {
                    inkCanvas.EraserShape =
                        forcePointEraser ? new EllipseStylusShape(50, 50) : new EllipseStylusShape(5, 5);
                    TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
            }
        }

        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e) {
            if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) {
                if (!isCursorHidden && Settings.Gesture.HideCursorWhenUsingTouchDevice &&
                    e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) {
                    System.Windows.Forms.Cursor.Hide();
                    isCursorHidden = true;
                }

                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                    || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                    || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

                TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
            }
        }

        private async void MainWindow_StylusUp(object sender, StylusEventArgs e) {
            if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) {
                try {
                    inkCanvas.Strokes.Add(GetStrokeVisual(e.StylusDevice.Id).Stroke);
                    await Task.Delay(5); // 避免渲染墨迹完成前预览墨迹被删除导致墨迹闪烁
                    inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));
                    inkCanvas_StrokeCollected(inkCanvas,
                        new InkCanvasStrokeCollectedEventArgs(GetStrokeVisual(e.StylusDevice.Id).Stroke));
                }
                catch (Exception ex) {
                    Label.Content = ex.ToString();
                }

                try {
                    StrokeVisualList.Remove(e.StylusDevice.Id);
                    VisualCanvasList.Remove(e.StylusDevice.Id);
                    TouchDownPointsList.Remove(e.StylusDevice.Id);
                    if (StrokeVisualList.Count == 0 || VisualCanvasList.Count == 0 || TouchDownPointsList.Count == 0) {
                        inkCanvas.Children.Clear();
                        StrokeVisualList.Clear();
                        VisualCanvasList.Clear();
                        TouchDownPointsList.Clear();
                    }
                }
                catch { }

                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            }
        }

        private void MainWindow_StylusMove(object sender, StylusEventArgs e) {
            if (!isCursorHidden && Settings.Gesture.HideCursorWhenUsingTouchDevice &&
                e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) {
                System.Windows.Forms.Cursor.Hide();
                isCursorHidden = true;
            }

            if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) {
                try {
                    if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None) return;
                    try {
                        if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                    }
                    catch { }

                    var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                    var stylusPointCollection = e.GetStylusPoints(this);
                    foreach (var stylusPoint in stylusPointCollection)
                        strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                    strokeVisual.Redraw();
                }
                catch { }
            }
        }

        private StrokeVisual GetStrokeVisual(int id) {
            if (StrokeVisualList.TryGetValue(id, out var visual)) return visual;

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas(strokeVisual);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        private VisualCanvas GetVisualCanvas(int id) {
            return VisualCanvasList.TryGetValue(id, out var visualCanvas) ? visualCanvas : null;
        }

        private InkCanvasEditingMode GetTouchDownPointsList(int id) {
            return TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode)
                ? inkCanvasEditingMode
                : inkCanvas.EditingMode;
        }

        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
            new Dictionary<int, InkCanvasEditingMode>();

        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion

        #region Touch Pointer Hide

        public bool isCursorHidden = false;

        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e) {
            if (e.StylusDevice == null) {
                if (isCursorHidden) {
                    System.Windows.Forms.Cursor.Show();
                    isCursorHidden = false;
                }
            } else if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) {
                if (isCursorHidden) {
                    System.Windows.Forms.Cursor.Show();
                    isCursorHidden = false;
                }
            }
        }

        #endregion

        private int lastTouchDownTime = 0, lastTouchUpTime = 0;

        private Point iniP = new Point(0, 0);
        private bool isLastTouchEraser = false;
        private bool forcePointEraser = true;

        private void Main_Grid_TouchDown(object sender, TouchEventArgs e) {
            if (!isCursorHidden && Settings.Gesture.HideCursorWhenUsingTouchDevice) {
                System.Windows.Forms.Cursor.Hide();
                isCursorHidden = true;
            }

            inkCanvas.CaptureTouch(e.TouchDevice);
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            if (!isHidingSubPanelsWhenInking) {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            if (NeedUpdateIniP()) iniP = e.GetTouchPoint(inkCanvas).Position;
            if (drawingShapeMode == 9 && isFirstTouchCuboid == false) MouseTouchMove(iniP);
            inkCanvas.Opacity = 1;

            if (!Settings.Gesture.DisableGestureEraser) {
                double boundsWidth = GetTouchBoundWidth(e);
                if ((Settings.Advanced.TouchMultiplier != 0 ||
                     !Settings.Advanced.IsSpecialScreen) //启用特殊屏幕且触摸倍数为 0 时禁用橡皮
                    && (boundsWidth > BoundsWidth)) {
                    isLastTouchEraser = true;
                    if (drawingShapeMode == 0 && forceEraser) return;
                    double EraserThresholdValue = Settings.Startup.IsEnableNibMode
                        ? Settings.Advanced.NibModeBoundsWidthThresholdValue
                        : Settings.Advanced.FingerModeBoundsWidthThresholdValue;
                    if (boundsWidth > BoundsWidth * EraserThresholdValue) {
                        boundsWidth *= (Settings.Startup.IsEnableNibMode
                            ? Settings.Advanced.NibModeBoundsWidthEraserSize
                            : Settings.Advanced.FingerModeBoundsWidthEraserSize);
                        if (Settings.Advanced.IsSpecialScreen) boundsWidth *= Settings.Advanced.TouchMultiplier;
                        eraserWidth = boundsWidth;
                        isEraserCircleShape = Settings.Canvas.EraserShapeType == 0;
                        isUsingStrokesEraser = false;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    } else {
                        isUsingStrokesEraser = true;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                } else {
                    isLastTouchEraser = false;
                    inkCanvas.EraserShape =
                        forcePointEraser ? new EllipseStylusShape(50, 50) : new EllipseStylusShape(5, 5);
                    if (forceEraser) return;
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
            }
        }

        public double GetTouchBoundWidth(TouchEventArgs e) {
            var args = e.GetTouchPoint(null).Bounds;
            if (!Settings.Advanced.IsQuadIR) return args.Width;
            else return Math.Sqrt(args.Width * args.Height); //四边红外
        }

        //记录触摸设备ID
        private List<int> dec = new List<int>();

        //中心点
        private Point centerPoint;
        private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        private bool isSingleFingerDragMode = false;

        private void inkCanvas_PreviewTouchDown(object sender, TouchEventArgs e) {
            inkCanvas.CaptureTouch(e.TouchDevice);
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            dec.Add(e.TouchDevice.Id);
            //设备1个的时候，记录中心点
            if (dec.Count == 1) {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;

                //记录第一根手指点击时的 StrokeCollection
                lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            }

            //设备两个及两个以上，将画笔功能关闭
            if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture) {
                if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None ||
                    inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;
                lastInkCanvasEditingMode = inkCanvas.EditingMode;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private void inkCanvas_PreviewTouchUp(object sender, TouchEventArgs e) {
            inkCanvas.ReleaseAllTouchCaptures();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            //手势完成后切回之前的状态
            if (dec.Count > 1)
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    inkCanvas.EditingMode = lastInkCanvasEditingMode;
            dec.Remove(e.TouchDevice.Id);
            inkCanvas.Opacity = 1;
            if (dec.Count == 0)
                if (lastTouchDownStrokeCollection.Count() != inkCanvas.Strokes.Count() &&
                    !(drawingShapeMode == 9 && !isFirstTouchCuboid)) {
                    var whiteboardIndex = CurrentWhiteboardIndex;
                    if (currentMode == 0) whiteboardIndex = 0;
                    strokeCollections[whiteboardIndex] = lastTouchDownStrokeCollection;
                }
        }

        private void inkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e) {
            e.Mode = ManipulationModes.All;
        }

        private void inkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e) { }

        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e) {
            if (e.Manipulators.Count() != 0) return;
            if (forceEraser) return;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }


        //private void inkCanvas_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        //{
        //    if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture || inkCanvas.Strokes.Count == 0 || dec.Count() < 2) return;
        //    _currentCommitType = CommitReason.Manipulation;
        //    StrokeCollection strokes = inkCanvas.GetSelectedStrokes();
        //    if (strokes.Count != 0)
        //    {
        //        inkCanvas.Strokes.Replace(strokes, strokes.Clone());
        //    }
        //    else
        //    {
        //        var originalStrokes = inkCanvas.Strokes;
        //        var targetStrokes = originalStrokes.Clone();
        //        originalStrokes.Replace(originalStrokes, targetStrokes);
        //    }
        //    _currentCommitType = CommitReason.UserInput;
        //}

        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e) {
            if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
            if ((dec.Count >= 2 && (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode ||
                                    BorderFloatingBarExitPPTBtn.Visibility != Visibility.Visible)) ||
                isSingleFingerDragMode) {
                var md = e.DeltaManipulation;
                var trans = md.Translation; // 获得位移矢量

                var m = new Matrix();

                if (Settings.Gesture.IsEnableTwoFingerTranslate)
                    m.Translate(trans.X, trans.Y); // 移动

                if (Settings.Gesture.IsEnableTwoFingerGestureTranslateOrRotation) {
                    var rotate = md.Rotation; // 获得旋转角度
                    var scale = md.Scale; // 获得缩放倍数

                    // Find center of element and then transform to get current location of center
                    var fe = e.Source as FrameworkElement;
                    var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                    center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

                    if (Settings.Gesture.IsEnableTwoFingerRotation)
                        m.RotateAt(rotate, center.X, center.Y); // 旋转
                    if (Settings.Gesture.IsEnableTwoFingerZoom)
                        m.ScaleAt(scale.X, scale.Y, center.X, center.Y); // 缩放
                }

                var strokes = inkCanvas.GetSelectedStrokes();
                if (strokes.Count != 0) {
                    foreach (var stroke in strokes) {
                        stroke.Transform(m, false);

                        foreach (var circle in circles)
                            if (stroke == circle.Stroke) {
                                circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                                    circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                                circle.Centroid = new Point(
                                    (circle.Stroke.StylusPoints[0].X +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                    (circle.Stroke.StylusPoints[0].Y +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                                break;
                            }

                        if (!Settings.Gesture.IsEnableTwoFingerZoom) continue;
                        try {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch { }
                    }
                } else {
                    if (Settings.Gesture.IsEnableTwoFingerZoom) {
                        foreach (var stroke in inkCanvas.Strokes) {
                            stroke.Transform(m, false);
                            try {
                                stroke.DrawingAttributes.Width *= md.Scale.X;
                                stroke.DrawingAttributes.Height *= md.Scale.Y;
                            }
                            catch { }
                        }

                        ;
                    } else {
                        foreach (var stroke in inkCanvas.Strokes) stroke.Transform(m, false);
                        ;
                    }

                    foreach (var circle in circles) {
                        circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                            circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                        circle.Centroid = new Point(
                            (circle.Stroke.StylusPoints[0].X +
                             circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                            (circle.Stroke.StylusPoints[0].Y +
                             circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2
                        );
                    }
                }
            }
        }
    }
}