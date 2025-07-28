using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private StrokeCollection[] strokeCollections = new StrokeCollection[101];
        private bool[] whiteboadLastModeIsRedo = new bool[101];
        private StrokeCollection lastTouchDownStrokeCollection = new StrokeCollection();

        private int CurrentWhiteboardIndex = 1;
        private int WhiteboardTotalCount = 1;
        private TimeMachineHistory[][] TimeMachineHistories = new TimeMachineHistory[101][]; //最多99页，0用来存储非白板时的墨迹以便还原

        // 保存每页白板图片信息
        private void SaveStrokes(bool isBackupMain = false) {
            if (isBackupMain) {
                var timeMachineHistory = timeMachine.ExportTimeMachineHistory();
                TimeMachineHistories[0] = timeMachineHistory;
                timeMachine.ClearStrokeHistory();
            } else {
                var timeMachineHistory = timeMachine.ExportTimeMachineHistory();
                TimeMachineHistories[CurrentWhiteboardIndex] = timeMachineHistory;
                timeMachine.ClearStrokeHistory();
            }
        }

        private void ClearStrokes(bool isErasedByCode) {
            _currentCommitType = CommitReason.ClearingCanvas;
            if (isErasedByCode) _currentCommitType = CommitReason.CodeInput;

            // 取消任何UI元素的选择，隐藏拉伸控件
            DeselectUIElement();

            // 只清除笔画，不清除图片元素
            // 图片元素的清除由调用方决定
            inkCanvas.Strokes.Clear();

            _currentCommitType = CommitReason.UserInput;
        }

        // 恢复每页白板图片信息
        private void RestoreStrokes(bool isBackupMain = false) {
            try {
                var targetIndex = isBackupMain ? 0 : CurrentWhiteboardIndex;

                // 先清空当前画布的所有内容（墨迹和图片）
                // 这里必须清除图片，因为页面切换时需要完全重置画布状态
                inkCanvas.Strokes.Clear();
                inkCanvas.Children.Clear();

                // 如果历史记录为空，直接返回（新页面或空页面）
                if (TimeMachineHistories[targetIndex] == null) {
                    timeMachine.ClearStrokeHistory();
                    return;
                }

                if (isBackupMain) {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[0]);
                    foreach (var item in TimeMachineHistories[0]) ApplyHistoryToCanvas(item);
                } else {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[CurrentWhiteboardIndex]);
                    // 通过时间机器历史恢复所有内容（墨迹和图片）
                    foreach (var item in TimeMachineHistories[CurrentWhiteboardIndex]) ApplyHistoryToCanvas(item);
                }
            }
            catch {
                // ignored
            }
        }

        private async void BtnWhiteBoardPageIndex_Click(object sender, EventArgs e) {
            if (sender == BtnLeftPageListWB) {
                if (BoardBorderLeftPageListView.Visibility == Visibility.Visible) {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
                } else {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
                    RefreshBlackBoardSidePageListView();
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderLeftPageListView);
                    await Task.Delay(1);
                    ScrollViewToVerticalTop(
                        (ListViewItem)BlackBoardLeftSidePageListView.ItemContainerGenerator.ContainerFromIndex(
                            CurrentWhiteboardIndex - 1), BlackBoardLeftSidePageListScrollViewer);
                }
            } else if (sender == BtnRightPageListWB)
            {
                if (BoardBorderRightPageListView.Visibility == Visibility.Visible) {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
                } else {
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
                    RefreshBlackBoardSidePageListView();
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderRightPageListView);
                    await Task.Delay(1);
                    ScrollViewToVerticalTop(
                        (ListViewItem)BlackBoardRightSidePageListView.ItemContainerGenerator.ContainerFromIndex(
                            CurrentWhiteboardIndex - 1), BlackBoardRightSidePageListScrollViewer);
                }
            }

        }

        private void BtnWhiteBoardSwitchPrevious_Click(object sender, EventArgs e) {
            if (CurrentWhiteboardIndex <= 1) return;

            // 取消任何UI元素的选择
            DeselectUIElement();

            SaveStrokes();

            ClearStrokes(true);
            CurrentWhiteboardIndex--;

            RestoreStrokes();

            UpdateIndexInfoDisplay();
        }

        private void BtnWhiteBoardSwitchNext_Click(object sender, EventArgs e) {
            Trace.WriteLine("113223234");

            if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenShot(true);
            if (CurrentWhiteboardIndex >= WhiteboardTotalCount) {
                // 在最后一页时，点击“新页面”按钮直接新增一页
                BtnWhiteBoardAdd_Click(sender, e);
                return;
            }

            // 取消任何UI元素的选择
            DeselectUIElement();

            SaveStrokes();

            ClearStrokes(true);
            CurrentWhiteboardIndex++;

            RestoreStrokes();

            UpdateIndexInfoDisplay();
        }

        private void BtnWhiteBoardAdd_Click(object sender, EventArgs e) {
            if (WhiteboardTotalCount >= 99) return;
            if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenShot(true);

            // 取消任何UI元素的选择
            DeselectUIElement();

            SaveStrokes();
            ClearStrokes(true);

            WhiteboardTotalCount++;
            CurrentWhiteboardIndex++;

            if (CurrentWhiteboardIndex != WhiteboardTotalCount)
                for (var i = WhiteboardTotalCount; i > CurrentWhiteboardIndex; i--)
                    TimeMachineHistories[i] = TimeMachineHistories[i - 1];

            // 确保新页面的历史记录为空
            TimeMachineHistories[CurrentWhiteboardIndex] = null;

            // 恢复新页面（这会清空画布，因为历史记录为null）
            RestoreStrokes();

            UpdateIndexInfoDisplay();

            if (WhiteboardTotalCount >= 99) BtnWhiteBoardAdd.IsEnabled = false;

            if (BlackBoardLeftSidePageListView.Visibility == Visibility.Visible) {
                RefreshBlackBoardSidePageListView();
            }
        }

        private void BtnWhiteBoardDelete_Click(object sender, RoutedEventArgs e) {
            ClearStrokes(true);

            if (CurrentWhiteboardIndex != WhiteboardTotalCount)
                for (var i = CurrentWhiteboardIndex; i <= WhiteboardTotalCount; i++)
                    TimeMachineHistories[i] = TimeMachineHistories[i + 1];
            else
                CurrentWhiteboardIndex--;

            WhiteboardTotalCount--;

            RestoreStrokes();

            UpdateIndexInfoDisplay();

            if (WhiteboardTotalCount < 99) BtnWhiteBoardAdd.IsEnabled = true;
        }

        private void UpdateIndexInfoDisplay() {
            TextBlockWhiteBoardIndexInfo.Text =
                $"{CurrentWhiteboardIndex}/{WhiteboardTotalCount}";

            bool isLastPage = CurrentWhiteboardIndex == WhiteboardTotalCount;
            bool isMaxPage = WhiteboardTotalCount >= 99;

            // 设置按钮文本
            BtnLeftWhiteBoardSwitchNextLabel.Text = isLastPage ? "新页面" : "下一页";
            BtnRightWhiteBoardSwitchNextLabel.Text = isLastPage ? "新页面" : "下一页";

            // 始终允许点击“下一页/新页面”按钮（除非已达最大页数）
            BtnWhiteBoardSwitchNext.IsEnabled = !isMaxPage;

            // 保持按钮常亮（高亮）
            BtnLeftWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
            BtnLeftWhiteBoardSwitchNextLabel.Opacity = 1;
            BtnRightWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
            BtnRightWhiteBoardSwitchNextLabel.Opacity = 1;

            BtnWhiteBoardSwitchPrevious.IsEnabled = true;

            if (CurrentWhiteboardIndex == 1) {
                BtnWhiteBoardSwitchPrevious.IsEnabled = false;
                BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
                BtnRightWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
            } else {
                BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 1;
                BtnRightWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 1;
            }

            BtnWhiteBoardDelete.IsEnabled = WhiteboardTotalCount != 1;
        }
    }
}