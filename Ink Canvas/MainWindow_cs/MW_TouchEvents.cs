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
using System.Windows.Threading;
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

        /// <summary>
        /// 保存画布上的非笔画元素（如图片、媒体元素等）
        /// </summary>
        private List<UIElement> PreserveNonStrokeElements()
        {
            var preservedElements = new List<UIElement>();

            // 遍历inkCanvas的所有子元素
            for (int i = inkCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = inkCanvas.Children[i];

                // 保存图片、媒体元素等非笔画相关的UI元素
                if (child is Image || child is MediaElement ||
                    (child is Border border && border.Name != "AdvancedEraserOverlay"))
                {
                    preservedElements.Add(child);
                }
            }

            return preservedElements;
        }

        /// <summary>
        /// 恢复之前保存的非笔画元素到画布
        /// </summary>
        private void RestoreNonStrokeElements(List<UIElement> preservedElements)
        {
            if (preservedElements == null) return;

            foreach (var element in preservedElements)
            {
                // 确保元素没有父容器再添加到inkCanvas
                if (element is FrameworkElement fe && fe.Parent == null)
                {
                    inkCanvas.Children.Add(element);
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

            // 修复：几何绘制模式下完全禁止触摸轨迹收集
            if (drawingShapeMode != 0)
            {
                // 确保几何绘制模式下不切换到Ink模式，避免触摸轨迹被收集
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
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

            LogHelper.WriteLogToFile($"MainWindow_StylusDown 被调用，笔尾状态: {e.StylusDevice.Inverted}, 当前 drawingShapeMode: {drawingShapeMode}, 当前 EditingMode: {inkCanvas.EditingMode}");

            // 新增：根据是否为笔尾自动切换橡皮擦/画笔模式
            if (e.StylusDevice.Inverted)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                LogHelper.WriteLogToFile("检测到笔尾，设置 EditingMode 为 EraseByPoint");
            }
            else
            {
                // 修复：几何绘制模式下完全禁止触摸轨迹收集
                if (drawingShapeMode != 0)
                {
                    // 确保几何绘制模式下不切换到Ink模式，避免触摸轨迹被收集
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    LogHelper.WriteLogToFile("几何绘制模式，设置 EditingMode 为 None");
                    return;
                }
                // 修复：保持当前的线擦模式，不要强制切换到Ink模式
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    LogHelper.WriteLogToFile("设置 EditingMode 为 Ink");
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
            try
            {
                LogHelper.WriteLogToFile($"MainWindow_StylusUp 被调用，EditingMode: {inkCanvas.EditingMode}, EnableInkFade: {Settings.Canvas.EnableInkFade}");

                var stroke = GetStrokeVisual(e.StylusDevice.Id).Stroke;
                LogHelper.WriteLogToFile($"获取到墨迹，StylusPoints数量: {stroke.StylusPoints.Count}");

                // 正常模式：添加到画布并参与墨迹纠正
                // 墨迹渐隐功能现在在 StrokeCollected 事件中统一处理所有输入方式
                LogHelper.WriteLogToFile("StylusUp: 添加墨迹到画布");

                inkCanvas.Strokes.Add(stroke);
                await Task.Delay(5); // 避免渲染墨迹完成前预览墨迹被删除导致墨迹闪烁
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
            // 修复：几何绘制模式下完全禁止触摸轨迹收集
            if (drawingShapeMode != 0)
            {
                // 确保几何绘制模式下不切换到Ink模式，避免触摸轨迹被收集
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
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
        private bool palmEraserTouchDownHandled; // 新增：标记手掌擦触摸按下是否已处理
        private DateTime palmEraserActivationTime; // 新增：记录手掌擦激活时间
        private const int PALM_ERASER_TIMEOUT_MS = 3000; // 修改：减少手掌擦超时时间（3秒）
        private DispatcherTimer palmEraserRecoveryTimer; // 新增：手掌擦恢复定时器
        private HashSet<int> palmEraserTouchIds = new HashSet<int>(); // 新增：记录参与手掌擦的触摸点ID

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
            // 修复：几何绘制模式下完全禁止触摸轨迹收集
            if (drawingShapeMode != 0)
            {
                // 确保几何绘制模式下不切换到Ink模式，避免触摸轨迹收集
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                // 几何绘制模式下不记录触摸点，避免触摸轨迹被收集
                SetCursorBasedOnEditingMode(inkCanvas);
                inkCanvas.CaptureTouch(e.TouchDevice);
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                // 修复：几何绘制模式下，只记录几何绘制的起点，不记录触摸轨迹
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
            dec.Add(e.TouchDevice.Id);

            // Palm Eraser 逻辑 - 优化：改进手掌判定条件，使用设备提供的触摸面积信息
            if (Settings.Canvas.EnablePalmEraser && dec.Count >= 2 && !isPalmEraserActive && !palmEraserTouchDownHandled)
            {
                touchPoint = e.GetTouchPoint(inkCanvas);
                var size = touchPoint.Size; // 使用设备提供的触摸面积信息
                var bounds = touchPoint.Bounds; // 保留bounds用于宽高比计算

                // 根据敏感度设置调整判定参数
                double palmAreaThreshold; // 改为面积阈值
                double aspectRatioThreshold;
                int minTouchPoints;

                switch (Settings.Canvas.PalmEraserSensitivity)
                {
                    case 0: // 低敏感度 - 更严格的判定
                        palmAreaThreshold = 6400; // 80*80的面积
                        aspectRatioThreshold = 0.4;
                        minTouchPoints = 4;
                        break;
                    case 1: // 中敏感度 - 平衡的判定
                        palmAreaThreshold = 3600; // 60*60的面积
                        aspectRatioThreshold = 0.3;
                        minTouchPoints = 3;
                        break;
                    case 2: // 高敏感度 - 较宽松的判定
                    default:
                        palmAreaThreshold = 2500; // 50*50的面积
                        aspectRatioThreshold = 0.25;
                        minTouchPoints = 2;
                        break;
                }

                // 计算触摸面积（使用设备提供的Size）
                double touchArea = size.Width * size.Height;

                // 计算宽高比（使用Bounds确保准确性）
                double aspectRatio = Math.Min(bounds.Width, bounds.Height) / Math.Max(bounds.Width, bounds.Height);

                // 改进的手掌判定条件：使用面积而不是单独的宽高
                bool isLargeTouch = touchArea >= palmAreaThreshold;
                bool isPalmLikeShape = aspectRatio >= aspectRatioThreshold;
                bool hasMultipleTouchPoints = dec.Count >= minTouchPoints;

                // 新增：额外的判定条件提高准确性
                bool isReasonableSize = size.Width >= 20 && size.Height >= 20 && size.Width <= 200 && size.Height <= 200; // 合理的触摸尺寸范围
                bool isNotTooElongated = aspectRatio >= 0.2; // 避免过于细长的触摸（可能是手指）
                bool hasEnoughArea = touchArea >= 400; // 最小面积要求，避免小面积误判

                if (isLargeTouch && isPalmLikeShape && hasMultipleTouchPoints && isReasonableSize && isNotTooElongated && hasEnoughArea)
                {
                    // 记录当前编辑模式和高光状态
                    palmEraserLastEditingMode = inkCanvas.EditingMode;
                    palmEraserLastIsHighlighter = drawingAttributes.IsHighlighter;

                    // 记录参与手掌擦的触摸点ID
                    palmEraserTouchIds.Clear();
                    foreach (int touchId in dec)
                    {
                        palmEraserTouchIds.Add(touchId);
                    }

                    // 切换为橡皮擦
                    EraserIcon_Click(null, null);
                    isPalmEraserActive = true;
                    palmEraserActivationTime = DateTime.Now; // 记录激活时间
                    palmEraserTouchDownHandled = true; // 标记已处理

                    // 启动恢复定时器，防止卡死
                    StartPalmEraserRecoveryTimer();

                    // 记录日志
                    LogHelper.WriteLogToFile($"Palm eraser activated - Sensitivity: {Settings.Canvas.PalmEraserSensitivity}, Touch area: {touchArea:F0}, Size: {size.Width}x{size.Height}, Bounds: {bounds.Width}x{bounds.Height}, Aspect ratio: {aspectRatio:F2}, Touch points: {dec.Count}, Reasonable size: {isReasonableSize}, Not elongated: {isNotTooElongated}, Enough area: {hasEnoughArea}");
                }
            }

            // 设备1个的时候，记录中心点
            if (dec.Count == 1)
            {
                touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;

                // 修复：只允许在此处赋值iniP，防止TouchMove等其他地方覆盖，保证几何绘制起点一致
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
                lastInkCanvasEditingMode = inkCanvas.EditingMode;
                // 修复：几何绘制模式下禁止切回Ink
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && drawingShapeMode == 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
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

            // Palm Eraser 逻辑：优化状态恢复机制
            dec.Remove(e.TouchDevice.Id);

            // 如果是手掌擦的触摸点，从记录中移除
            if (palmEraserTouchIds.Contains(e.TouchDevice.Id))
            {
                palmEraserTouchIds.Remove(e.TouchDevice.Id);
            }

            // 当所有手掌擦触摸点都抬起时，恢复原编辑模式
            if (isPalmEraserActive && palmEraserTouchIds.Count == 0)
            {
                LogHelper.WriteLogToFile($"Palm eraser recovery triggered - Touch points remaining: {palmEraserTouchIds.Count}, dec.Count: {dec.Count}");

                // 恢复高光状态
                drawingAttributes.IsHighlighter = palmEraserLastIsHighlighter;

                // 恢复编辑模式 - 优化：改进状态恢复逻辑
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
                palmEraserTouchDownHandled = false;
                palmEraserTouchIds.Clear();

                // 停止恢复定时器
                StopPalmEraserRecoveryTimer();

                // 确保触摸事件能正常响应
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.IsManipulationEnabled = true;

                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                LogHelper.WriteLogToFile("Palm eraser state reset completed");
            }

            // 新增：超时检测 - 如果手掌擦激活时间过长，强制重置状态
            if (isPalmEraserActive)
            {
                var timeSinceActivation = DateTime.Now - palmEraserActivationTime;
                if (timeSinceActivation.TotalMilliseconds > PALM_ERASER_TIMEOUT_MS)
                {
                    LogHelper.WriteLogToFile($"Palm eraser timeout detected ({timeSinceActivation.TotalMilliseconds}ms), forcing recovery", LogHelper.LogType.Warning);

                    // 强制恢复状态
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
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"Palm eraser timeout recovery failed: {ex.Message}, forcing to Ink mode", LogHelper.LogType.Error);
                        inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    }

                    // 重置所有手掌擦状态
                    isPalmEraserActive = false;
                    palmEraserTouchDownHandled = false;
                    palmEraserTouchIds.Clear();
                    inkCanvas.IsHitTestVisible = true;
                    inkCanvas.IsManipulationEnabled = true;

                    ViewboxFloatingBar.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                    // 停止恢复定时器
                    StopPalmEraserRecoveryTimer();

                    LogHelper.WriteLogToFile("Palm eraser timeout recovery completed");
                }
            }
            // 修复：几何绘制模式下，触摸抬手时应该正确处理，而不是简单模拟鼠标事件
            if (drawingShapeMode != 0)
            {
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

                    // 修复：确保手掌擦除后触摸事件能正常响应
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
                        palmEraserTouchDownHandled = false;
                        palmEraserTouchIds.Clear(); // 确保清空触摸点ID
                        inkCanvas.IsHitTestVisible = true;
                        inkCanvas.IsManipulationEnabled = true;

                        ViewboxFloatingBar.IsHitTestVisible = true;
                        BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                        // 停止恢复定时器
                        StopPalmEraserRecoveryTimer();

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
            // 修复：几何绘制模式下不自动切换到Ink模式，避免触摸轨迹被收集
            if (drawingShapeMode == 0
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                // 修复：确保多指手势完成后正确更新lastInkCanvasEditingMode
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

            // 修复：允许单指拖动选中的墨迹，即使禁用了多指手势
            if (isInMultiTouchMode) return;

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

                        if (!Settings.Gesture.IsEnableTwoFingerZoom) continue;
                        try
                        {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch { }
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

                        ;
                    }
                    else
                    {
                        foreach (var stroke in inkCanvas.Strokes) stroke.Transform(m, false);
                        ;
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
                // 修复：几何绘制模式下不自动切换到Ink模式，避免触摸轨迹被收集
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
                // 修复：几何绘制模式下不自动切换到Ink模式，避免触摸轨迹被收集
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

        /// <summary>
        /// 启动手掌擦恢复定时器，防止卡死状态
        /// </summary>
        private void StartPalmEraserRecoveryTimer()
        {
            if (palmEraserRecoveryTimer == null)
            {
                palmEraserRecoveryTimer = new DispatcherTimer();
                palmEraserRecoveryTimer.Interval = TimeSpan.FromMilliseconds(1000); // 每秒检查一次
                palmEraserRecoveryTimer.Tick += PalmEraserRecoveryTimer_Tick;
            }

            palmEraserRecoveryTimer.Start();
        }

        /// <summary>
        /// 停止手掌擦恢复定时器
        /// </summary>
        private void StopPalmEraserRecoveryTimer()
        {
            if (palmEraserRecoveryTimer != null)
            {
                palmEraserRecoveryTimer.Stop();
            }
        }

        /// <summary>
        /// 手掌擦恢复定时器事件处理
        /// </summary>
        private void PalmEraserRecoveryTimer_Tick(object sender, EventArgs e)
        {
            if (!isPalmEraserActive) return;

            // 检查是否超时
            var timeSinceActivation = DateTime.Now - palmEraserActivationTime;
            if (timeSinceActivation.TotalMilliseconds > PALM_ERASER_TIMEOUT_MS)
            {
                LogHelper.WriteLogToFile($"Palm eraser recovery timer triggered, forcing recovery after {timeSinceActivation.TotalMilliseconds}ms", LogHelper.LogType.Warning);

                // 强制恢复状态
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

                        LogHelper.WriteLogToFile($"Palm eraser timer recovery to mode: {palmEraserLastEditingMode}");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Palm eraser recovery timer failed: {ex.Message}, forcing to Ink mode", LogHelper.LogType.Error);
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }

                // 重置所有手掌擦状态
                isPalmEraserActive = false;
                palmEraserTouchDownHandled = false;
                palmEraserTouchIds.Clear();
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.IsManipulationEnabled = true;

                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                // 停止定时器
                StopPalmEraserRecoveryTimer();

                LogHelper.WriteLogToFile("Palm eraser timer recovery completed");
            }
        }
    }
}
