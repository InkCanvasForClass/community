using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void BoardChangeBackgroundColorBtn_MouseUp(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            // 创建背景选项面板（如果不存在）
            if (BackgroundPalette == null)
            {
                CreateBackgroundPalette();
            }

            // 显示或隐藏背景选项面板
            if (BackgroundPalette != null)
            {
                if (BackgroundPalette.Visibility == Visibility.Visible)
                {
                    // 如果面板已经显示，则隐藏它
                    AnimationsHelper.HideWithSlideAndFade(BackgroundPalette);
                }
                else
                {
                    // 隐藏其他可能显示的面板
                    AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                    AnimationsHelper.HideWithSlideAndFade(BorderTools);
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                    AnimationsHelper.HideWithSlideAndFade(PenPalette);
                    AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                    AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                    AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);

                    // 显示背景选项面板
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BackgroundPalette);
                }
                return;
            }

            // 原有的背景切换代码
            Settings.Canvas.UsingWhiteboard = !Settings.Canvas.UsingWhiteboard;
            SaveSettingsToFile();
            if (Settings.Canvas.UsingWhiteboard) {
                if (inkColor == 5) lastBoardInkColor = 0;
                ICCWaterMarkDark.Visibility = Visibility.Visible;
                ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
                
                // 设置为白板默认背景色
                Color defaultWhiteboardColor = Color.FromRgb(234, 235, 237);
                
                if (currentMode == 1) // 白板模式
                {
                    // 设置背景为默认白板背景色
                    GridBackgroundCover.Background = new SolidColorBrush(defaultWhiteboardColor);
                    
                    // 更新RGB滑块的值为默认白板背景色
                    if (BackgroundPalette != null && BackgroundPalette.Visibility == Visibility.Visible)
                    {
                        UpdateRGBSliders(defaultWhiteboardColor);
                    }
                    
                    // 更新自定义背景色为默认白板背景色
                    CustomBackgroundColor = defaultWhiteboardColor;
                    
                    // 保存到设置
                    string colorHex = $"#{defaultWhiteboardColor.R:X2}{defaultWhiteboardColor.G:X2}{defaultWhiteboardColor.B:X2}";
                    Settings.Canvas.CustomBackgroundColor = colorHex;
                    SaveSettingsToFile();
                }
                
                // 设置墨迹颜色为黑色
                CheckLastColor(0);
                forceEraser = false;
            }
            else {
                if (inkColor == 0) lastBoardInkColor = 5;
                ICCWaterMarkWhite.Visibility = Visibility.Visible;
                ICCWaterMarkDark.Visibility = Visibility.Collapsed;
                
                // 设置为黑板默认背景色
                Color defaultBlackboardColor = Color.FromRgb(22, 41, 36);
                
                if (currentMode == 1) // 黑板模式
                {
                    // 设置背景为默认黑板背景色
                    GridBackgroundCover.Background = new SolidColorBrush(defaultBlackboardColor);
                    
                    // 更新RGB滑块的值为默认黑板背景色
                    if (BackgroundPalette != null && BackgroundPalette.Visibility == Visibility.Visible)
                    {
                        UpdateRGBSliders(defaultBlackboardColor);
                    }
                    
                    // 更新自定义背景色为默认黑板背景色
                    CustomBackgroundColor = defaultBlackboardColor;
                    
                    // 保存到设置
                    string colorHex = $"#{defaultBlackboardColor.R:X2}{defaultBlackboardColor.G:X2}{defaultBlackboardColor.B:X2}";
                    Settings.Canvas.CustomBackgroundColor = colorHex;
                    SaveSettingsToFile();
                }
                
                // 设置墨迹颜色为白色
                CheckLastColor(5);
                forceEraser = false;
            }

            CheckColorTheme(true);
        }

        // 创建背景选项面板
        private void CreateBackgroundPalette()
        {
            // 确保加载自定义背景色
            LoadCustomBackgroundColor();
            
            // 创建一个类似于PenPalette的面板
            BackgroundPalette = new Border
            {
                Name = "BackgroundPalette",
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Colors.White),
                Opacity = 1,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = 300,
                MaxHeight = 400
            };
            
            // 确保面板显示在顶层
            System.Windows.Controls.Panel.SetZIndex(BackgroundPalette, 1000);

            // 创建面板内容
            var stackPanel = new StackPanel();

            // 创建标题栏
            var titleBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1e, 0x3a, 0x8a)),
                Height = 32,
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)),
                Margin = new Thickness(-1, -1, -1, 0),
                Padding = new Thickness(1, 1, 1, 0)
            };

            var titleCanvas = new System.Windows.Controls.Canvas { Height = 24, ClipToBounds = true };
            var titleText = new TextBlock
            {
                Text = "背景设置",
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Thickness(0, 5, 0, 0),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            };
            System.Windows.Controls.Canvas.SetLeft(titleText, 8);
            titleCanvas.Children.Add(titleText);

            // 关闭按钮
            var closeImage = new System.Windows.Controls.Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resources/new-icons/close-white.png", UriKind.Relative)),
                Height = 16,
                Width = 16
            };
            RenderOptions.SetBitmapScalingMode(closeImage, BitmapScalingMode.HighQuality);
            closeImage.MouseUp += CloseBordertools_MouseUp;
            System.Windows.Controls.Canvas.SetRight(closeImage, 8);
            System.Windows.Controls.Canvas.SetTop(closeImage, 4);
            titleCanvas.Children.Add(closeImage);

            titleBorder.Child = titleCanvas;
            stackPanel.Children.Add(titleBorder);

            // 创建背景选项内容区域
            var contentPanel = new StackPanel { Margin = new Thickness(8) };

            // 黑板/白板选择
            var modeTitle = new TextBlock
            {
                Text = "白板模式",
                Foreground = new SolidColorBrush(Color.FromRgb(0x17, 0x25, 0x54)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            contentPanel.Children.Add(modeTitle);

            var modePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            
            // 白板按钮
            var whiteboardButton = new Border
            {
                Width = 60,
                Height = 30,
                Background = Settings.Canvas.UsingWhiteboard ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) : new SolidColorBrush(Colors.LightGray),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var whiteboardText = new TextBlock
            {
                Text = "白板",
                Foreground = Settings.Canvas.UsingWhiteboard ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            whiteboardButton.Child = whiteboardText;
            whiteboardButton.MouseUp += (s, args) => {
                Settings.Canvas.UsingWhiteboard = true;
                SaveSettingsToFile();
                ICCWaterMarkDark.Visibility = Visibility.Visible;
                ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
                
                // 设置为白板默认背景色
                Color defaultWhiteboardColor = Color.FromRgb(234, 235, 237);
                
                if (currentMode == 1) // 白板模式
                {
                    // 设置背景为默认白板背景色
                    GridBackgroundCover.Background = new SolidColorBrush(defaultWhiteboardColor);
                    
                    // 更新RGB滑块的值为默认白板背景色
                    UpdateRGBSliders(defaultWhiteboardColor);
                    
                    // 更新自定义背景色为默认白板背景色
                    CustomBackgroundColor = defaultWhiteboardColor;
                    
                    // 保存到设置
                    string colorHex = $"#{defaultWhiteboardColor.R:X2}{defaultWhiteboardColor.G:X2}{defaultWhiteboardColor.B:X2}";
                    Settings.Canvas.CustomBackgroundColor = colorHex;
                    SaveSettingsToFile();
                }
                
                // 设置墨迹颜色为黑色
                CheckLastColor(0);
                forceEraser = false;
                
                CheckColorTheme(true);
                UpdateBackgroundButtonsState();
            };
            modePanel.Children.Add(whiteboardButton);
            
            // 黑板按钮
            var blackboardButton = new Border
            {
                Width = 60,
                Height = 30,
                Background = !Settings.Canvas.UsingWhiteboard ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) : new SolidColorBrush(Colors.LightGray),
                CornerRadius = new CornerRadius(4)
            };
            var blackboardText = new TextBlock
            {
                Text = "黑板",
                Foreground = !Settings.Canvas.UsingWhiteboard ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            blackboardButton.Child = blackboardText;
            blackboardButton.MouseUp += (s, args) => {
                Settings.Canvas.UsingWhiteboard = false;
                SaveSettingsToFile();
                ICCWaterMarkWhite.Visibility = Visibility.Visible;
                ICCWaterMarkDark.Visibility = Visibility.Collapsed;
                
                // 设置为黑板默认背景色
                Color defaultBlackboardColor = Color.FromRgb(22, 41, 36);
                
                if (currentMode == 1) // 黑板模式
                {
                    // 设置背景为默认黑板背景色
                    GridBackgroundCover.Background = new SolidColorBrush(defaultBlackboardColor);
                    
                    // 更新RGB滑块的值为默认黑板背景色
                    UpdateRGBSliders(defaultBlackboardColor);
                    
                    // 更新自定义背景色为默认黑板背景色
                    CustomBackgroundColor = defaultBlackboardColor;
                    
                    // 保存到设置
                    string colorHex = $"#{defaultBlackboardColor.R:X2}{defaultBlackboardColor.G:X2}{defaultBlackboardColor.B:X2}";
                    Settings.Canvas.CustomBackgroundColor = colorHex;
                    SaveSettingsToFile();
                }
                
                // 设置墨迹颜色为白色
                CheckLastColor(5);
                forceEraser = false;
                
                CheckColorTheme(true);
                UpdateBackgroundButtonsState();
            };
            modePanel.Children.Add(blackboardButton);

            contentPanel.Children.Add(modePanel);
            
             // 添加一条分隔线
             var separator = new Border
             {
                 Height = 1,
                 Background = new SolidColorBrush(Color.FromRgb(0xd4, 0xd4, 0xd8)),
                 Margin = new Thickness(0, 12, 0, 12)
             };
             contentPanel.Children.Add(separator);
             
             // 添加RGB颜色选择器部分
             var colorTitle = new TextBlock
             {
                 Text = "背景颜色",
                 Foreground = new SolidColorBrush(Color.FromRgb(0x17, 0x25, 0x54)),
                 FontSize = 10,
                 FontWeight = FontWeights.Bold,
                 HorizontalAlignment = HorizontalAlignment.Center,
                 Margin = new Thickness(0, 4, 0, 8)
             };
             contentPanel.Children.Add(colorTitle);
             
             // 创建颜色预览
             Border colorPreview = new Border
             {
                 Width = 100,
                 Height = 40,
                 BorderThickness = new Thickness(1),
                 BorderBrush = new SolidColorBrush(Color.FromRgb(0xd4, 0xd4, 0xd8)),
                 Background = new SolidColorBrush(Colors.White),
                 CornerRadius = new CornerRadius(4),
                 Margin = new Thickness(0, 0, 0, 10),
                 HorizontalAlignment = HorizontalAlignment.Center
             };
             contentPanel.Children.Add(colorPreview);
             
             // 获取当前背景颜色
             Color currentBackgroundColor;
             if (currentMode == 1) // 白板或黑板模式
             {
                 if (GridBackgroundCover.Background is SolidColorBrush brush)
                 {
                     currentBackgroundColor = brush.Color;
                 }
                 else
                 {
                     // 默认颜色
                     currentBackgroundColor = Settings.Canvas.UsingWhiteboard ? 
                         Color.FromRgb(234, 235, 237) : // 白板默认颜色
                         Color.FromRgb(22, 41, 36);    // 黑板默认颜色
                 }
             }
             else
             {
                 // 默认白色
                 currentBackgroundColor = Colors.White;
             }
             
             // 更新颜色预览
             colorPreview.Background = new SolidColorBrush(currentBackgroundColor);
             
             // 先创建所有滑块控件
             // R滑块和文本框
             var rPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 5) };
             var rLabel = new TextBlock { Text = "R:", Width = 20, VerticalAlignment = VerticalAlignment.Center };
             var rSlider = new System.Windows.Controls.Slider 
             { 
                 Minimum = 0, 
                 Maximum = 255, 
                 Value = currentBackgroundColor.R, 
                 Width = 150,
                 Margin = new Thickness(5, 0, 5, 0),
                 VerticalAlignment = VerticalAlignment.Center
             };
             var rValueText = new TextBlock 
             { 
                 Text = currentBackgroundColor.R.ToString(), 
                 Width = 30, 
                 VerticalAlignment = VerticalAlignment.Center,
                 TextAlignment = TextAlignment.Right
             };
             
             // G滑块和文本框
             var gPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 5) };
             var gLabel = new TextBlock { Text = "G:", Width = 20, VerticalAlignment = VerticalAlignment.Center };
             var gSlider = new System.Windows.Controls.Slider 
             { 
                 Minimum = 0, 
                 Maximum = 255, 
                 Value = currentBackgroundColor.G, 
                 Width = 150,
                 Margin = new Thickness(5, 0, 5, 0),
                 VerticalAlignment = VerticalAlignment.Center
             };
             var gValueText = new TextBlock 
             { 
                 Text = currentBackgroundColor.G.ToString(), 
                 Width = 30, 
                 VerticalAlignment = VerticalAlignment.Center,
                 TextAlignment = TextAlignment.Right
             };
             
             // B滑块和文本框
             var bPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 5) };
             var bLabel = new TextBlock { Text = "B:", Width = 20, VerticalAlignment = VerticalAlignment.Center };
             var bSlider = new System.Windows.Controls.Slider 
             { 
                 Minimum = 0, 
                 Maximum = 255, 
                 Value = currentBackgroundColor.B, 
                 Width = 150,
                 Margin = new Thickness(5, 0, 5, 0),
                 VerticalAlignment = VerticalAlignment.Center
             };
             var bValueText = new TextBlock 
             { 
                 Text = currentBackgroundColor.B.ToString(), 
                 Width = 30, 
                 VerticalAlignment = VerticalAlignment.Center,
                 TextAlignment = TextAlignment.Right
             };
             
             // 现在添加事件处理程序
             rSlider.ValueChanged += (s, e) => {
                 int value = (int)e.NewValue;
                 rValueText.Text = value.ToString();
                 UpdateColorPreview(colorPreview, rSlider, gSlider, bSlider);
             };
             
             gSlider.ValueChanged += (s, e) => {
                 int value = (int)e.NewValue;
                 gValueText.Text = value.ToString();
                 UpdateColorPreview(colorPreview, rSlider, gSlider, bSlider);
             };
             
             bSlider.ValueChanged += (s, e) => {
                 int value = (int)e.NewValue;
                 bValueText.Text = value.ToString();
                 UpdateColorPreview(colorPreview, rSlider, gSlider, bSlider);
             };
             
             // 添加控件到面板
             rPanel.Children.Add(rLabel);
             rPanel.Children.Add(rSlider);
             rPanel.Children.Add(rValueText);
             contentPanel.Children.Add(rPanel);
             
             gPanel.Children.Add(gLabel);
             gPanel.Children.Add(gSlider);
             gPanel.Children.Add(gValueText);
             contentPanel.Children.Add(gPanel);
             
             bPanel.Children.Add(bLabel);
             bPanel.Children.Add(bSlider);
             bPanel.Children.Add(bValueText);
             contentPanel.Children.Add(bPanel);
             
             // 应用按钮
             var applyButton = new Button
             {
                 Content = "应用颜色",
                 Margin = new Thickness(0, 10, 0, 0),
                 Padding = new Thickness(10, 5, 10, 5),
                 Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)),
                 Foreground = new SolidColorBrush(Colors.White),
                 BorderThickness = new Thickness(0),
                 HorizontalAlignment = HorizontalAlignment.Center
             };
             
             applyButton.Click += (s, e) => {
                 Color selectedColor = Color.FromRgb(
                     (byte)rSlider.Value,
                     (byte)gSlider.Value,
                     (byte)bSlider.Value
                 );
                 ApplyCustomBackgroundColor(selectedColor);
             };
             
             contentPanel.Children.Add(applyButton);

            stackPanel.Children.Add(contentPanel);

             // 将面板添加到父容器
             BackgroundPalette.Child = stackPanel;
             
             // 获取主窗口中的根网格，确保面板添加到顶层
             Grid mainGrid = FindName("Main_Grid") as Grid;
             if (mainGrid != null)
             {
                 // 删除可能已存在的BackgroundPalette
                 foreach (UIElement element in mainGrid.Children)
                 {
                     if (element is Border border && border.Name == "BackgroundPalette")
                     {
                         mainGrid.Children.Remove(border);
                         break;
                     }
                 }
                 
                 // 重新定位面板
                 BackgroundPalette.HorizontalAlignment = HorizontalAlignment.Center;
                 BackgroundPalette.VerticalAlignment = VerticalAlignment.Center;
                 BackgroundPalette.Margin = new Thickness(0, 0, 0, 0);
                 
                 // 添加到主网格
                 mainGrid.Children.Add(BackgroundPalette);
                 
                 // 设置面板位置
                 var clickElement = FindName("BoardChangeBackgroundColorBtn") as FrameworkElement;
                 if (clickElement != null)
                 {
                     Point position = clickElement.TranslatePoint(new Point(0, 0), mainGrid);
                     BackgroundPalette.Margin = new Thickness(
                         position.X - 150, 
                         position.Y + clickElement.ActualHeight + 5, 
                         0, 0);
                     BackgroundPalette.HorizontalAlignment = HorizontalAlignment.Left;
                     BackgroundPalette.VerticalAlignment = VerticalAlignment.Top;
                 }
             }
        }
        
        // 更新背景按钮状态
        private void UpdateBackgroundButtonsState()
        {
            if (BackgroundPalette != null && BackgroundPalette.Child is StackPanel stackPanel)
            {
                if (stackPanel.Children.Count > 1 && stackPanel.Children[1] is StackPanel contentPanel)
                {
                    if (contentPanel.Children.Count > 1 && contentPanel.Children[1] is StackPanel modePanel)
                    {
                        if (modePanel.Children.Count > 1)
                        {
                            var whiteboardButton = modePanel.Children[0] as Border;
                            var blackboardButton = modePanel.Children[1] as Border;
                            
                            if (whiteboardButton != null && whiteboardButton.Child is TextBlock whiteboardText)
                            {
                                whiteboardButton.Background = Settings.Canvas.UsingWhiteboard ? 
                                    new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) : 
                                    new SolidColorBrush(Colors.LightGray);
                                whiteboardText.Foreground = Settings.Canvas.UsingWhiteboard ?
                                    new SolidColorBrush(Colors.White) :
                                    new SolidColorBrush(Colors.Black);
                            }
                            
                            if (blackboardButton != null && blackboardButton.Child is TextBlock blackboardText)
                            {
                                blackboardButton.Background = !Settings.Canvas.UsingWhiteboard ?
                                    new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)) :
                                    new SolidColorBrush(Colors.LightGray);
                                blackboardText.Foreground = !Settings.Canvas.UsingWhiteboard ?
                                    new SolidColorBrush(Colors.White) :
                                    new SolidColorBrush(Colors.Black);
                            }
                        }
                    }
                }
            }
        }
        
                 // 添加成员变量保存背景面板引用
         private Border BackgroundPalette { get; set; }
         
         // 添加成员变量保存当前自定义背景色
         private Color? CustomBackgroundColor { get; set; }
         
         /// <summary>
         /// 更新颜色预览框的颜色
         /// </summary>
         private void UpdateColorPreview(Border colorPreview, System.Windows.Controls.Slider rSlider, System.Windows.Controls.Slider gSlider, System.Windows.Controls.Slider bSlider)
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

        private void BoardLassoIcon_Click(object sender, RoutedEventArgs e) {
            forceEraser = false;
            forcePointEraser = false;
            isLastTouchEraser = false;
            drawingShapeMode = 0;
            inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            SetCursorBasedOnEditingMode(inkCanvas);
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

        /// <summary>
        /// 根据当前背景颜色更新RGB滑块的值
        /// </summary>
        private void UpdateRGBSliders(Color color)
        {
            if (BackgroundPalette != null && BackgroundPalette.Child is StackPanel stackPanel)
            {
                if (stackPanel.Children.Count > 1 && stackPanel.Children[1] is StackPanel contentPanel)
                {
                    // 查找RGB滑块
                    System.Windows.Controls.Slider rSlider = null;
                    System.Windows.Controls.Slider gSlider = null;
                    System.Windows.Controls.Slider bSlider = null;
                    
                    // 遍历面板查找RGB滑块
                    foreach (var child in contentPanel.Children)
                    {
                        if (child is StackPanel panel && panel.Orientation == Orientation.Horizontal)
                        {
                            foreach (var panelChild in panel.Children)
                            {
                                if (panelChild is System.Windows.Controls.Slider slider)
                                {
                                    if (panel.Children.Count > 0 && panel.Children[0] is TextBlock label)
                                    {
                                        if (label.Text == "R:")
                                        {
                                            rSlider = slider;
                                        }
                                        else if (label.Text == "G:")
                                        {
                                            gSlider = slider;
                                        }
                                        else if (label.Text == "B:")
                                        {
                                            bSlider = slider;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // 更新滑块值
                    if (rSlider != null && gSlider != null && bSlider != null)
                    {
                        rSlider.Value = color.R;
                        gSlider.Value = color.G;
                        bSlider.Value = color.B;
                    }
                }
            }
        }
    }
}