using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        #region Floating Control

        /// <summary>
        /// 存储最后一次鼠标按下的边界对象
        /// </summary>
        private object lastBorderMouseDownObject;

        /// <summary>
        /// 暂存待插入白板的克隆墨迹（在弹出目标选择菜单期间使用）
        /// </summary>
        private StrokeCollection _pendingInsertStrokes;

        /// <summary>
        /// 处理边界鼠标按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 如果发送者是 BoardRandomDrawToolBtn 或 BoardSingleDrawToolBtn，且它们被隐藏，则不处理事件
        /// 否则存储当前鼠标按下的对象
        /// </remarks>
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果发送者是 BoardRandomDrawToolBtn 或 BoardSingleDrawToolBtn，且它们被隐藏，则不处理事件
            if (sender is FrameworkElement element)
            {
                if ((BoardRandomDrawToolBtn != null && element == BoardRandomDrawToolBtn ||
                     BoardSingleDrawToolBtn != null && element == BoardSingleDrawToolBtn) &&
                    element.Visibility != Visibility.Visible)
                {
                    return;
                }
            }

            lastBorderMouseDownObject = sender;
        }

        /// <summary>
        /// 处理墨迹选择克隆鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 执行墨迹克隆操作并记录日志
        /// </remarks>
        private void BorderStrokeSelectionClone_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("克隆墨迹")) return;
            if (lastBorderMouseDownObject != sender) return;

            try
            {
                var strokes = inkCanvas.GetSelectedStrokes();
                if (strokes.Count > 0)
                {
                    // 直接执行克隆操作，与图片克隆保持一致
                    CloneStrokes(strokes);
                    LogHelper.WriteLogToFile($"墨迹克隆完成: {strokes.Count} 个墨迹");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"墨迹克隆失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理墨迹选择克隆到新画板鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 克隆选中的墨迹到新画板并清除当前选择
        /// </remarks>
        private void BorderStrokeSelectionCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("克隆墨迹到新页面")) return;
            if (lastBorderMouseDownObject != sender) return;

            var strokes = inkCanvas.GetSelectedStrokes();
            inkCanvas.Select(new StrokeCollection());
            CloneStrokesToNewBoard(strokes);
        }

        /// <summary>
        /// 处理墨迹选择插入白板鼠标释放事件
        /// 克隆选中墨迹，弹出目标选择菜单（小白板/常规白板），确认后插入
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void BorderStrokeSelectionInsertToWhiteboard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("插入墨迹到白板")) return;
            if (lastBorderMouseDownObject != sender) return;

            var strokes = inkCanvas.GetSelectedStrokes();
            if (strokes.Count == 0) return;

            // 参考ICA模式：先取消选中，再克隆（确保克隆出的墨迹不携带选中态）
            inkCanvas.Select(new StrokeCollection());
            _pendingInsertStrokes = strokes.Clone();

            // 在发送者附近弹出目标选择菜单
            if (sender is FrameworkElement fe)
            {
                InsertToWhiteboardPopup.PlacementTarget = fe;
                InsertToWhiteboardPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            }
            InsertToWhiteboardPopup.IsOpen = true;
        }

        /// <summary>
        /// 将暂存的墨迹插入常规白板（当前页面）
        /// 参考ICA的BorderStrokeSelectionCloneToBoardOrNewPage_Click模式
        /// </summary>
        internal void ExecuteInsertStrokesToRegularWhiteboard()
        {
            if (_pendingInsertStrokes == null || _pendingInsertStrokes.Count == 0) return;

            try
            {
                // 确保主画布上没有选中状态（防止选中态渲染污染克隆墨迹）
                inkCanvas.Select(new StrokeCollection());
                HideSelectionDisplay();
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (currentMode != 1)
                {
                    // 从屏幕批注模式切换到白板模式（内部会 save→clear→restore）
                    ImageBlackboard_MouseUp(null, null);
                }

                // 切换模式后，白板画布已就绪，此时加入克隆的墨迹
                inkCanvas.Strokes.Add(_pendingInsertStrokes);
                timeMachine.CommitStrokeUserInputHistory(_pendingInsertStrokes);

                LogHelper.WriteLogToFile($"墨迹插入常规白板完成: {_pendingInsertStrokes.Count} 个墨迹");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"墨迹插入常规白板失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _pendingInsertStrokes = null;
            }
        }

        /// <summary>
        /// 将暂存的墨迹插入小白板（浮窗）的当前页面
        /// </summary>
        internal void ExecuteInsertStrokesToMiniWhiteboard()
        {
            if (_pendingInsertStrokes == null || _pendingInsertStrokes.Count == 0) return;

            try
            {
                // 确保主画布上没有选中状态
                inkCanvas.Select(new StrokeCollection());
                HideSelectionDisplay();
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (_miniWhiteboardWindow == null || !_miniWhiteboardWindow.IsLoaded)
                {
                    OpenMiniWhiteboard();
                }
                else if (!_miniWhiteboardWindow.IsVisible)
                {
                    _miniWhiteboardWindow.Show();
                    _miniWhiteboardWindow.Activate();
                }

                _miniWhiteboardWindow.InsertStrokes(_pendingInsertStrokes);

                LogHelper.WriteLogToFile($"墨迹插入小白板完成: {_pendingInsertStrokes.Count} 个墨迹");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"墨迹插入小白板失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _pendingInsertStrokes = null;
            }
        }

        /// <summary>
        /// 处理墨迹选择删除鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 调用 SymbolIconDelete_MouseUp 方法执行删除操作
        /// </remarks>
        private void BorderStrokeSelectionDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;
            SymbolIconDelete_MouseUp(sender, e);
        }

        /// <summary>
        /// 处理笔宽减小鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 调用 ChangeStrokeThickness 方法减小笔宽
        /// </remarks>
        private void GridPenWidthDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("修改墨迹粗细")) return;
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(0.8);
        }

        /// <summary>
        /// 处理笔宽增大鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 调用 ChangeStrokeThickness 方法增大笔宽
        /// </remarks>
        private void GridPenWidthIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("修改墨迹粗细")) return;
            if (lastBorderMouseDownObject != sender) return;
            ChangeStrokeThickness(1.25);
        }

        /// <summary>
        /// 更改选中墨迹的粗细
        /// </summary>
        /// <param name="multipler">缩放倍数</param>
        /// <remarks>
        /// 对选中的每个墨迹应用缩放倍数
        /// 确保新的粗细在允许的范围内
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void ChangeStrokeThickness(double multipler)
        {
            if (TryBlockFrozenPageMutation("修改墨迹粗细")) return;
            foreach (var stroke in inkCanvas.GetSelectedStrokes())
            {
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

        /// <summary>
        /// 处理笔宽恢复默认鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 将选中墨迹的粗细恢复为默认值
        /// </remarks>
        private void GridPenWidthRestore_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("修改墨迹粗细")) return;
            if (lastBorderMouseDownObject != sender) return;

            foreach (var stroke in inkCanvas.GetSelectedStrokes())
            {
                stroke.DrawingAttributes.Width = inkCanvas.DefaultDrawingAttributes.Width;
                stroke.DrawingAttributes.Height = inkCanvas.DefaultDrawingAttributes.Height;
            }
        }

        /// <summary>
        /// 处理水平翻转鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 对选中的墨迹应用水平翻转变换
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void ImageFlipHorizontal_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("翻转墨迹")) return;
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

        /// <summary>
        /// 处理垂直翻转鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 对选中的墨迹应用垂直翻转变换
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void ImageFlipVertical_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("翻转墨迹")) return;
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

        // ... existing code ...
        /// <summary>
        /// 处理顺时针旋转45度鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 对选中的墨迹应用45度旋转变换
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void ImageRotate45_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("旋转墨迹")) return;
            if (lastBorderMouseDownObject != sender) return;

            var m = new Matrix();

            // Find center of element and then transform to get current location of center
            var fe = e.Source as FrameworkElement;
            var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
            center = new Point(inkCanvas.GetSelectionBounds().Left + inkCanvas.GetSelectionBounds().Width / 2,
                inkCanvas.GetSelectionBounds().Top + inkCanvas.GetSelectionBounds().Height / 2);
            center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

            // Update matrix to reflect translation/rotation
            m.RotateAt(45, center.X, center.Y); // 顺时针旋转45度

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

        /// <summary>
        /// 处理顺时针旋转90度鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 只有当鼠标按下和释放的是同一个对象时才处理
        /// 对选中的墨迹应用90度旋转变换
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void ImageRotate90_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("旋转墨迹")) return;
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

        /// <summary>
        /// 墨迹选择覆盖层鼠标按下状态
        /// </summary>
        private bool isGridInkCanvasSelectionCoverMouseDown;
        /// <summary>
        /// 墨迹拖动状态
        /// </summary>
        private bool isStrokeDragging = false;
        /// <summary>
        /// 墨迹拖动起始点
        /// </summary>
        private Point strokeDragStartPoint;
        /// <summary>
        /// 墨迹选择克隆集合
        /// </summary>
        private StrokeCollection StrokesSelectionClone = new StrokeCollection();

        /// <summary>
        /// 处理墨迹选择覆盖层鼠标按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 如果有选中的墨迹，检查点击位置是否在选择框边界内
        /// 如果在边界内，开始拖动墨迹
        /// 如果在边界外，取消选择
        /// </remarks>
        private void GridInkCanvasSelectionCover_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("移动墨迹"))
            {
                e.Handled = true;
                return;
            }

            var clickPoint = e.GetPosition(inkCanvas);
            var selectedStrokes = inkCanvas.GetSelectedStrokes();
            if (selectedStrokes.Count > 0)
            {
                var selectionBounds = inkCanvas.GetSelectionBounds();
                if (selectionBounds.Contains(clickPoint))
                {
                    // A click inside an existing stroke selection belongs to the stroke
                    // drag gesture, even when an SVG scene occupies the same area.
                    isGridInkCanvasSelectionCoverMouseDown = true;
                    isStrokeDragging = true;
                    strokeDragStartPoint = clickPoint;
                    GridInkCanvasSelectionCover.CaptureMouse();
                    GridInkCanvasSelectionCover.Cursor = Cursors.SizeAll;
                    e.Handled = true;
                    return;
                }
            }

            // Only let an SVG scene handle clicks outside the active stroke selection.
            var sceneElement = FindSecAgentSceneElementAtCanvasPoint(clickPoint);
            if (sceneElement != null)
            {
                isGridInkCanvasSelectionCoverMouseDown = false;
                isStrokeDragging = false;
                GridInkCanvasSelectionCover.ReleaseMouseCapture();
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                Element_MouseLeftButtonDown(sceneElement, e);
                e.Handled = true;
                return;
            }

            isGridInkCanvasSelectionCoverMouseDown = selectedStrokes.Count > 0;
            isStrokeDragging = false;
            if (selectedStrokes.Count > 0)
            {
                // Clicking outside the selection clears the stroke selection.
                inkCanvas.Select(new StrokeCollection());
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }

            e.Handled = true;
        }

        /// <summary>
        /// 处理墨迹选择覆盖层鼠标移动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 如果正在拖动墨迹，执行拖动操作
        /// 如果鼠标在选中区域移动，更新墨迹选中栏位置
        /// </remarks>
        private FrameworkElement FindSecAgentSceneElementAtCanvasPoint(Point canvasPoint)
        {
            if (inkCanvas == null) return null;

            foreach (var child in inkCanvas.Children.OfType<FrameworkElement>().Reverse())
            {
                if (!IsSecAgentEditableSceneElement(child) && !IsSecAgentEditableSceneGroup(child))
                    continue;

                var bounds = GetSceneElementBounds(child);
                if (bounds.Contains(canvasPoint))
                {
                    return child;
                }
            }

            return null;
        }

        private void GridInkCanvasSelectionCover_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isGridInkCanvasSelectionCoverMouseDown) return;

            // 如果正在拖动墨迹，执行拖动操作
            if (isStrokeDragging && GridInkCanvasSelectionCover.IsMouseCaptured)
            {
                var currentPoint = e.GetPosition(inkCanvas);
                var delta = currentPoint - strokeDragStartPoint;

                // 创建变换矩阵
                var matrix = new Matrix();
                matrix.Translate(delta.X, delta.Y);

                // 对选中的墨迹应用变换
                var selectedStrokes = inkCanvas.GetSelectedStrokes();
                foreach (var stroke in selectedStrokes)
                {
                    stroke.Transform(matrix, false);
                }

                // 更新选中栏、选择框和旋转锚点位置
                updateBorderStrokeSelectionControlLocation();
                UpdateSelectionDisplay();

                // 更新起始点
                strokeDragStartPoint = currentPoint;
            }
            else if (inkCanvas.GetSelectedStrokes().Count > 0)
            {
                // 当鼠标在选中区域移动时，更新墨迹选中栏位置
                updateBorderStrokeSelectionControlLocation();
            }
        }

        /// <summary>
        /// 处理墨迹选择覆盖层鼠标释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 结束墨迹拖动
        /// 只有在没有选中墨迹时才隐藏选中栏
        /// </remarks>
        private void GridInkCanvasSelectionCover_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isGridInkCanvasSelectionCoverMouseDown) return;

            // 结束墨迹拖动
            if (isStrokeDragging)
            {
                isStrokeDragging = false;
                GridInkCanvasSelectionCover.ReleaseMouseCapture();
                GridInkCanvasSelectionCover.Cursor = Cursors.Arrow;
            }

            isGridInkCanvasSelectionCoverMouseDown = false;

            // 只有在没有选中墨迹时才隐藏选中栏
            if (inkCanvas.GetSelectedStrokes().Count == 0)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                HideSelectionDisplay();
            }
            else
            {
                CommitPendingStrokeManipulationHistory();
                RefreshStrokeSelectionChrome();
            }
        }

        /// <summary>
        /// 处理选择按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 如果当前是选择模式，检查是否全选
        /// 如果全选，则切换到墨迹模式再切换回选择模式
        /// 如果不是全选，则选择所有有效墨迹
        /// 如果当前不是选择模式，则切换到选择模式
        /// </remarks>
        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = true;
            drawingShapeMode = 0;
            // 视频展台特殊模式下必须保持 IsManipulationEnabled=true，
            // 否则 WPF 不会把触摸提升为 Manipulation 事件，VideoPresenterSpecialMode_ManipulationDelta
            // 收不到拖动/缩放事件，用户切到"选择"后无法拖动摄像头画面（Q7 根因之二）。
            // 普通模式下仍然设为 false（保持原有行为：选择模式下不让 InkCanvas 触发 Manipulation）。
            if (!_isVideoPresenterSpecialMode)
            {
                inkCanvas.IsManipulationEnabled = false;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                if (inkCanvas.GetSelectedStrokes().Count == inkCanvas.Strokes.Count)
                {
                    // 使用集中化的工具模式切换方法
                    SetCurrentToolMode(InkCanvasEditingMode.Ink,
                        () => { SetCurrentToolMode(InkCanvasEditingMode.Select); });
                }
                else
                {
                    var selectedStrokes = new StrokeCollection();
                    foreach (var stroke in inkCanvas.Strokes)
                        if (stroke.GetBounds().Width > 0 && stroke.GetBounds().Height > 0)
                            selectedStrokes.Add(stroke);
                    inkCanvas.Select(selectedStrokes);
                }
            }
            else
            {
                // 使用集中化的工具模式切换方法
                SetCurrentToolMode(InkCanvasEditingMode.Select);
            }
        }

        /// <summary>
        /// 墨迹选择控件宽度
        /// </summary>
        private double BorderStrokeSelectionControlWidth = 490.0;
        /// <summary>
        /// 墨迹选择控件高度
        /// </summary>
        private double BorderStrokeSelectionControlHeight = 80.0;
        /// <summary>
        /// 程序更改墨迹选择状态
        /// </summary>
        private bool isProgramChangeStrokeSelection;

        /// <summary>
        /// 处理墨迹画布选择更改事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 优先检查墨迹选择状态
        /// 如果有墨迹被选中，显示墨迹选择栏和选择框
        /// 如果有图片元素被选中，不显示选择框
        /// 如果没有选中任何内容，隐藏选择框
        /// </remarks>
        private void inkCanvas_SelectionChanged(object sender, EventArgs e)
        {
            if (isProgramChangeStrokeSelection) return;

            var selectedStrokes = inkCanvas.GetSelectedStrokes();
            var rawSelectedElements = inkCanvas.GetSelectedElements();

            var validElements = rawSelectedElements
                .OfType<FrameworkElement>()
                .Where(el => IsBitmapLikeCanvasElement(el))
                .ToList();

            // 如果套索圈中了墨迹或元素
            if ((selectedStrokes != null && selectedStrokes.Count > 0) || validElements.Count > 0)
            {
                _selectedElements.Clear();
                foreach (var el in validElements)
                {
                    _selectedElements.Add(el);
                }

                currentSelectedElement = (_selectedElements.Count == 1 && (selectedStrokes == null || selectedStrokes.Count == 0))
                    ? _selectedElements.First()
                    : null;

                BorderStrokeSelectionClone.Background = Brushes.Transparent;
                UpdateSelectionDisplay();
                return;
            }

            // 如果套索没圈中内容，但此时有之前点选的元素
            if (_selectedElements.Count > 0)
            {
                UpdateSelectionDisplay();
                return;
            }

            // 没有选中任何内容，隐藏选择框与工具栏
            ClearAllSelection();
        }


        /// <summary>
        /// 更新墨迹选中栏位置
        /// </summary>
        /// <remarks>
        /// 计算墨迹选中栏的位置，确保在墨迹下方显示
        /// 如果选中栏会超出屏幕底部，则显示在墨迹上方
        /// 如果上方也没有空间，则显示在顶部
        /// </remarks>
        private void updateBorderStrokeSelectionControlLocation()
        {
            var bounds = GetCompositeSelectionBounds();
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = inkCanvas.GetSelectionBounds();
            }

            var borderLeft = (bounds.Left + bounds.Right - BorderStrokeSelectionControlWidth) / 2;
            var borderTop = bounds.Bottom + 10; // 在选区下方10像素处显示
            if (borderLeft < 0) borderLeft = 0;
            if (borderTop < 0) borderTop = 0;
            if (Width - borderLeft < BorderStrokeSelectionControlWidth || double.IsNaN(borderLeft))
                borderLeft = Width - BorderStrokeSelectionControlWidth;
            if (Height - borderTop < BorderStrokeSelectionControlHeight || double.IsNaN(borderTop))
                borderTop = Height - BorderStrokeSelectionControlHeight;

            // 确保选中栏始终显示在选区下方；如果超出屏幕底部，则显示在上方
            if (borderTop + BorderStrokeSelectionControlHeight > Height)
            {
                borderTop = bounds.Top - BorderStrokeSelectionControlHeight - 10;
                if (borderTop < 0) borderTop = 10;
            }

            BorderStrokeSelectionControl.Margin = new Thickness(borderLeft, borderTop, 0, 0);
        }
        /// <summary>
        /// 处理墨迹选择覆盖层操作开始事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作开始事件参数</param>
        /// <remarks>
        /// 设置操作模式为所有模式
        /// </remarks>
        private void GridInkCanvasSelectionCover_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.Mode = ManipulationModes.All;
        }

        /// <summary>
        /// 处理墨迹选择覆盖层操作完成事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作完成事件参数</param>
        /// <remarks>
        /// 如果有 StrokeManipulationHistory，则提交历史记录
        /// 如果有 DrawingAttributesHistory，则提交历史记录
        /// </remarks>
        private void GridInkCanvasSelectionCover_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            CommitPendingStrokeManipulationHistory();

            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }

            RefreshStrokeSelectionChrome();
        }

        /// <summary>
        /// 处理选择覆盖层的操作增量事件，将来自触摸或操作的平移、缩放和旋转应用到当前选中的墨迹上并更新选择控件位置。
        /// </summary>
        /// <param name="sender">触发事件的元素（通常为选择覆盖层）。</param>
        /// <param name="e">包含平移、缩放和旋转增量的 ManipulationDeltaEventArgs。</param>
        /// <remarks>
        /// - 当只有单指触摸且已有选中墨迹时，不在此处处理拖动（由 TouchMove 处理）。
        /// - 三指及以上触摸时禁用缩放。 
        /// - 若 StrokesSelectionClone 非空，则对其内的墨迹应用变换；否则在允许两指旋转时也会应用旋转变换。 
        /// - 处理完成后会刷新并更新边框/选择控件的位置。
        /// </remarks>
        private void GridInkCanvasSelectionCover_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (TryBlockFrozenPageMutation("移动或缩放墨迹"))
            {
                e.Handled = true;
                return;
            }
            try
            {
                if (dec.Count >= 1)
                {
                    // 单指时，让TouchMove处理拖动
                    if (dec.Count == 1 && inkCanvas.GetSelectedStrokes().Count > 0)
                    {
                        return;
                    }

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

                    // 应用变换到选中的墨迹
                    foreach (var stroke in strokes)
                    {
                        stroke.Transform(m, false);
                    }

                    updateBorderStrokeSelectionControlLocation();
                    UpdateSelectionDisplay();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"墨迹ManipulationDelta错误: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理选区覆盖层的触摸按下事件：记录触摸设备 ID，并在第一个触点时保存选择中心与用于拖拽的初始触点位置。
        /// </summary>
        /// <param name="sender">事件源（触摸事件的发送者）。</param>
        /// <param name="e">触摸事件参数，包含触点位置与设备 ID。</param>
        private void GridInkCanvasSelectionCover_TouchDown(object sender, TouchEventArgs e)
        {
            if (TryBlockFrozenPageMutation("移动墨迹"))
            {
                e.Handled = true;
                return;
            }
            dec.Add(e.TouchDevice.Id);
            //设备1个的时候，记录中心点
            if (dec.Count == 1)
            {
                var touchPoint = e.GetTouchPoint(null);
                centerPoint = touchPoint.Position;
                lastTouchPointOnGridInkCanvasCover = touchPoint.Position;

                var touchPointInCanvas = e.GetTouchPoint(inkCanvas);
                lastDragPointInCanvas = touchPointInCanvas.Position;
            }
        }

        /// <summary>
        /// 处理选区覆盖层的触摸结束事件：更新触摸跟踪状态并根据触摸位置和当前选区状态显示或隐藏选区覆盖层与克隆集合。
        /// </summary>
        /// <param name="sender">触发事件的对象（通常为选区覆盖层）。</param>
        /// <param name="e">触摸事件参数，包含触点信息。</param>
        private void GridInkCanvasSelectionCover_TouchUp(object sender, TouchEventArgs e)
        {
            dec.Remove(e.TouchDevice.Id);
            if (dec.Count >= 1) return;
            isProgramChangeStrokeSelection = false;

            lastDragPointInCanvas = new Point(0, 0);

            var touchUpPoint = e.GetTouchPoint(null).Position;
            if (lastTouchPointOnGridInkCanvasCover == touchUpPoint)
            {
                var touchPointInCanvas = e.GetTouchPoint(inkCanvas).Position;
                var selectionBounds = inkCanvas.GetSelectionBounds();

                if (selectionBounds.Contains(touchPointInCanvas))
                {
                    StrokesSelectionClone = new StrokeCollection();
                    CommitPendingStrokeManipulationHistory();
                    RefreshStrokeSelectionChrome();
                    return;
                }

                isProgramChangeStrokeSelection = true;
                inkCanvas.Select(new StrokeCollection());
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                isProgramChangeStrokeSelection = false;
                StrokesSelectionClone = new StrokeCollection();
                HideSelectionDisplay();
            }
            else if (inkCanvas.GetSelectedStrokes().Count == 0)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                StrokesSelectionClone = new StrokeCollection();
                HideSelectionDisplay();
            }
            else
            {
                StrokesSelectionClone = new StrokeCollection();
                CommitPendingStrokeManipulationHistory();
                RefreshStrokeSelectionChrome();
            }
        }

        /// <summary>
        /// 处理触摸移动事件，按单指拖动并平移当前选中的墨迹，同时更新选择控件的位置。
        /// </summary>
        /// <remarks>仅在存在选中墨迹且当前仅有一个触摸点时生效；若起始触摸点未记录（lastDragPointInCanvas 为 (0,0)）则不移动。只有当触摸位移在任一方向超过 1 像素时，才对每条选中墨迹应用平移变换，并更新选择控件位置与最后触摸点。</remarks>
        private void GridInkCanvasSelectionCover_TouchMove(object sender, TouchEventArgs e)
        {
            if (TryBlockFrozenPageMutation("移动墨迹"))
            {
                e.Handled = true;
                return;
            }
            // 处理触摸移动事件 - 用于拖动选中的墨迹
            if (inkCanvas.GetSelectedStrokes().Count > 0 && dec.Count == 1)
            {
                var currentTouchPoint = e.GetTouchPoint(inkCanvas).Position;

                // 检查是否有有效的起始触摸点
                if (lastDragPointInCanvas != new Point(0, 0))
                {
                    var delta = currentTouchPoint - lastDragPointInCanvas;

                    // 只有当移动距离足够大时才进行拖动（避免微小移动造成的抖动）
                    if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
                    {
                        // 创建变换矩阵
                        var matrix = new Matrix();
                        matrix.Translate(delta.X, delta.Y);

                        // 对选中的墨迹应用变换
                        var selectedStrokes = inkCanvas.GetSelectedStrokes();
                        foreach (var stroke in selectedStrokes)
                        {
                            stroke.Transform(matrix, false);
                        }

                        // 更新选中栏、选择框和旋转锚点位置
                        updateBorderStrokeSelectionControlLocation();
                        UpdateSelectionDisplay();

                        // 更新最后触摸点
                        lastDragPointInCanvas = currentTouchPoint;
                    }
                }
            }
        }

        private Point lastTouchPointOnGridInkCanvasCover = new Point(0, 0);
        private Point lastDragPointInCanvas = new Point(0, 0);

        /// <summary>
        /// 切换到选择（套索）工具模式并同步光标显示。
        /// </summary>
        /// <remarks>
        /// 同时取消强制橡皮擦和点式橡皮擦状态，并将绘制形状模式重置为默认（0）。
        /// </remarks>
        private void LassoSelect_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.Select);
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        /// <summary>
        /// 处理套索选择按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 设置工具模式为选择模式
        /// 启用墨迹画布的操作支持
        /// 设置光标为选择模式光标
        /// </remarks>
        private void BtnLassoSelect_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.Select);
            inkCanvas.IsManipulationEnabled = false;
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        #region UIElement Selection and Resize

        /// <summary>
        /// 获取UI元素的边界
        /// </summary>
        /// <param name="element">UI元素</param>
        /// <returns>UI元素的边界矩形</returns>
        /// <remarks>
        /// 如果元素是FrameworkElement，获取其位置和大小
        /// 如果元素有RenderTransform，尝试使用变换后的边界
        /// 如果变换失败，回退到简单计算
        /// 如果元素不是FrameworkElement，返回空矩形
        /// </remarks>
        private Rect GetUIElementBounds(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                var left = InkCanvas.GetLeft(element);
                var top = InkCanvas.GetTop(element);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                var width = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                var height = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;

                // 检查是否有RenderTransform
                if (fe.RenderTransform != null && fe.RenderTransform != Transform.Identity)
                {
                    try
                    {
                        // 如果有变换，使用变换后的边界
                        var transform = element.TransformToAncestor(inkCanvas);
                        var elementBounds = new Rect(0, 0, width, height);
                        var transformedBounds = transform.TransformBounds(elementBounds);
                        return transformedBounds;
                    }
                    catch
                    {
                        // 变换失败时回退到简单计算
                        return new Rect(left, top, width, height);
                    }
                }

                // 没有变换时直接使用位置和大小
                return new Rect(left, top, width, height);
            }

            return new Rect(0, 0, 0, 0);
        }

        #endregion

        #region Selection Display and Resize Handles
        /// <summary>
        /// 当前复合选区中包含的所有非墨迹媒体元素（图片、视频、PDF等）
        /// </summary>
        public readonly HashSet<FrameworkElement> _selectedElements = new();

        public class ElementTransformSnapshot
        {
            public double BaseLeft { get; set; }
            public double BaseTop { get; set; }
            public double ScaleX { get; set; }
            public double ScaleY { get; set; }
            public double TranslateX { get; set; }
            public double TranslateY { get; set; }
            public double RotateAngle { get; set; }
            public Point VisualCenterCanvas { get; set; }
            public Size VisualSize { get; set; }
        }

        private readonly Dictionary<FrameworkElement, ElementTransformSnapshot> _elementInitialStates = new();
        private Rect _compositeTransformInitialBounds;
        private Point _compositeTransformPivot;
        private Vector _accumulatedCompositeResizeDelta;
        private Vector _accumulatedCompositeMoveDelta;
        private double _accumulatedCompositeRotateAngle;
        private ImageResizeCorner? _activeCompositeResizeCorner;

        private void GridInkCanvasSelectionCover_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (GridInkCanvasSelectionCover.IsVisible && inkCanvas.GetSelectedStrokes().Count > 0)
            {
                updateBorderStrokeSelectionControlLocation();
                UpdateSelectionDisplay();
            }
            else if (currentSelectedElement == null)
            {
                HideSelectionDisplay();
            }
        }

        private void RefreshStrokeSelectionChrome()
        {
            if (inkCanvas.GetSelectedStrokes().Count == 0)
            {
                HideSelectionDisplay();
                return;
            }

            GridInkCanvasSelectionCover.Visibility = Visibility.Visible;
            updateBorderStrokeSelectionControlLocation();
            UpdateSelectionDisplay();
        }

        /// <summary>
        /// 计算当前复合选区（选中的墨迹 + 选中的所有媒体元素）的联合正外接矩形
        /// </summary>
        public Rect GetCompositeSelectionBounds()
        {
            Rect unionBounds = Rect.Empty;

            var strokes = inkCanvas?.GetSelectedStrokes();
            if (strokes != null && strokes.Count > 0)
            {
                var strokeBounds = inkCanvas.GetSelectionBounds();
                if (!strokeBounds.IsEmpty && strokeBounds.Width > 0 && strokeBounds.Height > 0)
                {
                    unionBounds = strokeBounds;
                }
            }

            foreach (var elem in _selectedElements)
            {
                if (elem == null || elem.Visibility != Visibility.Visible) continue;
                var b = GetUIElementBounds(elem);
                if (!b.IsEmpty && b.Width > 0 && b.Height > 0)
                {
                    unionBounds = unionBounds.IsEmpty ? b : Rect.Union(unionBounds, b);
                }
            }

            return unionBounds;
        }

        /// <summary>
        /// 在画布上显示当前复合选区的统一选择框与工具栏（与图片/墨迹共用 ImageSelectionOverlay）。
        /// </summary>
        private void UpdateSelectionDisplay()
        {
            UpdateCompositeSelectionOverlay();
        }

        /// <summary>
        /// 隐藏选区选择框与工具栏。
        /// </summary>
        private void HideSelectionDisplay()
        {
            HideStrokeSelectionOverlay();
            HideImageResizeHandles();
        }

        private void UpdateStrokeSelectionOverlay()
        {
            UpdateCompositeSelectionOverlay();
        }

        public void UpdateCompositeSelectionOverlay()
        {
            try
            {
                if (inkCanvas == null || ImageSelectionOverlay == null) return;

                var bounds = GetCompositeSelectionBounds();
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    HideSelectionDisplay();
                    return;
                }

                EnsureImageOverlayHooks();

                var strokes = inkCanvas.GetSelectedStrokes();
                bool hasStrokes = strokes != null && strokes.Count > 0;

                // 仅选中单张元素且无墨迹时：跟随该元素局部旋转与专属图片工具栏
                if (_selectedElements.Count == 1 && !hasStrokes)
                {
                    var singleElem = _selectedElements.First();
                    currentSelectedElement = singleElem;
                    AttachOverlayTracking(singleElem);
                    UpdateImageResizeHandlesPosition(default);
                    ImageSelectionOverlay.Visibility = Visibility.Visible;

                    if (BorderImageSelectionControl != null)
                    {
                        BorderImageSelectionControl.Visibility = Visibility.Visible;
                        UpdateImageSelectionToolbarPosition(singleElem);
                    }
                    if (BorderStrokeSelectionControl != null)
                    {
                        BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;
                    }

                    SuppressNativeInkCanvasSelectionAdorners();
                    return;
                }

                // 复合选区（墨迹+图片、或多图片、或纯墨迹）
                DetachOverlayTracking();
                var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                ImageSelectionOverlay.SetMode(SelectionOverlayMode.Element);
                ImageSelectionOverlay.UpdateFrame(center, bounds.Width, bounds.Height, 0);
                ImageSelectionOverlay.Visibility = Visibility.Visible;

                if (BorderStrokeSelectionControl != null)
                {
                    BorderStrokeSelectionControl.Visibility = Visibility.Visible;
                    updateBorderStrokeSelectionControlLocation();
                }
                if (BorderImageSelectionControl != null)
                {
                    BorderImageSelectionControl.Visibility = Visibility.Collapsed;
                }

                SuppressNativeInkCanvasSelectionAdorners();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新复合选区失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void HideStrokeSelectionOverlay()
        {
            try
            {
                if (BorderStrokeSelectionControl != null)
                {
                    BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;
                }
                if (_selectedElements.Count == 0 && currentSelectedElement == null && ImageSelectionOverlay != null)
                {
                    ImageSelectionOverlay.Visibility = Visibility.Collapsed;
                }
                if (GridInkCanvasSelectionCover != null)
                {
                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏墨迹选中框失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 全局清除所有选择状态（包括墨迹和图片），并关闭所有选择框与工具栏
        /// </summary>
        public void ClearAllSelection()
        {
            try
            {
                // 1. 清除图片/元素选择
                if (_selectedElements.Count > 0 || currentSelectedElement != null)
                {
                    var elements = _selectedElements.ToList();
                    _selectedElements.Clear();
                    currentSelectedElement = null;
                    foreach (var elem in elements)
                    {
                        try { UnselectElement(elem); } catch { }
                    }
                }

                // 2. 清除墨迹选择
                if (inkCanvas != null && inkCanvas.GetSelectedStrokes().Count > 0)
                {
                    isProgramChangeStrokeSelection = true;
                    try
                    {
                        inkCanvas.Select(new StrokeCollection());
                    }
                    finally
                    {
                        isProgramChangeStrokeSelection = false;
                    }
                }

                // 3. 隐藏新旧选择框与浮动栏
                HideSelectionDisplay();
                HideImageResizeHandles();

                if (BorderStrokeSelectionControl != null)
                    BorderStrokeSelectionControl.Visibility = Visibility.Collapsed;

                if (BorderImageSelectionControl != null)
                    BorderImageSelectionControl.Visibility = Visibility.Collapsed;

                if (ImageSelectionOverlay != null)
                    ImageSelectionOverlay.Visibility = Visibility.Collapsed;

                if (GridInkCanvasSelectionCover != null)
                    GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                // 4. 重置缓存
                StrokesSelectionClone?.Clear();
                _strokeTransformInitialPoints?.Clear();
                ResetCompositeTransformBaseline();

                // 5. 再次抑制原生选择修饰器
                SuppressNativeInkCanvasSelectionAdorners();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Selection] ClearAllSelection 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 为复合选区（选中的墨迹与所有选中元素）建立交互变换基准快照。
        /// </summary>
        public void PrepareCompositeTransformBaseline()
        {
            if (inkCanvas == null) return;

            // 1. 墨迹快照
            _strokeTransformInitialPoints.Clear();
            var selectedStrokes = inkCanvas.GetSelectedStrokes();
            if (selectedStrokes != null)
            {
                foreach (var stroke in selectedStrokes)
                {
                    _strokeTransformInitialPoints[stroke] = stroke.StylusPoints.Clone();
                }
            }

            // 2. 元素快照
            _elementInitialStates.Clear();
            foreach (var elem in _selectedElements)
            {
                if (elem == null) continue;
                var (ox, oy, visW, visH) = GetElementVisualBox(elem);
                double left = InkCanvas.GetLeft(elem); if (double.IsNaN(left)) left = 0;
                double top = InkCanvas.GetTop(elem); if (double.IsNaN(top)) top = 0;

                double sx = 1, sy = 1, tx = 0, ty = 0, angle = 0;
                if (elem.RenderTransform is TransformGroup tg)
                {
                    var st = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                    var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                    var rt = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                    if (st != null) { sx = st.ScaleX; sy = st.ScaleY; }
                    if (tt != null) { tx = tt.X; ty = tt.Y; }
                    if (rt != null) { angle = rt.Angle; }
                }

                Point visualCenterLocal = new Point(ox + visW / 2, oy + visH / 2);
                Point centerCanvas;
                try
                {
                    centerCanvas = elem.TransformToAncestor(inkCanvas).Transform(visualCenterLocal);
                }
                catch
                {
                    centerCanvas = new Point(left + tx + (ox + visW / 2) * sx, top + ty + (oy + visH / 2) * sy);
                }

                _elementInitialStates[elem] = new ElementTransformSnapshot
                {
                    BaseLeft = left,
                    BaseTop = top,
                    ScaleX = sx,
                    ScaleY = sy,
                    TranslateX = tx,
                    TranslateY = ty,
                    RotateAngle = angle,
                    VisualCenterCanvas = centerCanvas,
                    VisualSize = new Size(visW, visH)
                };
            }

            // 3. 初始外接包围盒
            _compositeTransformInitialBounds = GetCompositeSelectionBounds();
            _accumulatedCompositeResizeDelta = new Vector(0, 0);
            _accumulatedCompositeMoveDelta = new Vector(0, 0);
            _accumulatedCompositeRotateAngle = 0;
            _activeCompositeResizeCorner = null;
        }

        public void ResetCompositeTransformBaseline()
        {
            _elementInitialStates.Clear();
            _compositeTransformInitialBounds = Rect.Empty;
            _accumulatedCompositeResizeDelta = new Vector(0, 0);
            _accumulatedCompositeMoveDelta = new Vector(0, 0);
            _accumulatedCompositeRotateAngle = 0;
            _activeCompositeResizeCorner = null;
        }

        /// <summary>
        /// 抑制 WPF InkCanvas 内部原生选择修饰器（InkCanvasSelectionAdorner），
        /// 仅隐藏墨迹选中的原生把手/选择框，保持 AdornerLayer 本身可见，
        /// 避免破坏套索选择（Lasso Selection）的橡皮筋交互。
        /// </summary>
        /// <remarks>
        /// 重要：只能用 Opacity=0 隐藏，绝不能设置 Visibility=Collapsed 或 IsHitTestVisible=false。
        /// 实测（.NET 10 WPF，最小复现注入鼠标套索验证）：
        ///   - Visibility=Collapsed 或 IsHitTestVisible=false 会让 InkCanvas 的套索状态机
        ///     完全无法启动（StartLasso 不执行）——拖动无虚线反馈、松手不触发 SelectionChanged、
        ///     GetSelectedStrokes 恒为空，触屏与鼠标同时失效；
        ///   - 仅 Opacity=0 时套索功能完整（LassoHelper 的点串视觉挂在 InkPresenter 上，
        ///     不经过该 Adorner），原生选中框/把手不可见，由 ImageSelectionOverlay 接管。
        /// </remarks>
        private void SuppressNativeInkCanvasSelectionAdorners()
        {
            if (inkCanvas == null) return;
            try
            {
                SuppressVisualTreeAdorners(inkCanvas);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Ink] 抑制原生选择修饰器失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void SuppressVisualTreeAdorners(DependencyObject parent)
        {
            if (parent == null) return;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Documents.AdornerLayer adornerLayer)
                {
                    if (adornerLayer.Visibility != Visibility.Visible)
                        adornerLayer.Visibility = Visibility.Visible;
                    if (adornerLayer.Opacity < 1.0)
                        adornerLayer.Opacity = 1.0;
                    if (!adornerLayer.IsHitTestVisible)
                        adornerLayer.IsHitTestVisible = true;

                    int adornerCount = VisualTreeHelper.GetChildrenCount(adornerLayer);
                    for (int j = 0; j < adornerCount; j++)
                    {
                        var adornerChild = VisualTreeHelper.GetChild(adornerLayer, j);
                        if (adornerChild is System.Windows.Documents.Adorner adorner)
                        {
                            var typeName = adorner.GetType().Name;
                            if (typeName.Contains("SelectionAdorner") || typeName.Contains("InkCanvasSelection"))
                            {
                                // 仅用透明度隐藏；Collapsed/IsHitTestVisible=false 会杀死套索（见方法注释）
                                adorner.Opacity = 0;
                            }
                        }
                    }
                }
                else if (child is System.Windows.Documents.Adorner adorner)
                {
                    var typeName = adorner.GetType().Name;
                    if (typeName.Contains("SelectionAdorner") || typeName.Contains("InkCanvasSelection"))
                    {
                        adorner.Opacity = 0;
                    }
                }
                SuppressVisualTreeAdorners(child);
            }
        }

        public void MoveCompositeSelection(Vector delta)
        {
            if (TryBlockFrozenPageMutation("移动")) return;
            try
            {
                var strokes = inkCanvas?.GetSelectedStrokes();
                bool hasStrokes = strokes != null && strokes.Count > 0;
                bool hasElements = _selectedElements.Count > 0;
                if (!hasStrokes && !hasElements) return;

                if (_strokeTransformInitialPoints.Count == 0 && _elementInitialStates.Count == 0)
                {
                    PrepareCompositeTransformBaseline();
                }

                _accumulatedCompositeMoveDelta += delta;

                // 1. 平移墨迹
                if (hasStrokes)
                {
                    foreach (var stroke in strokes)
                    {
                        if (_strokeTransformInitialPoints.TryGetValue(stroke, out var initialPoints))
                        {
                            var newPoints = new StylusPointCollection();
                            for (int i = 0; i < initialPoints.Count; i++)
                            {
                                newPoints.Add(new StylusPoint(
                                    initialPoints[i].X + _accumulatedCompositeMoveDelta.X,
                                    initialPoints[i].Y + _accumulatedCompositeMoveDelta.Y,
                                    initialPoints[i].PressureFactor));
                            }
                            stroke.StylusPoints = newPoints;
                        }
                    }
                }

                // 2. 平移元素
                foreach (var (elem, snapshot) in _elementInitialStates)
                {
                    if (elem.RenderTransform is TransformGroup tg)
                    {
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (tt != null)
                        {
                            tt.X = snapshot.TranslateX + _accumulatedCompositeMoveDelta.X;
                            tt.Y = snapshot.TranslateY + _accumulatedCompositeMoveDelta.Y;
                        }
                    }
                }

                UpdateCompositeSelectionOverlay();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复合移动失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public void RotateCompositeSelection(double angleDelta)
        {
            if (TryBlockFrozenPageMutation("旋转")) return;
            try
            {
                var strokes = inkCanvas?.GetSelectedStrokes();
                bool hasStrokes = strokes != null && strokes.Count > 0;
                bool hasElements = _selectedElements.Count > 0;
                if (!hasStrokes && !hasElements) return;

                if (_compositeTransformInitialBounds.IsEmpty || (_strokeTransformInitialPoints.Count == 0 && _elementInitialStates.Count == 0))
                {
                    PrepareCompositeTransformBaseline();
                }

                _accumulatedCompositeRotateAngle += angleDelta;

                var center = new Point(
                    _compositeTransformInitialBounds.Left + _compositeTransformInitialBounds.Width / 2,
                    _compositeTransformInitialBounds.Top + _compositeTransformInitialBounds.Height / 2);

                var matrix = new Matrix();
                matrix.RotateAt(_accumulatedCompositeRotateAngle, center.X, center.Y);

                // 1. 旋转墨迹
                if (hasStrokes)
                {
                    foreach (var stroke in strokes)
                    {
                        if (_strokeTransformInitialPoints.TryGetValue(stroke, out var initialPoints))
                        {
                            var newPoints = new StylusPointCollection();
                            for (int i = 0; i < initialPoints.Count; i++)
                            {
                                var pt = matrix.Transform(new Point(initialPoints[i].X, initialPoints[i].Y));
                                newPoints.Add(new StylusPoint(pt.X, pt.Y, initialPoints[i].PressureFactor));
                            }
                            stroke.StylusPoints = newPoints;
                        }
                    }
                }

                // 2. 旋转元素：公转围绕 Center，自转增加角度
                foreach (var (elem, snapshot) in _elementInitialStates)
                {
                    if (elem.RenderTransform is TransformGroup tg)
                    {
                        var st = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var rt = tg.Children.OfType<RotateTransform>().FirstOrDefault();

                        if (tt != null && rt != null)
                        {
                            Point newCenter = matrix.Transform(snapshot.VisualCenterCanvas);

                            var (ox, oy, visW, visH) = GetElementVisualBox(elem);
                            double curSx = st?.ScaleX ?? snapshot.ScaleX;
                            double curSy = st?.ScaleY ?? snapshot.ScaleY;
                            double preRotationCenterX = snapshot.BaseLeft + (ox + visW / 2) * curSx;
                            double preRotationCenterY = snapshot.BaseTop + (oy + visH / 2) * curSy;

                            tt.X = newCenter.X - preRotationCenterX;
                            tt.Y = newCenter.Y - preRotationCenterY;

                            rt.Angle = (snapshot.RotateAngle + _accumulatedCompositeRotateAngle) % 360.0;
                        }
                    }
                }

                UpdateCompositeSelectionOverlay();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复合旋转失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public void ResizeCompositeSelectionByCorner(Vector canvasDelta, ImageResizeCorner corner, bool lockAspect)
        {
            if (TryBlockFrozenPageMutation("缩放")) return;
            try
            {
                var strokes = inkCanvas?.GetSelectedStrokes();
                bool hasStrokes = strokes != null && strokes.Count > 0;
                bool hasElements = _selectedElements.Count > 0;
                if (!hasStrokes && !hasElements) return;

                if (_compositeTransformInitialBounds.IsEmpty || (_strokeTransformInitialPoints.Count == 0 && _elementInitialStates.Count == 0))
                {
                    PrepareCompositeTransformBaseline();
                }

                if (_compositeTransformInitialBounds.IsEmpty || _compositeTransformInitialBounds.Width <= 0 || _compositeTransformInitialBounds.Height <= 0)
                {
                    return;
                }

                if (_activeCompositeResizeCorner != corner)
                {
                    _activeCompositeResizeCorner = corner;
                    switch (corner)
                    {
                        case ImageResizeCorner.TopLeft:
                            _compositeTransformPivot = new Point(_compositeTransformInitialBounds.Right, _compositeTransformInitialBounds.Bottom);
                            break;
                        case ImageResizeCorner.TopRight:
                            _compositeTransformPivot = new Point(_compositeTransformInitialBounds.Left, _compositeTransformInitialBounds.Bottom);
                            break;
                        case ImageResizeCorner.BottomLeft:
                            _compositeTransformPivot = new Point(_compositeTransformInitialBounds.Right, _compositeTransformInitialBounds.Top);
                            break;
                        case ImageResizeCorner.BottomRight:
                            _compositeTransformPivot = new Point(_compositeTransformInitialBounds.Left, _compositeTransformInitialBounds.Top);
                            break;
                    }
                }

                _accumulatedCompositeResizeDelta += canvasDelta;

                double origW = _compositeTransformInitialBounds.Width;
                double origH = _compositeTransformInitialBounds.Height;
                double newW = origW;
                double newH = origH;

                switch (corner)
                {
                    case ImageResizeCorner.TopLeft:
                        newW = origW - _accumulatedCompositeResizeDelta.X;
                        newH = origH - _accumulatedCompositeResizeDelta.Y;
                        break;
                    case ImageResizeCorner.TopRight:
                        newW = origW + _accumulatedCompositeResizeDelta.X;
                        newH = origH - _accumulatedCompositeResizeDelta.Y;
                        break;
                    case ImageResizeCorner.BottomLeft:
                        newW = origW - _accumulatedCompositeResizeDelta.X;
                        newH = origH + _accumulatedCompositeResizeDelta.Y;
                        break;
                    case ImageResizeCorner.BottomRight:
                        newW = origW + _accumulatedCompositeResizeDelta.X;
                        newH = origH + _accumulatedCompositeResizeDelta.Y;
                        break;
                }

                const double minDimension = 15.0;
                newW = Math.Max(minDimension, newW);
                newH = Math.Max(minDimension, newH);

                if ((lockAspect || hasElements) && origW > 0 && origH > 0)
                {
                    double uniform = Math.Min(newW / origW, newH / origH);
                    newW = origW * uniform;
                    newH = origH * uniform;
                }

                double scaleX = newW / origW;
                double scaleY = newH / origH;

                if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0)
                {
                    return;
                }

                var matrix = new Matrix();
                matrix.ScaleAt(scaleX, scaleY, _compositeTransformPivot.X, _compositeTransformPivot.Y);

                // 1. 缩放墨迹
                if (hasStrokes)
                {
                    foreach (var stroke in strokes)
                    {
                        if (_strokeTransformInitialPoints.TryGetValue(stroke, out var initialPoints))
                        {
                            var newPoints = new StylusPointCollection();
                            for (int i = 0; i < initialPoints.Count; i++)
                            {
                                var pt = matrix.Transform(new Point(initialPoints[i].X, initialPoints[i].Y));
                                newPoints.Add(new StylusPoint(pt.X, pt.Y, initialPoints[i].PressureFactor));
                            }
                            stroke.StylusPoints = newPoints;
                        }
                    }
                }

                // 2. 缩放所有元素（尺寸变大 + 中心位移以锁定相对位置）
                foreach (var (elem, snapshot) in _elementInitialStates)
                {
                    if (elem.RenderTransform is TransformGroup tg)
                    {
                        var st = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                        var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                        var rt = tg.Children.OfType<RotateTransform>().FirstOrDefault();

                        if (st != null && tt != null)
                        {
                            st.ScaleX = snapshot.ScaleX * scaleX;
                            st.ScaleY = snapshot.ScaleY * scaleY;

                            Point newCenter = matrix.Transform(snapshot.VisualCenterCanvas);

                            var (ox, oy, visW, visH) = GetElementVisualBox(elem);
                            double preRotationCenterX = snapshot.BaseLeft + (ox + visW / 2) * st.ScaleX;
                            double preRotationCenterY = snapshot.BaseTop + (oy + visH / 2) * st.ScaleY;

                            tt.X = newCenter.X - preRotationCenterX;
                            tt.Y = newCenter.Y - preRotationCenterY;

                            if (rt != null)
                            {
                                rt.CenterX = (ox + visW / 2) * st.ScaleX;
                                rt.CenterY = (oy + visH / 2) * st.ScaleY;
                            }
                        }
                    }
                }

                UpdateCompositeSelectionOverlay();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复合缩放失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion
    }
}