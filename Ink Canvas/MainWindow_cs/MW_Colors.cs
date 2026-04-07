using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 当前墨水颜色
        /// </summary>
        private int inkColor = 1;

        /// <summary>
        /// 颜色切换检查，处理颜色变更和相关UI状态
        /// </summary>
        /// <param name="hidePanels">是否隐藏面板</param>
        /// <remarks>
        /// - 隐藏相关面板
        /// - 处理透明背景情况
        /// - 处理选中笔画的颜色更新
        /// - 提交笔画属性历史记录
        /// - 设置工具模式为墨水模式
        /// - 取消单指拖动模式
        /// - 检查颜色主题
        /// </remarks>
        private void ColorSwitchCheck(bool hidePanels = true)
        {
            if (hidePanels)
            {
                HideSubPanels("color");
            }
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                if (currentMode == 1)
                {
                    currentMode = 0;
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                    AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                    // 在PPT模式下隐藏手势面板和手势按钮
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                    EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                    SyncPdfPageSidebarWithCanvas();
                }

                BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
            }

            var strokes = inkCanvas.GetSelectedStrokes();
            if (strokes.Count != 0)
            {
                foreach (var stroke in strokes)
                    try
                    {
                        stroke.DrawingAttributes.Color = inkCanvas.DefaultDrawingAttributes.Color;
                    }
                    catch
                    {
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
            else
            {
                inkCanvas.IsManipulationEnabled = true;
                drawingShapeMode = 0;
                // 使用集中化的工具模式切换方法
                SetCurrentToolMode(InkCanvasEditingMode.Ink);
                CancelSingleFingerDragMode();
                CheckColorTheme();
            }

            isLongPressSelected = false;
        }

        /// <summary>
        /// 是否使用亮色主题颜色
        /// </summary>
        private bool isUselightThemeColor;

        /// <summary>
        /// 桌面模式是否使用亮色主题颜色
        /// </summary>
        private bool isDesktopUselightThemeColor;

        /// <summary>
        /// 笔类型（0是签字笔，1是荧光笔）
        /// </summary>
        private int penType;

        /// <summary>
        /// 桌面模式最后使用的墨水颜色
        /// </summary>
        private int lastDesktopInkColor = 1;

        /// <summary>
        /// 白板模式最后使用的墨水颜色
        /// </summary>
        private int lastBoardInkColor = 5;

        /// <summary>
        /// 荧光笔颜色
        /// </summary>
        private int highlighterColor = 102;

        /// <summary>
        /// 根据当前模式、画笔类型与主题设置，应用并同步画布颜色、笔触颜色与界面配色指示器。
        /// </summary>
        /// <param name="changeColorTheme">为 true 时（且非桌面模式）根据白板/黑板设置刷新背景色、水印色和亮/暗主题标志；为 false 则仅同步颜色相关状态。</param>
        private void CheckColorTheme(bool changeColorTheme = false)
        {
            if (changeColorTheme)
                if (currentMode != 0)
                {
                    if (Settings.Canvas.UsingWhiteboard)
                    {
                        // 检查是否有自定义背景色，如果有则使用自定义背景色
                        if (CustomBackgroundColor.HasValue)
                        {
                            GridBackgroundCover.Background = new SolidColorBrush(CustomBackgroundColor.Value);
                        }
                        else
                        {
                            GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        }
                        WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                        WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                        BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                        isUselightThemeColor = false;
                    }
                    else
                    {
                        // 黑板模式下，检查是否有自定义背景色
                        if (CustomBackgroundColor.HasValue)
                        {
                            GridBackgroundCover.Background = new SolidColorBrush(CustomBackgroundColor.Value);
                        }
                        else
                        {
                            GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                        }
                        WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                        isUselightThemeColor = true;
                    }
                }

            if (currentMode == 0)
            {
                isUselightThemeColor = isDesktopUselightThemeColor;
                inkColor = lastDesktopInkColor;
            }
            else
            {
                inkColor = lastBoardInkColor;
            }

            double alpha = inkCanvas.DefaultDrawingAttributes.Color.A;
            if (penType == 0 && Settings?.Canvas != null)
            {
                double settingAlpha = Settings.Canvas.InkAlpha;
                if (settingAlpha >= 0 && settingAlpha <= 255)
                    alpha = settingAlpha;
            }

            if (penType == 0)
            {
                if (inkColor == 0)
                {
                    // Black
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 0, 0, 0);
                }
                else if (inkColor == 5)
                {
                    // White
                    inkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb((byte)alpha, 255, 255, 255);
                }
                else if (isUselightThemeColor)
                {
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
                else
                {
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
            else if (penType == 1)
            {
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

            var penPalette = BorderPenColorPaletteControl as MainWindow_controls.PenColorPalette;
            var boardPenPalette = BoardBorderPenColorPaletteControl as MainWindow_controls.PenColorPalette;
            penPalette?.SetUseLightThemeColors(isUselightThemeColor);
            boardPenPalette?.SetUseLightThemeColors(isUselightThemeColor);
            penPalette?.SetSelectedColorCode(inkColor);
            boardPenPalette?.SetSelectedColorCode(inkColor);

            var highlighterPalette = (object)HighlighterPenColorsPanel as MainWindow_controls.HighlighterColorPalette;
            var boardHighlighterPalette = (object)BoardHighlighterPenColorsPanel as MainWindow_controls.HighlighterColorPalette;
            highlighterPalette?.SetSelectedColorCode(highlighterColor);
            boardHighlighterPalette?.SetSelectedColorCode(highlighterColor);

            // 更新快捷调色盘选择指示器
            if (penType == 0)
            {
                UpdateQuickColorPaletteIndicator(inkCanvas.DefaultDrawingAttributes.Color);
            }
        }

        /// <summary>
        /// 检查并更新最后使用的颜色
        /// </summary>
        /// <param name="inkColor">墨水颜色</param>
        /// <param name="isHighlighter">是否为荧光笔</param>
        /// <remarks>
        /// - 如果是荧光笔，更新荧光笔颜色
        /// - 否则，根据当前模式更新相应的最后使用颜色
        /// </remarks>
        private void CheckLastColor(int inkColor, bool isHighlighter = false)
        {
            if (isHighlighter)
            {
                highlighterColor = inkColor;
            }
            else
            {
                if (currentMode == 0) lastDesktopInkColor = inkColor;
                else lastBoardInkColor = inkColor;
            }
        }

        /// <summary>
        /// 检查并更新笔类型UI状态
        /// </summary>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// - 根据笔类型显示或隐藏相应的面板
        /// - 更新标签按钮的样式和状态
        /// - 执行面板动画
        /// - 处理签字笔和荧光笔的不同UI状态
        /// </remarks>
        private async void CheckPenTypeUIState()
        {
            if (penType == 0)
            {
                BorderPenSettingsControl?.SetPenTypeVisualState(0);
                BoardPenSettingsControl?.SetPenTypeVisualState(0);
                BorderPenColorPaletteControl.Visibility = Visibility.Visible;
                HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;

                BoardBorderPenColorPaletteControl.Visibility = Visibility.Visible;
                BoardHighlighterPenColorsPanel.Visibility = Visibility.Collapsed;

                // PenPalette.Margin = new Thickness(-160, -200, -33, 32);
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -200, -33, 32),
                        EasingFunction = new CubicEase()
                    };
                    PenPalette.BeginAnimation(MarginProperty, marginAnimation);
                });

                await Dispatcher.InvokeAsync(() =>
                {
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
            else if (penType == 1)
            {
                BorderPenSettingsControl?.SetPenTypeVisualState(1);
                BoardPenSettingsControl?.SetPenTypeVisualState(1);
                BorderPenColorPaletteControl.Visibility = Visibility.Collapsed;
                HighlighterPenColorsPanel.Visibility = Visibility.Visible;

                BoardBorderPenColorPaletteControl.Visibility = Visibility.Collapsed;
                BoardHighlighterPenColorsPanel.Visibility = Visibility.Visible;

                // PenPalette.Margin = new Thickness(-160, -157, -33, 32);
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = PenPalette.Margin,
                        To = new Thickness(-160, -157, -33, 32),
                        EasingFunction = new CubicEase()
                    };
                    PenPalette.BeginAnimation(MarginProperty, marginAnimation);
                });

                await Dispatcher.InvokeAsync(() =>
                {
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

        /// <summary>
        /// 切换到默认签字笔
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 设置笔类型为0（签字笔）
        /// - 更新笔类型UI状态
        /// - 检查颜色主题
        /// - 设置画笔属性（宽度、高度、笔尖形状、是否为荧光笔）
        /// </remarks>
        private void SwitchToDefaultPen(object sender, MouseButtonEventArgs e)
        {
            penType = 0;
            CheckPenTypeUIState();
            CheckColorTheme();
            drawingAttributes.Width = Settings.Canvas.InkWidth;
            drawingAttributes.Height = Settings.Canvas.InkWidth;
            drawingAttributes.StylusTip = StylusTip.Ellipse;
            drawingAttributes.IsHighlighter = false;
        }

        /// <summary>
        /// 切换到荧光笔
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 设置笔类型为1（荧光笔）
        /// - 更新笔类型UI状态
        /// - 检查颜色主题
        /// - 设置画笔属性（宽度、高度、笔尖形状、是否为荧光笔）
        /// - 确保荧光笔模式切换后正确更新颜色和快捷调色板指示器
        /// </remarks>
        private void SwitchToHighlighterPen(object sender, MouseButtonEventArgs e)
        {
            penType = 1;
            CheckPenTypeUIState();
            CheckColorTheme();
            drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
            drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
            drawingAttributes.StylusTip = StylusTip.Rectangle;
            drawingAttributes.IsHighlighter = true;

            // 确保荧光笔模式切换后正确更新颜色和快捷调色板指示器
            ColorSwitchCheck(false);
        }

        /// <summary>
        /// 处理黑色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(0);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理红色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorRed_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(1);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理绿色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorGreen_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(2);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理蓝色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorBlue_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(3);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理黄色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorYellow_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(4);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理白色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorWhite_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(5);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理粉色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorPink_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(6);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理橙色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorOrange_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(8);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理青色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnColorTeal_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(7);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔黑色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorBlack_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(100, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔白色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorWhite_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(101, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔红色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorRed_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(102, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔黄色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorYellow_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(103, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔绿色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorGreen_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(104, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔锌色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorZinc_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(105, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔蓝色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorBlue_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(106, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔紫色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorPurple_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(107, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔青色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorTeal_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(108, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        /// <summary>
        /// 处理荧光笔橙色按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BtnHighlighterColorOrange_Click(object sender, RoutedEventArgs e)
        {
            CheckLastColor(109, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void HighlighterColorPalette_ColorSelected(object sender, RoutedEventArgs e)
        {
            var palette = sender as MainWindow_controls.HighlighterColorPalette;
            if (palette == null)
            {
                return;
            }

            CheckLastColor(palette.SelectedColorCode, true);
            penType = 1;
            CheckPenTypeUIState();
            ColorSwitchCheck();
        }

        private void PenColorPalette_ColorSelected(object sender, RoutedEventArgs e)
        {
            var palette = sender as MainWindow_controls.PenColorPalette;
            if (palette == null)
            {
                return;
            }

            CheckLastColor(palette.SelectedColorCode);
            ColorSwitchCheck();
        }

        /// <summary>
        /// 将字符串转换为颜色对象
        /// </summary>
        /// <param name="colorStr">颜色字符串（格式：#FFFFFFFF）</param>
        /// <returns>颜色对象</returns>
        /// <remarks>
        /// - 解析颜色字符串为ARGB值
        /// - 转换为Color对象返回
        /// </remarks>
        private Color StringToColor(string colorStr)
        {
            var argb = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                var charArray = colorStr.Substring(i * 2 + 1, 2).ToCharArray();
                var b1 = toByte(charArray[0]);
                var b2 = toByte(charArray[1]);
                argb[i] = (byte)(b2 | (b1 << 4));
            }

            return Color.FromArgb(argb[0], argb[1], argb[2], argb[3]); //#FFFFFFFF
        }

        /// <summary>
        /// 将字符转换为字节
        /// </summary>
        /// <param name="c">字符</param>
        /// <returns>字节值</returns>
        /// <remarks>
        /// - 将十六进制字符转换为对应的字节值
        /// - 支持0-9和A-F字符
        /// </remarks>
        private static byte toByte(char c)
        {
            var b = (byte)"0123456789ABCDEF".IndexOf(c);
            return b;
        }
    }
}
