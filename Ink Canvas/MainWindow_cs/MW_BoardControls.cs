using Ink_Canvas.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Windows.Controls;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private StrokeCollection[] strokeCollections = new StrokeCollection[101];
        private bool[] whiteboadLastModeIsRedo = new bool[101];
        private StrokeCollection lastTouchDownStrokeCollection = new StrokeCollection();

        private int CurrentWhiteboardIndex = 1;
        private int WhiteboardTotalCount = 1;
        private TimeMachineHistory[][] TimeMachineHistories = new TimeMachineHistory[101][]; //最多99页，0用来存储非白板时的墨迹以便还原

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
            inkCanvas.Strokes.Clear();
            _currentCommitType = CommitReason.UserInput;
        }

        private void RestoreStrokes(bool isBackupMain = false) {
            try {
                if (TimeMachineHistories[CurrentWhiteboardIndex] == null) return; //防止白板打开后不居中
                if (isBackupMain) {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[0]);
                    foreach (var item in TimeMachineHistories[0]) ApplyHistoryToCanvas(item);
                } else {
                    timeMachine.ImportTimeMachineHistory(TimeMachineHistories[CurrentWhiteboardIndex]);
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
                BtnWhiteBoardAdd_Click(sender, e);
                return;
            }

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
            SaveStrokes();
            ClearStrokes(true);

            WhiteboardTotalCount++;
            CurrentWhiteboardIndex++;

            if (CurrentWhiteboardIndex != WhiteboardTotalCount)
                for (var i = WhiteboardTotalCount; i > CurrentWhiteboardIndex; i--)
                    TimeMachineHistories[i] = TimeMachineHistories[i - 1];

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

            if (CurrentWhiteboardIndex == WhiteboardTotalCount) {
                var newImageSource = new BitmapImage();
                newImageSource.BeginInit();
                newImageSource.UriSource = new Uri("/Resources/Icons-Fluent/ic_fluent_add_circle_24_regular.png",
                    UriKind.RelativeOrAbsolute);
                newImageSource.EndInit();
                //BoardLeftPannelNextPage.Source = newImageSource;
                //BoardRightPannelNextPage.Source = newImageSource;
                //BoardRightPannelNextPageTextBlock.Text = "加页";
                //BoardLeftPannelNextPageTextBlock.Text = "加页";
            } else {
                var newImageSource = new BitmapImage();
                newImageSource.BeginInit();
                newImageSource.UriSource =
                    new Uri("/Resources/Icons-Fluent/ic_fluent_arrow_circle_right_24_regular.png",
                        UriKind.RelativeOrAbsolute);
                newImageSource.EndInit();
                //BoardLeftPannelNextPage.Source = newImageSource;
                //BoardRightPannelNextPage.Source = newImageSource;
                //BoardRightPannelNextPageTextBlock.Text = "下一页";
                //BoardLeftPannelNextPageTextBlock.Text = "下一页";
            }

            BtnWhiteBoardSwitchPrevious.IsEnabled = true;
            BtnWhiteBoardSwitchNext.IsEnabled = true;

            if (CurrentWhiteboardIndex == 1) {
                BtnWhiteBoardSwitchPrevious.IsEnabled = false;
                BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
                BtnLeftWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnLeftWhiteBoardSwitchNextLabel.Opacity = 1;

                BtnRightWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
                BtnRightWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnRightWhiteBoardSwitchNextLabel.Opacity = 1;
            } else {
                BtnLeftWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 1;

                BtnRightWhiteBoardSwitchPreviousGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 1;

                if (CurrentWhiteboardIndex == WhiteboardTotalCount) {
                    BtnLeftWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                    BtnLeftWhiteBoardSwitchNextLabel.Opacity = 0.5;

                    BtnRightWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                    BtnRightWhiteBoardSwitchNextLabel.Opacity = 0.5;
                    BtnWhiteBoardSwitchNext.IsEnabled = false;
                } else {
                    BtnLeftWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                    BtnLeftWhiteBoardSwitchNextLabel.Opacity = 1;

                    BtnRightWhiteBoardSwitchNextGeometry.Brush = new SolidColorBrush(Color.FromArgb(255, 24, 24, 27));
                    BtnRightWhiteBoardSwitchNextLabel.Opacity = 1;
                }
            }

            BtnWhiteBoardDelete.IsEnabled = WhiteboardTotalCount != 1;
        }
    }
}