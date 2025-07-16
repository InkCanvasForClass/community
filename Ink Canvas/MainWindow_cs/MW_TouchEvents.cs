using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
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
                
                // 退出多指书写模式后，恢复手掌擦功能
                // 这里不需要特别操作，因为设置了isInMultiTouchMode = false后，
                // 下次触发Main_Grid_TouchDown时会自动判断并启用手掌擦功能
            }
            else {
                // 进入多指书写模式前，如果当前处于手掌擦状态，先关闭手掌擦
                if (isLastTouchEraser) {
                    isLastTouchEraser = false;
                    currentPalmEraserShape = null;
                }
                
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
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            if (!isHidingSubPanelsWhenInking) {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            double boundWidth = e.GetTouchPoint(null).Bounds.Width, eraserMultiplier = 1.0;
            if (!Settings.Advanced.EraserBindTouchMultiplier && Settings.Advanced.IsSpecialScreen)
                eraserMultiplier = 1 / Settings.Advanced.TouchMultiplier;

            if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen) //启用特殊屏幕且触摸倍数为 0 时禁用橡皮
                && boundWidth > BoundsWidth * 2.5) {
                if (drawingShapeMode == 0 && forceEraser) return;
                currentPalmEraserShape = GetPalmRectangleEraserShape(eraserMultiplier);
                inkCanvas.EraserShape = currentPalmEraserShape;
                TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                isLastTouchEraser = true;
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            else {
                TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
                // 修复面积擦时不显示橡皮形状：无论 forcePointEraser 状态，均显示 50x50 橡皮
                inkCanvas.EraserShape = new EllipseStylusShape(50, 50);
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e) {

            inkCanvas.CaptureStylus();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            // 确保手写笔模式下显示光标
            if (Settings.Canvas.IsShowCursor) {
                inkCanvas.ForceCursor = true;
                inkCanvas.UseCustomCursor = true;
                
                // 根据当前编辑模式设置不同的光标
                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                    inkCanvas.Cursor = Cursors.Cross;
                } else if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink) {
                    var sri = Application.GetResourceStream(new Uri("Resources/Cursors/Pen.cur", UriKind.Relative));
                    if (sri != null)
                        inkCanvas.Cursor = new Cursor(sri.Stream);
                }
                
                // 强制显示光标
                System.Windows.Forms.Cursor.Show();
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
        }

        private async void MainWindow_StylusUp(object sender, StylusEventArgs e) {
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

            inkCanvas.ReleaseStylusCapture();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;
        }

        private void MainWindow_StylusMove(object sender, StylusEventArgs e) {
            try {
                if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None) return;
                try {
                    if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                }
                catch { }

                // 确保手写笔移动时光标保持可见
                if (Settings.Canvas.IsShowCursor) {
                    inkCanvas.ForceCursor = true;
                    inkCanvas.UseCustomCursor = true;
                    System.Windows.Forms.Cursor.Show();
                }

                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                var stylusPointCollection = e.GetStylusPoints(this);
                foreach (var stylusPoint in stylusPointCollection)
                    strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                strokeVisual.Redraw();
            }
            catch { }
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
            return TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode) ? inkCanvasEditingMode : inkCanvas.EditingMode;
        }

        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
            new Dictionary<int, InkCanvasEditingMode>();

        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion


        private int lastTouchDownTime = 0, lastTouchUpTime = 0;

        private Point iniP = new Point(0, 0);
        private bool isLastTouchEraser = false;
        private bool forcePointEraser = true;
        // 用于记录手掌擦的尺寸和形状
        private StylusShape currentPalmEraserShape = null;

        /// <summary>
        /// 根据用户在设置面板中选择的橡皮大小，生成"手掌橡皮"默认的矩形黑板擦形状。
        /// 该形状大小不随触控面积等实时变化，仅受设置的橡皮大小影响。
        /// </summary>
        /// <param name="multiplier">特殊屏幕触摸倍数修正系数</param>
        /// <returns>RectangleStylusShape</returns>
        private StylusShape GetPalmRectangleEraserShape(double multiplier = 1.0) {
            double k = 1;
            switch (Settings.Canvas.EraserSize) {
                case 0:
                    k = 0.5;
                    break;
                case 1:
                    k = 0.8;
                    break;
                case 3:
                    k = 1.25;
                    break;
                case 4:
                    k = 1.5;
                    break;
            }

            // 参照圆形橡皮 k*90 的基准，将矩形宽度压缩到 0.6，保持高度一致
            double baseLen = k * 90 * multiplier;
            return new RectangleStylusShape(baseLen * 0.6, baseLen);
        }

        private void Main_Grid_TouchDown(object sender, TouchEventArgs e) {
            // 确保触摸时显示自定义光标
            if (Settings.Canvas.IsShowCursor) {
                inkCanvas.ForceCursor = true;
                System.Windows.Forms.Cursor.Show();
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
            
            // 如果已处于多指书写模式，禁用手掌擦功能
            if (isInMultiTouchMode) {
                isLastTouchEraser = false;
                currentPalmEraserShape = null;
                return;
            }
            
            // 如果已经处于手掌擦状态，保持状态不变
            if (isLastTouchEraser && currentPalmEraserShape != null) {
                inkCanvas.EraserShape = currentPalmEraserShape;
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                return;
            }
            
            double boundsWidth = GetTouchBoundWidth(e), eraserMultiplier = 1.0;
            if (!Settings.Advanced.EraserBindTouchMultiplier && Settings.Advanced.IsSpecialScreen)
                eraserMultiplier = 1 / Settings.Advanced.TouchMultiplier;
                
            // 检查触控点数量，只有大于等于三个触控点时才激活手掌擦功能
            if (dec.Count >= 3 && boundsWidth > BoundsWidth) {
                // 保存当前的编辑模式，以便恢复
                if (!isLastTouchEraser) {
                    prePalmEraserEditingMode = inkCanvas.EditingMode;
                    // 模拟点击橡皮选项卡
                    EraserIcon_Click(null, null);
                }
                
                isLastTouchEraser = true;
                if (drawingShapeMode == 0 && forceEraser) return;
                if (boundsWidth > BoundsWidth * 2.5) {
                    // 直接使用固定尺寸的矩形黑板擦形状，不随触控面积动态变化
                    currentPalmEraserShape = GetPalmRectangleEraserShape(eraserMultiplier);
                    inkCanvas.EraserShape = currentPalmEraserShape;
                    TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                }
                else {
                    if (StackPanelPPTControls.Visibility == Visibility.Visible && inkCanvas.Strokes.Count == 0 &&
                        Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl) {
                        isLastTouchEraser = false;
                        currentPalmEraserShape = null;
                        inkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
                        inkCanvas.Opacity = 0.1;
                    }
                    else {
                        // 手掌橡皮固定为矩形黑板擦，大小由设置决定
                        currentPalmEraserShape = GetPalmRectangleEraserShape(eraserMultiplier);
                        inkCanvas.EraserShape = currentPalmEraserShape;
                        TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                    }
                }
            }
            else {
                isLastTouchEraser = false;
                currentPalmEraserShape = null;
                // 修复面积擦时不显示橡皮形状：无论 forcePointEraser 状态，均显示 50x50 橡皮
                inkCanvas.EraserShape = new EllipseStylusShape(50, 50);
                // 修复触屏状态下几何绘制功能不可用的问题：在几何绘制模式下不应该因为forceEraser而直接返回
                if (forceEraser && drawingShapeMode == 0) return;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        private double GetTouchBoundWidth(TouchEventArgs e) {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height); //四边红外
            if (Settings.Advanced.IsSpecialScreen) value *= Settings.Advanced.TouchMultiplier;
            return value;
        }

        //记录触摸设备ID
        private List<int> dec = new List<int>();

        //中心点
        private Point centerPoint;
        private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        private bool isSingleFingerDragMode = false;

        // 保存触发手掌擦前的编辑模式，用于手掌擦结束后恢复
        private InkCanvasEditingMode prePalmEraserEditingMode = InkCanvasEditingMode.Ink;
        
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
            
            // 如果是手掌触发的面积擦抬起，需要确保橡皮擦形状被正确重置
            if (isLastTouchEraser && dec.Count == 0) {
                isLastTouchEraser = false;
                currentPalmEraserShape = null; // 清除保存的手掌擦形状
                
                // 当手掌擦消失时，恢复到之前的编辑模式
                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                    // 根据之前的编辑模式模拟点击相应的选项卡
                    if (prePalmEraserEditingMode == InkCanvasEditingMode.Ink) {
                        // 模拟点击批注选项卡
                        PenIcon_Click(null, null);
                    } else if (prePalmEraserEditingMode == InkCanvasEditingMode.None || 
                               prePalmEraserEditingMode == InkCanvasEditingMode.Select) {
                        // 模拟点击光标选项卡
                        CursorIcon_Click(null, null);
                    } else {
                        // 其他编辑模式时恢复之前的模式
                        inkCanvas.EditingMode = prePalmEraserEditingMode;
                        if (forcePointEraser) {
                            // 重新应用当前设置的橡皮擦形状
                            ApplyCurrentEraserShape();
                        }
                    }
                }
            }
            
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

        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e) {
            // 手掌擦时禁止移动/缩放
            if (isLastTouchEraser || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                return;
            // 三指及以上禁止缩放
            bool disableScale = dec.Count >= 3;
            if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
            if ((dec.Count >= 2 && (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode ||
                                    StackPanelPPTControls.Visibility != Visibility.Visible ||
                                    StackPanelPPTButtons.Visibility == Visibility.Collapsed)) ||
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
                    if (Settings.Gesture.IsEnableTwoFingerZoom && !disableScale)
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
                }
                else {
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
                    }
                    else {
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
