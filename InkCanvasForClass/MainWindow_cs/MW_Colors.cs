using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Ink_Canvas.Popups;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        private int inkColor = 1;

        private void ColorSwitchCheck() {
            //HideSubPanels("color");
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent) {
                if (currentMode == 1) {
                    currentMode = 0;
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                    AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);
                }

                BtnHideInkCanvas_Click(null, null);
            }

            var strokes = inkCanvas.GetSelectedStrokes();
            if (strokes.Count != 0) {
                foreach (var stroke in strokes)
                    try {
                        stroke.DrawingAttributes.Color = inkCanvas.DefaultDrawingAttributes.Color;
                    }
                    catch {
                        // ignored
                    }
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
            else {
                inkCanvas.IsManipulationEnabled = true;
                drawingShapeMode = 0;
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                CancelSingleFingerDragMode();
                forceEraser = false;
                CheckColorTheme();
            }

            isLongPressSelected = false;
        }

        private bool isUselightThemeColor = false, isDesktopUselightThemeColor = false;
        private int penType = 0; // 0是签字笔，1是荧光笔
        private int lastDesktopInkColor = 1, lastBoardInkColor = 5;
        private int highlighterColor = 102;

        private void CheckColorTheme(bool changeColorTheme = false) {
            if (changeColorTheme)
                if (currentMode != 0) {
                    var bgC = BoardPagesSettingsList[CurrentWhiteboardIndex - 1].BackgroundColor;
                    GridBackgroundCover.Background = new SolidColorBrush(BoardBackgroundColors[(int)bgC]);
                    if (bgC == BlackboardBackgroundColorEnum.BlackBoardGreen
                        || bgC == BlackboardBackgroundColorEnum.BlueBlack
                        || bgC == BlackboardBackgroundColorEnum.GrayBlack
                        || bgC == BlackboardBackgroundColorEnum.RealBlack) {
                        WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        isUselightThemeColor = true;
                    } else {
                        WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(22, 22,22));
                        WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(22, 22, 22));
                        BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(22, 22, 22));
                        isUselightThemeColor = false;
                    }
                }

            if (currentMode == 0) {
                isUselightThemeColor = isDesktopUselightThemeColor;
                inkColor = lastDesktopInkColor;
            }
            else {
                inkColor = lastBoardInkColor;
            }

            double alpha = inkCanvas.DefaultDrawingAttributes.Color.A;

            if (penType == 0) {
                if (inkColor == 0) {
                    // Black
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 0, 0, 0);
                }
                else if (inkColor == 5) {
                    // White
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 255, 255, 255);
                }
                else if (isUselightThemeColor) {
                    if (inkColor == 1)
                        // Red
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 239, 68, 68);
                    else if (inkColor == 2)
                        // Green
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 34, 197, 94);
                    else if (inkColor == 3)
                        // Blue
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 59, 130, 246);
                    else if (inkColor == 4)
                        // Yellow
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 250, 204, 21);
                    else if (inkColor == 6)
                        // Pink
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 236, 72, 153);
                    else if (inkColor == 7)
                        // Teal (亮色)
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 20, 184, 166);
                    else if (inkColor == 8)
                        // Orange (亮色)
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 249, 115, 22);
                }
                else {
                    if (inkColor == 1)
                        // Red
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 220, 38, 38);
                    else if (inkColor == 2)
                        // Green
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 22, 163, 74);
                    else if (inkColor == 3)
                        // Blue
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 37, 99, 235);
                    else if (inkColor == 4)
                        // Yellow
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 234, 179, 8);
                    else if (inkColor == 6)
                        // Pink ( Purple )
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 147, 51, 234);
                    else if (inkColor == 7)
                        // Teal (暗色)
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 13, 148, 136);
                    else if (inkColor == 8)
                        // Orange (暗色)
                        inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 234, 88, 12);
                }
            }
            else if (penType == 1) {
                if (highlighterColor == 100)
                    // Black
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(0, 0, 0);
                else if (highlighterColor == 101)
                    // White
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(250, 250, 250);
                else if (highlighterColor == 102)
                    // Red
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(239, 68, 68);
                else if (highlighterColor == 103)
                    // Yellow
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(253, 224, 71);
                else if (highlighterColor == 104)
                    // Green
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(74, 222, 128);
                else if (highlighterColor == 105)
                    // Zinc
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(113, 113, 122);
                else if (highlighterColor == 106)
                    // Blue
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(59, 130, 246);
                else if (highlighterColor == 107)
                    // Purple
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(168, 85, 247);
                else if (highlighterColor == 108)
                    // teal
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(45, 212, 191);
                else if (highlighterColor == 109)
                    // Orange
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(249, 115, 22);
            }

            if (isUselightThemeColor) {
                // 亮系
                // 亮色的红色
                BorderPenColorRed.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                BoardBorderPenColorRed.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                // 亮色的绿色
                BorderPenColorGreen.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                BoardBorderPenColorGreen.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                // 亮色的蓝色
                BorderPenColorBlue.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                BoardBorderPenColorBlue.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                // 亮色的黄色
                BorderPenColorYellow.Background = new SolidColorBrush(Color.FromRgb(250, 204, 21));
                BoardBorderPenColorYellow.Background = new SolidColorBrush(Color.FromRgb(250, 204, 21));
                // 亮色的粉色
                BorderPenColorPink.Background = new SolidColorBrush(Color.FromRgb(236, 72, 153));
                BoardBorderPenColorPink.Background = new SolidColorBrush(Color.FromRgb(236, 72, 153));
                // 亮色的Teal
                BorderPenColorTeal.Background = new SolidColorBrush(Color.FromRgb(20, 184, 166));
                BoardBorderPenColorTeal.Background = new SolidColorBrush(Color.FromRgb(20, 184, 166));
                // 亮色的Orange
                BorderPenColorOrange.Background = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                BoardBorderPenColorOrange.Background = new SolidColorBrush(Color.FromRgb(249, 115, 22));

                var newImageSource = new BitmapImage();
                newImageSource.BeginInit();
                newImageSource.UriSource = new Uri("/Resources/Icons-Fluent/ic_fluent_weather_moon_24_regular.png",
                    UriKind.RelativeOrAbsolute);
                newImageSource.EndInit();
                ColorThemeSwitchIcon.Source = newImageSource;
                BoardColorThemeSwitchIcon.Source = newImageSource;

                ColorThemeSwitchTextBlock.Text = "暗系";
                BoardColorThemeSwitchTextBlock.Text = "暗系";
            }
            else {
                // 暗系
                // 暗色的红色
                BorderPenColorRed.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                BoardBorderPenColorRed.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                // 暗色的绿色
                BorderPenColorGreen.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                BoardBorderPenColorGreen.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                // 暗色的蓝色
                BorderPenColorBlue.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                BoardBorderPenColorBlue.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                // 暗色的黄色
                BorderPenColorYellow.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                BoardBorderPenColorYellow.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                // 暗色的紫色对应亮色的粉色
                BorderPenColorPink.Background = new SolidColorBrush(Color.FromRgb(147, 51, 234));
                BoardBorderPenColorPink.Background = new SolidColorBrush(Color.FromRgb(147, 51, 234));
                // 暗色的Teal
                BorderPenColorTeal.Background = new SolidColorBrush(Color.FromRgb(13, 148, 136));
                BoardBorderPenColorTeal.Background = new SolidColorBrush(Color.FromRgb(13, 148, 136));
                // 暗色的Orange
                BorderPenColorOrange.Background = new SolidColorBrush(Color.FromRgb(234, 88, 12));
                BoardBorderPenColorOrange.Background = new SolidColorBrush(Color.FromRgb(234, 88, 12));

                var newImageSource = new BitmapImage();
                newImageSource.BeginInit();
                newImageSource.UriSource = new Uri("/Resources/Icons-Fluent/ic_fluent_weather_sunny_24_regular.png",
                    UriKind.RelativeOrAbsolute);
                newImageSource.EndInit();
                ColorThemeSwitchIcon.Source = newImageSource;
                BoardColorThemeSwitchIcon.Source = newImageSource;

                ColorThemeSwitchTextBlock.Text = "亮系";
                BoardColorThemeSwitchTextBlock.Text = "亮系";
            }

            // 改变选中提示
            ViewboxBtnColorBlackContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorBlueContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorGreenContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorRedContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorYellowContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorWhiteContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorPinkContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorTealContent.Visibility = Visibility.Collapsed;
            ViewboxBtnColorOrangeContent.Visibility = Visibility.Collapsed;

            BoardViewboxBtnColorBlackContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorBlueContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorGreenContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorRedContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorYellowContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorWhiteContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorPinkContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorTealContent.Visibility = Visibility.Collapsed;
            BoardViewboxBtnColorOrangeContent.Visibility = Visibility.Collapsed;

            HighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Collapsed;
            HighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Collapsed;

            BoardHighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Collapsed;
            BoardHighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Collapsed;

            switch (inkColor) {
                case 0:
                    ViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    break;
                case 1:
                    ViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    break;
                case 2:
                    ViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    break;
                case 3:
                    ViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    break;
                case 4:
                    ViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    break;
                case 5:
                    ViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    break;
                case 6:
                    ViewboxBtnColorPinkContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorPinkContent.Visibility = Visibility.Visible;
                    break;
                case 7:
                    ViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    break;
                case 8:
                    ViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    break;
            }

            switch (highlighterColor) {
                case 100:
                    HighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    break;
                case 101:
                    HighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    break;
                case 102:
                    HighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    break;
                case 103:
                    HighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    break;
                case 104:
                    HighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    break;
                case 105:
                    HighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Visible;
                    break;
                case 106:
                    HighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    break;
                case 107:
                    HighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Visible;
                    break;
                case 108:
                    HighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    break;
                case 109:
                    HighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void CheckLastColor(int inkColor, bool isHighlighter = false) {
            if (isHighlighter == true) {
                highlighterColor = inkColor;
            }
            else {
                if (currentMode == 0) lastDesktopInkColor = inkColor;
                else lastBoardInkColor = inkColor;
            }
        }

        private async void CheckPenTypeUIState() {
            if (penType == 0) {
                DefaultPenPropsPanel.Visibility = Visibility.Visible;
                DefaultPenColorsPanel.Visibility = Visibility.Visible;
                HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
                HighlighterPenPropsPanel.Visibility = Visibility.Collapsed;
                DefaultPenTabButton.Opacity = 1;
                DefaultPenTabButtonText.FontWeight = FontWeights.Bold;
                DefaultPenTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                DefaultPenTabButtonText.FontSize = 9.5;
                DefaultPenTabButton.Background = new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));
                DefaultPenTabButtonIndicator.Visibility = Visibility.Visible;
                HighlightPenTabButton.Opacity = 0.9;
                HighlightPenTabButtonText.FontWeight = FontWeights.Normal;
                HighlightPenTabButtonText.FontSize = 9;
                HighlightPenTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                HighlightPenTabButton.Background = new SolidColorBrush(Colors.Transparent);
                HighlightPenTabButtonIndicator.Visibility = Visibility.Collapsed;

                BoardDefaultPenPropsPanel.Visibility = Visibility.Visible;
                BoardDefaultPenColorsPanel.Visibility = Visibility.Visible;
                BoardHighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
                BoardHighlighterPenPropsPanel.Visibility = Visibility.Collapsed;
                BoardDefaultPenTabButton.Opacity = 1;
                BoardDefaultPenTabButtonText.FontWeight = FontWeights.Bold;
                BoardDefaultPenTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                BoardDefaultPenTabButtonText.FontSize = 9.5;
                BoardDefaultPenTabButton.Background = new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));
                BoardDefaultPenTabButtonIndicator.Visibility = Visibility.Visible;
                BoardHighlightPenTabButton.Opacity = 0.9;
                BoardHighlightPenTabButtonText.FontWeight = FontWeights.Normal;
                BoardHighlightPenTabButtonText.FontSize = 9;
                BoardHighlightPenTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                BoardHighlightPenTabButton.Background = new SolidColorBrush(Colors.Transparent);
                BoardHighlightPenTabButtonIndicator.Visibility = Visibility.Collapsed;

                // PenPalette.Margin = new Thickness(-160, -200, -33, 32);
                await Dispatcher.InvokeAsync(() => {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -200, -33, 32),
                        EasingFunction = new CubicEase()
                    };
                    PenPalette.BeginAnimation(MarginProperty, marginAnimation);
                });

                await Dispatcher.InvokeAsync(() => {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -200, -33, 50),
                        EasingFunction = new CubicEase()
                    };
                    BoardPenPaletteGrid.BeginAnimation(MarginProperty, marginAnimation);
                });


                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() => { PenPalette.Margin = new Thickness(-160, -200, -33, 32); });

                await Dispatcher.InvokeAsync(() => { BoardPenPaletteGrid.Margin = new Thickness(-160, -200, -33, 50); });
            }
            else if (penType == 1) {
                DefaultPenPropsPanel.Visibility = Visibility.Collapsed;
                DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
                HighlighterPenColorsPanel.Visibility = Visibility.Visible;
                HighlighterPenPropsPanel.Visibility = Visibility.Visible;
                DefaultPenTabButton.Opacity = 0.9;
                DefaultPenTabButtonText.FontWeight = FontWeights.Normal;
                DefaultPenTabButtonText.FontSize = 9;
                DefaultPenTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                DefaultPenTabButton.Background = new SolidColorBrush(Colors.Transparent);
                DefaultPenTabButtonIndicator.Visibility = Visibility.Collapsed;
                HighlightPenTabButton.Opacity = 1;
                HighlightPenTabButtonText.FontWeight = FontWeights.Bold;
                HighlightPenTabButtonText.FontSize = 9.5;
                HighlightPenTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                HighlightPenTabButton.Background = new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));
                HighlightPenTabButtonIndicator.Visibility = Visibility.Visible;

                BoardDefaultPenPropsPanel.Visibility = Visibility.Collapsed;
                BoardDefaultPenColorsPanel.Visibility = Visibility.Collapsed;
                BoardHighlighterPenColorsPanel.Visibility = Visibility.Visible;
                BoardHighlighterPenPropsPanel.Visibility = Visibility.Visible;
                BoardDefaultPenTabButton.Opacity = 0.9;
                BoardDefaultPenTabButtonText.FontWeight = FontWeights.Normal;
                BoardDefaultPenTabButtonText.FontSize = 9;
                BoardDefaultPenTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                BoardDefaultPenTabButton.Background = new SolidColorBrush(Colors.Transparent);
                BoardDefaultPenTabButtonIndicator.Visibility = Visibility.Collapsed;
                BoardHighlightPenTabButton.Opacity = 1;
                BoardHighlightPenTabButtonText.FontWeight = FontWeights.Bold;
                BoardHighlightPenTabButtonText.FontSize = 9.5;
                BoardHighlightPenTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                BoardHighlightPenTabButton.Background = new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));
                BoardHighlightPenTabButtonIndicator.Visibility = Visibility.Visible;

                // PenPalette.Margin = new Thickness(-160, -157, -33, 32);
                await Dispatcher.InvokeAsync(() => {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -157, -33, 32),
                        EasingFunction = new CubicEase()
                    };
                    PenPalette.BeginAnimation(MarginProperty, marginAnimation);
                });

                await Dispatcher.InvokeAsync(() => {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -154, -33, 50),
                        EasingFunction = new CubicEase()
                    };
                    BoardPenPaletteGrid.BeginAnimation(MarginProperty, marginAnimation);
                });

                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() => { PenPalette.Margin = new Thickness(-160, -157, -33, 32); });

                await Dispatcher.InvokeAsync(() => { BoardPenPaletteGrid.Margin = new Thickness(-160, -154, -33, 50); });
            }
        }

        private void SwitchToDefaultPen(object sender, MouseButtonEventArgs e) {
            penType = 0;
            CheckPenTypeUIState();
            CheckColorTheme();
            drawingAttributes.Width = Settings.Canvas.InkWidth;
            drawingAttributes.Height = Settings.Canvas.InkWidth;
            drawingAttributes.StylusTip = StylusTip.Ellipse;
            drawingAttributes.IsHighlighter = false;
        }

        private void SwitchToHighlighterPen(object sender, MouseButtonEventArgs e) {
            penType = 1;
            CheckPenTypeUIState();
            CheckColorTheme();
            drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
            drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
            drawingAttributes.StylusTip = StylusTip.Rectangle;
            drawingAttributes.IsHighlighter = true;
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(0);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorRed_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(1);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorGreen_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(2);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorBlue_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(3);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorYellow_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(4);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorWhite_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(5);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorPink_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(6);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorOrange_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(8);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnColorTeal_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(7);
            forceEraser = false;
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorBlack_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(100, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorWhite_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(101, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorRed_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(102, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorYellow_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(103, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorGreen_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(104, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorZinc_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(105, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorBlue_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(106, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorPurple_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(107, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorTeal_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(108, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void BtnHighlighterColorOrange_Click(object sender, RoutedEventArgs e) {
            CheckLastColor(109, true);
            penType = 1;
            forceEraser = false;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }
       
        private Color StringToColor(string colorStr) {
            var argb = new byte[4];
            for (var i = 0; i < 4; i++) {
                var charArray = colorStr.Substring(i * 2 + 1, 2).ToCharArray();
                var b1 = toByte(charArray[0]);
                var b2 = toByte(charArray[1]);
                argb[i] = (byte)(b2 | (b1 << 4));
            }

            return Color.FromArgb(argb[0], argb[1], argb[2], argb[3]); //#FFFFFFFF
        }

        private static byte toByte(char c) {
            var b = (byte)"0123456789ABCDEF".IndexOf(c);
            return b;
        }

        #region PenPaletteV2

        private void PenPaletteV2Init() {
            FloatingToolBarV2.PenPaletteV2_ColorSelectionChanged += PenpaletteV2_ColorSelectionChanged;
            FloatingToolBarV2.PenPaletteV2_ColorModeChanged += PenpaletteV2_ColorModeChanged;
            FloatingToolBarV2.PenPaletteV2_CustomColorChanged += PenpaletteV2_CustomColorChanged;
            FloatingToolBarV2.PenPaletteV2_PenModeChanged += PenpaletteV2_PenModeChanged;
            FloatingToolBarV2.PenPaletteV2.SelectedColor = ColorPalette.ColorPaletteColor.ColorRed;
        }

        private void PenpaletteV2_ColorSelectionChanged(object sender, ColorPalette.ColorSelectionChangedEventArgs e) {
            if (e.TriggerMode == ColorPalette.TriggerMode.TriggeredByCode) return;
            drawingAttributes.Color = FloatingToolBarV2.PenPaletteV2.GetColor(e.NowColor, false, null);
        }

        private void PenpaletteV2_ColorModeChanged(object sender, ColorPalette.ColorModeChangedEventArgs e) {
            if (e.TriggerMode == ColorPalette.TriggerMode.TriggeredByCode) return;
            drawingAttributes.Color = FloatingToolBarV2.PenPaletteV2.GetColor(FloatingToolBarV2.PenPaletteV2.SelectedColor, false, null);
        }

        private void PenpaletteV2_CustomColorChanged(object sender, ColorPalette.CustomColorChangedEventArgs e) {
            if (e.TriggerMode == ColorPalette.TriggerMode.TriggeredByCode) return;
            if (FloatingToolBarV2.PenPaletteV2.SelectedColor == ColorPalette.ColorPaletteColor.ColorCustom) 
                drawingAttributes.Color = e.NowColor??new Color();
        }

        private void PenpaletteV2_PenModeChanged(object sender, ColorPalette.PenModeChangedEventArgs e) {
            penType = e.NowMode == ColorPalette.PenMode.HighlighterMode ? 1 : 0;
            drawingAttributes.Width = e.NowMode == ColorPalette.PenMode.HighlighterMode ? Settings.Canvas.HighlighterWidth / 2 : Settings.Canvas.InkWidth;
            drawingAttributes.Height = e.NowMode == ColorPalette.PenMode.HighlighterMode ? Settings.Canvas.HighlighterWidth : Settings.Canvas.InkWidth;
            drawingAttributes.StylusTip = e.NowMode == ColorPalette.PenMode.HighlighterMode ? StylusTip.Rectangle : StylusTip.Ellipse;
            drawingAttributes.IsHighlighter = e.NowMode == ColorPalette.PenMode.HighlighterMode;
        }

        #endregion
    }
}