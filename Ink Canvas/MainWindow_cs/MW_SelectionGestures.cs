using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        #region Floating Control

        private object lastBorderMouseDownObject;

        private void Border_MouseDown(object sender, MouseButtonEventArgs e) {
            // 如果发送者是 RandomDrawPanel 或 SingleDrawPanel，且它们被隐藏，则不处理事件
            if (sender is SimpleStackPanel panel) {
                if ((panel == RandomDrawPanel || panel == SingleDrawPanel) && 
                    panel.Visibility != Visibility.Visible) {
                    return;
                }
            }
            lastBorderMouseDownObject = sender;
        }

        private bool isStrokeSelectionCloneOn;

        private void BorderStrokeSelectionClone_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            if (isStrokeSelectionCloneOn) {
                BorderStrokeSelectionClone.Background = Brushes.Transparent;

                isStrokeSelectionCloneOn = false;
            }
            else {
                BorderStrokeSelectionClone.Background = new SolidColorBrush(StringToColor("#FF1ED760"));

                isStrokeSelectionCloneOn = true;
            }
        }

        private void BorderStrokeSelectionCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            var strokes = inkCanvas.GetSelectedStrokes();
            inkCanvas.Select(new StrokeCollection());
            strokes = strokes.Clone();
            BtnWhiteBoardAdd_Click(null, null);
            inkCanvas.Strokes.Add(strokes);
        }

        private void BorderStrokeSelectionDelete_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            SymbolIconDelete_MouseUp(sender, e);
        }

        private void GridPenWidthDecrease_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(0.8);
        }

        private void GridPenWidthIncrease_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(1.25);
        }

        private void ChangeStrokeThickness(double multipler) {
            foreach (var stroke in inkCanvas.GetSelectedStrokes()) {
                var newWidth = stroke.DrawingAttributes.Width * multipler;
                var newHeight = stroke.DrawingAttributes.Height * multipler;
                if (!(newWidth >= DrawingAttributes.MinWidth) || !(newWidth <= DrawingAttributes.MaxWidth)
                                                              || !(newHeight >= DrawingAttributes.MinHeight) ||
                                                              !(newHeight <= DrawingAttributes.MaxHeight)) continue;
                stroke.DrawingAttributes.Width = newWidth;
                stroke.DrawingAttributes.Height = newHeight;
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
        }

        private void GridPenWidthRestore_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            foreach (var stroke in inkCanvas.GetSelectedStrokes()) {
                stroke.DrawingAttributes.Width = inkCanvas.DefaultDrawingAttributes.Width;
                stroke.DrawingAttributes.Height = inkCanvas.DefaultDrawingAttributes.Height;
            }
        }

        private void ImageFlipHorizontal_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            var m = new Matrix();

            // Find center of element and then transform to get current location of center
            var fe = e.Source as FrameworkElement;
            var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
            center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            // Update matrix to reflect translation/rotation
            m.ScaleAt(-1, 1, center.X, center.Y); // 缩放

            var targetStrokes = inkCanvas.GetSelectedStrokes();
            foreach (var stroke in targetStrokes) stroke.Transform(m, false);

            if (DrawingAttributesHistory.Count > 0)
            {
                //var collecion = new StrokeCollection();
                //foreach (var item in DrawingAttributesHistory)
                //{
                //    collecion.Add(item.Key);
                //}
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }

            //updateBorderStrokeSelectionControlLocation();
        }

        private void ImageFlipVertical_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            var m = new Matrix();

            // Find center of element and then transform to get current location of center
            var fe = e.Source as FrameworkElement;
            var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
            center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            // Update matrix to reflect translation/rotation
            m.ScaleAt(1, -1, center.X, center.Y); // 缩放

            var targetStrokes = inkCanvas.GetSelectedStrokes();
            foreach (var stroke in targetStrokes) stroke.Transform(m, false);

            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        private void ImageRotate45_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            var m = new Matrix();

            // Find center of element and then transform to get current location of center
            var fe = e.Source as FrameworkElement;
            var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
            center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            // Update matrix to reflect translation/rotation
            m.RotateAt(45, center.X, center.Y); // 旋转

            var targetStrokes = inkCanvas.GetSelectedStrokes();
            foreach (var stroke in targetStrokes) stroke.Transform(m, false);

            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        private void ImageRotate90_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            var m = new Matrix();

            // Find center of element and then transform to get current location of center
            var fe = e.Source as FrameworkElement;
            var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
            center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            // Update matrix to reflect translation/rotation
            m.RotateAt(90, center.X, center.Y); // 旋转

            var targetStrokes = inkCanvas.GetSelectedStrokes();
            foreach (var stroke in targetStrokes) stroke.Transform(m, false);

            if (DrawingAttributesHistory.Count > 0)
            {
                var collecion = new StrokeCollection();
                foreach (var item in DrawingAttributesHistory)
                {
                    collecion.Add(item.Key);
                }
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
        }

        #endregion

        private bool isGridInkCanvasSelectionCoverMouseDown;
        private StrokeCollection StrokesSelectionClone = new StrokeCollection();

        private void GridInkCanvasSelectionCover_MouseDown(object sender, MouseButtonEventArgs e) {
            isGridInkCanvasSelectionCoverMouseDown = true;
        }

        private void GridInkCanvasSelectionCover_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isGridInkCanvasSelectionCoverMouseDown) return;
            isGridInkCanvasSelectionCoverMouseDown = false;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e) {
            ExitMultiTouchModeIfNeeded();
            forceEraser = true;
            drawingShapeMode = 0;
            inkCanvas.IsManipulationEnabled = false;
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select) {
                if (inkCanvas.GetSelectedStrokes().Count == inkCanvas.Strokes.Count) {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    inkCanvas.EditingMode = InkCanvasEditingMode.Select;
                }
                else {
                    var selectedStrokes = new StrokeCollection();
                    foreach (var stroke in inkCanvas.Strokes)
                        if (stroke.GetBounds().Width > 0 && stroke.GetBounds().Height > 0)
                            selectedStrokes.Add(stroke);
                    inkCanvas.Select(selectedStrokes);
                }
            }
            else {
                inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            }
        }

        private double BorderStrokeSelectionControlWidth = 490.0;
        private double BorderStrokeSelectionControlHeight = 80.0;
        private bool isProgramChangeStrokeSelection;

        private void inkCanvas_SelectionChanged(object sender, EventArgs e) {
            if (isProgramChangeStrokeSelection) return;
            if (inkCanvas.GetSelectedStrokes().Count == 0) {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                // 当没有选中笔画时，检查是否有选中的UIElement
                CheckUIElementSelection();
            }
            else {
                GridInkCanvasSelectionCover.Visibility = Visibility.Visible;
                BorderStrokeSelectionClone.Background = Brushes.Transparent;
                isStrokeSelectionCloneOn = false;
                updateBorderStrokeSelectionControlLocation();
                // 当选中笔画时，取消UIElement选择
                DeselectUIElement();
            }
        }

        private void CheckUIElementSelection()
        {
            // 检查InkCanvas中的UIElement是否被选中
            var selectedElements = inkCanvas.GetSelectedElements();
            if (selectedElements.Count > 0)
            {
                var element = selectedElements[0];
                SelectUIElement(element);
            }
            else
            {
                DeselectUIElement();
            }
        }

        private void updateBorderStrokeSelectionControlLocation() {
            var borderLeft = (inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Right -
                              BorderStrokeSelectionControlWidth) / 2;
            var borderTop = inkCanvas.GetSelectionBounds().Bottom + 1;
            if (borderLeft < 0) borderLeft = 0;
            if (borderTop < 0) borderTop = 0;
            if (Width - borderLeft < BorderStrokeSelectionControlWidth || double.IsNaN(borderLeft))
                borderLeft = Width - BorderStrokeSelectionControlWidth;
            if (Height - borderTop < BorderStrokeSelectionControlHeight || double.IsNaN(borderTop))
                borderTop = Height - BorderStrokeSelectionControlHeight;

            if (borderTop > 60) borderTop -= 60;
            BorderStrokeSelectionControl.Margin = new Thickness(borderLeft, borderTop, 0, 0);
        }

        private void GridInkCanvasSelectionCover_ManipulationStarting(object sender, ManipulationStartingEventArgs e) {
            e.Mode = ManipulationModes.All;
        }

        private void GridInkCanvasSelectionCover_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e) {
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
        }

        private void GridInkCanvasSelectionCover_ManipulationDelta(object sender, ManipulationDeltaEventArgs e) {
            try {
                if (dec.Count >= 1) {
                    bool disableScale = dec.Count >= 3;
                    var md = e.DeltaManipulation;
                    var trans = md.Translation; // 获得位移矢量
                    var rotate = md.Rotation; // 获得旋转角度
                    var scale = md.Scale; // 获得缩放倍数

                    var m = new Matrix();

                    // Find center of element and then transform to get current location of center
                    var fe = e.Source as FrameworkElement;
                    var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                    center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                        inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
                    center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

                    // Update matrix to reflect translation/rotation
                    m.Translate(trans.X, trans.Y); // 移动
                    if (!disableScale)
                        m.ScaleAt(scale.X, scale.Y, center.X, center.Y); // 缩放

                    var strokes = inkCanvas.GetSelectedStrokes();
                    if (StrokesSelectionClone.Count != 0)
                        strokes = StrokesSelectionClone;
                    else if (Settings.Gesture.IsEnableTwoFingerRotationOnSelection)
                        m.RotateAt(rotate, center.X, center.Y); // 旋转
                    foreach (var stroke in strokes) {
                        stroke.Transform(m, false);

                        try {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch { }
                    }

                    updateBorderStrokeSelectionControlLocation();
                }
            }
            catch { }
        }

        private void GridInkCanvasSelectionCover_TouchDown(object sender, TouchEventArgs e) { }

        private void GridInkCanvasSelectionCover_TouchUp(object sender, TouchEventArgs e) { }

        private Point lastTouchPointOnGridInkCanvasCover = new Point(0, 0);

        private void GridInkCanvasSelectionCover_PreviewTouchDown(object sender, TouchEventArgs e) {
            dec.Add(e.TouchDevice.Id);
            //设备1个的时候，记录中心点
            if (dec.Count == 1) {
                var touchPoint = e.GetTouchPoint(null);
                centerPoint = touchPoint.Position;
                lastTouchPointOnGridInkCanvasCover = touchPoint.Position;

                if (isStrokeSelectionCloneOn) {
                    var strokes = inkCanvas.GetSelectedStrokes();
                    isProgramChangeStrokeSelection = true;
                    inkCanvas.Select(new StrokeCollection());
                    StrokesSelectionClone = strokes.Clone();
                    inkCanvas.Select(strokes);
                    isProgramChangeStrokeSelection = false;
                    inkCanvas.Strokes.Add(StrokesSelectionClone);
                }
                else {
                    // 新增：启动套索选择模式
                    inkCanvas.EditingMode = InkCanvasEditingMode.Select;
                    inkCanvas.Select(new StrokeCollection());
                }
            }
        }

        private void GridInkCanvasSelectionCover_PreviewTouchUp(object sender, TouchEventArgs e) {
            dec.Remove(e.TouchDevice.Id);
            if (dec.Count >= 1) return;
            isProgramChangeStrokeSelection = false;
            if (lastTouchPointOnGridInkCanvasCover == e.GetTouchPoint(null).Position) {
                if (!(lastTouchPointOnGridInkCanvasCover.X < inkCanvas.GetSelectionBounds().Left) &&
                    !(lastTouchPointOnGridInkCanvasCover.Y < inkCanvas.GetSelectionBounds().Top) &&
                    !(lastTouchPointOnGridInkCanvasCover.X > inkCanvas.GetSelectionBounds().Right) &&
                    !(lastTouchPointOnGridInkCanvasCover.Y > inkCanvas.GetSelectionBounds().Bottom)) return;
                inkCanvas.Select(new StrokeCollection());
                StrokesSelectionClone = new StrokeCollection();
            }
            else if (inkCanvas.GetSelectedStrokes().Count == 0) {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                StrokesSelectionClone = new StrokeCollection();
            }
            else {
                GridInkCanvasSelectionCover.Visibility = Visibility.Visible;
                StrokesSelectionClone = new StrokeCollection();
            }
        }

        private void LassoSelect_Click(object sender, RoutedEventArgs e) {
            ExitMultiTouchModeIfNeeded();
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private void BtnLassoSelect_Click(object sender, RoutedEventArgs e) {
            ExitMultiTouchModeIfNeeded();
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            inkCanvas.IsManipulationEnabled = true;
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        #region UIElement Selection and Resize

        private UIElement selectedUIElement;
        private System.Windows.Controls.Canvas resizeHandlesCanvas;
        private readonly List<Rectangle> resizeHandles = new List<Rectangle>();
        private bool isResizing;
        private ResizeDirection currentResizeDirection = ResizeDirection.None;
        private Point resizeStartPoint;
        private Rect originalElementBounds;

        // 图片工具栏相关
        private Border borderImageSelectionControl;
        private double BorderImageSelectionControlWidth = 200.0;
        private double BorderImageSelectionControlHeight = 80.0;

        private enum ResizeDirection
        {
            None,
            TopLeft,
            TopCenter,
            TopRight,
            MiddleLeft,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

        private void InitializeUIElementSelection()
        {
            // 创建拖拽手柄画布
            if (resizeHandlesCanvas == null)
            {
                resizeHandlesCanvas = new System.Windows.Controls.Canvas
                {
                    Background = Brushes.Transparent,
                    IsHitTestVisible = true,
                    Visibility = Visibility.Collapsed
                };

                // 将手柄画布添加到主网格中，确保它在InkCanvas之上
                var mainGrid = inkCanvas.Parent as Grid;
                if (mainGrid != null)
                {
                    mainGrid.Children.Add(resizeHandlesCanvas);
                    Panel.SetZIndex(resizeHandlesCanvas, 1000); // 确保在最上层
                }
            }

            // 初始化图片工具栏引用
            if (borderImageSelectionControl == null)
            {
                borderImageSelectionControl = FindName("BorderImageSelectionControl") as Border;
            }

            // 创建8个拖拽手柄
            CreateResizeHandles();
        }

        private void CreateResizeHandles()
        {
            resizeHandles.Clear();
            resizeHandlesCanvas.Children.Clear();

            var directions = new[]
            {
                ResizeDirection.TopLeft, ResizeDirection.TopCenter, ResizeDirection.TopRight,
                ResizeDirection.MiddleLeft, ResizeDirection.MiddleRight,
                ResizeDirection.BottomLeft, ResizeDirection.BottomCenter, ResizeDirection.BottomRight
            };

            foreach (var direction in directions)
            {
                var handle = new Rectangle
                {
                    Width = 12,
                    Height = 12,
                    Fill = Brushes.White,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Cursor = GetCursorForDirection(direction),
                    Tag = direction
                };

                handle.MouseDown += ResizeHandle_MouseDown;
                handle.MouseMove += ResizeHandle_MouseMove;
                handle.MouseUp += ResizeHandle_MouseUp;

                resizeHandles.Add(handle);
                resizeHandlesCanvas.Children.Add(handle);
            }
        }

        private Cursor GetCursorForDirection(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    return Cursors.SizeNESW;
                case ResizeDirection.TopCenter:
                case ResizeDirection.BottomCenter:
                    return Cursors.SizeNS;
                case ResizeDirection.MiddleLeft:
                case ResizeDirection.MiddleRight:
                    return Cursors.SizeWE;
                default:
                    return Cursors.Arrow;
            }
        }

        private void SelectUIElement(UIElement element)
        {
            if (selectedUIElement == element) return;

            // 取消之前的选择
            DeselectUIElement();

            // 清除笔画选择
            if (inkCanvas.GetSelectedStrokes().Count > 0)
            {
                isProgramChangeStrokeSelection = true;
                inkCanvas.Select(new StrokeCollection());
                isProgramChangeStrokeSelection = false;
            }

            selectedUIElement = element;
            LogHelper.WriteLogToFile($"SelectUIElement: 设置选中元素为 {element?.GetType().Name ?? "null"}", LogHelper.LogType.Trace);

            if (element != null)
            {
                // 初始化选择系统（如果还没有初始化）
                if (resizeHandlesCanvas == null)
                {
                    InitializeUIElementSelection();
                }

                // 根据元素类型显示不同的工具栏
                if (element is Image)
                {
                    ShowImageToolbar();
                    LogHelper.WriteLogToFile($"SelectUIElement: 显示图片工具栏", LogHelper.LogType.Trace);
                }
                else
                {
                    // 对于其他UI元素，显示拖拽手柄
                    ShowResizeHandles();
                    LogHelper.WriteLogToFile($"SelectUIElement: 显示拖拽手柄", LogHelper.LogType.Trace);
                }
            }
        }

        private void DeselectUIElement()
        {
            selectedUIElement = null;
            HideResizeHandles();
            HideImageToolbar();
        }

        private void ShowResizeHandles()
        {
            if (selectedUIElement == null || resizeHandlesCanvas == null) return;

            var bounds = GetUIElementBounds(selectedUIElement);
            UpdateResizeHandlesPosition(bounds);
            resizeHandlesCanvas.Visibility = Visibility.Visible;
        }

        private void HideResizeHandles()
        {
            if (resizeHandlesCanvas != null)
            {
                resizeHandlesCanvas.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowImageToolbar()
        {
            if (selectedUIElement == null || borderImageSelectionControl == null) return;

            var bounds = GetUIElementBounds(selectedUIElement);
            UpdateImageToolbarPosition(bounds);
            borderImageSelectionControl.Visibility = Visibility.Visible;
        }

        private void HideImageToolbar()
        {
            if (borderImageSelectionControl != null)
            {
                borderImageSelectionControl.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateImageToolbarPosition(Rect bounds)
        {
            if (borderImageSelectionControl == null) return;

            // 计算工具栏位置，类似于墨迹选择工具栏的逻辑
            var toolbarX = bounds.X + bounds.Width / 2 - BorderImageSelectionControlWidth / 2;
            var toolbarY = bounds.Y + bounds.Height + 10; // 在图片下方10像素处

            // 确保工具栏不会超出画布边界
            if (toolbarX < 0) toolbarX = 0;
            if (toolbarX + BorderImageSelectionControlWidth > inkCanvas.ActualWidth)
                toolbarX = inkCanvas.ActualWidth - BorderImageSelectionControlWidth;

            if (toolbarY + BorderImageSelectionControlHeight > inkCanvas.ActualHeight)
                toolbarY = bounds.Y - BorderImageSelectionControlHeight - 10; // 如果下方空间不够，显示在上方

            borderImageSelectionControl.Margin = new Thickness(toolbarX, toolbarY, 0, 0);
        }

        private Rect GetUIElementBounds(UIElement element)
        {
            var left = InkCanvas.GetLeft(element);
            var top = InkCanvas.GetTop(element);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            var width = 0.0;
            var height = 0.0;

            if (element is FrameworkElement fe)
            {
                width = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                height = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            }

            return new Rect(left, top, width, height);
        }

        private void UpdateResizeHandlesPosition(Rect bounds)
        {
            if (resizeHandles.Count != 8) return;

            var handleSize = 12.0;
            var halfHandle = handleSize / 2;

            // 计算手柄位置
            var positions = new[]
            {
                new Point(bounds.Left - halfHandle, bounds.Top - halfHandle), // TopLeft
                new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Top - halfHandle), // TopCenter
                new Point(bounds.Right - halfHandle, bounds.Top - halfHandle), // TopRight
                new Point(bounds.Left - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), // MiddleLeft
                new Point(bounds.Right - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), // MiddleRight
                new Point(bounds.Left - halfHandle, bounds.Bottom - halfHandle), // BottomLeft
                new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Bottom - halfHandle), // BottomCenter
                new Point(bounds.Right - halfHandle, bounds.Bottom - halfHandle) // BottomRight
            };

            for (int i = 0; i < resizeHandles.Count && i < positions.Length; i++)
            {
                System.Windows.Controls.Canvas.SetLeft(resizeHandles[i], positions[i].X);
                System.Windows.Controls.Canvas.SetTop(resizeHandles[i], positions[i].Y);
            }
        }

        private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (selectedUIElement == null) return;

            var handle = sender as Rectangle;
            if (handle?.Tag is ResizeDirection direction)
            {
                isResizing = true;
                currentResizeDirection = direction;
                resizeStartPoint = e.GetPosition(inkCanvas);
                originalElementBounds = GetUIElementBounds(selectedUIElement);

                handle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isResizing || selectedUIElement == null) return;

            var currentPoint = e.GetPosition(inkCanvas);
            var deltaX = currentPoint.X - resizeStartPoint.X;
            var deltaY = currentPoint.Y - resizeStartPoint.Y;

            ResizeUIElement(deltaX, deltaY);
            e.Handled = true;
        }

        private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isResizing)
            {
                isResizing = false;
                currentResizeDirection = ResizeDirection.None;

                var handle = sender as Rectangle;
                handle?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ResizeUIElement(double deltaX, double deltaY)
        {
            if (selectedUIElement == null) return;

            var newBounds = originalElementBounds;
            const double minSize = 20.0;

            switch (currentResizeDirection)
            {
                case ResizeDirection.TopLeft:
                    var newWidth = originalElementBounds.Width - deltaX;
                    var newHeight = originalElementBounds.Height - deltaY;
                    if (newWidth >= minSize && newHeight >= minSize)
                    {
                        newBounds.X = originalElementBounds.X + deltaX;
                        newBounds.Y = originalElementBounds.Y + deltaY;
                        newBounds.Width = newWidth;
                        newBounds.Height = newHeight;
                    }
                    break;

                case ResizeDirection.TopCenter:
                    var newHeightTC = originalElementBounds.Height - deltaY;
                    if (newHeightTC >= minSize)
                    {
                        newBounds.Y = originalElementBounds.Y + deltaY;
                        newBounds.Height = newHeightTC;
                    }
                    break;

                case ResizeDirection.TopRight:
                    var newWidthTR = originalElementBounds.Width + deltaX;
                    var newHeightTR = originalElementBounds.Height - deltaY;
                    if (newWidthTR >= minSize && newHeightTR >= minSize)
                    {
                        newBounds.Y = originalElementBounds.Y + deltaY;
                        newBounds.Width = newWidthTR;
                        newBounds.Height = newHeightTR;
                    }
                    break;

                case ResizeDirection.MiddleLeft:
                    var newWidthML = originalElementBounds.Width - deltaX;
                    if (newWidthML >= minSize)
                    {
                        newBounds.X = originalElementBounds.X + deltaX;
                        newBounds.Width = newWidthML;
                    }
                    break;

                case ResizeDirection.MiddleRight:
                    var newWidthMR = originalElementBounds.Width + deltaX;
                    if (newWidthMR >= minSize)
                    {
                        newBounds.Width = newWidthMR;
                    }
                    break;

                case ResizeDirection.BottomLeft:
                    var newWidthBL = originalElementBounds.Width - deltaX;
                    var newHeightBL = originalElementBounds.Height + deltaY;
                    if (newWidthBL >= minSize && newHeightBL >= minSize)
                    {
                        newBounds.X = originalElementBounds.X + deltaX;
                        newBounds.Width = newWidthBL;
                        newBounds.Height = newHeightBL;
                    }
                    break;

                case ResizeDirection.BottomCenter:
                    var newHeightBC = originalElementBounds.Height + deltaY;
                    if (newHeightBC >= minSize)
                    {
                        newBounds.Height = newHeightBC;
                    }
                    break;

                case ResizeDirection.BottomRight:
                    var newWidthBR = originalElementBounds.Width + deltaX;
                    var newHeightBR = originalElementBounds.Height + deltaY;
                    if (newWidthBR >= minSize && newHeightBR >= minSize)
                    {
                        newBounds.Width = newWidthBR;
                        newBounds.Height = newHeightBR;
                    }
                    break;
            }

            // 应用新的尺寸和位置
            ApplyUIElementBounds(selectedUIElement, newBounds);

            // 更新手柄位置
            UpdateResizeHandlesPosition(newBounds);
        }

        private void ApplyUIElementBounds(UIElement element, Rect bounds)
        {
            InkCanvas.SetLeft(element, bounds.X);
            InkCanvas.SetTop(element, bounds.Y);

            if (element is FrameworkElement fe)
            {
                fe.Width = bounds.Width;
                fe.Height = bounds.Height;
            }
        }

        private void UIElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            LogHelper.WriteLogToFile($"UIElement_MouseDown: 编辑模式={inkCanvas.EditingMode}, 元素类型={sender.GetType().Name}", LogHelper.LogType.Trace);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                var element = sender as UIElement;
                if (element != null)
                {
                    // 切换到选择模式并选择这个元素
                    inkCanvas.Select(new[] { element });
                    SelectUIElement(element);
                    LogHelper.WriteLogToFile($"UIElement_MouseDown: 选择了UI元素 {element.GetType().Name}", LogHelper.LogType.Trace);
                    e.Handled = true;
                }
            }
            else
            {
                LogHelper.WriteLogToFile($"UIElement_MouseDown: 编辑模式不是Select，无法选择UI元素", LogHelper.LogType.Trace);
            }
        }



        #endregion
    }
}