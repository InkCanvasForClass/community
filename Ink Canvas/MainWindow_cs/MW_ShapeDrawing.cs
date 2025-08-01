﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        #region Floating Bar Control

        private void ImageDrawShape_MouseUp(object sender, MouseButtonEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == ShapeDrawFloatingBarBtn && lastBorderMouseDownObject != ShapeDrawFloatingBarBtn) return;

            // FloatingBarIcons_MouseUp_New(sender);
            if (BorderDrawShape.Visibility == Visibility.Visible) {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
            }
            else {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BorderDrawShape);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderDrawShape);
            }
        }

        #endregion Floating Bar Control

        private int drawingShapeMode;
        private bool isLongPressSelected; // 用于存是否是"选中"状态，便于后期抬笔后不做切换到笔的处理

        #region Buttons

        private void SymbolIconPinBorderDrawShape_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            ToggleSwitchDrawShapeBorderAutoHide.IsOn = !ToggleSwitchDrawShapeBorderAutoHide.IsOn;

            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn)
                ((SymbolIcon)sender).Symbol = Symbol.Pin;
            else
                ((SymbolIcon)sender).Symbol = Symbol.UnPin;
        }

        private object lastMouseDownSender;
        private DateTime lastMouseDownTime = DateTime.MinValue;

        private async void Image_MouseDown(object sender, MouseButtonEventArgs e) {
            lastMouseDownSender = sender;
            lastMouseDownTime = DateTime.Now;

            await Task.Delay(500);

            if (lastMouseDownSender == sender) {
                lastMouseDownSender = null;
                var dA = new DoubleAnimation(1, 0.3, new Duration(TimeSpan.FromMilliseconds(100)));
                ((UIElement)sender).BeginAnimation(OpacityProperty, dA);

                forceEraser = true;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = true;
                if (sender == ImageDrawLine || sender == BoardImageDrawLine)
                    drawingShapeMode = 1;
                else if (sender == ImageDrawDashedLine || sender == BoardImageDrawDashedLine)
                    drawingShapeMode = 8;
                else if (sender == ImageDrawDotLine || sender == BoardImageDrawDotLine)
                    drawingShapeMode = 18;
                else if (sender == ImageDrawArrow || sender == BoardImageDrawArrow)
                    drawingShapeMode = 2;
                else if (sender == ImageDrawParallelLine || sender == BoardImageDrawParallelLine) drawingShapeMode = 15;
                isLongPressSelected = true;
                if (isSingleFingerDragMode) BtnFingerDragMode_Click(BtnFingerDragMode, null);
            }
        }

        private void BtnPen_Click(object sender, RoutedEventArgs e) {
            // 禁用高级橡皮擦系统
            DisableAdvancedEraserSystem();

            // 修复：从橡皮擦切换到批注模式时，退出多指书写模式
            // 这解决了从橡皮擦切换为批注时被锁定为多指书写的问题
            ExitMultiTouchModeIfNeeded();

            // 如果当前已是批注模式，再次点击弹出批注子面板
            if (penType == 0 && inkCanvas.EditingMode == InkCanvasEditingMode.Ink && !drawingAttributes.IsHighlighter) {
                return;
            }
            // 否则只切换到批注模式，不弹出子面板
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            penType = 0;
            drawingAttributes.IsHighlighter = false;
            drawingAttributes.StylusTip = StylusTip.Ellipse;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

            // 修复：确保从橡皮擦切换到笔时，多指手势功能能正确恢复
            // 更新lastInkCanvasEditingMode以确保多指手势逻辑正确
            lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;

            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private Task<bool> CheckIsDrawingShapesInMultiTouchMode() {
            if (isInMultiTouchMode) {
                // 不关闭多指书写模式，而是保存状态，暂时禁用多指书写相关的事件处理
                // 不再调用 ToggleSwitchEnableMultiTouchMode.IsOn = false;
                
                // 暂时禁用多指书写事件处理，以避免冲突
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                
                // 记录已暂时禁用多指书写模式，但实际上多指书写开关仍然为打开状态
                lastIsInMultiTouchMode = true;
            }

            return Task.FromResult(true);
        }

        private async void BtnDrawLine_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(1);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            if (isLongPressSelected) {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                ImageDrawLine.BeginAnimation(OpacityProperty, dA);
            }
            DrawShapePromptToPen();
        }

        private async void BtnDrawDashedLine_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(8);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            if (isLongPressSelected) {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                ImageDrawDashedLine.BeginAnimation(OpacityProperty, dA);
            }
            DrawShapePromptToPen();
        }

        private async void BtnDrawDotLine_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(18);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            if (isLongPressSelected) {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                ImageDrawDotLine.BeginAnimation(OpacityProperty, dA);
            }
            DrawShapePromptToPen();
        }

        private async void BtnDrawArrow_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(2);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            if (isLongPressSelected) {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                ImageDrawArrow.BeginAnimation(OpacityProperty, dA);
            }
            DrawShapePromptToPen();
        }

        private async void BtnDrawParallelLine_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(15);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            if (isLongPressSelected) {
                if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();
                var dA = new DoubleAnimation(1, 1, new Duration(TimeSpan.FromMilliseconds(0)));
                ImageDrawParallelLine.BeginAnimation(OpacityProperty, dA);
            }
            DrawShapePromptToPen();
        }

        private async void BtnDrawCoordinate1_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(11);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCoordinate2_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(12);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCoordinate3_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(13);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCoordinate4_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(14);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCoordinate5_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(17);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawRectangle_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(3);
            CancelSingleFingerDragMode();
            isLongPressSelected = true; // 设置为选中状态，避免抬笔后切换回笔模式
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawRectangleCenter_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(19);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawEllipse_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(4);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCircle_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(5);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCenterEllipse_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(16);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCenterEllipseWithFocalPoint_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(23);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawDashedCircle_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(10);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawHyperbola_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(24);
            drawMultiStepShapeCurrentStep = 0;
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawHyperbolaWithFocalPoint_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(25);
            drawMultiStepShapeCurrentStep = 0;
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawParabola1_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(20);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawParabolaWithFocalPoint_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(22);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawParabola2_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(21);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCylinder_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(6);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCone_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(7);
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        private async void BtnDrawCuboid_Click(object sender, MouseButtonEventArgs e) {
            await CheckIsDrawingShapesInMultiTouchMode();
            EnterShapeDrawingMode(9);
            isFirstTouchCuboid = true;
            CuboidFrontRectIniP = new Point();
            CuboidFrontRectEndP = new Point();
            CancelSingleFingerDragMode();
            lastMouseDownSender = null;
            DrawShapePromptToPen();
        }

        #endregion

        private void inkCanvas_TouchMove(object sender, TouchEventArgs e) {
            // 确保套索选择模式下触摸移动时光标保持可见
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select) {
                SetCursorBasedOnEditingMode(inkCanvas);
            }

            // 处理几何绘制模式
            if (drawingShapeMode != 0) {
                if (isWaitUntilNextTouchDown && dec.Count > 1) return;
                if (dec.Count > 1) {
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }
                    return;
                }
                // 在几何绘制模式下，确保处理单点触控的移动事件
                Point touchPoint = e.GetTouchPoint(inkCanvas).Position;
                MouseTouchMove(touchPoint);
                return; // 处理完几何绘制后直接返回，不执行后面的代码
            }

            // 其它模式下，允许橡皮、套索、批注等正常工作，不覆盖EditingMode
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint ||
                inkCanvas.EditingMode == InkCanvasEditingMode.Select ||
                inkCanvas.EditingMode == InkCanvasEditingMode.Ink) {
                // 允许正常橡皮、套索、批注
            }
        }

        private int drawMultiStepShapeCurrentStep; //多笔完成的图形 当前所处在的笔画

        private StrokeCollection drawMultiStepShapeSpecialStrokeCollection = new StrokeCollection(); //多笔完成的图形 当前所处在的笔画

        //double drawMultiStepShapeSpecialParameter1 = 0.0; //多笔完成的图形 特殊参数 通常用于表示a
        //double drawMultiStepShapeSpecialParameter2 = 0.0; //多笔完成的图形 特殊参数 通常用于表示b
        private double drawMultiStepShapeSpecialParameter3; //多笔完成的图形 特殊参数 通常用于表示k

        #region 形状绘制主函数

        private void MouseTouchMove(Point endP) {
            // 禁用原有的FitToCurve，使用新的高级贝塞尔曲线平滑
            if (Settings.Canvas.FitToCurve) drawingAttributes.FitToCurve = false;
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;
            List<Point> pointList;
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var newIniP = iniP;
            switch (drawingShapeMode) {
                case 1:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 8:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateDashedLineStrokeCollection(iniP, endP));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 18:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateDotLineStrokeCollection(iniP, endP));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 2:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    double w = 15, h = 10;
                    var theta = Math.Atan2(iniP.Y - endP.Y, iniP.X - endP.X);
                    var sint = Math.Sin(theta);
                    var cost = Math.Cos(theta);

                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X + (w * cost - h * sint), endP.Y + (w * sint + h * cost)),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X + (w * cost + h * sint), endP.Y - (h * cost - w * sint))
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 15:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var d = GetDistance(iniP, endP);
                    if (d == 0) return;
                    var sinTheta = (iniP.Y - endP.Y) / d;
                    var cosTheta = (endP.X - iniP.X) / d;
                    var tanTheta = Math.Abs(sinTheta / cosTheta);
                    double x = 25;
                    if (Math.Abs(tanTheta) < 1.0 / 12) {
                        sinTheta = 0;
                        cosTheta = 1;
                        endP.Y = iniP.Y;
                    }

                    if (tanTheta < 0.63 && tanTheta > 0.52) //30
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.5;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.866;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (tanTheta < 1.08 && tanTheta > 0.92) //45
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.707;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.707;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (tanTheta < 1.95 && tanTheta > 1.63) //60
                    {
                        sinTheta = sinTheta / Math.Abs(sinTheta) * 0.866;
                        cosTheta = cosTheta / Math.Abs(cosTheta) * 0.5;
                        endP.Y = iniP.Y - d * sinTheta;
                        endP.X = iniP.X + d * cosTheta;
                    }

                    if (Math.Abs(cosTheta / sinTheta) < 1.0 / 12) {
                        endP.X = iniP.X;
                        sinTheta = 1;
                        cosTheta = 0;
                    }

                    strokes.Add(GenerateLineStroke(new Point(iniP.X - 3 * x * sinTheta, iniP.Y - 3 * x * cosTheta),
                        new Point(endP.X - 3 * x * sinTheta, endP.Y - 3 * x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X - x * sinTheta, iniP.Y - x * cosTheta),
                        new Point(endP.X - x * sinTheta, endP.Y - x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X + x * sinTheta, iniP.Y + x * cosTheta),
                        new Point(endP.X + x * sinTheta, endP.Y + x * cosTheta)));
                    strokes.Add(GenerateLineStroke(new Point(iniP.X + 3 * x * sinTheta, iniP.Y + 3 * x * cosTheta),
                        new Point(endP.X + 3 * x * sinTheta, endP.Y + 3 * x * cosTheta)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 11:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateArrowLineStroke(new Point(2 * iniP.X - (endP.X - 20), iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, 2 * iniP.Y - (endP.Y + 20)),
                        new Point(iniP.X, endP.Y)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 12:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.X - endP.X) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X + (iniP.X - endP.X) / Math.Abs(iniP.X - endP.X) * 25, iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, 2 * iniP.Y - (endP.Y + 20)),
                        new Point(iniP.X, endP.Y)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 13:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(new Point(2 * iniP.X - (endP.X - 20), iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X, iniP.Y + (iniP.Y - endP.Y) / Math.Abs(iniP.Y - endP.Y) * 25),
                        new Point(iniP.X, endP.Y)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 14:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X + (iniP.X - endP.X) / Math.Abs(iniP.X - endP.X) * 25, iniP.Y),
                        new Point(endP.X, iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(
                        new Point(iniP.X, iniP.Y + (iniP.Y - endP.Y) / Math.Abs(iniP.Y - endP.Y) * 25),
                        new Point(iniP.X, endP.Y)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 17:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X + Math.Abs(endP.X - iniP.X), iniP.Y)));
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X, iniP.Y - Math.Abs(endP.Y - iniP.Y))));
                    d = (Math.Abs(iniP.X - endP.X) + Math.Abs(iniP.Y - endP.Y)) / 2;
                    strokes.Add(GenerateArrowLineStroke(new Point(iniP.X, iniP.Y),
                        new Point(iniP.X - d / 1.76, iniP.Y + d / 1.76)));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 3:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = new List<Point> {
                        new Point(iniP.X, iniP.Y),
                        new Point(iniP.X, endP.Y),
                        new Point(endP.X, endP.Y),
                        new Point(endP.X, iniP.Y),
                        new Point(iniP.X, iniP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 19:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var a = iniP.X - endP.X;
                    var b = iniP.Y - endP.Y;
                    pointList = new List<Point> {
                        new Point(iniP.X - a, iniP.Y - b),
                        new Point(iniP.X - a, iniP.Y + b),
                        new Point(iniP.X + a, iniP.Y + b),
                        new Point(iniP.X + a, iniP.Y - b),
                        new Point(iniP.X - a, iniP.Y - b)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 4:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    pointList = GenerateEllipseGeometry(iniP, endP);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 5:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var R = GetDistance(iniP, endP);
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - R, iniP.Y - R),
                        new Point(iniP.X + R, iniP.Y + R));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 16:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    var halfA = endP.X - iniP.X;
                    var halfB = endP.Y - iniP.Y;
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - halfA, iniP.Y - halfB),
                        new Point(iniP.X + halfA, iniP.Y + halfB));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    try {
                        inkCanvas.Strokes.Remove(lastTempStroke);
                    }
                    catch { }

                    lastTempStroke = stroke;
                    inkCanvas.Strokes.Add(stroke);
                    break;
                case 23:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    a = Math.Abs(endP.X - iniP.X);
                    b = Math.Abs(endP.Y - iniP.Y);
                    pointList = GenerateEllipseGeometry(new Point(iniP.X - a, iniP.Y - b),
                        new Point(iniP.X + a, iniP.Y + b));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke);
                    var c = Math.Sqrt(Math.Abs(a * a - b * b));
                    StylusPoint stylusPoint;
                    if (a > b) {
                        stylusPoint = new StylusPoint(iniP.X + c, iniP.Y, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point) {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                        stylusPoint = new StylusPoint(iniP.X - c, iniP.Y, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point) {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                    }
                    else if (a < b) {
                        stylusPoint = new StylusPoint(iniP.X, iniP.Y - c, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point) {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                        stylusPoint = new StylusPoint(iniP.X, iniP.Y + c, (float)1.0);
                        point = new StylusPointCollection();
                        point.Add(stylusPoint);
                        stroke = new Stroke(point) {
                            DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                        };
                        strokes.Add(stroke.Clone());
                    }

                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch { }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 10:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    R = GetDistance(iniP, endP);
                    strokes = GenerateDashedLineEllipseStrokeCollection(new Point(iniP.X - R, iniP.Y - R),
                        new Point(iniP.X + R, iniP.Y + R));
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 24:
                case 25:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //双曲线 x^2/a^2 - y^2/b^2 = 1
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    var pointList2 = new List<Point>();
                    var pointList3 = new List<Point>();
                    var pointList4 = new List<Point>();
                    if (drawMultiStepShapeCurrentStep == 0) {
                        //第一笔：画渐近线
                        var k = Math.Abs((endP.Y - iniP.Y) / (endP.X - iniP.X));
                        strokes.Add(
                            GenerateDashedLineStrokeCollection(new Point(2 * iniP.X - endP.X, 2 * iniP.Y - endP.Y),
                                endP));
                        strokes.Add(GenerateDashedLineStrokeCollection(new Point(2 * iniP.X - endP.X, endP.Y),
                            new Point(endP.X, 2 * iniP.Y - endP.Y)));
                        drawMultiStepShapeSpecialParameter3 = k;
                        drawMultiStepShapeSpecialStrokeCollection = strokes;
                    }
                    else {
                        //第二笔：画双曲线
                        var k = drawMultiStepShapeSpecialParameter3;
                        var isHyperbolaFocalPointOnXAxis = Math.Abs((endP.Y - iniP.Y) / (endP.X - iniP.X)) < k;
                        if (isHyperbolaFocalPointOnXAxis) {
                            // 焦点在 x 轴上
                            a = Math.Sqrt(Math.Abs((endP.X - iniP.X) * (endP.X - iniP.X) -
                                                   (endP.Y - iniP.Y) * (endP.Y - iniP.Y) / (k * k)));
                            b = a * k;
                            pointList = new List<Point>();
                            for (var i = a; i <= Math.Abs(endP.X - iniP.X); i += 0.5) {
                                var rY = Math.Sqrt(Math.Abs(k * k * i * i - b * b));
                                pointList.Add(new Point(iniP.X + i, iniP.Y - rY));
                                pointList2.Add(new Point(iniP.X + i, iniP.Y + rY));
                                pointList3.Add(new Point(iniP.X - i, iniP.Y - rY));
                                pointList4.Add(new Point(iniP.X - i, iniP.Y + rY));
                            }
                        }
                        else {
                            // 焦点在 y 轴上
                            a = Math.Sqrt(Math.Abs((endP.Y - iniP.Y) * (endP.Y - iniP.Y) -
                                                   (endP.X - iniP.X) * (endP.X - iniP.X) * (k * k)));
                            b = a / k;
                            pointList = new List<Point>();
                            for (var i = a; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5) {
                                var rX = Math.Sqrt(Math.Abs(i * i / k / k - b * b));
                                pointList.Add(new Point(iniP.X - rX, iniP.Y + i));
                                pointList2.Add(new Point(iniP.X + rX, iniP.Y + i));
                                pointList3.Add(new Point(iniP.X - rX, iniP.Y - i));
                                pointList4.Add(new Point(iniP.X + rX, iniP.Y - i));
                            }
                        }

                        try {
                            point = new StylusPointCollection(pointList);
                            stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList2);
                            stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList3);
                            stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            point = new StylusPointCollection(pointList4);
                            stroke = new Stroke(point)
                                { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                            strokes.Add(stroke.Clone());
                            if (drawingShapeMode == 25) {
                                //画焦点
                                c = Math.Sqrt(a * a + b * b);
                                stylusPoint = isHyperbolaFocalPointOnXAxis
                                    ? new StylusPoint(iniP.X + c, iniP.Y, (float)1.0)
                                    : new StylusPoint(iniP.X, iniP.Y + c, (float)1.0);
                                point = new StylusPointCollection();
                                point.Add(stylusPoint);
                                stroke = new Stroke(point)
                                    { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                                strokes.Add(stroke.Clone());
                                stylusPoint = isHyperbolaFocalPointOnXAxis
                                    ? new StylusPoint(iniP.X - c, iniP.Y, (float)1.0)
                                    : new StylusPoint(iniP.X, iniP.Y - c, (float)1.0);
                                point = new StylusPointCollection();
                                point.Add(stylusPoint);
                                stroke = new Stroke(point)
                                    { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                                strokes.Add(stroke.Clone());
                            }
                        }
                        catch {
                            return;
                        }
                    }

                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 20:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y=ax^2
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    a = (iniP.Y - endP.Y) / ((iniP.X - endP.X) * (iniP.X - endP.X));
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.X - iniP.X); i += 0.5) {
                        pointList.Add(new Point(iniP.X + i, iniP.Y - a * i * i));
                        pointList2.Add(new Point(iniP.X - i, iniP.Y - a * i * i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 21:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y^2=ax
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    a = (iniP.X - endP.X) / ((iniP.Y - endP.Y) * (iniP.Y - endP.Y));
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5) {
                        pointList.Add(new Point(iniP.X - a * i * i, iniP.Y + i));
                        pointList2.Add(new Point(iniP.X - a * i * i, iniP.Y - i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 22:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    //抛物线 y^2=ax, 含焦点
                    if (Math.Abs(iniP.X - endP.X) < 0.01 || Math.Abs(iniP.Y - endP.Y) < 0.01) return;
                    var p = (iniP.Y - endP.Y) * (iniP.Y - endP.Y) / (2 * (iniP.X - endP.X));
                    a = 0.5 / p;
                    pointList = new List<Point>();
                    pointList2 = new List<Point>();
                    for (var i = 0.0; i <= Math.Abs(endP.Y - iniP.Y); i += 0.5) {
                        pointList.Add(new Point(iniP.X - a * i * i, iniP.Y + i));
                        pointList2.Add(new Point(iniP.X - a * i * i, iniP.Y - i));
                    }

                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    point = new StylusPointCollection(pointList2);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    stylusPoint = new StylusPoint(iniP.X - p / 2, iniP.Y, (float)1.0);
                    point = new StylusPointCollection();
                    point.Add(stylusPoint);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 6:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    newIniP = iniP;
                    if (iniP.Y > endP.Y) {
                        newIniP = new Point(iniP.X, endP.Y);
                        endP = new Point(endP.X, iniP.Y);
                    }

                    var topA = Math.Abs(newIniP.X - endP.X);
                    var topB = topA / 2.646;
                    //顶部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, newIniP.Y - topB / 2),
                        new Point(endP.X, newIniP.Y + topB / 2));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //底部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, endP.Y - topB / 2),
                        new Point(endP.X, endP.Y + topB / 2), false);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    strokes.Add(GenerateDashedLineEllipseStrokeCollection(new Point(newIniP.X, endP.Y - topB / 2),
                        new Point(endP.X, endP.Y + topB / 2), true, false));
                    //左侧
                    pointList = new List<Point> {
                        new Point(newIniP.X, newIniP.Y),
                        new Point(newIniP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //右侧
                    pointList = new List<Point> {
                        new Point(endP.X, newIniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 7:
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (iniP.Y > endP.Y) {
                        newIniP = new Point(iniP.X, endP.Y);
                        endP = new Point(endP.X, iniP.Y);
                    }

                    var bottomA = Math.Abs(newIniP.X - endP.X);
                    var bottomB = bottomA / 2.646;
                    //底部椭圆
                    pointList = GenerateEllipseGeometry(new Point(newIniP.X, endP.Y - bottomB / 2),
                        new Point(endP.X, endP.Y + bottomB / 2), false);
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    strokes.Add(GenerateDashedLineEllipseStrokeCollection(new Point(newIniP.X, endP.Y - bottomB / 2),
                        new Point(endP.X, endP.Y + bottomB / 2), true, false));
                    //左侧
                    pointList = new List<Point> {
                        new Point((newIniP.X + endP.X) / 2, newIniP.Y),
                        new Point(newIniP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    //右侧
                    pointList = new List<Point> {
                        new Point((newIniP.X + endP.X) / 2, newIniP.Y),
                        new Point(endP.X, endP.Y)
                    };
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                    try {
                        inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }

                    lastTempStrokeCollection = strokes;
                    inkCanvas.Strokes.Add(strokes);
                    break;
                case 9:
                    // 画长方体
                    _currentCommitType = CommitReason.ShapeDrawing;
                    if (isFirstTouchCuboid) {
                        //分开画线条方便后期单独擦除某一条棱
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, iniP.Y), new Point(iniP.X, endP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, endP.Y), new Point(endP.X, endP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(endP.X, endP.Y), new Point(endP.X, iniP.Y)));
                        strokes.Add(GenerateLineStroke(new Point(iniP.X, iniP.Y), new Point(endP.X, iniP.Y)));
                        try {
                            inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                        }
                        catch {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        lastTempStrokeCollection = strokes;
                        inkCanvas.Strokes.Add(strokes);
                        CuboidFrontRectIniP = iniP;
                        CuboidFrontRectEndP = endP;
                    }
                    else {
                        d = CuboidFrontRectIniP.Y - endP.Y;
                        if (d < 0) d = -d; //就是懒不想做反向的，不要让我去做，想做自己做好之后 Pull Request
                        a = CuboidFrontRectEndP.X - CuboidFrontRectIniP.X; //正面矩形长
                        b = CuboidFrontRectEndP.Y - CuboidFrontRectIniP.Y; //正面矩形宽

                        //横上
                        var newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        var newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //横下 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //斜左上
                        newLineIniP = new Point(CuboidFrontRectIniP.X, CuboidFrontRectIniP.Y);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //斜右上
                        newLineIniP = new Point(CuboidFrontRectEndP.X, CuboidFrontRectIniP.Y);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //斜左下 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X, CuboidFrontRectEndP.Y);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //斜右下
                        newLineIniP = new Point(CuboidFrontRectEndP.X, CuboidFrontRectEndP.Y);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());
                        //竖左 (虚线)
                        newLineIniP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectIniP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectIniP.X + d, CuboidFrontRectEndP.Y - d);
                        strokes.Add(GenerateDashedLineStrokeCollection(newLineIniP, newLineEndP));
                        //竖右
                        newLineIniP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectIniP.Y - d);
                        newLineEndP = new Point(CuboidFrontRectEndP.X + d, CuboidFrontRectEndP.Y - d);
                        pointList = new List<Point> { newLineIniP, newLineEndP };
                        point = new StylusPointCollection(pointList);
                        stroke = new Stroke(point) { DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone() };
                        strokes.Add(stroke.Clone());

                        try {
                            inkCanvas.Strokes.Remove(lastTempStrokeCollection);
                        }
                        catch {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        lastTempStrokeCollection = strokes;
                        inkCanvas.Strokes.Add(strokes);
                    }

                    break;
            }
        }

        #endregion

        private bool isFirstTouchCuboid = true;
        private Point CuboidFrontRectIniP;
        private Point CuboidFrontRectEndP;

        private Stroke lastTempStroke;
        private StrokeCollection lastTempStrokeCollection = new StrokeCollection();

        private bool isWaitUntilNextTouchDown;

        private List<Point> GenerateEllipseGeometry(Point st, Point ed, bool isDrawTop = true,
            bool isDrawBottom = true) {
            var a = 0.5 * (ed.X - st.X);
            var b = 0.5 * (ed.Y - st.Y);
            var pointList = new List<Point>();
            if (isDrawTop && isDrawBottom) {
                for (double r = 0; r <= 2 * Math.PI; r = r + 0.01)
                    pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                        0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
            }
            else {
                if (isDrawBottom)
                    for (double r = 0; r <= Math.PI; r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                if (isDrawTop)
                    for (var r = Math.PI; r <= 2 * Math.PI; r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
            }

            return pointList;
        }

        private StrokeCollection GenerateDashedLineEllipseStrokeCollection(Point st, Point ed, bool isDrawTop = true,
            bool isDrawBottom = true) {
            var a = 0.5 * (ed.X - st.X);
            var b = 0.5 * (ed.Y - st.Y);
            var step = 0.05;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            if (isDrawBottom)
                for (var i = 0.0; i < 1.0; i += step * 1.66) {
                    pointList = new List<Point>();
                    for (var r = Math.PI * i; r <= Math.PI * (i + step); r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                }

            if (isDrawTop)
                for (var i = 1.0; i < 2.0; i += step * 1.66) {
                    pointList = new List<Point>();
                    for (var r = Math.PI * i; r <= Math.PI * (i + step); r = r + 0.01)
                        pointList.Add(new Point(0.5 * (st.X + ed.X) + a * Math.Cos(r),
                            0.5 * (st.Y + ed.Y) + b * Math.Sin(r)));
                    point = new StylusPointCollection(pointList);
                    stroke = new Stroke(point) {
                        DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                    };
                    strokes.Add(stroke.Clone());
                }

            return strokes;
        }

        private Stroke GenerateLineStroke(Point st, Point ed) {
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            pointList = new List<Point> {
                new Point(st.X, st.Y),
                new Point(ed.X, ed.Y)
            };
            point = new StylusPointCollection(pointList);
            stroke = new Stroke(point) {
                DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
            };
            return stroke;
        }

        private Stroke GenerateArrowLineStroke(Point st, Point ed) {
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;

            double w = 20, h = 7;
            var theta = Math.Atan2(st.Y - ed.Y, st.X - ed.X);
            var sint = Math.Sin(theta);
            var cost = Math.Cos(theta);

            pointList = new List<Point> {
                new Point(st.X, st.Y),
                new Point(ed.X, ed.Y),
                new Point(ed.X + (w * cost - h * sint), ed.Y + (w * sint + h * cost)),
                new Point(ed.X, ed.Y),
                new Point(ed.X + (w * cost + h * sint), ed.Y - (h * cost - w * sint))
            };
            point = new StylusPointCollection(pointList);
            stroke = new Stroke(point) {
                DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
            };
            return stroke;
        }


        private StrokeCollection GenerateDashedLineStrokeCollection(Point st, Point ed) {
            double step = 5;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var d = GetDistance(st, ed);
            var sinTheta = (ed.Y - st.Y) / d;
            var cosTheta = (ed.X - st.X) / d;
            for (var i = 0.0; i < d; i += step * 2.76) {
                pointList = new List<Point> {
                    new Point(st.X + i * cosTheta, st.Y + i * sinTheta),
                    new Point(st.X + Math.Min(i + step, d) * cosTheta, st.Y + Math.Min(i + step, d) * sinTheta)
                };
                point = new StylusPointCollection(pointList);
                stroke = new Stroke(point) {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };
                strokes.Add(stroke.Clone());
            }

            return strokes;
        }

        private StrokeCollection GenerateDotLineStrokeCollection(Point st, Point ed) {
            double step = 3;
            var pointList = new List<Point>();
            StylusPointCollection point;
            Stroke stroke;
            var strokes = new StrokeCollection();
            var d = GetDistance(st, ed);
            var sinTheta = (ed.Y - st.Y) / d;
            var cosTheta = (ed.X - st.X) / d;
            for (var i = 0.0; i < d; i += step * 2.76) {
                var stylusPoint = new StylusPoint(st.X + i * cosTheta, st.Y + i * sinTheta, (float)0.8);
                point = new StylusPointCollection();
                point.Add(stylusPoint);
                stroke = new Stroke(point) {
                    DrawingAttributes = inkCanvas.DefaultDrawingAttributes.Clone()
                };
                strokes.Add(stroke.Clone());
            }

            return strokes;
        }

        private bool isMouseDown;

        private void inkCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            inkCanvas.CaptureMouse();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            isMouseDown = true;
            if (NeedUpdateIniP()) iniP = e.GetPosition(inkCanvas);
        }

        private void inkCanvas_MouseMove(object sender, MouseEventArgs e) {
            if (isMouseDown) MouseTouchMove(e.GetPosition(inkCanvas));
        }

        private void inkCanvas_MouseUp(object sender, MouseButtonEventArgs e) {
            inkCanvas.ReleaseMouseCapture();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            if (drawingShapeMode == 5) {
                if (lastTempStroke != null) {
                    var circle = new Circle(new Point(), 0, lastTempStroke);
                    circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                        circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                    circle.Centroid = new Point(
                        (circle.Stroke.StylusPoints[0].X +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                        (circle.Stroke.StylusPoints[0].Y +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                    circles.Add(circle);
                }

                if (lastIsInMultiTouchMode) {
                    // 不再重新启用开关，而是恢复多指书写相关的事件处理
                    // ToggleSwitchEnableMultiTouchMode.IsOn = true;
                    
                    // 恢复多指书写事件处理
                    inkCanvas.StylusDown += MainWindow_StylusDown;
                    inkCanvas.StylusMove += MainWindow_StylusMove;
                    inkCanvas.StylusUp += MainWindow_StylusUp;
                    inkCanvas.TouchDown += MainWindow_TouchDown;
                    
                    lastIsInMultiTouchMode = false;
                }
            }

            // 修改此处逻辑，确保在正确的情况下才切换回笔模式
            if (drawingShapeMode != 9 && drawingShapeMode != 0 && drawingShapeMode != 24 && drawingShapeMode != 25) {
                if (isLongPressSelected) { 
                    // 如果是长按选中的情况，保持图形模式，不做任何切换
                    isWaitUntilNextTouchDown = true; // 保持当前绘图模式直到下一次触摸
                }
                else {
                    BtnPen_Click(null, null); //画完一次还原到笔模式
                    if (lastIsInMultiTouchMode) {
                        // 不再重新启用开关，而是恢复多指书写相关的事件处理
                        // ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        
                        // 恢复多指书写事件处理
                        inkCanvas.StylusDown += MainWindow_StylusDown;
                        inkCanvas.StylusMove += MainWindow_StylusMove;
                        inkCanvas.StylusUp += MainWindow_StylusUp;
                        inkCanvas.TouchDown += MainWindow_TouchDown;
                        
                        lastIsInMultiTouchMode = false;
                    }
                }
            }

            if (drawingShapeMode == 9) {
                if (isFirstTouchCuboid) {
                    if (CuboidStrokeCollection == null) CuboidStrokeCollection = new StrokeCollection();
                    isFirstTouchCuboid = false;
                    var newIniP = new Point(Math.Min(CuboidFrontRectIniP.X, CuboidFrontRectEndP.X),
                        Math.Min(CuboidFrontRectIniP.Y, CuboidFrontRectEndP.Y));
                    var newEndP = new Point(Math.Max(CuboidFrontRectIniP.X, CuboidFrontRectEndP.X),
                        Math.Max(CuboidFrontRectIniP.Y, CuboidFrontRectEndP.Y));
                    CuboidFrontRectIniP = newIniP;
                    CuboidFrontRectEndP = newEndP;
                    try {
                        CuboidStrokeCollection.Add(lastTempStrokeCollection);
                    }
                    catch {
                        Trace.WriteLine("lastTempStrokeCollection failed.");
                    }
                }
                else {
                    BtnPen_Click(null, null); //画完还原到笔模式
                    if (lastIsInMultiTouchMode) {
                        // 不再重新启用开关，而是恢复多指书写相关的事件处理
                        // ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        
                        // 恢复多指书写事件处理
                        inkCanvas.StylusDown += MainWindow_StylusDown;
                        inkCanvas.StylusMove += MainWindow_StylusMove;
                        inkCanvas.StylusUp += MainWindow_StylusUp;
                        inkCanvas.TouchDown += MainWindow_TouchDown;
                        
                        lastIsInMultiTouchMode = false;
                    }

                    if (_currentCommitType == CommitReason.ShapeDrawing) {
                        try {
                            CuboidStrokeCollection.Add(lastTempStrokeCollection);
                        }
                        catch {
                            Trace.WriteLine("lastTempStrokeCollection failed.");
                        }

                        _currentCommitType = CommitReason.UserInput;
                        timeMachine.CommitStrokeUserInputHistory(CuboidStrokeCollection);
                        CuboidStrokeCollection = null;
                    }
                }
            }

            if (drawingShapeMode == 24 || drawingShapeMode == 25) {
                if (drawMultiStepShapeCurrentStep == 0) {
                    drawMultiStepShapeCurrentStep = 1;
                }
                else {
                    drawMultiStepShapeCurrentStep = 0;
                    if (drawMultiStepShapeSpecialStrokeCollection != null) {
                        var opFlag = false;
                        switch (Settings.Canvas.HyperbolaAsymptoteOption) {
                            case OptionalOperation.Yes:
                                opFlag = true;
                                break;
                            case OptionalOperation.No:
                                opFlag = false;
                                break;
                            case OptionalOperation.Ask:
                                opFlag = MessageBox.Show("是否移除渐近线？", "Ink Canvas", MessageBoxButton.YesNo) !=
                                         MessageBoxResult.Yes;
                                break;
                        }

                        ;
                        if (!opFlag) inkCanvas.Strokes.Remove(drawMultiStepShapeSpecialStrokeCollection);
                    }

                    BtnPen_Click(null, null); //画完还原到笔模式
                    if (lastIsInMultiTouchMode) {
                        // 不再重新启用开关，而是恢复多指书写相关的事件处理
                        // ToggleSwitchEnableMultiTouchMode.IsOn = true;
                        
                        // 恢复多指书写事件处理
                        inkCanvas.StylusDown += MainWindow_StylusDown;
                        inkCanvas.StylusMove += MainWindow_StylusMove;
                        inkCanvas.StylusUp += MainWindow_StylusUp;
                        inkCanvas.TouchDown += MainWindow_TouchDown;
                        
                        lastIsInMultiTouchMode = false;
                    }
                }
            }

            isMouseDown = false;
            if (ReplacedStroke != null || AddedStroke != null) {
                timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }

            if (_currentCommitType == CommitReason.ShapeDrawing && drawingShapeMode != 9) {
                _currentCommitType = CommitReason.UserInput;
                StrokeCollection collection = null;
                if (lastTempStrokeCollection != null && lastTempStrokeCollection.Count > 0)
                    collection = lastTempStrokeCollection;
                else if (lastTempStroke != null) collection = new StrokeCollection { lastTempStroke };
                if (collection != null) timeMachine.CommitStrokeUserInputHistory(collection);
            }

            lastTempStroke = null;
            lastTempStrokeCollection = null;

            if (StrokeManipulationHistory?.Count > 0)
            {
                timeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
                foreach (var item in StrokeManipulationHistory)
                {
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
                }
                StrokeManipulationHistory = null;
            }

            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }

            // 应用高级贝塞尔曲线平滑
            if (Settings.Canvas.UseAdvancedBezierSmoothing)
            {
                try
                {
                    // 对临时笔画应用平滑
                    if (lastTempStroke != null && _inkSmoothingManager != null)
                    {
                        var smoothedStroke = _inkSmoothingManager.SmoothStroke(lastTempStroke);
                        if (smoothedStroke != lastTempStroke)
                        {
                            inkCanvas.Strokes.Remove(lastTempStroke);
                            lastTempStroke = smoothedStroke;
                            inkCanvas.Strokes.Add(smoothedStroke);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"形状绘制高级贝塞尔曲线平滑失败: {ex.Message}");
                }
            }
            else if (Settings.Canvas.FitToCurve) 
            {
                drawingAttributes.FitToCurve = true;
            }
        }

        private bool NeedUpdateIniP() {
            if (drawingShapeMode == 24 || drawingShapeMode == 25)
                if (drawMultiStepShapeCurrentStep == 1)
                    return false;
            return true;
        }

        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e) {
            if (e.StylusDevice == null) {
                // 鼠标移动时保持光标可见
                System.Windows.Forms.Cursor.Show();
                
                // 如果用户设置了显示光标，则确保光标显示正确
                if (Settings.Canvas.IsShowCursor && inkCanvas != null) {
                    inkCanvas.ForceCursor = true;
                    inkCanvas.UseCustomCursor = true;
                }
            } else {
                // 只有当用户未设置显示光标时才隐藏
                if (!Settings.Canvas.IsShowCursor) {
                    System.Windows.Forms.Cursor.Hide();
                } else if (inkCanvas != null) {
                    // 如果用户设置了显示光标，则确保光标显示正确
                    inkCanvas.ForceCursor = true;
                    inkCanvas.UseCustomCursor = true;
                    System.Windows.Forms.Cursor.Show();
                }
            }
        }

        // 在MainWindow类中添加：
        private void EnterShapeDrawingMode(int mode) {
            forceEraser = true;
            forcePointEraser = false;
            drawingShapeMode = mode;
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            SetCursorBasedOnEditingMode(inkCanvas);
        }
    }
}