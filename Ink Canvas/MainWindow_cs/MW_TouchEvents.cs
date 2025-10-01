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
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Multi-Touch

        private bool isInMultiTouchMode;
        private List<int> dec = new List<int>();
        private bool isSingleFingerDragMode;
        private Point centerPoint = new Point(0, 0);
        private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        private DateTime lastTouchDownTime = DateTime.MinValue;
        private const double MULTI_TOUCH_DELAY_MS = 100; 
        private bool isMultiTouchTimerActive = false;
        
        /// </summary> 
        /// 保存画布上的非笔画元素（如图片、媒体元素等）
        /// </summary>
        private List<UIElement> PreserveNonStrokeElements()
        {
            var preservedElements = new List<UIElement>();

            // 遍历inkCanvas的所有子元素，创建副本而不是直接引用
            for (int i = inkCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = inkCanvas.Children[i];

                // 保存图片、媒体元素等非笔画相关的UI元素
                if (child is Image || child is MediaElement ||
                    (child is Border border && border.Name != "EraserOverlayCanvas"))
                {
                    // 创建元素的深拷贝，避免直接引用导致的问题
                    var clonedElement = CloneUIElement(child);
                    if (clonedElement != null)
                    {
                        preservedElements.Add(clonedElement);
                    }
                }
            }

            return preservedElements;
        }

        /// <summary>
        /// 克隆UI元素，创建深拷贝
        /// </summary>
        private UIElement CloneUIElement(UIElement originalElement)
        {
            try
            {
                if (originalElement is Image originalImage)
                {
                    var clonedImage = new Image();
                    
                    // 复制图片源
                    if (originalImage.Source is BitmapSource bitmapSource)
                    {
                        clonedImage.Source = bitmapSource;
                    }
                    
                    // 复制属性
                    clonedImage.Width = originalImage.Width;
                    clonedImage.Height = originalImage.Height;
                    clonedImage.Stretch = originalImage.Stretch;
                    clonedImage.StretchDirection = originalImage.StretchDirection;
                    clonedImage.Name = originalImage.Name;
                    clonedImage.IsHitTestVisible = originalImage.IsHitTestVisible;
                    clonedImage.Focusable = originalImage.Focusable;
                    clonedImage.Cursor = originalImage.Cursor;
                    clonedImage.IsManipulationEnabled = originalImage.IsManipulationEnabled;
                    
                    // 复制位置
                    InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(originalImage));
                    InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(originalImage));
                    
                    // 复制变换
                    if (originalImage.RenderTransform != null)
                    {
                        clonedImage.RenderTransform = originalImage.RenderTransform.Clone();
                    }
                    
                    return clonedImage;
                }
                else if (originalElement is MediaElement originalMedia)
                {
                    var clonedMedia = new MediaElement();
                    
                    // 复制媒体属性
                    clonedMedia.Source = originalMedia.Source;
                    clonedMedia.Width = originalMedia.Width;
                    clonedMedia.Height = originalMedia.Height;
                    clonedMedia.Name = originalMedia.Name;
                    clonedMedia.IsHitTestVisible = originalMedia.IsHitTestVisible;
                    clonedMedia.Focusable = originalMedia.Focusable;
                    
                    // 复制位置
                    InkCanvas.SetLeft(clonedMedia, InkCanvas.GetLeft(originalMedia));
                    InkCanvas.SetTop(clonedMedia, InkCanvas.GetTop(originalMedia));
                    
                    // 复制变换
                    if (originalMedia.RenderTransform != null)
                    {
                        clonedMedia.RenderTransform = originalMedia.RenderTransform.Clone();
                    }
                    
                    return clonedMedia;
                }
                else if (originalElement is Border originalBorder)
                {
                    var clonedBorder = new Border();
                    
                    // 复制边框属性
                    clonedBorder.Width = originalBorder.Width;
                    clonedBorder.Height = originalBorder.Height;
                    clonedBorder.Name = originalBorder.Name;
                    clonedBorder.IsHitTestVisible = originalBorder.IsHitTestVisible;
                    clonedBorder.Focusable = originalBorder.Focusable;
                    clonedBorder.Background = originalBorder.Background;
                    clonedBorder.BorderBrush = originalBorder.BorderBrush;
                    clonedBorder.BorderThickness = originalBorder.BorderThickness;
                    clonedBorder.CornerRadius = originalBorder.CornerRadius;
                    
                    // 复制位置
                    InkCanvas.SetLeft(clonedBorder, InkCanvas.GetLeft(originalBorder));
                    InkCanvas.SetTop(clonedBorder, InkCanvas.GetTop(originalBorder));
                    
                    // 复制变换
                    if (originalBorder.RenderTransform != null)
                    {
                        clonedBorder.RenderTransform = originalBorder.RenderTransform.Clone();
                    }
                    
                    return clonedBorder;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"克隆UI元素失败: {ex.Message}", LogHelper.LogType.Error);
            }
            
            return null;
        }

        /// <summary>
        /// 恢复之前保存的非笔画元素到画布
        /// </summary>
        private void RestoreNonStrokeElements(List<UIElement> preservedElements)
        {
            if (preservedElements == null) return;

            foreach (var element in preservedElements)
            {
                try
                {
                    // 由于现在使用的是克隆的元素，不需要检查Parent属性
                    inkCanvas.Children.Add(element);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"恢复非笔画元素失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        private void BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isInMultiTouchMode)
            {
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = false;

            }
            else
            {

                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = true;
            }
        }

        private void MainWindow_TouchDown(object sender, TouchEventArgs e)
        {
            // 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var touchPoint = e.GetTouchPoint(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果触摸发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收触摸事件
            if (floatingBarBounds.Contains(touchPoint.Position))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收触摸事件
                return;
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            if (!isHidingSubPanelsWhenInking)
            {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                
                isTouchDown = true;
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;
                
                // 设置起始点
                if (NeedUpdateIniP()) iniP = e.GetTouchPoint(inkCanvas).Position;
                
                return;
            }

            // 只保留普通橡皮逻辑
            TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
            inkCanvas.EraserShape = new EllipseStylusShape(50, 50);
            if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e)
        {
            // 检查手写笔点击是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var stylusPoint = e.GetPosition(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果手写笔点击发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收手写笔事件
            if (floatingBarBounds.Contains(stylusPoint))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收手写笔事件
                return;
            }


            // 根据是否为笔尾自动切换橡皮擦/画笔模式
            if (e.StylusDevice.Inverted)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            else
            {
                if (drawingShapeMode != 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    
                    isTouchDown = true;
                    ViewboxFloatingBar.IsHitTestVisible = false;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = false;
                    
                    // 设置起始点
                    if (NeedUpdateIniP()) iniP = e.GetPosition(inkCanvas);
                    
                    return;
                }
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
                else
                {
                    LogHelper.WriteLogToFile("保持当前线擦模式");
                }
            }
            SetCursorBasedOnEditingMode(inkCanvas);

            inkCanvas.CaptureStylus();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            // 确保手写笔模式下显示光标
            if (Settings.Canvas.IsShowCursor)
            {
                inkCanvas.ForceCursor = true;
                inkCanvas.UseCustomCursor = true;

                // 根据当前编辑模式设置不同的光标
                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    inkCanvas.Cursor = Cursors.Cross;
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                {
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

        private async void MainWindow_StylusUp(object sender, StylusEventArgs e)
        {
            if (drawingShapeMode != 0)
            {
                // 重置触摸状态
                isTouchDown = false;
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                
                // 对于双曲线等需要多步绘制的图形，手写笔抬起时应该进入下一步
                if (drawingShapeMode == 24 || drawingShapeMode == 25)
                {
                    if (drawMultiStepShapeCurrentStep == 0)
                    {
                        // 第一笔完成，进入第二笔
                        drawMultiStepShapeCurrentStep = 1;
                    }
                    else
                    {
                        // 第二笔完成，完成绘制
                        var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        {
                            RoutedEvent = MouseLeftButtonUpEvent,
                            Source = inkCanvas
                        };
                        inkCanvas_MouseUp(inkCanvas, mouseArgs);
                    }
                }
                else
                {
                    // 其他单步绘制的图形，手写笔抬起时完成绘制
                    var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonUpEvent,
                        Source = inkCanvas
                    };
                    inkCanvas_MouseUp(inkCanvas, mouseArgs);
                }
                
                return;
            }

            try
            {
                var stroke = GetStrokeVisual(e.StylusDevice.Id).Stroke;

                inkCanvas.Strokes.Add(stroke);
                await Task.Delay(5); 
                inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));

                inkCanvas_StrokeCollected(inkCanvas,
                new InkCanvasStrokeCollectedEventArgs(stroke));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MainWindow_StylusUp 出错: {ex}", LogHelper.LogType.Error);
                Label.Content = ex.ToString();
            }

            try
            {
                StrokeVisualList.Remove(e.StylusDevice.Id);
                VisualCanvasList.Remove(e.StylusDevice.Id);
                TouchDownPointsList.Remove(e.StylusDevice.Id);
                if (StrokeVisualList.Count == 0 || VisualCanvasList.Count == 0 || TouchDownPointsList.Count == 0)
                {
                    // 只清除手写笔预览相关的Canvas，不清除所有子元素
                    foreach (var canvas in VisualCanvasList.Values.ToList())
                    {
                        if (inkCanvas.Children.Contains(canvas))
                        {
                            inkCanvas.Children.Remove(canvas);
                        }
                    }
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

        private void MainWindow_StylusMove(object sender, StylusEventArgs e)
        {
            try
            {
                if (drawingShapeMode != 0)
                {
                    if (isTouchDown)
                    {
                        Point stylusPoint = e.GetPosition(inkCanvas);
                        MouseTouchMove(stylusPoint);
                    }
                    return;
                }

                if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None) return;
                try
                {
                    if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                }
                catch { }

                // 确保手写笔移动时光标保持可见
                if (Settings.Canvas.IsShowCursor)
                {
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

        private StrokeVisual GetStrokeVisual(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual)) return visual;

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas(strokeVisual);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        private VisualCanvas GetVisualCanvas(int id)
        {
            return VisualCanvasList.TryGetValue(id, out var visualCanvas) ? visualCanvas : null;
        }

        private InkCanvasEditingMode GetTouchDownPointsList(int id)
        {
            return TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode) ? inkCanvasEditingMode : inkCanvas.EditingMode;
        }

        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
            new Dictionary<int, InkCanvasEditingMode>();

        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion




        private Point iniP = new Point(0, 0);

        private void Main_Grid_TouchDown(object sender, TouchEventArgs e)
        {
            // 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var touchPoint = e.GetTouchPoint(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果触摸发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收触摸事件
            if (floatingBarBounds.Contains(touchPoint.Position))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收触摸事件
                return;
            }

            SetCursorBasedOnEditingMode(inkCanvas);
            inkCanvas.CaptureTouch(e.TouchDevice);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                // 橡皮状态下只return，保证橡皮状态可保持
                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                // 套索选状态下不直接return，允许触摸事件继续处理
                dec.Add(e.TouchDevice.Id);
                return;
            }
            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                
                // 设置触摸状态，类似鼠标事件处理
                isTouchDown = true;
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;
                
                // 设置起始点
                if (NeedUpdateIniP()) iniP = e.GetTouchPoint(inkCanvas).Position;
                
                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
            {
                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
            {
                return;
            }
            if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        // 手掌擦相关变量
        private bool isPalmEraserActive;
        private InkCanvasEditingMode palmEraserLastEditingMode = InkCanvasEditingMode.Ink;
        private bool palmEraserLastIsHighlighter;
        private bool palmEraserWasEnabledBeforeMultiTouch;

        public double GetTouchBoundWidth(TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            if (!Settings.Advanced.IsQuadIR) return args.Width;
            else return Math.Sqrt(args.Width * args.Height); // 四边红外
        }

        private void inkCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            // 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var touchPoint = e.GetTouchPoint(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果触摸发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收触摸事件
            if (floatingBarBounds.Contains(touchPoint.Position))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收触摸事件
                return;
            }

            // 橡皮状态下不做任何切换，直接return，保证橡皮可持续
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
            {
                return;
            }
            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                SetCursorBasedOnEditingMode(inkCanvas);
                inkCanvas.CaptureTouch(e.TouchDevice);
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;
                
                isTouchDown = true;

                if (dec.Count == 0)
                {
                    var inkTouchPoint = e.GetTouchPoint(inkCanvas);
                    // 对于双曲线绘制，第一笔时记录起点，第二笔时不更新起点
                    if (drawingShapeMode == 24 || drawingShapeMode == 25)
                    {
                        // 双曲线绘制：第一笔记录起点，第二笔保持第一笔的起点
                        if (drawMultiStepShapeCurrentStep == 0)
                        {
                            iniP = inkTouchPoint.Position;
                        }
                        // 第二笔时不更新iniP，保持第一笔的起点
                    }
                    else
                    {
                        // 其他图形正常记录起点
                        iniP = inkTouchPoint.Position;
                    }
                    lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
                }
                dec.Add(e.TouchDevice.Id);
                return;
            }

            // 非几何绘制模式下的正常触摸处理
            SetCursorBasedOnEditingMode(inkCanvas);
            inkCanvas.CaptureTouch(e.TouchDevice);
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;
            lastTouchDownTime = DateTime.Now;
            dec.Add(e.TouchDevice.Id);

            // Palm Eraser 逻辑 
            if (Settings.Canvas.EnablePalmEraser && !isPalmEraserActive)
            {
                touchPoint = e.GetTouchPoint(inkCanvas);
                double boundWidth = GetTouchBoundWidth(e);


                if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen) 
                    && (boundWidth > BoundsWidth))
                {
                    // 根据敏感度调整阈值倍数
                    double thresholdMultiplier;
                    switch (Settings.Canvas.PalmEraserSensitivity)
                    {
                        case 0: // 低敏感度
                            thresholdMultiplier = 3.0;
                            break;
                        case 1: // 中敏感度
                            thresholdMultiplier = 2.5;
                            break;
                        case 2: // 高敏感度
                        default:
                            thresholdMultiplier = 2.0;
                            break;
                    }

                    double EraserThresholdValue = Settings.Startup.IsEnableNibMode ? 
                        Settings.Advanced.NibModeBoundsWidthThresholdValue : 
                        Settings.Advanced.FingerModeBoundsWidthThresholdValue;
                    
                    if (boundWidth > BoundsWidth * EraserThresholdValue * thresholdMultiplier)
                    {
                        // 记录当前编辑模式和高光状态
                        palmEraserLastEditingMode = inkCanvas.EditingMode;
                        palmEraserLastIsHighlighter = drawingAttributes.IsHighlighter;

                        // 动态调整橡皮大小
                        boundWidth *= (Settings.Startup.IsEnableNibMode ? 
                            Settings.Advanced.NibModeBoundsWidthEraserSize : 
                            Settings.Advanced.FingerModeBoundsWidthEraserSize);
                        
                        if (Settings.Advanced.IsSpecialScreen) 
                            boundWidth *= Settings.Advanced.TouchMultiplier;
                        
                        inkCanvas.EraserShape = new EllipseStylusShape(boundWidth, boundWidth);
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                        isPalmEraserActive = true;
                        
                        // 启用橡皮擦覆盖层显示手掌擦样式
                        EnableEraserOverlay();
                        // 更新橡皮擦大小以匹配手掌擦面积
                        eraserWidth = boundWidth;
                        UpdateEraserStyle();
                        // 显示初始橡皮擦反馈位置
                        touchPoint = e.GetTouchPoint(inkCanvas);
                        EraserOverlay_PointerDown(sender);
                        EraserOverlay_PointerMove(sender, touchPoint.Position);
                        if (Settings.Canvas.IsShowCursor)
                        {
                            inkCanvas.ForceCursor = false;
                            inkCanvas.UseCustomCursor = false;
                        }
                    }
                }
            }

            // 设备1个的时候，记录中心点
            if (dec.Count == 1)
            {
                touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;

                if (drawingShapeMode != 0)
                {
                    // 对于双曲线绘制，第一笔时记录起点，第二笔时不更新起点
                    if (drawingShapeMode == 24 || drawingShapeMode == 25)
                    {
                        // 双曲线绘制：第一笔记录起点，第二笔保持第一笔的起点
                        if (drawMultiStepShapeCurrentStep == 0)
                        {
                            iniP = touchPoint.Position;
                        }
                        // 第二笔时不更新iniP，保持第一笔的起点
                    }
                    else
                    {
                        // 其他图形正常记录起点
                        iniP = touchPoint.Position;
                    }
                }

                // 记录第一根手指点击时的 StrokeCollection
                lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            }
            //设备两个及两个以上，将画笔功能关闭
            if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
            {
                if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None ||
                    inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;
                var timeSinceLastTouch = (DateTime.Now - lastTouchDownTime).TotalMilliseconds;
                if (timeSinceLastTouch < MULTI_TOUCH_DELAY_MS && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    if (!isMultiTouchTimerActive)
                    {
                        isMultiTouchTimerActive = true;
                        var remainingTime = MULTI_TOUCH_DELAY_MS - timeSinceLastTouch;
                        System.Threading.Tasks.Task.Delay((int)remainingTime).ContinueWith(_ => 
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (dec.Count > 1 && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                                {
                                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                                }
                                isMultiTouchTimerActive = false;
                            });
                        });
                    }
                    return;
                }
                    
                lastInkCanvasEditingMode = inkCanvas.EditingMode;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && drawingShapeMode == 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
            }
        }

        private void inkCanvas_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            
            // 如果手掌擦激活，更新橡皮擦反馈位置
            if (isPalmEraserActive)
            {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                EraserOverlay_PointerMove(sender, touchPoint.Position);
            }
        }

        private void inkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            // 橡皮状态下不做任何切换，直接return，保证橡皮可持续
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint && !isPalmEraserActive)
            {
                return;
            }
            inkCanvas.ReleaseAllTouchCaptures();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            // Palm Eraser 逻辑
            dec.Remove(e.TouchDevice.Id);
            
            // 重置多触控点定时器状态
            if (dec.Count <= 1)
            {
                isMultiTouchTimerActive = false;
            }
            

            // 当手掌擦激活且所有触摸点都抬起时，恢复原编辑模式
            if (isPalmEraserActive && dec.Count == 0)
            {
                LogHelper.WriteLogToFile($"Palm eraser recovery triggered - Touch points remaining: {dec.Count}");

                // 恢复高光状态
                drawingAttributes.IsHighlighter = palmEraserLastIsHighlighter;

                // 恢复编辑模式
                try
                {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                    {
                        // 根据之前的状态恢复
                        switch (palmEraserLastEditingMode)
                        {
                            case InkCanvasEditingMode.Ink:
                                PenIcon_Click(null, null);
                                break;
                            case InkCanvasEditingMode.Select:
                                SymbolIconSelect_MouseUp(null, null);
                                break;
                            default:
                                inkCanvas.EditingMode = palmEraserLastEditingMode;
                                break;
                        }

                        LogHelper.WriteLogToFile($"Palm eraser recovered to mode: {palmEraserLastEditingMode}");
                    }
                }
                catch (Exception ex)
                {
                    // 如果恢复失败，强制切换到批注模式
                    LogHelper.WriteLogToFile($"Palm eraser recovery failed: {ex.Message}, forcing to Ink mode", LogHelper.LogType.Error);
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }

                // 重置手掌擦状态
                isPalmEraserActive = false;
                
                // 禁用橡皮擦覆盖层
                DisableEraserOverlay();
                if (Settings.Canvas.IsShowCursor)
                {
                    inkCanvas.ForceCursor = true;
                    inkCanvas.UseCustomCursor = true;
                }

                LogHelper.WriteLogToFile("Palm eraser state reset completed");
            }

            if (drawingShapeMode != 0)
            {
                isTouchDown = false;
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                
                // 对于双曲线等需要多步绘制的图形，触摸抬手时应该进入下一步
                if (drawingShapeMode == 24 || drawingShapeMode == 25)
                {
                    // 双曲线绘制：触摸抬手时进入下一步，但不自动触发鼠标抬起事件
                    // 让用户继续绘制第二笔
                    if (drawMultiStepShapeCurrentStep == 0)
                    {
                        // 第一笔完成，进入第二笔
                        drawMultiStepShapeCurrentStep = 1;
                    }
                    else
                    {
                        // 第二笔完成，完成绘制
                        var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        {
                            RoutedEvent = MouseLeftButtonUpEvent,
                            Source = inkCanvas
                        };
                        inkCanvas_MouseUp(inkCanvas, mouseArgs);
                    }
                }
                else
                {
                    // 其他单步绘制的图形，触摸抬手时完成绘制
                    var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonUpEvent,
                        Source = inkCanvas
                    };
                    inkCanvas_MouseUp(inkCanvas, mouseArgs);
                }
            }

            // 手势完成后切回之前的状态
            if (drawingShapeMode == 0)
            {
                if (dec.Count > 1)
                {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    {
                        if (lastInkCanvasEditingMode != InkCanvasEditingMode.EraseByPoint)
                        {
                            inkCanvas.EditingMode = lastInkCanvasEditingMode;
                        }
                    }
                }
                else if (dec.Count == 0)
                {
                    // 当所有触摸点都抬起时，确保正确恢复编辑模式
                    // 这对于从橡皮擦切换到笔后恢复多指手势功能很重要
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.None &&
                        lastInkCanvasEditingMode != InkCanvasEditingMode.None &&
                        lastInkCanvasEditingMode != InkCanvasEditingMode.EraseByPoint)
                    {
                        inkCanvas.EditingMode = lastInkCanvasEditingMode;
                    }

                if (isPalmEraserActive)
                    {
                        LogHelper.WriteLogToFile("Palm eraser force recovery - all touch points cleared");

                        // 恢复高光状态
                        drawingAttributes.IsHighlighter = palmEraserLastIsHighlighter;

                        // 恢复编辑模式
                        try
                        {
                            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                            {
                                switch (palmEraserLastEditingMode)
                                {
                                    case InkCanvasEditingMode.Ink:
                                        PenIcon_Click(null, null);
                                        break;
                                    case InkCanvasEditingMode.Select:
                                        SymbolIconSelect_MouseUp(null, null);
                                        break;
                                    default:
                                        inkCanvas.EditingMode = palmEraserLastEditingMode;
                                        break;
                                }
                                LogHelper.WriteLogToFile($"Palm eraser force recovered to mode: {palmEraserLastEditingMode}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"Palm eraser force recovery failed: {ex.Message}, forcing to Ink mode", LogHelper.LogType.Error);
                            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                        }

                        // 如果手掌擦还在激活状态但触摸点已清空，强制重置状态
                        isPalmEraserActive = false;
                        inkCanvas.IsHitTestVisible = true;
                        inkCanvas.IsManipulationEnabled = true;

                        ViewboxFloatingBar.IsHitTestVisible = true;
                        BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                        DisableEraserOverlay();
                        if (Settings.Canvas.IsShowCursor)
                        {
                            inkCanvas.ForceCursor = true;
                            inkCanvas.UseCustomCursor = true;
                        }

                        LogHelper.WriteLogToFile("Palm eraser force recovery completed");
                    }
                }
            }
            inkCanvas.Opacity = 1;

            if (dec.Count == 0)
                if (lastTouchDownStrokeCollection.Count() != inkCanvas.Strokes.Count() &&
                    !(drawingShapeMode == 9 && !isFirstTouchCuboid))
                {
                    var whiteboardIndex = CurrentWhiteboardIndex;
                    if (currentMode == 0) whiteboardIndex = 0;
                    strokeCollections[whiteboardIndex] = lastTouchDownStrokeCollection;
                }
        }

        private void inkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.Mode = ManipulationModes.All;
        }

        private void inkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e) { }

        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (e.Manipulators.Count() != 0) return;
            if (drawingShapeMode == 0
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
            }
        }

        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            // 手掌擦时禁止移动/缩放
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                return;
            // 三指及以上禁止缩放
            bool disableScale = dec.Count >= 3;

            if (isInMultiTouchMode) return;
                
            if (dec.Count == 0 && (isSingleFingerDragMode || isInMultiTouchMode))
            {
                ResetTouchStates();
                return;
            }

            // 如果是单指拖动选中的墨迹，允许处理
            if (dec.Count == 1 && inkCanvas.GetSelectedStrokes().Count > 0)
            {
                var md = e.DeltaManipulation;
                var trans = md.Translation; // 获得位移矢量

                if (trans.X != 0 || trans.Y != 0)
                {
                    var m = new Matrix();
                    m.Translate(trans.X, trans.Y); // 移动

                    var strokes = inkCanvas.GetSelectedStrokes();
                    foreach (var stroke in strokes)
                    {
                        stroke.Transform(m, false);
                    }

                    // 更新选择框位置
                    updateBorderStrokeSelectionControlLocation();
                }
                return;
            }

            if (!Settings.Gesture.IsEnableTwoFingerGesture) return;
            if ((dec.Count >= 2 && (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode ||
                                    StackPanelPPTControls.Visibility != Visibility.Visible ||
                                    StackPanelPPTButtons.Visibility == Visibility.Collapsed)) ||
                isSingleFingerDragMode)
            {
                var md = e.DeltaManipulation;
                var trans = md.Translation; // 获得位移矢量

                var m = new Matrix();

                if (Settings.Gesture.IsEnableTwoFingerTranslate)
                    m.Translate(trans.X, trans.Y); // 移动

                if (Settings.Gesture.IsEnableTwoFingerGestureTranslateOrRotation)
                {
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
                if (strokes.Count != 0)
                {
                    foreach (var stroke in strokes)
                    {
                        stroke.Transform(m, false);

                        foreach (var circle in circles)
                            if (stroke == circle.Stroke)
                            {
                                circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                                    circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                                circle.Centroid = new Point(
                                    (circle.Stroke.StylusPoints[0].X +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                    (circle.Stroke.StylusPoints[0].Y +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                                break;
                            }

                    }
                }
                else
                {
                    if (Settings.Gesture.IsEnableTwoFingerZoom)
                    {
                        foreach (var stroke in inkCanvas.Strokes)
                        {
                            stroke.Transform(m, false);
                            try
                            {
                                stroke.DrawingAttributes.Width *= md.Scale.X;
                                stroke.DrawingAttributes.Height *= md.Scale.Y;
                            }
                            catch { }
                        }

                        // 同时变换画布上的图片元素
                        TransformCanvasImages(m);
                    }
                    else
                    {
                        foreach (var stroke in inkCanvas.Strokes) stroke.Transform(m, false);
                        
                        // 同时变换画布上的图片元素
                        TransformCanvasImages(m);
                    }

                    foreach (var circle in circles)
                    {
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

        /// <summary>
        /// 变换画布上的图片元素，使其与墨迹同步移动
        /// </summary>
        private void TransformCanvasImages(Matrix matrix)
        {
            try
            {
                // 遍历inkCanvas的所有子元素，找到图片元素
                for (int i = inkCanvas.Children.Count - 1; i >= 0; i--)
                {
                    var child = inkCanvas.Children[i];
                    
                    if (child is Image image)
                    {
                        // 应用矩阵变换到图片
                        ApplyMatrixTransformToImage(image, matrix);
                    }
                    else if (child is MediaElement mediaElement)
                    {
                        // 对媒体元素也应用变换
                        ApplyMatrixTransformToMediaElement(mediaElement, matrix);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"变换画布图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 对图片应用矩阵变换
        /// </summary>
        private void ApplyMatrixTransformToImage(Image image, Matrix matrix)
        {
            try
            {
                // 获取图片的RenderTransform，如果不存在则创建新的TransformGroup
                TransformGroup transformGroup = image.RenderTransform as TransformGroup;
                if (transformGroup == null)
                {
                    transformGroup = new TransformGroup();
                    image.RenderTransform = transformGroup;
                }

                // 创建新的MatrixTransform并添加到变换组
                var matrixTransform = new MatrixTransform(matrix);
                transformGroup.Children.Add(matrixTransform);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用图片变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 对媒体元素应用矩阵变换
        /// </summary>
        private void ApplyMatrixTransformToMediaElement(MediaElement mediaElement, Matrix matrix)
        {
            try
            {
                // 获取媒体元素的RenderTransform，如果不存在则创建新的TransformGroup
                TransformGroup transformGroup = mediaElement.RenderTransform as TransformGroup;
                if (transformGroup == null)
                {
                    transformGroup = new TransformGroup();
                    mediaElement.RenderTransform = transformGroup;
                }

                // 创建新的MatrixTransform并添加到变换组
                var matrixTransform = new MatrixTransform(matrix);
                transformGroup.Children.Add(matrixTransform);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用媒体元素变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 退出多指书写模式，恢复InkCanvas的TouchDown事件绑定
        private void ExitMultiTouchModeIfNeeded()
        {
            if (isInMultiTouchMode)
            {
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && drawingShapeMode == 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = false;
                // 关闭多指书写时，恢复手掌擦开关
                if (palmEraserWasEnabledBeforeMultiTouch)
                {
                    Settings.Canvas.EnablePalmEraser = true;
                    if (ToggleSwitchEnablePalmEraser != null)
                        ToggleSwitchEnablePalmEraser.IsOn = true;
                }
            }
        }

        // 进入多指书写模式，绑定Main_Grid_TouchDown
        private void EnterMultiTouchModeIfNeeded()
        {
            if (!isInMultiTouchMode)
            {
                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && drawingShapeMode == 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = true;
                // 启用多指书写时，自动禁用手掌擦
                palmEraserWasEnabledBeforeMultiTouch = Settings.Canvas.EnablePalmEraser;
                Settings.Canvas.EnablePalmEraser = false;
                if (ToggleSwitchEnablePalmEraser != null)
                    ToggleSwitchEnablePalmEraser.IsOn = false;
            }
        }


    }
}
