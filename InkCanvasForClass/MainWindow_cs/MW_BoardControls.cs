using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
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
    public partial class MainWindow : PerformanceTransparentWin {
        private StrokeCollection[] strokeCollections = new StrokeCollection[101];
        private bool[] whiteboadLastModeIsRedo = new bool[101];
        private StrokeCollection lastTouchDownStrokeCollection = new StrokeCollection();

        public int CurrentWhiteboardIndex = 1;
        public int WhiteboardTotalCount = 1;
        private TimeMachineHistory[][] TimeMachineHistories = new TimeMachineHistory[101][]; //最多99页，0用来存储非白板时的墨迹以便还原

        public Color[] BoardBackgroundColors = new Color[6] {
            Color.FromRgb(39, 39, 42), 
            Color.FromRgb(23, 42, 37),
            Color.FromRgb(234, 235, 237), 
            Color.FromRgb(15, 23, 42), 
            Color.FromRgb(181, 230, 181), 
            Color.FromRgb(0, 0, 0)
        };

        public class BoardPageSettings {
            public BlackboardBackgroundColorEnum BackgroundColor { get; set; } = BlackboardBackgroundColorEnum.White;
            public BlackboardBackgroundPatternEnum BackgroundPattern { get; set; } = BlackboardBackgroundPatternEnum.None;
        }

        public List<BoardPageSettings> BoardPagesSettingsList = new List<BoardPageSettings>() {
            new BoardPageSettings()
        };

        #region Board Background

        /// <summary>
        ///     更新白板模式下每頁的背景顏色，可以直接根據當前的<c>CurrentWhiteboardIndex</c>獲取背景配置並更新，也可以自己修改當前的背景顏色
        /// </summary>
        /// <param name="id">要修改的背景顏色的ID，傳入null會根據當前的<c>CurrentWhiteboardIndex</c>去讀取有關背景顏色的配置並更新</param>
        private void UpdateBoardBackground(int? id) {
            if (id != null) BoardPagesSettingsList[CurrentWhiteboardIndex - 1].BackgroundColor = (BlackboardBackgroundColorEnum)id;
            var bgC = BoardPagesSettingsList[CurrentWhiteboardIndex - 1].BackgroundColor;
            if (bgC == BlackboardBackgroundColorEnum.BlackBoardGreen
                || bgC == BlackboardBackgroundColorEnum.BlueBlack
                || bgC == BlackboardBackgroundColorEnum.GrayBlack
                || bgC == BlackboardBackgroundColorEnum.RealBlack) {
                if (inkColor == 0) lastBoardInkColor = 5;
            } else {
                if (inkColor == 5) lastBoardInkColor = 0;
            }
            CheckColorTheme(true);
            UpdateBoardBackgroundPanelDisplayStatus();
        }

        private void BoardBackgroundColor1Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(0);
        }

        private void BoardBackgroundColor2Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(1);
        }

        private void BoardBackgroundColor3Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(2);
        }

        private void BoardBackgroundColor4Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(3);
        }

        private void BoardBackgroundColor5Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(4);
        }

        private void BoardBackgroundColor6Border_MouseUp(object sender, MouseButtonEventArgs e) {
            UpdateBoardBackground(5);
        }

        private void UpdateBoardBackgroundPanelDisplayStatus() {
            BoardBackgroundColor1Checkbox.Visibility = Visibility.Collapsed;
            BoardBackgroundColor2Checkbox.Visibility = Visibility.Collapsed;
            BoardBackgroundColor3Checkbox.Visibility = Visibility.Collapsed;
            BoardBackgroundColor4Checkbox.Visibility = Visibility.Collapsed;
            BoardBackgroundColor5Checkbox.Visibility = Visibility.Collapsed;
            BoardBackgroundColor6Checkbox.Visibility = Visibility.Collapsed;

            if (currentMode == 1) {
                var index = CurrentWhiteboardIndex - 1;
                var bg = BoardPagesSettingsList[index];
                if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)0) BoardBackgroundColor1Checkbox.Visibility = Visibility.Visible;
                else if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)1) BoardBackgroundColor2Checkbox.Visibility = Visibility.Visible;
                else if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)2) BoardBackgroundColor3Checkbox.Visibility = Visibility.Visible;
                else if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)3) BoardBackgroundColor4Checkbox.Visibility = Visibility.Visible;
                else if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)4) BoardBackgroundColor5Checkbox.Visibility = Visibility.Visible;
                else if (bg.BackgroundColor == (BlackboardBackgroundColorEnum)5) BoardBackgroundColor6Checkbox.Visibility = Visibility.Visible;
            }
        }

        #endregion

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
            UpdateBoardBackground(null);
        }

        private void BtnWhiteBoardSwitchNext_Click(object sender, EventArgs e) {
            if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenshot(true);
            if (CurrentWhiteboardIndex >= WhiteboardTotalCount) {
                BtnWhiteBoardAdd_Click(sender, e);
                return;
            }

            SaveStrokes();

            ClearStrokes(true);
            CurrentWhiteboardIndex++;

            RestoreStrokes();

            UpdateIndexInfoDisplay();
            UpdateBoardBackground(null);
        }

        private void BtnWhiteBoardAdd_Click(object sender, EventArgs e) {
            if (WhiteboardTotalCount >= 99) return;
            if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenshot(true);
            SaveStrokes();
            ClearStrokes(true);

            BoardPagesSettingsList.Insert(CurrentWhiteboardIndex, new BoardPageSettings() {
                BackgroundColor = Settings.Canvas.UseDefaultBackgroundColorForEveryNewAddedBlackboardPage ? Settings.Canvas.BlackboardBackgroundColor : BoardPagesSettingsList[CurrentWhiteboardIndex-1].BackgroundColor,
                BackgroundPattern = Settings.Canvas.UseDefaultBackgroundPatternForEveryNewAddedBlackboardPage ?  Settings.Canvas.BlackboardBackgroundPattern : BoardPagesSettingsList[CurrentWhiteboardIndex - 1].BackgroundPattern,
            });

            WhiteboardTotalCount++;
            CurrentWhiteboardIndex++;

            if (CurrentWhiteboardIndex != WhiteboardTotalCount)
                for (var i = WhiteboardTotalCount; i > CurrentWhiteboardIndex; i--)
                    TimeMachineHistories[i] = TimeMachineHistories[i - 1];

            UpdateIndexInfoDisplay();

            //if (WhiteboardTotalCount >= 99) BtnWhiteBoardAdd.IsEnabled = false;

            if (BlackBoardLeftSidePageListView.Visibility == Visibility.Visible) {
                RefreshBlackBoardSidePageListView();
            }

            UpdateBoardBackground(null);
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

            //if (WhiteboardTotalCount < 99) BtnWhiteBoardAdd.IsEnabled = true;
        }

        private bool _whiteboardModePreviousPageButtonEnabled = false;
        private bool _whiteboardModeNextPageButtonEnabled = false;
        private bool _whiteboardModeNewPageButtonEnabled = false;
        private bool _whiteboardModeNewPageButtonMerged = false;

        public bool WhiteboardModePreviousPageButtonEnabled {
            get => _whiteboardModePreviousPageButtonEnabled;
            set {
                _whiteboardModePreviousPageButtonEnabled = value;
                var geo = new GeometryDrawing[]
                    { BtnLeftWhiteBoardSwitchPreviousGeometry, BtnRightWhiteBoardSwitchPreviousGeometry };
                var label = new TextBlock[]
                    { BtnLeftWhiteBoardSwitchPreviousLabel, BtnRightWhiteBoardSwitchPreviousLabel };
                var border = new Border[]
                    { BtnWhiteBoardSwitchPreviousL, BtnWhiteBoardSwitchPreviousR };
                foreach (var gd in geo)
                    gd.Brush = new SolidColorBrush(Color.FromArgb((byte)(value ? 255 : 127), 24, 24, 27));
                foreach (var tb in label) tb.Opacity = value ? 1 : 0.5;
                foreach (var bd in border) bd.IsHitTestVisible = value;
            }
        }

        public bool WhiteboardModeNextPageButtonEnabled {
            get => _whiteboardModeNextPageButtonEnabled;
            set {
                _whiteboardModeNextPageButtonEnabled = value;
                var geo = new GeometryDrawing[]
                    { BtnLeftWhiteBoardSwitchNextGeometry, BtnRightWhiteBoardSwitchNextGeometry };
                var label = new TextBlock[]
                    { BtnLeftWhiteBoardSwitchNextLabel, BtnRightWhiteBoardSwitchNextLabel };
                var border = new Border[]
                    { BtnWhiteBoardSwitchNextL, BtnWhiteBoardSwitchNextR };
                foreach (var gd in geo)
                    gd.Brush = new SolidColorBrush(Color.FromArgb((byte)(value ? 255 : 127), 24, 24, 27));
                foreach (var tb in label) tb.Opacity = value ? 1 : 0.5;
                foreach (var bd in border) bd.IsHitTestVisible = value;
            }
        }

        public bool WhiteboardModeNewPageButtonEnabled {
            get => _whiteboardModeNewPageButtonEnabled;
            set {
                _whiteboardModeNewPageButtonEnabled = value;
                var geo = new GeometryDrawing[]
                    { BtnWhiteboardAddGeometryLeft, BtnWhiteboardAddGeometryRight, BtnWhiteboardAddGeometryRightSecondary };
                var label = new TextBlock[]
                    { BtnWhiteboardAddTextBlockLeft, BtnWhiteboardAddTextBlockRight, BtnWhiteboardAddTextBlockRightSecondary };
                var border = new Border[]
                    { BtnWhiteboardAddLeft, BtnWhiteboardAddRight, BtnWhiteboardAddRightSecondary };
                foreach (var gd in geo)
                    gd.Brush = new SolidColorBrush(Color.FromArgb((byte)(value ? 255 : 127), 24, 24, 27));
                foreach (var tb in label) tb.Opacity = value ? 1 : 0.5;
                foreach (var bd in border) bd.IsHitTestVisible = value;
            }
        }

        public bool WhiteboardModeNewPageButtonMerged {
            get => _whiteboardModeNewPageButtonMerged;
            set {
                _whiteboardModeNewPageButtonMerged = value;
                BtnWhiteBoardSwitchNextL.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                BtnLeftPageListWB.CornerRadius = value ? new CornerRadius(0, 5, 5, 0) : new CornerRadius(0);
                BtnWhiteBoardSwitchNextR.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                BtnRightPageListWB.CornerRadius = value ? new CornerRadius(0, 5, 5, 0) : new CornerRadius(0);
                BtnWhiteboardAddRightSecondary.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateIndexInfoDisplay() {
            BtnLeftPageListWBTextCount.Text =
                $"{CurrentWhiteboardIndex}/{WhiteboardTotalCount}";
            BtnRightPageListWBTextCount.Text =
                $"{CurrentWhiteboardIndex}/{WhiteboardTotalCount}";

            WhiteboardModePreviousPageButtonEnabled = CurrentWhiteboardIndex > 1;
            WhiteboardModeNextPageButtonEnabled = CurrentWhiteboardIndex < WhiteboardTotalCount;
            WhiteboardModeNewPageButtonEnabled = WhiteboardTotalCount < 99;
            WhiteboardModeNewPageButtonMerged = CurrentWhiteboardIndex == WhiteboardTotalCount;
        }
    }
}