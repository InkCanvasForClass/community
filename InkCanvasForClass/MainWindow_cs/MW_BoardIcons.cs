using Ink_Canvas.Helpers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {

        private Border lastBoardToolBtnDownBorder = null;
        private Border lastBoardSideBtnDownBorder = null;

        private void BoardToolBtnMouseDown(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder != null) return;
            lastBoardToolBtnDownBorder = (Border)sender;
            if (lastBoardToolBtnDownBorder.Name == "BoardPen") {
                BoardMouseFeedbakBorder.Margin = new Thickness(60, 0, 0, 0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0);
            } else if (lastBoardToolBtnDownBorder.Name == "BoardSelect") {
                BoardMouseFeedbakBorder.Margin = new Thickness(0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(4.5,0,0,4.5);
            } else if (lastBoardToolBtnDownBorder.Name == "BoardEraser") {
                BoardMouseFeedbakBorder.Margin = new Thickness(120, 0, 0, 0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0);
            } else if (lastBoardToolBtnDownBorder.Name == "BoardGeometry") {
                BoardMouseFeedbakBorder.Margin = new Thickness(180, 0, 0, 0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0);
            } else if (lastBoardToolBtnDownBorder.Name == "BoardUndo") {
                BoardMouseFeedbakBorder.Margin = new Thickness(240, 0, 0, 0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0);
            } else if (lastBoardToolBtnDownBorder.Name == "BoardRedo") {
                BoardMouseFeedbakBorder.Margin = new Thickness(300, 0, 0, 0);
                ((Border)BoardMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0,4.5,4.5,0);
            }
            BoardMouseFeedbakBorder.Visibility = Visibility.Visible;
        }

        private void BoardSideBtnMouseDown(object sender, MouseButtonEventArgs e) {
            if (lastBoardSideBtnDownBorder != null) return;
            lastBoardSideBtnDownBorder = (Border)sender;
            if (lastBoardSideBtnDownBorder.Name == "BoardBackground") {
                BoardSideBtnMouseFeedbakBorder.Margin = new Thickness(60, 0, 0, 0);
                ((Border)BoardSideBtnMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(0,4.5,4.5,0);
                BoardSideBtnMouseFeedbakBorder.Visibility = Visibility.Visible;
            } else if (lastBoardSideBtnDownBorder.Name == "BoardGesture") {
                BoardSideBtnMouseFeedbakBorder.Margin = new Thickness(0);
                ((Border)BoardSideBtnMouseFeedbakBorder.Child).CornerRadius = new CornerRadius(4.5,0,0,4.5);
                BoardSideBtnMouseFeedbakBorder.Visibility = Visibility.Visible;
            }
        }

        private void BoardToolBtnMouseLeave(object sender, MouseEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            lastBoardToolBtnDownBorder = null;
            BoardMouseFeedbakBorder.Visibility = Visibility.Collapsed;
        }

        private void BoardSideBtnMouseLeave(object sender, MouseEventArgs e) {
            if (lastBoardSideBtnDownBorder == null) return;
            lastBoardSideBtnDownBorder = null;
            BoardSideBtnMouseFeedbakBorder.Visibility = Visibility.Collapsed;
        }

        private void BoardPenMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            PenIcon_Click(null,null);
        }

        private void BoardSelectMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            SymbolIconSelect_MouseUp(null,null);
        }

        private void BoardGestureMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardSideBtnDownBorder == null) return;
            BoardSideBtnMouseLeave(sender, null);
            TwoFingerGestureBorder_MouseUp(null, null);
        }

        private void BoardChangeBackgroundColorBtn_MouseUp(object sender, RoutedEventArgs e) {
            if (lastBoardSideBtnDownBorder == null) return;
            BoardSideBtnMouseLeave(sender, null);
            UpdateBoardBackgroundPanelDisplayStatus();
            if (BoardBackgroundPopup.Visibility == Visibility.Visible) {
                AnimationsHelper.HideWithSlideAndFade(BoardBackgroundPopup);
            } else {
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBackgroundPopup);
            }
        }

        private void BoardEraserMouseUp(object sender, RoutedEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint ||
                inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke) {
                if (BoardEraserSizePanel.Visibility == Visibility.Collapsed) {
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardEraserSizePanel);
                } else {
                    AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                }
            } else {
                EraserIcon_Click(null,null);
            }
        }

        private void BoardGeometryMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            ImageDrawShape_MouseUp(null,null);
        }

        private void BoardUndoMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            SymbolIconUndo_MouseUp(null,null);
        }

        private void BoardRedoMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBoardToolBtnDownBorder == null) return;
            BoardToolBtnMouseLeave(sender, null);
            SymbolIconRedo_MouseUp(null,null);
        }

        private void BoardEraserIconByStrokes_Click(object sender, RoutedEventArgs e) {
            //if (BoardEraserByStrokes.Background.ToString() == "#FF679CF4") {
            //    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardDeleteIcon);
            //}
            //else {
                forceEraser = true;
                forcePointEraser = false;

                // update tool selection
                SelectedMode = ICCToolsEnum.EraseByStrokeMode;
                ForceUpdateToolSelection(null);

                inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                drawingShapeMode = 0;

                inkCanvas_EditingModeChanged(inkCanvas, null);
                CancelSingleFingerDragMode();

                HideSubPanels("eraserByStrokes");
            //}
        }

        private void BoardSymbolIconDelete_MouseUp(object sender, RoutedEventArgs e) {
            PenIcon_Click(null, null);
            SymbolIconDelete_MouseUp(null, null);
        }
        private void BoardSymbolIconDeleteInkAndHistories_MouseUp(object sender, RoutedEventArgs e)
        {
            PenIcon_Click(null, null);
            SymbolIconDelete_MouseUp(null, null);
            if (Settings.Canvas.ClearCanvasAndClearTimeMachine == false) timeMachine.ClearStrokeHistory();
        }

        private void BoardLaunchEasiCamera_MouseUp(object sender, MouseButtonEventArgs e) {
            ImageBlackboard_MouseUp(null, null);
            SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
        }

        private void BoardLaunchDesmos_MouseUp(object sender, MouseButtonEventArgs e) {
            HideSubPanelsImmediately();
            ImageBlackboard_MouseUp(null, null);
            Process.Start("https://www.desmos.com/calculator?lang=zh-CN");
        }
    }
}