using Ink_Canvas.Helpers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void BoardChangeBackgroundColorBtn_MouseUp(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.UsingWhiteboard = !Settings.Canvas.UsingWhiteboard;
            SaveSettingsToFile();
            if (Settings.Canvas.UsingWhiteboard) {
                if (inkColor == 5) lastBoardInkColor = 0;
                ICCWaterMarkDark.Visibility = Visibility.Visible;
                ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
            }
            else {
                if (inkColor == 0) lastBoardInkColor = 5;
                ICCWaterMarkWhite.Visibility = Visibility.Visible;
                ICCWaterMarkDark.Visibility = Visibility.Collapsed;
            }

            CheckColorTheme(true);
        }

        private void BoardEraserIcon_Click(object sender, RoutedEventArgs e) {
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint ||
                inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke) {
                if (BoardEraserSizePanel.Visibility == Visibility.Collapsed) {
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardEraserSizePanel);
                } else {
                    AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                }
            } else {
                forceEraser = true;
                forcePointEraser = true;
                double k = 1;
                if (Settings.Canvas.EraserShapeType == 0) {
                    switch (BoardComboBoxEraserSize.SelectedIndex)
                    {
                        case 0:
                            k = 0.5;
                            break;
                        case 1:
                            k = 0.8;
                            break;
                        case 3:
                            k = 1.25;
                            break;
                        case 4:
                            k = 1.8;
                            break;
                    }
                    inkCanvas.EraserShape = new EllipseStylusShape(k * 90, k * 90);
                } else if (Settings.Canvas.EraserShapeType == 1) {
                    switch (BoardComboBoxEraserSize.SelectedIndex)
                    {
                        case 0:
                            k = 0.7;
                            break;
                        case 1:
                            k = 0.9;
                            break;
                        case 3:
                            k = 1.2;
                            break;
                        case 4:
                            k = 1.6;
                            break;
                    }
                    inkCanvas.EraserShape = new RectangleStylusShape(k * 90 * 0.6, k * 90);
                }
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                drawingShapeMode = 0;

                inkCanvas_EditingModeChanged(inkCanvas, null);
                CancelSingleFingerDragMode();

                HideSubPanels("eraser");
            }
        }

        private void BoardEraserIconByStrokes_Click(object sender, RoutedEventArgs e) {
            //if (BoardEraserByStrokes.Background.ToString() == "#FF679CF4") {
            //    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardDeleteIcon);
            //}
            //else {
                forceEraser = true;
                forcePointEraser = false;

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