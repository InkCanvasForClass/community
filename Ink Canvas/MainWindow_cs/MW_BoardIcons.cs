using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 处理背景颜色按钮点击事件，显示或隐藏背景颜色选项面板
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查应用是否已加载
        /// - 创建背景选项面板（如果不存在）
        /// - 显示或隐藏背景选项面板
        /// - 隐藏其他可能显示的面板
        /// - 处理白板/黑板模式切换
        /// - 更新背景颜色和墨迹颜色
        /// </remarks>
        private void BoardChangeBackgroundColorBtn_MouseUp(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            if (BackgroundPalette.Visibility == Visibility.Visible)
            {
                AnimationsHelper.HideWithSlideAndFade(BackgroundPalette);
            }
            else
            {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
                AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

                LoadCustomBackgroundColor();
                UpdateBackgroundButtonsState();
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BackgroundPalette);
            }
        }

        private void WhiteboardModeBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Settings.Canvas.UsingWhiteboard = true;
            SaveSettingsToFile();
            ICCWaterMarkDark.Visibility = Visibility.Visible;
            ICCWaterMarkWhite.Visibility = Visibility.Collapsed;

            Color defaultWhiteboardColor = Color.FromRgb(255, 255, 255);

            if (currentMode == 1)
            {
                GridBackgroundCover.Background = new SolidColorBrush(defaultWhiteboardColor);
                UpdateRGBSliders(defaultWhiteboardColor);
                CustomBackgroundColor = defaultWhiteboardColor;
                string colorHex = $"#{defaultWhiteboardColor.R:X2}{defaultWhiteboardColor.G:X2}{defaultWhiteboardColor.B:X2}";
                Settings.Canvas.CustomBackgroundColor = colorHex;
                SaveSettingsToFile();
            }

            CheckLastColor(0);
            forceEraser = false;
            CheckColorTheme(true);
            UpdateBackgroundButtonsState();
        }

        private void BlackboardModeBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Settings.Canvas.UsingWhiteboard = false;
            SaveSettingsToFile();
            ICCWaterMarkWhite.Visibility = Visibility.Visible;
            ICCWaterMarkDark.Visibility = Visibility.Collapsed;

            Color defaultBlackboardColor = Color.FromRgb(22, 41, 36);

            if (currentMode == 1)
            {
                GridBackgroundCover.Background = new SolidColorBrush(defaultBlackboardColor);
                UpdateRGBSliders(defaultBlackboardColor);
                CustomBackgroundColor = defaultBlackboardColor;
                string colorHex = $"#{defaultBlackboardColor.R:X2}{defaultBlackboardColor.G:X2}{defaultBlackboardColor.B:X2}";
                Settings.Canvas.CustomBackgroundColor = colorHex;
                SaveSettingsToFile();
            }

            CheckLastColor(5);
            forceEraser = false;
            CheckColorTheme(true);
            UpdateBackgroundButtonsState();
        }

        private void BackgroundRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BackgroundRValue != null)
            {
                BackgroundRValue.Text = ((int)e.NewValue).ToString();
                UpdateColorPreviewFromSliders();
            }
        }

        private void BackgroundGSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BackgroundGValue != null)
            {
                BackgroundGValue.Text = ((int)e.NewValue).ToString();
                UpdateColorPreviewFromSliders();
            }
        }

        private void BackgroundBSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BackgroundBValue != null)
            {
                BackgroundBValue.Text = ((int)e.NewValue).ToString();
                UpdateColorPreviewFromSliders();
            }
        }

        private void ApplyBackgroundColorBtn_Click(object sender, RoutedEventArgs e)
        {
            Color selectedColor = Color.FromRgb(
                (byte)BackgroundRSlider.Value,
                (byte)BackgroundGSlider.Value,
                (byte)BackgroundBSlider.Value
            );
            ApplyCustomBackgroundColor(selectedColor);
        }

        private void UpdateColorPreviewFromSliders()
        {
            if (BackgroundColorPreview != null)
            {
                Color previewColor = Color.FromRgb(
                    (byte)BackgroundRSlider.Value,
                    (byte)BackgroundGSlider.Value,
                    (byte)BackgroundBSlider.Value
                );
                BackgroundColorPreview.Background = new SolidColorBrush(previewColor);
            }
        }

        private void UpdateBackgroundButtonsState()
        {
            if (WhiteboardModeBtn != null)
            {
                WhiteboardModeBtn.Background = Settings.Canvas.UsingWhiteboard ?
                    new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) :
                    new SolidColorBrush(Colors.LightGray);
                if (WhiteboardModeBtn.Child is TextBlock whiteboardText)
                {
                    whiteboardText.Foreground = Settings.Canvas.UsingWhiteboard ?
                        new SolidColorBrush(Colors.White) :
                        new SolidColorBrush(Colors.Black);
                }
            }

            if (BlackboardModeBtn != null)
            {
                BlackboardModeBtn.Background = !Settings.Canvas.UsingWhiteboard ?
                    new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) :
                    new SolidColorBrush(Colors.LightGray);
                if (BlackboardModeBtn.Child is TextBlock blackboardText)
                {
                    blackboardText.Foreground = !Settings.Canvas.UsingWhiteboard ?
                        new SolidColorBrush(Colors.White) :
                        new SolidColorBrush(Colors.Black);
                }
            }
        }

        /// <summary>
        /// 当前自定义背景色
        /// </summary>
        private Color? CustomBackgroundColor { get; set; }

        /// <summary>
        /// 更新颜色预览框的颜色
        /// </summary>
        private void UpdateColorPreview(Border colorPreview, Slider rSlider, Slider gSlider, Slider bSlider)
        {
            Color previewColor = Color.FromRgb(
                (byte)rSlider.Value,
                (byte)gSlider.Value,
                (byte)bSlider.Value
            );
            colorPreview.Background = new SolidColorBrush(previewColor);
        }

        /// <summary>
        /// 应用自定义背景颜色
        /// </summary>
        private void ApplyCustomBackgroundColor(Color color)
        {
            // 保存当前选择的颜色
            CustomBackgroundColor = color;

            // 将颜色转换为十六进制字符串并保存到设置中
            string colorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            Settings.Canvas.CustomBackgroundColor = colorHex;

            // 只在白板或黑板模式下应用自定义背景色
            if (currentMode == 1) // 白板或黑板模式
            {
                // 设置白板/黑板模式下的背景
                GridBackgroundCover.Background = new SolidColorBrush(color);
            }

            // 保存设置
            SaveSettingsToFile();

            // 立即更新界面
            if (BackgroundPalette != null)
            {
                UpdateBackgroundButtonsState();
                UpdateRGBSliders(color); // 更新RGB滑块的值
            }

            // 显示提示信息
            ShowNotification($"已应用自定义背景色: {colorHex}");
        }

        /// <summary>
        /// 从设置中加载自定义背景色
        /// </summary>
        private void LoadCustomBackgroundColor()
        {
            if (!string.IsNullOrEmpty(Settings.Canvas.CustomBackgroundColor))
            {
                try
                {
                    // 解析颜色字符串
                    string colorHex = Settings.Canvas.CustomBackgroundColor;
                    if (colorHex.StartsWith("#") && colorHex.Length == 7) // #RRGGBB 格式
                    {
                        byte r = Convert.ToByte(colorHex.Substring(1, 2), 16);
                        byte g = Convert.ToByte(colorHex.Substring(3, 2), 16);
                        byte b = Convert.ToByte(colorHex.Substring(5, 2), 16);

                        // 保存到内存中
                        CustomBackgroundColor = Color.FromRgb(r, g, b);
                    }
                }
                catch (Exception ex)
                {
                    // 解析失败，根据当前模式设置默认颜色
                    if (!Settings.Canvas.UsingWhiteboard)
                    {
                        // 黑板模式默认颜色
                        CustomBackgroundColor = Color.FromRgb(22, 41, 36);
                    }
                    else
                    {
                        // 白板模式默认颜色
                        CustomBackgroundColor = Color.FromRgb(234, 235, 237);
                    }

                    // 可以在这里记录日志
                    Console.WriteLine($"解析自定义背景色失败: {ex.Message}");
                }
            }
            else
            {
                // 如果没有设置自定义背景色，根据当前模式设置默认颜色
                if (!Settings.Canvas.UsingWhiteboard)
                {
                    // 黑板模式默认颜色
                    CustomBackgroundColor = Color.FromRgb(22, 41, 36);
                }
                else
                {
                    // 白板模式默认颜色
                    CustomBackgroundColor = Color.FromRgb(234, 235, 237);
                }
            }

            // 只在白板或黑板模式下应用自定义背景色
            if (currentMode == 1 && CustomBackgroundColor.HasValue) // 白板或黑板模式
            {
                // 设置白板/黑板模式下的背景
                GridBackgroundCover.Background = new SolidColorBrush(CustomBackgroundColor.Value);

                // 更新RGB滑块的值（如果调色板已经创建）
                if (BackgroundPalette != null && BackgroundPalette.Visibility == Visibility.Visible)
                {
                    UpdateRGBSliders(CustomBackgroundColor.Value);
                }
            }
        }

        /// <summary>
        /// 处理套索工具图标点击事件，切换到选择模式
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 禁用橡皮擦模式
        /// - 禁用形状绘制模式
        /// - 设置当前工具模式为选择模式
        /// - 根据编辑模式设置光标
        /// </remarks>
        private void BoardLassoIcon_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.Select);
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        /// <summary>
        /// 处理橡皮擦图标点击事件，切换到按笔画擦除模式
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 禁用高级橡皮擦系统
        /// - 启用橡皮擦模式
        /// - 设置橡皮擦形状为圆形
        /// - 设置当前工具模式为按笔画擦除

        /// 处理删除图标点击事件，清空画布内容
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 调用钢笔图标点击事件
        /// - 调用符号删除鼠标抬起事件
        /// - 根据设置决定是否清空图片
        /// - 如果设置为清空图片，则清空所有子元素
        /// - 否则，保存非笔画元素并在清空后恢复
        /// </remarks>
        private void BoardSymbolIconDelete_MouseUp(object sender, RoutedEventArgs e)
        {
            PenIcon_Click(null, null);
            SymbolIconDelete_MouseUp(null, null);

            // 根据设置决定是否清空图片
            if (Settings.Canvas.ClearCanvasAlsoClearImages)
            {
                // 如果设置为清空图片，则直接清空所有子元素
                Debug.WriteLine("BoardSymbolIconDelete: Clearing all children including images");
                inkCanvas.Children.Clear();
            }
            else
            {
                // 保存非笔画元素（如图片）
                Debug.WriteLine("BoardSymbolIconDelete: Preserving non-stroke elements (images)");
                var preservedElements = PreserveNonStrokeElements();
                Debug.WriteLine($"BoardSymbolIconDelete: Preserved elements count: {preservedElements.Count}");
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                Debug.WriteLine($"BoardSymbolIconDelete: inkCanvas.Children.Count after restore: {inkCanvas.Children.Count}");
            }
        }
        /// <summary>
        /// 处理删除墨迹和历史记录图标点击事件，清空画布内容和时间机器历史
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 调用钢笔图标点击事件
        /// - 调用符号删除鼠标抬起事件
        /// - 根据设置决定是否清空时间机器历史
        /// - 根据设置决定是否清空图片
        /// - 如果设置为清空图片，则清空所有子元素
        /// - 否则，保存非笔画元素并在清空后恢复
        /// </remarks>
        private void BoardSymbolIconDeleteInkAndHistories_MouseUp(object sender, RoutedEventArgs e)
        {
            PenIcon_Click(null, null);
            SymbolIconDelete_MouseUp(null, null);
            if (!Settings.Canvas.ClearCanvasAndClearTimeMachine) timeMachine.ClearStrokeHistory();

            // 根据设置决定是否清空图片
            if (Settings.Canvas.ClearCanvasAlsoClearImages)
            {
                // 如果设置为清空图片，则直接清空所有子元素
                Debug.WriteLine("BoardSymbolIconDeleteInkAndHistories: Clearing all children including images");
                inkCanvas.Children.Clear();
            }
            else
            {
                // 保存非笔画元素（如图片）
                Debug.WriteLine("BoardSymbolIconDeleteInkAndHistories: Preserving non-stroke elements (images)");
                var preservedElements = PreserveNonStrokeElements();
                Debug.WriteLine($"BoardSymbolIconDeleteInkAndHistories: Preserved elements count: {preservedElements.Count}");
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                Debug.WriteLine($"BoardSymbolIconDeleteInkAndHistories: inkCanvas.Children.Count after restore: {inkCanvas.Children.Count}");
            }
        }

        /// <summary>
        /// 处理启动希沃视频展台图标点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 调用图片黑板鼠标抬起事件
        /// - 启动希沃视频展台软件
        /// </remarks>
        private void BoardLaunchEasiCamera_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ImageBlackboard_MouseUp(null, null);
            SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
        }

        /// <summary>
        /// 处理启动Desmos计算器图标点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 立即隐藏所有子面板
        /// - 调用图片黑板鼠标抬起事件
        /// - 打开Desmos计算器网页
        /// </remarks>
        private void BoardLaunchDesmos_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HideSubPanelsImmediately();
            ImageBlackboard_MouseUp(null, null);
            Process.Start("https://www.desmos.com/calculator?lang=zh-CN");
        }

        /// <summary>
        /// 根据当前背景颜色更新RGB滑块的值
        /// </summary>
        private void UpdateRGBSliders(Color color)
        {
            if (BackgroundRSlider != null) BackgroundRSlider.Value = color.R;
            if (BackgroundGSlider != null) BackgroundGSlider.Value = color.G;
            if (BackgroundBSlider != null) BackgroundBSlider.Value = color.B;
        }
    }
}
