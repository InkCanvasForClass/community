using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Multi-Touch

        /// <summary>
        /// 是否处于多点触控模式
        /// </summary>
        private bool isInMultiTouchMode;
        /// <summary>
        /// 存储触摸设备ID的列表
        /// </summary>
        private List<int> dec = new List<int>();
        /// <summary>
        /// 是否处于单指拖动模式
        /// </summary>
        private bool isSingleFingerDragMode;
        /// <summary>
        /// 中心点坐标
        /// </summary>
        private Point centerPoint = new Point(0, 0);
        /// <summary>
        /// 上次的InkCanvas编辑模式
        /// </summary>
        private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        /// <summary>
        /// 上次触摸按下的时间
        /// </summary>
        private DateTime lastTouchDownTime = DateTime.MinValue;
        /// <summary>
        /// 多点触控延迟时间（毫秒）
        /// </summary>
        private const double MULTI_TOUCH_DELAY_MS = 100;

        /// <summary>
        /// 保存画布上的非笔画元素（如图片、媒体元素等）
        /// </summary>
        /// <returns>返回保存的非笔画元素列表</returns>
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
                    var clonedMedia = new MediaElement
                    {
                        Source = originalMedia.Source,
                        Width = originalMedia.Width,
                        Height = originalMedia.Height,
                        Name = originalMedia.Name,
                        IsHitTestVisible = originalMedia.IsHitTestVisible,
                        Focusable = originalMedia.Focusable,
                        RenderTransform = originalMedia.RenderTransform?.Clone()
                    };

                    // 复制位置
                    InkCanvas.SetLeft(clonedMedia, InkCanvas.GetLeft(originalMedia));
                    InkCanvas.SetTop(clonedMedia, InkCanvas.GetTop(originalMedia));

                    return clonedMedia;
                }
                else if (originalElement is Border originalBorder)
                {
                    var clonedBorder = new Border
                    {
                        Width = originalBorder.Width,
                        Height = originalBorder.Height,
                        Name = originalBorder.Name,
                        IsHitTestVisible = originalBorder.IsHitTestVisible,
                        Focusable = originalBorder.Focusable,
                        Background = originalBorder.Background,
                        BorderBrush = originalBorder.BorderBrush,
                        BorderThickness = originalBorder.BorderThickness,
                        CornerRadius = originalBorder.CornerRadius,
                        RenderTransform = originalBorder.RenderTransform?.Clone()
                    };

                    // 复制位置
                    InkCanvas.SetLeft(clonedBorder, InkCanvas.GetLeft(originalBorder));
                    InkCanvas.SetTop(clonedBorder, InkCanvas.GetTop(originalBorder));

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

        /// <summary>
        /// 多点触控模式切换按钮的鼠标抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 切换多点触控模式和单点触控模式，包括以下步骤：
        /// 1. 如果当前处于多点触控模式，则切换到单点触控模式
        ///    - 移除手写笔和触摸事件处理程序
        ///    - 添加触摸事件处理程序
        ///    - 设置InkCanvas编辑模式为Ink（如果当前不是橡皮擦模式）
        ///    - 保存并恢复非笔画元素
        ///    - 设置isInMultiTouchMode为false
        /// 2. 如果当前处于单点触控模式，则切换到多点触控模式
        ///    - 添加手写笔事件处理程序
        ///    - 添加触摸事件处理程序
        ///    - 移除触摸事件处理程序
        ///    - 设置InkCanvas编辑模式为None（如果当前不是橡皮擦模式）
        ///    - 保存并恢复非笔画元素
        ///    - 设置isInMultiTouchMode为true
        /// </remarks>
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

        /// <summary>
        /// 主窗口的触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理触摸按下事件，包括以下逻辑：
        /// 1. 如果当前处于橡皮擦模式或选择模式，则直接返回
        /// 2. 如果当前没有隐藏子面板，则隐藏子面板
        /// 3. 如果当前处于图形绘制模式，则：
        ///    - 设置InkCanvas编辑模式为None
        ///    - 设置触摸状态为按下
        ///    - 禁用浮动栏和黑板UI网格的命中测试
        ///    - 设置起始点坐标
        ///    - 直接返回
        /// 4. 否则，设置触摸按下点的编辑模式为None
        /// 5. 如果当前不是橡皮擦模式，则设置InkCanvas编辑模式为None
        /// </remarks>
        private void MainWindow_TouchDown(object sender, TouchEventArgs e)
        {

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
            if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        /// <summary>
        /// 主窗口的手写笔按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔按下事件参数</param>
        /// <remarks>
        /// 处理手写笔按下事件，包括以下逻辑：
        /// 1. 检查手写笔点击是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮并返回
        /// 2. 根据手写笔是否倒置自动切换橡皮擦/画笔模式：
        ///    - 如果手写笔倒置，设置编辑模式为EraseByPoint
        ///    - 如果手写笔正常：
        ///       - 如果当前处于图形绘制模式，设置编辑模式为None，设置触摸状态为按下，禁用浮动栏和黑板UI网格的命中测试，设置起始点坐标并返回
        ///       - 如果当前不是线擦模式，设置编辑模式为Ink
        ///       - 否则，保持当前线擦模式
        /// 3. 捕获手写笔输入
        /// 4. 禁用浮动栏和黑板UI网格的命中测试
        /// 5. 根据编辑模式设置光标
        /// 6. 如果当前处于橡皮擦模式或选择模式，则直接返回
        /// 7. 设置触摸按下点的编辑模式为None
        /// </remarks>
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
            inkCanvas.CaptureStylus();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            SetCursorBasedOnEditingMode(inkCanvas);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
        }

        /// <summary>
        /// 主窗口的手写笔抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔事件参数</param>
        /// <remarks>
        /// 处理手写笔抬起事件，包括以下逻辑：
        /// 1. 如果当前处于图形绘制模式：
        ///    - 重置触摸状态
        ///    - 启用浮动栏和黑板UI网格的命中测试
        ///    - 对于双曲线等需要多步绘制的图形，根据当前步骤决定是进入下一步还是完成绘制
        ///    - 对于其他单步绘制的图形，直接完成绘制
        ///    - 直接返回
        /// 2. 否则，尝试获取并处理笔画：
        ///    - 获取笔画视觉对象的笔画
        ///    - 如果笔画不为空，将其添加到InkCanvas，移除视觉画布，并触发笔画收集事件
        ///    - 如果笔画为空，仅移除视觉画布
        /// 3. 清理相关资源：
        ///    - 从StrokeVisualList、VisualCanvasList和TouchDownPointsList中移除当前手写笔设备ID
        ///    - 如果列表为空，清除所有手写笔预览相关的Canvas并清空列表
        /// 4. 释放手写笔捕获
        /// 5. 启用浮动栏和黑板UI网格的命中测试
        /// 6. 根据编辑模式设置光标
        /// </remarks>
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

                if (stroke != null)
                {
                    inkCanvas.Strokes.Add(stroke);
                    await Task.Delay(5);
                    inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));

                    inkCanvas_StrokeCollected(inkCanvas,
                    new InkCanvasStrokeCollectedEventArgs(stroke));
                }
                else
                {
                    await Task.Delay(5);
                    inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));
                }
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            inkCanvas.ReleaseStylusCapture();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        /// <summary>
        /// 主窗口的手写笔移动事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔事件参数</param>
        /// <remarks>
        /// 处理手写笔移动事件，包括以下逻辑：
        /// 1. 如果当前处于图形绘制模式且触摸状态为按下：
        ///    - 获取手写笔在InkCanvas上的位置
        ///    - 调用MouseTouchMove方法处理移动
        ///    - 直接返回
        /// 2. 如果触摸按下点的编辑模式不是None，则直接返回
        /// 3. 尝试检查手写笔按钮状态，如果第二个按钮被按下，则直接返回
        /// 4. 否则，获取笔画视觉对象，添加手写笔点，并重新绘制
        /// 5. 捕获并忽略所有异常
        /// </remarks>
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
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }


                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                var stylusPointCollection = e.GetStylusPoints(this);
                foreach (var stylusPoint in stylusPointCollection)
                    strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                strokeVisual.Redraw();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 获取笔画视觉对象方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回笔画视觉对象</returns>
        /// <remarks>
        /// 根据设备ID获取笔画视觉对象，如果不存在则创建新的：
        /// 1. 尝试从StrokeVisualList中获取笔画视觉对象
        /// 2. 如果不存在，创建新的StrokeVisual实例，使用InkCanvas的默认绘制属性的克隆
        /// 3. 将新的笔画视觉对象添加到StrokeVisualList
        /// 4. 创建新的VisualCanvas实例，将其设置为笔画视觉对象的视觉画布
        /// 5. 将新的视觉画布添加到VisualCanvasList和InkCanvas的子元素中
        /// 6. 返回笔画视觉对象
        /// </remarks>
        private StrokeVisual GetStrokeVisual(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual)) return visual;

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas();
            strokeVisual.SetVisualCanvas(visualCanvas);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        /// <summary>
        /// 获取视觉画布方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回视觉画布对象，如果不存在则返回null</returns>
        /// <remarks>
        /// 根据设备ID从VisualCanvasList中获取视觉画布对象
        /// </remarks>
        private VisualCanvas GetVisualCanvas(int id)
        {
            return VisualCanvasList.TryGetValue(id, out var visualCanvas) ? visualCanvas : null;
        }

        /// <summary>
        /// 获取触摸按下点的编辑模式方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回触摸按下点的编辑模式，如果不存在则返回InkCanvas的当前编辑模式</returns>
        /// <remarks>
        /// 根据设备ID从TouchDownPointsList中获取触摸按下点的编辑模式
        /// </remarks>
        private InkCanvasEditingMode GetTouchDownPointsList(int id)
        {
            return TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode) ? inkCanvasEditingMode : inkCanvas.EditingMode;
        }

        /// <summary>
        /// 触摸按下点的编辑模式字典，键为设备ID，值为编辑模式
        /// </summary>
        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
            new Dictionary<int, InkCanvasEditingMode>();

        /// <summary>
        /// 笔画视觉对象字典，键为设备ID，值为笔画视觉对象
        /// </summary>
        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        /// <summary>
        /// 视觉画布字典，键为设备ID，值为视觉画布对象
        /// </summary>
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion




        private Point iniP = new Point(0, 0);

        /// <summary>
        /// 主网格的触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理主网格的触摸按下事件，包括以下逻辑：
        /// 1. 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮并返回
        /// 2. 根据编辑模式设置光标
        /// 3. 捕获触摸输入
        /// 4. 如果当前处于点擦模式，则直接返回
        /// 5. 如果当前处于图形绘制模式：
        ///    - 设置编辑模式为None
        ///    - 设置触摸状态为按下
        ///    - 禁用浮动栏和黑板UI网格的命中测试
        ///    - 设置起始点坐标
        ///    - 直接返回
        /// 6. 如果当前处于选择模式、墨水模式或线擦模式，则直接返回
        /// 7. 如果当前不是橡皮擦模式，则设置编辑模式为Ink
        /// </remarks>
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
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
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

        /// <summary>
        /// 获取触摸边界宽度方法
        /// </summary>
        /// <param name="e">触摸事件参数</param>
        /// <returns>返回触摸边界宽度</returns>
        /// <remarks>
        /// 根据触摸事件参数计算触摸边界宽度，包括以下逻辑：
        /// 1. 获取触摸点的边界
        /// 2. 如果不是四边红外屏幕，使用边界宽度
        /// 3. 如果是四边红外屏幕，使用边界宽度和高度的平方根
        /// 4. 如果是特殊屏幕，乘以触摸倍数
        /// 5. 返回计算得到的触摸边界宽度
        /// </remarks>
        public double GetTouchBoundWidth(TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height); //四边红外
            if (Settings.Advanced.IsSpecialScreen) value *= Settings.Advanced.TouchMultiplier;
            return value;
        }

        /// <summary>
        /// InkCanvas的预览触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理InkCanvas的预览触摸按下事件，包括以下逻辑：
        /// 1. 捕获触摸输入
        /// 2. 禁用浮动栏和黑板UI网格的命中测试
        /// 3. 将触摸设备ID添加到dec列表中
        /// 4. 当只有一个触摸设备时：
        ///    - 记录中心点坐标
        ///    - 记录第一根手指点击时的StrokeCollection
        /// 5. 当有两个或以上触摸设备，或者处于单指拖动模式，或者禁用了双指手势时：
        ///    - 如果处于多点触控模式或禁用了双指手势，则直接返回
        ///    - 如果当前编辑模式为None或Select，则直接返回
        ///    - 记录当前的编辑模式
        ///    - 设置编辑模式为None，关闭画笔功能
        /// </remarks>
        private void InkCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            inkCanvas.CaptureTouch(e.TouchDevice);
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            dec.Add(e.TouchDevice.Id);
            //设备1个的时候，记录中心点
            if (dec.Count == 1)
            {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;

                //记录第一根手指点击时的 StrokeCollection
                lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            }
            //设备两个及两个以上，将画笔功能关闭
            if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
            {
                if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None ||
                    inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;
                lastInkCanvasEditingMode = inkCanvas.EditingMode;
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        /// <summary>
        /// InkCanvas的预览触摸移动事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 空方法，预留用于处理InkCanvas的预览触摸移动事件
        /// </remarks>
        private void InkCanvas_PreviewTouchMove(object sender, TouchEventArgs e)
        {
        }

        /// <summary>
        /// InkCanvas的预览触摸抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理InkCanvas的预览触摸抬起事件，包括以下逻辑：
        /// 1. 释放所有触摸捕获
        /// 2. 启用浮动栏和黑板UI网格的命中测试
        /// 3. 如果有多个触摸设备且当前编辑模式为None，则切回之前的编辑模式
        /// 4. 从dec列表中移除当前触摸设备ID
        /// 5. 当没有触摸设备时：
        ///    - 重置单指拖动模式和等待下一次触摸按下的标志
        ///    - 如果当前不是图形绘制模式且编辑模式不是橡皮擦或选择模式，则切回之前的编辑模式
        /// 6. 如果当前处于图形绘制模式：
        ///    - 重置触摸状态
        ///    - 启用浮动栏和黑板UI网格的命中测试
        ///    - 对于双曲线等需要多步绘制的图形，根据当前步骤决定是进入下一步还是完成绘制
        ///    - 对于其他单步绘制的图形，直接完成绘制
        /// 7. 设置InkCanvas的透明度为1
        /// 8. 当没有触摸设备且笔画数量发生变化，且不是绘制长方体的第一次触摸时，保存笔画集合
        /// </remarks>
        private void InkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            inkCanvas.ReleaseAllTouchCaptures();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            //手势完成后切回之前的状态
            if (dec.Count > 1)
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    inkCanvas.EditingMode = lastInkCanvasEditingMode;
            dec.Remove(e.TouchDevice.Id);

            if (dec.Count == 0)
            {
                isSingleFingerDragMode = false;
                isWaitUntilNextTouchDown = false;
                if (drawingShapeMode == 0
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && inkCanvas.EditingMode != InkCanvasEditingMode.Select
                    && inkCanvas.EditingMode != InkCanvasEditingMode.None)
                {
                    if (lastInkCanvasEditingMode != InkCanvasEditingMode.None)
                    {
                        inkCanvas.EditingMode = lastInkCanvasEditingMode;
                    }
                }
            }

            if (drawingShapeMode != 0)
            {
                isTouchDown = false;
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;

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
                    var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonUpEvent,
                        Source = inkCanvas
                    };
                    inkCanvas_MouseUp(inkCanvas, mouseArgs);
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

        /// <summary>
        /// InkCanvas的操作开始事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作开始事件参数</param>
        /// <remarks>
        /// 设置操作模式为所有模式
        /// </remarks>
        private void InkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.Mode = ManipulationModes.All;
        }

        /// <summary>
        /// InkCanvas的操作惯性开始事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作惯性开始事件参数</param>
        /// <remarks>
        /// 空方法，预留用于处理InkCanvas的操作惯性开始事件
        /// </remarks>
        private void InkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e) { }

        /// <summary>
        /// 主网格的操作完成事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作完成事件参数</param>
        /// <remarks>
        /// 处理主网格的操作完成事件，包括以下逻辑：
        /// 1. 当没有操作器时：
        ///    - 清除dec列表
        ///    - 重置单指拖动模式标志
        ///    - 如果当前不是图形绘制模式且编辑模式不是橡皮擦或选择模式，则设置编辑模式为Ink
        /// </remarks>
        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (e.Manipulators.Count() == 0)
            {
                if (dec.Count > 0)
                {
                    dec.Clear();
                }
                isSingleFingerDragMode = false;

                if (drawingShapeMode == 0
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
                }
            }
        }

        /// <summary>
        /// 主网格的操作增量事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作增量事件参数</param>
        /// <remarks>
        /// 处理主网格的操作增量事件，包括以下逻辑：
        /// 1. 如果当前处于多点触控模式或禁用了双指手势，则直接返回
        /// 2. 检查是否有多个操作器
        /// 3. 检查是否应该使用双指手势
        /// 4. 如果应该使用双指手势：
        ///    - 获取位移矢量
        ///    - 创建矩阵变换
        ///    - 如果启用了双指平移，则应用平移变换
        ///    - 计算中心点（用于缩放和旋转）
        ///    - 如果启用了双指平移或旋转，则应用旋转变换
        ///    - 如果启用了双指缩放，则应用缩放变换
        ///    - 处理选中的笔画：
        ///       - 对每个选中的笔画应用变换
        ///       - 对圆形笔画更新半径和中心点
        ///       - 如果启用了双指缩放，更新笔画的宽度和高度
        ///    - 处理未选中的笔画：
        ///       - 对所有笔画应用变换
        ///       - 如果启用了双指缩放，更新笔画的宽度和高度
        ///       - 同时变换画布上的图片元素
        ///       - 对所有圆形笔画更新半径和中心点
        /// </remarks>
        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;

            bool hasMultipleManipulators = e.Manipulators.Count() >= 2;
            bool shouldUseTwoFingerGesture = (dec.Count >= 2 && hasMultipleManipulators &&
                                             (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode ||
                                              StackPanelPPTControls.Visibility != Visibility.Visible ||
                                              StackPanelPPTButtons.Visibility == Visibility.Collapsed)) ||
                                            isSingleFingerDragMode;

            if (shouldUseTwoFingerGesture)
            {
                var md = e.DeltaManipulation;
                var trans = md.Translation; // 获得位移矢量

                var m = new Matrix();

                if (Settings.Gesture.IsEnableTwoFingerTranslate)
                    m.Translate(trans.X, trans.Y); // 移动

                // 计算中心点（用于缩放和旋转）
                var fe = e.Source as FrameworkElement;
                var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

                if (Settings.Gesture.IsEnableTwoFingerGestureTranslateOrRotation)
                {
                    var rotate = md.Rotation; // 获得旋转角度

                    if (Settings.Gesture.IsEnableTwoFingerRotation)
                        m.RotateAt(rotate, center.X, center.Y); // 旋转
                }

                if (Settings.Gesture.IsEnableTwoFingerZoom)
                {
                    var scale = md.Scale; // 获得缩放倍数
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

                        if (!Settings.Gesture.IsEnableTwoFingerZoom) continue;
                        try
                        {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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
                if (!(image.RenderTransform is TransformGroup transformGroup))
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
                if (!(mediaElement.RenderTransform is TransformGroup transformGroup))
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
    }
}

