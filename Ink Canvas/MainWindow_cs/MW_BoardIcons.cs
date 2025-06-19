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
                
                // 使用统一的方法应用橡皮擦形状，确保一致性
                ApplyCurrentEraserShape();
                
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