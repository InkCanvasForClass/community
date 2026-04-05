using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 存储每个白板页面的墨迹集合
        /// </summary>
        private StrokeCollection[] strokeCollections = new StrokeCollection[101];

        /// <summary>
        /// 存储每个白板页面的最后操作模式是否为重做
        /// </summary>
        private bool[] whiteboadLastModeIsRedo = new bool[101];

        /// <summary>
        /// 存储最后一次触摸按下时的墨迹集合
        /// </summary>
        private StrokeCollection lastTouchDownStrokeCollection = new StrokeCollection();

        /// <summary>
        /// 当前白板页面索引
        /// </summary>
        private int CurrentWhiteboardIndex = 1;

        /// <summary>
        /// 白板页面总数
        /// </summary>
        private int WhiteboardTotalCount = 1;

        /// <summary>
        /// 存储每个白板页面的时间机器历史记录
        /// </summary>
        private TimeMachineHistory[][] TimeMachineHistories = new TimeMachineHistory[101][];

        /// <summary>
        /// 存储每个白板页面的多指书写模式状态
        /// </summary>
        private bool[] savedMultiTouchModeStates = new bool[101];

        /// <summary>
        /// 将当前画布上的所有未保存的图片/媒体和墨迹提交到时间机器历史并将导出结果保存为指定页的快照。
        /// </summary>
        /// <param name="isBackupMain">为 true 时将导出结果保存到主备份槽（索引 0）；为 false 时保存到当前白板索引。</param>
        /// <remarks>
        /// - 会提交画布上缺失于历史记录的 Image/MediaElement（但跳过 Tag 等于 VideoPresenterLiveFrameTag 的 Image）和缺失的墨迹；  
        /// - 导出后把结果存入 TimeMachineHistories 的相应索引，并保存当前多指书写模式到 savedMultiTouchModeStates；  
        /// - 导出后会清除时间机器的临时墨迹历史以释放内存。  
        /// - 此方法有副作用：修改 TimeMachineHistories、savedMultiTouchModeStates，并通过 timeMachine 的提交方法改变其内部历史状态。
        /// </remarks>
        private void SaveStrokes(bool isBackupMain = false)
        {
            // 确保画布上的所有UI元素都被保存到时间机器历史记录中
            var currentHistory = timeMachine.ExportTimeMachineHistory();
            var elementsInHistory = new HashSet<UIElement>();

            // 收集已经在历史记录中的元素
            if (currentHistory != null)
            {
                foreach (var h in currentHistory)
                {
                    if (h.CommitType == TimeMachineHistoryType.ElementInsert &&
                        h.InsertedElement != null &&
                        !h.StrokeHasBeenCleared)
                    {
                        elementsInHistory.Add(h.InsertedElement);
                    }
                }
            }

            // 检查画布上的所有UI元素，确保它们都在历史记录中
            var missingElements = 0;
            foreach (UIElement child in inkCanvas.Children)
            {
                if (child is Image || child is MediaElement || child is PdfEmbeddedView)
                {
                    if (child is Image img && img.Tag is string tag && tag == VideoPresenterLiveFrameTag)
                    {
                        continue;
                    }
                    if (!elementsInHistory.Contains(child))
                    {
                        timeMachine.CommitElementInsertHistory(child);
                        missingElements++;
                    }
                }
            }


            // 确保画布上的所有墨迹都被保存
            if (inkCanvas.Strokes.Count > 0)
            {
                // 检查是否有墨迹没有在时间机器历史记录中
                var strokesInHistory = new HashSet<Stroke>();
                if (currentHistory != null)
                {
                    foreach (var h in currentHistory)
                    {
                        if (h.CommitType == TimeMachineHistoryType.UserInput &&
                            h.CurrentStroke != null &&
                            !h.StrokeHasBeenCleared)
                        {
                            foreach (Stroke stroke in h.CurrentStroke)
                            {
                                strokesInHistory.Add(stroke);
                            }
                        }
                    }
                }

                // 收集没有在历史记录中的墨迹
                var missingStrokes = new StrokeCollection();
                foreach (Stroke stroke in inkCanvas.Strokes)
                {
                    if (!strokesInHistory.Contains(stroke))
                    {
                        missingStrokes.Add(stroke);
                    }
                }

                if (missingStrokes.Count > 0)
                {
                    timeMachine.CommitStrokeUserInputHistory(missingStrokes);
                }
            }

            if (isBackupMain)
            {
                var timeMachineHistory = timeMachine.ExportTimeMachineHistory();
                TimeMachineHistories[0] = timeMachineHistory;
                // 保存多指书写模式状态
                savedMultiTouchModeStates[0] = isInMultiTouchMode;
                timeMachine.ClearStrokeHistory();


            }
            else
            {
                var timeMachineHistory = timeMachine.ExportTimeMachineHistory();
                TimeMachineHistories[CurrentWhiteboardIndex] = timeMachineHistory;
                // 保存多指书写模式状态
                savedMultiTouchModeStates[CurrentWhiteboardIndex] = isInMultiTouchMode;
                timeMachine.ClearStrokeHistory();


            }
        }

        /// <summary>
        /// 清除画布上的所有墨迹并执行内存清理
        /// </summary>
        /// <param name="isErasedByCode">是否由代码触发的清除操作</param>
        /// <remarks>
        /// - 根据参数设置当前提交类型
        /// - 清除画布上的所有墨迹
        /// - 恢复当前提交类型为用户输入
        /// </remarks>
        private void ClearStrokes(bool isErasedByCode)
        {
            _currentCommitType = CommitReason.ClearingCanvas;
            if (isErasedByCode) _currentCommitType = CommitReason.CodeInput;

            inkCanvas.Strokes.Clear();

            _currentCommitType = CommitReason.UserInput;
        }

        private static HashSet<UIElement> CollectRemovedElementsFromHistory(TimeMachineHistory[] history)
        {
            var set = new HashSet<UIElement>();
            if (history == null) return set;
            foreach (var h in history)
            {
                if (h.CommitType == TimeMachineHistoryType.ElementInsert && h.StrokeHasBeenCleared && h.InsertedElement != null)
                    set.Add(h.InsertedElement);
            }
            return set;
        }

        /// <summary>
        /// 恢复指定白板页面的墨迹和元素信息
        /// </summary>
        /// <param name="isBackupMain">是否恢复主备份页面</param>
        /// <remarks>
        /// - 隐藏图片选择工具栏
        /// - 清空当前画布的墨迹和所有内容
        /// - 从时间机器历史记录中恢复页面内容
        /// - 恢复多指书写模式状态
        /// - 包含异常处理
        /// </remarks>
        private void RestoreStrokes(bool isBackupMain = false)
        {
            try
            {
                // 隐藏图片选择工具栏
                if (currentSelectedElement != null)
                {
                    // 保存当前编辑模式
                    var previousEditingMode = inkCanvas.EditingMode;
                    UnselectElement(currentSelectedElement);
                    // 恢复编辑模式
                    inkCanvas.EditingMode = previousEditingMode;
                    currentSelectedElement = null;
                }

                var targetIndex = isBackupMain ? 0 : CurrentWhiteboardIndex;

                // 先清空当前画布的墨迹
                inkCanvas.Strokes.Clear();

                // 清空当前画布的所有内容（墨迹和图片）
                // 这里必须清除图片，因为页面切换时需要完全重置画布状态
                inkCanvas.Children.Clear();

                // 如果历史记录为空，直接返回（新页面或空页面）
                if (TimeMachineHistories[targetIndex] == null)
                {
                    timeMachine.ClearStrokeHistory();
                    return;
                }

                if (isBackupMain)
                {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[0]);
                    var removed0 = CollectRemovedElementsFromHistory(TimeMachineHistories[0]);
                    var elementsToProcess = new List<UIElement>();
                    foreach (var item in TimeMachineHistories[0])
                    {
                        if (item.CommitType == TimeMachineHistoryType.ElementInsert &&
                            !item.StrokeHasBeenCleared &&
                            item.InsertedElement != null &&
                            !removed0.Contains(item.InsertedElement))
                        {
                            elementsToProcess.Add(item.InsertedElement);
                        }
                        ApplyHistoryToCanvas(item, null, removed0);
                    }
                    RestoreMultiTouchModeState(0);
                    ProcessElementsAfterRestore(elementsToProcess);
                }
                else
                {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[CurrentWhiteboardIndex]);
                    var removed = CollectRemovedElementsFromHistory(TimeMachineHistories[CurrentWhiteboardIndex]);
                    var elementsToProcess = new List<UIElement>();
                    foreach (var item in TimeMachineHistories[CurrentWhiteboardIndex])
                    {
                        if (item.CommitType == TimeMachineHistoryType.ElementInsert &&
                            !item.StrokeHasBeenCleared &&
                            item.InsertedElement != null &&
                            !removed.Contains(item.InsertedElement))
                        {
                            elementsToProcess.Add(item.InsertedElement);
                        }
                        ApplyHistoryToCanvas(item, null, removed);
                    }
                    RestoreMultiTouchModeState(CurrentWhiteboardIndex);
                    ProcessElementsAfterRestore(elementsToProcess);
                }


            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// 在恢复页面后统一处理所有图片/媒体元素的位置和事件绑定，提升含图片页面的加载性能。
        /// 先批量添加所有元素到画布，再统一处理位置和事件，减少布局更新次数。
        /// </summary>
        private void ProcessElementsAfterRestore(List<UIElement> elements)
        {
            if (elements == null || elements.Count == 0) return;

            // 使用低优先级异步处理，让 UI 先响应，图片位置和事件绑定稍后完成
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                foreach (var element in elements)
                {
                    if (!inkCanvas.Children.Contains(element)) continue;

                    if (element is Image img)
                    {
                        double left = InkCanvas.GetLeft(img);
                        double top = InkCanvas.GetTop(img);
                        if (double.IsNaN(left) || double.IsNaN(top))
                        {
                            CenterAndScaleElement(img);
                        }
                        BindElementEvents(img);
                    }
                    else if (element is MediaElement media)
                    {
                        double left = InkCanvas.GetLeft(media);
                        double top = InkCanvas.GetTop(media);
                        if (double.IsNaN(left) || double.IsNaN(top))
                        {
                            CenterAndScaleElement(media);
                        }
                        BindElementEvents(media);
                    }
                    else if (element is PdfEmbeddedView pdf)
                    {
                        double left = InkCanvas.GetLeft(pdf);
                        double top = InkCanvas.GetTop(pdf);
                        if (double.IsNaN(left) || double.IsNaN(top))
                        {
                            CenterAndScaleElement(pdf);
                        }
                        BindElementEvents(pdf);
                    }
                }
            }));
        }

        /// <summary>
        /// 恢复多指书写模式状态
        /// </summary>
        private void RestoreMultiTouchModeState(int pageIndex)
        {
            try
            {
                // 检查是否保存了多指书写模式状态
                if (savedMultiTouchModeStates[pageIndex])
                {
                    // 更新UI状态
                    if (ToggleSwitchEnableMultiTouchMode != null)
                    {
                        ToggleSwitchEnableMultiTouchMode.IsOn = true;
                    }

                    LogHelper.WriteLogToFile($"恢复多指书写模式状态 - 页面索引: {pageIndex}", LogHelper.LogType.Info);
                }
                else
                {
                    // 更新UI状态
                    if (ToggleSwitchEnableMultiTouchMode != null)
                    {
                        ToggleSwitchEnableMultiTouchMode.IsOn = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复多指书写模式状态失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理白板页面索引按钮点击事件，显示或隐藏侧边页面列表
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 处理左侧页面列表按钮点击：显示或隐藏左侧页面列表
        /// - 处理右侧页面列表按钮点击：显示或隐藏右侧页面列表
        /// - 显示页面列表时会刷新列表内容并滚动到当前页面
        /// </remarks>
        private async void BtnWhiteBoardPageIndex_Click(object sender, EventArgs e)
        {
            if (sender == BtnLeftPageListWB)
            {
                if (BoardBorderLeftPageListView.Visibility == Visibility.Visible)
                {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
                }
                else
                {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
                    RefreshBlackBoardSidePageListView();
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderLeftPageListView);
                    await Task.Delay(1);
                    var leftContainer = BlackBoardLeftSidePageListView.ItemContainerGenerator.ContainerFromIndex(
                        CurrentWhiteboardIndex - 1) as ListViewItem;
                    if (leftContainer != null)
                    {
                        ScrollViewToVerticalTop(leftContainer, BlackBoardLeftSidePageListScrollViewer);
                    }
                }
            }
            else if (sender == BtnRightPageListWB)
            {
                if (BoardBorderRightPageListView.Visibility == Visibility.Visible)
                {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
                }
                else
                {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
                    RefreshBlackBoardSidePageListView();
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderRightPageListView);
                    await Task.Delay(1);
                    var rightContainer = BlackBoardRightSidePageListView.ItemContainerGenerator.ContainerFromIndex(
                        CurrentWhiteboardIndex - 1) as ListViewItem;
                    if (rightContainer != null)
                    {
                        ScrollViewToVerticalTop(rightContainer, BlackBoardRightSidePageListScrollViewer);
                    }
                }
            }

        }

        /// <summary>
        /// 切换到前一白板页并在切换过程中保存与恢复画布和相关状态（如果当前已是第一页则不执行任何操作）。
        /// </summary>
        /// <remarks>
        /// 该方法在切换前会取消当前选中元素（同时保留并恢复编辑模式）、调用视频呈现器的离开页前钩子、保存当前页的笔迹与元素、清空画布；切换到前一页后恢复该页内容、调用视频呈现器的页已更改钩子并刷新页面索引显示。
        /// </remarks>
        private void BtnWhiteBoardSwitchPrevious_Click(object sender, EventArgs e)
        {
            if (CurrentWhiteboardIndex <= 1) return;

            // 隐藏图片选择工具栏
            if (currentSelectedElement != null)
            {
                // 保存当前编辑模式
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                // 恢复编辑模式
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();

            ClearStrokes(true);
            CurrentWhiteboardIndex--;

            RestoreStrokes();
            VideoPresenter_OnPageChanged();

            UpdateIndexInfoDisplay();
        }

        /// <summary>
        /// 切换到白板的下一页；在到达最后一页时会新增一页。方法在切页前保存当前页面的笔迹/多媒体状态，在切页后恢复目标页面的内容并更新界面状态。
        /// </summary>
        /// <param name="sender">触发事件的源对象（通常为按钮）。</param>
        /// <param name="e">事件参数。</param>
        private void BtnWhiteBoardSwitchNext_Click(object sender, EventArgs e)
        {
            if (CurrentWhiteboardIndex < WhiteboardTotalCount &&
                Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                CaptureAndEnqueueScreenshotSave(isHideNotification: true);

            if (CurrentWhiteboardIndex >= WhiteboardTotalCount)
            {
                // 在最后一页时，点击"新页面"按钮直接新增一页
                BtnWhiteBoardAdd_Click(sender, e);
                return;
            }

            // 隐藏图片选择工具栏
            if (currentSelectedElement != null)
            {
                // 保存当前编辑模式
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                // 恢复编辑模式
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();

            ClearStrokes(true);
            CurrentWhiteboardIndex++;

            RestoreStrokes();
            VideoPresenter_OnPageChanged();

            UpdateIndexInfoDisplay();
        }

        /// <summary>
        /// 在白板集合中添加一个新页面：在切换前保存并清除当前页面的笔迹与状态，插入新空白页面，恢复并刷新与页面相关的 UI 状态。
        /// </summary>
        /// <remarks>
        /// - 在达到最大页面数（99）时不执行任何操作。  
        /// - 在切换前若启用了自动保存且笔迹数量超过阈值，会保存当前画面截图。  
        /// - 若有选中元素，会取消选中并恢复编辑模式。  
        /// - 将当前页面的历史保存到时间轴并清空画布，然后在白板集合中插入一个空白页面（其历史为 null），随后恢复该页面并触发页面变更回调。  
        /// - 更新页码显示并在达到上限时禁用添加按钮；若侧边页列表可见，则刷新该列表。  
        /// </remarks>
        private void BtnWhiteBoardAdd_Click(object sender, EventArgs e)
        {
            if (WhiteboardTotalCount >= 99) return;
            if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                CaptureAndEnqueueScreenshotSave(isHideNotification: true);

            // 隐藏图片选择工具栏
            if (currentSelectedElement != null)
            {
                // 保存当前编辑模式
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                // 恢复编辑模式
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();
            ClearStrokes(true);

            WhiteboardTotalCount++;
            CurrentWhiteboardIndex++;

            if (CurrentWhiteboardIndex != WhiteboardTotalCount)
            {
                for (var i = WhiteboardTotalCount; i > CurrentWhiteboardIndex; i--)
                {
                    TimeMachineHistories[i] = TimeMachineHistories[i - 1];
                    savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i - 1];
                }
            }

            // 确保新页面的历史记录为空
            TimeMachineHistories[CurrentWhiteboardIndex] = null;

            // 恢复新页面（这会清空画布，因为历史记录为null）
            RestoreStrokes();
            VideoPresenter_OnPageChanged();

            UpdateIndexInfoDisplay();

            if (WhiteboardTotalCount >= 99) BtnWhiteBoardAdd.IsEnabled = false;

            if (BlackBoardLeftSidePageListView.Visibility == Visibility.Visible)
            {
                RefreshBlackBoardSidePageListView();
            }
        }

        /// <summary>
        /// 处理白板页面删除按钮点击事件，删除当前白板页面
        /// </summary>
        private void BtnWhiteBoardDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteWhiteBoardPageByIndex(CurrentWhiteboardIndex);
        }

        /// <summary>
        /// 按页码删除指定白板页（用于预览列表等）。仅当总页数大于 1 时有效。
        /// </summary>
        /// <param name="pageIndex">要删除的页码（1 到 WhiteboardTotalCount）</param>
        private void DeleteWhiteBoardPageByIndex(int pageIndex)
        {
            if (WhiteboardTotalCount <= 1 || pageIndex < 1 || pageIndex > WhiteboardTotalCount)
                return;

            if (currentSelectedElement != null)
            {
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            if (pageIndex == CurrentWhiteboardIndex)
            {
                ClearStrokes(true);

                var oldTotal = WhiteboardTotalCount;
                if (CurrentWhiteboardIndex != oldTotal)
                {
                    for (var i = CurrentWhiteboardIndex; i < oldTotal; i++)
                    {
                        TimeMachineHistories[i] = FlattenPageHistory(TimeMachineHistories[i + 1]);
                        savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i + 1];
                    }
                }
                else
                {
                    CurrentWhiteboardIndex--;
                }

                TimeMachineHistories[oldTotal] = null;
                WhiteboardTotalCount--;
                RestoreStrokes();
            }
            else if (pageIndex < CurrentWhiteboardIndex)
            {
                for (var i = pageIndex; i < WhiteboardTotalCount; i++)
                {
                    TimeMachineHistories[i] = FlattenPageHistory(TimeMachineHistories[i + 1]);
                    savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i + 1];
                }
                TimeMachineHistories[WhiteboardTotalCount] = null;
                WhiteboardTotalCount--;
                CurrentWhiteboardIndex--;
            }
            else
            {
                for (var i = pageIndex; i < WhiteboardTotalCount; i++)
                {
                    TimeMachineHistories[i] = FlattenPageHistory(TimeMachineHistories[i + 1]);
                    savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i + 1];
                }
                TimeMachineHistories[WhiteboardTotalCount] = null;
                WhiteboardTotalCount--;
            }

            UpdateIndexInfoDisplay();
            if (WhiteboardTotalCount < 99) BtnWhiteBoardAdd.IsEnabled = true;
            if (BoardBorderLeftPageListView?.Visibility == Visibility.Visible ||
                BoardBorderRightPageListView?.Visibility == Visibility.Visible)
                RefreshBlackBoardSidePageListView();
        }

        /// <summary>
        /// 更新白板页码信息显示和按钮状态
        /// </summary>
        /// <remarks>
        /// - 更新页码显示文本
        /// - 设置下一页按钮文本（根据是否为最后一页）
        /// - 启用或禁用下一页按钮（根据是否为最后一页和最大页面数）
        /// - 设置按钮颜色和透明度
        /// - 启用或禁用上一页按钮（根据是否为第一页）
        /// - 设置删除按钮状态（根据页面总数）
        /// </remarks>
        private void UpdateIndexInfoDisplay()
        {
            TextBlockWhiteBoardIndexInfo.Text =
                $"{CurrentWhiteboardIndex}/{WhiteboardTotalCount}";

            bool isLastPage = CurrentWhiteboardIndex == WhiteboardTotalCount;
            bool isMaxPage = WhiteboardTotalCount >= 99;

            // 设置按钮文本
            BtnLeftWhiteBoardSwitchNextLabel.Text = isLastPage ? "新页面" : "下一页";
            BtnRightWhiteBoardSwitchNextLabel.Text = isLastPage ? "新页面" : "下一页";

            if (isLastPage)
            {
                BtnWhiteBoardSwitchNext.IsEnabled = !isMaxPage;
            }
            else
            {
                BtnWhiteBoardSwitchNext.IsEnabled = true;
            }

            // 获取主题颜色资源
            var iconForegroundBrush = Application.Current.FindResource("IconForeground") as SolidColorBrush;

            // 设置下一页按钮颜色
            if (iconForegroundBrush != null)
            {
                BtnLeftWhiteBoardSwitchNextGeometry.Brush = iconForegroundBrush;
                BtnRightWhiteBoardSwitchNextGeometry.Brush = iconForegroundBrush;
            }
            BtnLeftWhiteBoardSwitchNextLabel.Opacity = 1;
            BtnRightWhiteBoardSwitchNextLabel.Opacity = 1;

            BtnWhiteBoardSwitchPrevious.IsEnabled = true;

            if (CurrentWhiteboardIndex == 1)
            {
                BtnWhiteBoardSwitchPrevious.IsEnabled = false;
                if (iconForegroundBrush != null)
                {
                    var disabledBrush = new SolidColorBrush(Color.FromArgb(127, iconForegroundBrush.Color.R, iconForegroundBrush.Color.G, iconForegroundBrush.Color.B));
                    BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = disabledBrush;
                    BtnRightWhiteBoardSwitchPreviousGeometry.Brush = disabledBrush;
                }
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
            }
            else
            {
                if (iconForegroundBrush != null)
                {
                    BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = iconForegroundBrush;
                    BtnRightWhiteBoardSwitchPreviousGeometry.Brush = iconForegroundBrush;
                }
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 1;
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 1;
            }

            BtnWhiteBoardDelete.IsEnabled = WhiteboardTotalCount != 1;
        }
    }
}