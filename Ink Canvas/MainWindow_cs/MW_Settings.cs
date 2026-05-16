using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        #region Behavior


        /// <summary>
        /// 处理PowerPoint支持开关状态更改事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当PowerPoint支持开关状态更改时：
        /// 1. 保存PowerPoint支持设置
        /// 2. 如果关闭PowerPoint支持，同时也关闭WPS支持
        /// 3. 如果开启PowerPoint支持，初始化PPT管理器并开始监控
        /// 4. 如果关闭PowerPoint支持，停止监控
        /// </remarks>

        /// <summary>
        /// 处理使用ROT PPT链接开关状态更改事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当使用ROT PPT链接开关状态更改时：
        /// 1. 保存ROT PPT链接设置
        /// 2. 停止PPT监控
        /// 3. 如果开启ROT PPT链接且启用了PowerPoint增强，关闭PowerPoint增强
        /// 4. 初始化PPT管理器
        /// 5. 如果启用了PowerPoint支持，开始PPT监控
        /// 6. 记录切换PPT联动架构的日志
        /// </remarks>

        /// <summary>
        /// 处理新幻灯片放映时显示画布开关状态更改事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当新幻灯片放映时显示画布开关状态更改时，保存设置到文件
        /// </remarks>

        #endregion

        #region Startup

        private void ToggleSwitchEnableNibMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            if (sender == ToggleSwitchEnableNibMode)
                BoardToggleSwitchEnableNibMode.IsOn = ToggleSwitchEnableNibMode.IsOn;
            else
                ToggleSwitchEnableNibMode.IsOn = BoardToggleSwitchEnableNibMode.IsOn;
            Settings.Startup.IsEnableNibMode = ToggleSwitchEnableNibMode.IsOn;

            if (Settings.Startup.IsEnableNibMode)
                BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
            else
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;
            SaveSettingsToFile();
        }

        #endregion

        #region Appearance





        private static readonly Lazy<object> HitokotoHttpClient = new Lazy<object>(CreateHitokotoClient, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 创建用于获取一言（Hitokoto）数据的HttpClient
        /// </summary>
        /// <returns>创建的HttpClient实例，如果创建失败则返回null</returns>
        /// <remarks>
        /// 创建HttpClient时：
        /// 1. 设置超时时间为5秒
        /// 2. 尝试设置User-Agent头
        /// 3. 捕获并记录创建过程中的异常
        /// </remarks>
        private static object CreateHitokotoClient()
        {
            try
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("InkCanvas-Hitokoto/1.0");
                }
                catch
                {
                }
                return client;
            }
            catch (Exception ex)
            {
                try
                {
                    LogHelper.WriteLogToFile($"无法创建 HttpClient (System.Net.Http 可能缺失): {ex.Message}", LogHelper.LogType.Warning);
                }
                catch
                {
                }
                return null;
            }
        }

        /// <summary>
        /// 根据当前外观设置更新白板水印的名言文本。
        /// </summary>
        /// <remarks>
        /// 当配置为内置来源时（0：OSUPlayer、1：名言警句、2：高考俗语）从对应数组中随机选择一条并设置为水印文本；
        /// 当配置为一言（3）时会异步请求 Hitokoto API 并在请求中显示占位提示，成功时将返回文本设为水印，失败时记录警告日志并设置可读的失败提示文本。此方法会修改 BlackBoardWaterMark.Text，并在发生异常时记录日志且设置合适的回退文本。
        /// </remarks>
        internal async Task UpdateChickenSoupTextAsync()
        {
            try
            {
                if (!Settings.Appearance.EnableChickenSoupInWhiteboardMode)
                {
                    return;
                }

                if (Settings.Appearance.ChickenSoupSource == 0)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.OSUPlayerYuLu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.OSUPlayerYuLu[randChickenSoupIndex];
                }
                else if (Settings.Appearance.ChickenSoupSource == 1)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.MingYanJingJu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.MingYanJingJu[randChickenSoupIndex];
                }
                else if (Settings.Appearance.ChickenSoupSource == 2)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.GaoKaoPhrases.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.GaoKaoPhrases[randChickenSoupIndex];
                }
                else if (Settings.Appearance.ChickenSoupSource == 3)
                {
                    BlackBoardWaterMark.Text = "正在获取一言...";

                    try
                    {
                        object clientObj = null;
                        try
                        {
                            clientObj = HitokotoHttpClient.Value;
                        }
                        catch (Exception initEx)
                        {
                            LogHelper.WriteLogToFile($"一言 HTTP 客户端初始化失败: {initEx.Message}", LogHelper.LogType.Warning);
                            BlackBoardWaterMark.Text = "一言功能不可用（HTTP 库不可用）";
                            return;
                        }

                        if (clientObj == null || !(clientObj is HttpClient client))
                        {
                            BlackBoardWaterMark.Text = "一言功能不可用（HTTP 库不可用）";
                            return;
                        }

                        var cats = Settings.Appearance.HitokotoCategories;
                        if (cats == null || cats.Count == 0)
                            cats = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l" };

                        var urlBuilder = new StringBuilder("https://v1.hitokoto.cn/?encode=text");
                        foreach (var category in cats)
                        {
                            urlBuilder.Append($"&c={category}");
                        }

                        var response = await client.GetAsync(urlBuilder.ToString()).ConfigureAwait(true);
                        response.EnsureSuccessStatusCode();

                        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            BlackBoardWaterMark.Text = text.Trim();
                        }
                        else
                        {
                            BlackBoardWaterMark.Text = "一言暂时没有返回内容";
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"一言 API 请求失败: {ex.Message}", LogHelper.LogType.Warning);
                        BlackBoardWaterMark.Text = "一言功能不可用";
                    }
                }
                else if (Settings.Appearance.ChickenSoupSource == 4)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.PhigrosTips.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.PhigrosTips[randChickenSoupIndex];
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新白板名言时出错: {ex.Message}", LogHelper.LogType.Warning);
                if (Settings.Appearance.ChickenSoupSource == 3 && BlackBoardWaterMark != null)
                {
                    try { BlackBoardWaterMark.Text = "一言功能不可用"; } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                }
            }
        }




        /// <summary>
        /// 根据设置更新浮动栏图标
        /// </summary>
        /// <remarks>
        /// 根据设置的浮动栏图标索引更新图标：
        /// 1. 为不同的图标索引设置不同的图标源
        /// 2. 为不同的图标设置不同的边距
        /// 3. 支持自定义图标
        /// 4. 自定义图标加载失败时使用默认图标
        /// </remarks>
        public void UpdateFloatingBarIcon()
        {
            if (FloatingbarHeadIconImg == null) return;
            int index = Settings.Appearance.FloatingBarImg;

            if (index == 0)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            }
            else if (index == 1)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc-noshadow.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            }
            else if (index == 2)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc-dark.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            }
            else if (index == 3)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc-sharpdark.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            }
            else if (index == 4)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc-transparent-light-small.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            }
            else if (index == 5)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc-transparent-dark-small.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(1.2);
            }
            else if (index == 6)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandoujiyanhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            }
            else if (index == 7)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanshounvhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            }
            else if (index == 8)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanciya.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            }
            else if (index == 9)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanneikuhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            }
            else if (index == 10)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandogeyuanliangwo.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            }
            else if (index == 11)
            {
                FloatingbarHeadIconImg.Source =
                    CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/tiebahuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1);
            }
            else if (index >= 12 && index - 12 < Settings.Appearance.CustomFloatingBarImgs.Count)
            {
                // 使用自定义图标
                var customIcon = Settings.Appearance.CustomFloatingBarImgs[index - 12];
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    var targetPixels = (int)Math.Round(58 * dpi.DpiScaleX);
                    var decodePixels = targetPixels * 2;
                    if (decodePixels < 64) decodePixels = 64;
                    if (decodePixels > 512) decodePixels = 512;

                    FloatingbarHeadIconImg.Source = CreateBitmapImage(new Uri(customIcon.FilePath), decodePixels);
                    FloatingbarHeadIconImg.Margin = new Thickness(2);
                }
                catch
                {
                    // 如果加载失败，使用默认图标
                    FloatingbarHeadIconImg.Source = CreateBitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc.png"));
                    FloatingbarHeadIconImg.Margin = new Thickness(0.5);
                }
            }
        }

        private static BitmapImage CreateBitmapImage(Uri uri, int decodePixelWidth = 0)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0)
            {
                image.DecodePixelWidth = decodePixelWidth;
            }
            image.EndInit();
            image.Freeze();
            return image;
        }

        /// <summary>
        /// 更新组合框中的自定义图标选项
        /// </summary>
        /// <remarks>
        /// 更新自定义图标选项时：
        /// 1. 保留前12个内置图标选项
        /// 2. 移除所有现有的自定义图标选项
        /// 3. 添加新的自定义图标选项
        /// 4. 为自定义图标选项设置字体
        /// </remarks>
        public void UpdateCustomIconsInComboBox()
        {
        }



        //[Obsolete]
        //private void ToggleSwitchShowButtonPPTNavigation_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowPPTNavigation = ToggleSwitchShowButtonPPTNavigation.IsOn;
        //    var vis = Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
        //    PPTLBPageButton.Visibility = vis;
        //    PPTRBPageButton.Visibility = vis;
        //    PPTLSPageButton.Visibility = vis;
        //    PPTRSPageButton.Visibility = vis;
        //    SaveSettingsToFile();
        //}

        //[Obsolete]
        //private void ToggleSwitchShowBottomPPTNavigationPanel_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = ToggleSwitchShowBottomPPTNavigationPanel.IsOn;
        //    if (IsInPptPresentationMode)
        //        //BottomViewboxPPTSidesControl.Visibility = Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel
        //        //    ? Visibility.Visible
        //        //    : Visibility.Collapsed;
        //    SaveSettingsToFile();
        //}

        //[Obsolete]
        //private void ToggleSwitchShowSidePPTNavigationPanel_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = ToggleSwitchShowSidePPTNavigationPanel.IsOn;
        //    if (IsInPptPresentationMode) {
        //        LeftSidePanelForPPTNavigation.Visibility = Settings.PowerPointSettings.IsShowSidePPTNavigationPanel
        //            ? Visibility.Visible
        //            : Visibility.Collapsed;
        //        RightSidePanelForPPTNavigation.Visibility = Settings.PowerPointSettings.IsShowSidePPTNavigationPanel
        //            ? Visibility.Visible
        //            : Visibility.Collapsed;
        //    }

        //    SaveSettingsToFile();
        //}



        public void UpdatePPTBtnSlidersStatus()
        {
        }


        /// <summary>
        /// 更新PPT UI管理器设置的通用方法
        /// </summary>
        public void UpdatePPTUIManagerSettings()
        {
            if (_pptUIManager != null && IsInPptPresentationMode)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.PPTBButtonsOption = Settings.PowerPointSettings.PPTBButtonsOption;
                _pptUIManager.PPTLSButtonPosition = Settings.PowerPointSettings.PPTLSButtonPosition;
                _pptUIManager.PPTRSButtonPosition = Settings.PowerPointSettings.PPTRSButtonPosition;
                _pptUIManager.PPTLBButtonPosition = Settings.PowerPointSettings.PPTLBButtonPosition;
                _pptUIManager.PPTRBButtonPosition = Settings.PowerPointSettings.PPTRBButtonPosition;
                _pptUIManager.EnablePPTButtonPageClickable = Settings.PowerPointSettings.EnablePPTButtonPageClickable;
                _pptUIManager.EnablePPTButtonLongPressPageTurn = Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn;
                _pptUIManager.PPTLSButtonOpacity = Settings.PowerPointSettings.PPTLSButtonOpacity;
                _pptUIManager.PPTRSButtonOpacity = Settings.PowerPointSettings.PPTRSButtonOpacity;
                _pptUIManager.PPTLBButtonOpacity = Settings.PowerPointSettings.PPTLBButtonOpacity;
                _pptUIManager.PPTRBButtonOpacity = Settings.PowerPointSettings.PPTRBButtonOpacity;
                _pptUIManager.UpdateNavigationPanelsVisibility();
                _pptUIManager.UpdateNavigationButtonStyles();
            }
        }

        public void UpdatePPTBtnPreview()
        {
        }











        #endregion

        #region Canvas

        /// <summary>笔锋下拉 UI 顺序：0 实时笔锋，1 基于点集，2 基于速率，3 关闭。与存储值 InkStyle：3,0,1,2 对应。</summary>
        private static int PenStyleUiIndexFromInkStyle(int inkStyle)
        {
            switch (inkStyle)
            {
                case 3: return 0;
                case 0: return 1;
                case 1: return 2;
                case 2: return 3;
                default: return 1;
            }
        }

        private static int InkStyleFromPenStyleUiIndex(int uiIndex)
        {
            switch (uiIndex)
            {
                case 0: return 3;
                case 1: return 0;
                case 2: return 1;
                case 3: return 2;
                default: return 0;
            }
        }

        private void ComboBoxPenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            int uiIndex = sender == ComboBoxPenStyle
                ? ComboBoxPenStyle.SelectedIndex
                : BoardComboBoxPenStyle.SelectedIndex;
            if (uiIndex < 0) return;

            Settings.Canvas.InkStyle = InkStyleFromPenStyleUiIndex(uiIndex);
            if (sender == ComboBoxPenStyle)
                BoardComboBoxPenStyle.SelectedIndex = uiIndex;
            else
                ComboBoxPenStyle.SelectedIndex = uiIndex;

            EnsureRealtimeStylusPipelineBinding();
            SaveSettingsToFile();
        }



        private void SwitchToCircleEraser(object sender, MouseButtonEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 0;
            SaveSettingsToFile();
            CheckEraserTypeTab();

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            // 确保当前处于橡皮擦模式时能立即看到效果
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }

        private void SwitchToRectangleEraser(object sender, MouseButtonEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 1;
            SaveSettingsToFile();
            CheckEraserTypeTab();

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            // 确保当前处于橡皮擦模式时能立即看到效果
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }


        private void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            if (sender == BoardInkWidthSlider) InkWidthSlider.Value = ((Slider)sender).Value;
            if (sender == InkWidthSlider) BoardInkWidthSlider.Value = ((Slider)sender).Value;
            drawingAttributes.Height = ((Slider)sender).Value / 2;
            drawingAttributes.Width = ((Slider)sender).Value / 2;
            Settings.Canvas.InkWidth = ((Slider)sender).Value / 2;
            SaveSettingsToFile();
        }

        private void HighlighterWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            // if (sender == BoardInkWidthSlider) InkWidthSlider.Value = ((Slider)sender).Value;
            // if (sender == InkWidthSlider) BoardInkWidthSlider.Value = ((Slider)sender).Value;
            drawingAttributes.Height = ((Slider)sender).Value;
            drawingAttributes.Width = ((Slider)sender).Value / 2;
            Settings.Canvas.HighlighterWidth = ((Slider)sender).Value;
            SaveSettingsToFile();
        }

        /// <summary>
        /// 将画笔不透明度更新为滑块的当前值，并保存到设置中。
        /// </summary>
        /// <remarks>
        /// 使用滑块的当前值作为 alpha 通道更新 drawingAttributes.Color，同时将该值写入 Settings.Canvas.InkAlpha 并持久化配置文件。
        /// </remarks>
        private void InkAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            var NowR = drawingAttributes.Color.R;
            var NowG = drawingAttributes.Color.G;
            var NowB = drawingAttributes.Color.B;
            drawingAttributes.Color = Color.FromArgb((byte)((Slider)sender).Value, NowR, NowG, NowB);
            Settings.Canvas.InkAlpha = ((Slider)sender).Value;
            SaveSettingsToFile();
        }

        private void LaserPenWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            if (penType == 2)
            {
                drawingAttributes.Width = ((Slider)sender).Value;
                drawingAttributes.Height = ((Slider)sender).Value;
            }
            Settings.Canvas.LaserPenWidth = ((Slider)sender).Value;
            SaveSettingsToFile();
        }

        private void LaserPenAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            if (penType == 2)
            {
                var NowR = drawingAttributes.Color.R;
                var NowG = drawingAttributes.Color.G;
                var NowB = drawingAttributes.Color.B;
                drawingAttributes.Color = Color.FromArgb((byte)((Slider)sender).Value, NowR, NowG, NowB);
            }
            Settings.Canvas.LaserPenAlpha = (int)((Slider)sender).Value;
            SaveSettingsToFile();
        }

        private void LaserPenFadeTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Canvas.InkFadeTime = (int)((Slider)sender).Value * 1000;
            if (_inkFadeManager != null)
            {
                _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
            }
            SaveSettingsToFile();
        }

        /// <summary>
        /// 根据组合框的当前选择更新双曲线渐近线选项（Settings.Canvas.HyperbolaAsymptoteOption），并将更改保存到设置文件。
        /// </summary>

        #endregion

        #region Automation

        public void StartOrStoptimerCheckAutoFold()
        {
            if (Settings.Automation.IsEnableAutoFold)
                _unifiedMainWindowTimer?.Start();
            else
                _unifiedMainWindowTimer?.Stop();
        }


        #endregion

        #region Gesture


        private void ToggleSwitchEnableTwoFingerZoom_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            var toggle = (iNKORE.UI.WPF.Modern.Controls.ToggleSwitch)sender;
            bool isOn = toggle.IsOn;

            if (sender == BoardToggleSwitchEnableTwoFingerZoom)
                Settings.Gesture.IsEnableTwoFingerZoomBoard = isOn;
            else
                Settings.Gesture.IsEnableTwoFingerZoom = isOn;

            if (isOn)
            {
                if (sender == BoardToggleSwitchEnableTwoFingerZoom)
                    BoardToggleSwitchEnableMultiTouchMode.IsOn = false;
                else
                    ToggleSwitchEnableMultiTouchMode.IsOn = false;
            }

            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMultiTouchMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = (iNKORE.UI.WPF.Modern.Controls.ToggleSwitch)sender;
            bool isOn = toggle.IsOn;
            bool isBoardSender = sender == BoardToggleSwitchEnableMultiTouchMode;

            if (isBoardSender)
                Settings.Gesture.IsEnableMultiTouchModeBoard = isOn;
            else
                Settings.Gesture.IsEnableMultiTouchMode = isOn;

            if (isOn)
            {
                if (!isInMultiTouchMode)
                {
                    // 保存当前编辑模式和绘图工具状态
                    InkCanvasEditingMode currentEditingMode = inkCanvas.EditingMode;
                    int currentDrawingShapeMode = drawingShapeMode;
                    bool currentForceEraser = forceEraser;

                    inkCanvas.StylusDown += MainWindow_StylusDown;
                    inkCanvas.StylusMove += MainWindow_StylusMove;
                    inkCanvas.StylusUp += MainWindow_StylusUp;
                    inkCanvas.TouchDown += MainWindow_TouchDown;
                    inkCanvas.TouchDown -= Main_Grid_TouchDown;

                    // 先设为None再设回原来的模式，避免可能的事件冲突
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    // 保存非笔画元素（如图片）
                    var preservedElements = PreserveNonStrokeElements();
                    inkCanvas.Children.Clear();
                    // 恢复非笔画元素
                    RestoreNonStrokeElements(preservedElements);
                    isInMultiTouchMode = true;

                    palmEraserWasEnabledBeforeMultiTouch = Settings.Canvas.EnablePalmEraser;
                    Settings.Canvas.EnablePalmEraser = false;
                    SaveSettingsToFile();

                    // 恢复到之前的编辑状态
                    inkCanvas.EditingMode = currentEditingMode;
                    drawingShapeMode = currentDrawingShapeMode;
                    forceEraser = currentForceEraser;
                }
            }
            else
            {
                if (isInMultiTouchMode)
                {
                    // 保存当前编辑模式和绘图工具状态
                    InkCanvasEditingMode currentEditingMode = inkCanvas.EditingMode;
                    int currentDrawingShapeMode = drawingShapeMode;
                    bool currentForceEraser = forceEraser;

                    inkCanvas.StylusDown -= MainWindow_StylusDown;
                    inkCanvas.StylusMove -= MainWindow_StylusMove;
                    inkCanvas.StylusUp -= MainWindow_StylusUp;
                    inkCanvas.TouchDown -= MainWindow_TouchDown;
                    inkCanvas.TouchDown += Main_Grid_TouchDown;

                    // 先设为None再设回原来的模式，避免可能的事件冲突
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    // 保存非笔画元素（如图片）
                    var preservedElements = PreserveNonStrokeElements();
                    inkCanvas.Children.Clear();
                    // 恢复非笔画元素
                    RestoreNonStrokeElements(preservedElements);
                    isInMultiTouchMode = false;

                    if (palmEraserWasEnabledBeforeMultiTouch)
                    {
                        Settings.Canvas.EnablePalmEraser = true;
                        SaveSettingsToFile();
                    }

                    // 恢复到之前的编辑状态
                    inkCanvas.EditingMode = currentEditingMode;
                    drawingShapeMode = currentDrawingShapeMode;
                    forceEraser = currentForceEraser;
                }
            }

            EnsureRealtimeStylusPipelineBinding();

            // 如果启用多指书写模式，强制禁用同模式下的所有双指手势
            if (isOn)
            {
                if (isBoardSender)
                {
                    Settings.Gesture.IsEnableTwoFingerTranslateBoard = false;
                    Settings.Gesture.IsEnableTwoFingerZoomBoard = false;
                    Settings.Gesture.IsEnableTwoFingerRotationBoard = false;
                    if (BoardToggleSwitchEnableTwoFingerTranslate != null)
                        BoardToggleSwitchEnableTwoFingerTranslate.IsOn = false;
                    if (BoardToggleSwitchEnableTwoFingerZoom != null)
                        BoardToggleSwitchEnableTwoFingerZoom.IsOn = false;
                    if (BoardToggleSwitchEnableTwoFingerRotation != null)
                        BoardToggleSwitchEnableTwoFingerRotation.IsOn = false;
                }
                else
                {
                    Settings.Gesture.IsEnableTwoFingerTranslate = false;
                    Settings.Gesture.IsEnableTwoFingerZoom = false;
                    Settings.Gesture.IsEnableTwoFingerRotation = false;
                    if (ToggleSwitchEnableTwoFingerTranslate != null)
                        ToggleSwitchEnableTwoFingerTranslate.IsOn = false;
                    if (ToggleSwitchEnableTwoFingerZoom != null)
                        ToggleSwitchEnableTwoFingerZoom.IsOn = false;
                    if (ToggleSwitchEnableTwoFingerRotation != null)
                        ToggleSwitchEnableTwoFingerRotation.IsOn = false;
                }
            }

            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerTranslate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            var toggle = (iNKORE.UI.WPF.Modern.Controls.ToggleSwitch)sender;
            bool isOn = toggle.IsOn;

            if (sender == BoardToggleSwitchEnableTwoFingerTranslate)
                Settings.Gesture.IsEnableTwoFingerTranslateBoard = isOn;
            else
                Settings.Gesture.IsEnableTwoFingerTranslate = isOn;

            if (isOn)
            {
                if (sender == BoardToggleSwitchEnableTwoFingerTranslate)
                    BoardToggleSwitchEnableMultiTouchMode.IsOn = false;
                else
                    ToggleSwitchEnableMultiTouchMode.IsOn = false;
            }

            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerRotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            var toggle = (iNKORE.UI.WPF.Modern.Controls.ToggleSwitch)sender;
            bool isOn = toggle.IsOn;

            if (sender == BoardToggleSwitchEnableTwoFingerRotation)
                Settings.Gesture.IsEnableTwoFingerRotationBoard = isOn;
            else
                Settings.Gesture.IsEnableTwoFingerRotation = isOn;

            if (isOn)
            {
                if (sender == BoardToggleSwitchEnableTwoFingerRotation)
                    BoardToggleSwitchEnableMultiTouchMode.IsOn = false;
                else
                    ToggleSwitchEnableMultiTouchMode.IsOn = false;
            }

            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }



        #endregion

        #region Reset

        /// <summary>
        /// 将应用设置重置为推荐的默认配置。
        /// </summary>
        /// <remarks>
        /// 该方法会重新创建全局 Settings 实例并应用推荐值，覆盖大部分子模块配置（如外观、画布、自动化、PPT、手势、高级选项等）。
        /// 在重置过程中会保留并恢复当前 Settings.Automation 中的 AutoDelSavedFiles 与 AutoDelSavedFilesDaysThreshold 两项值以避免意外删除策略变化。
        /// </remarks>
        public static void SetSettingsToRecommendation()
        {
            var AutoDelSavedFilesDays = Settings.Automation.AutoDelSavedFiles;
            var AutoDelSavedFilesDaysThreshold = Settings.Automation.AutoDelSavedFilesDaysThreshold;
            Settings = new Settings();
            Settings.Advanced.IsSpecialScreen = true;
            Settings.Advanced.IsQuadIR = false;
            Settings.Advanced.TouchMultiplier = 0.3;
            Settings.Advanced.NibModeBoundsWidth = 5;
            Settings.Advanced.FingerModeBoundsWidth = 20;
            Settings.Advanced.NibModeBoundsWidthThresholdValue = 2.5;
            Settings.Advanced.FingerModeBoundsWidthThresholdValue = 2.5;
            Settings.Advanced.NibModeBoundsWidthEraserSize = 0.8;
            Settings.Advanced.FingerModeBoundsWidthEraserSize = 0.8;
            Settings.Advanced.EraserBindTouchMultiplier = true;
            Settings.Advanced.IsLogEnabled = true;
            Settings.Advanced.IsSecondConfirmWhenShutdownApp = false;
            Settings.Advanced.IsEnableEdgeGestureUtil = false;
            Settings.Advanced.EdgeGestureUtilOnlyAffectBlackboardMode = false;
            Settings.Advanced.IsEnableFullScreenHelper = false;
            Settings.Advanced.IsEnableAvoidFullScreenHelper = true;
            Settings.Advanced.IsEnableForceFullScreen = false;
            Settings.Advanced.IsEnableDPIChangeDetection = false;
            Settings.Advanced.IsEnableResolutionChangeDetection = false;
            Settings.Advanced.EnableMultiScreenSupport = true;
            Settings.Advanced.FollowMouseForScreenSelection = true;

            Settings.Appearance.IsEnableDisPlayNibModeToggler = false;
            Settings.Appearance.IsColorfulViewboxFloatingBar = false;
            Settings.Appearance.ViewboxFloatingBarScaleTransformValue = 1;
            Settings.Appearance.ViewboxBlackBoardScaleTransformValue = 0.8;
            Settings.Appearance.IsTransparentButtonBackground = true;
            Settings.Appearance.IsShowExitButton = true;
            Settings.Appearance.IsShowEraserButton = true;
            Settings.Appearance.IsShowHideControlButton = false;
            Settings.Appearance.IsShowLRSwitchButton = false;
            Settings.Appearance.IsShowModeFingerToggleSwitch = true;
            Settings.Appearance.IsShowQuickPanel = true;
            Settings.Appearance.Theme = 0;
            Settings.Appearance.EnableChickenSoupInWhiteboardMode = true;
            Settings.Appearance.EnableTimeDisplayInWhiteboardMode = true;
            Settings.Appearance.ChickenSoupSource = 1;
            Settings.Appearance.ViewboxFloatingBarOpacityValue = 1.0;
            Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = 1.0;
            Settings.Appearance.EnableTrayIcon = true;

            // 浮动栏按钮显示控制默认值
            Settings.Appearance.IsShowShapeButton = true;
            Settings.Appearance.IsShowUndoButton = true;
            Settings.Appearance.IsShowRedoButton = true;
            Settings.Appearance.IsShowClearButton = true;
            Settings.Appearance.IsShowWhiteboardButton = true;
            Settings.Appearance.IsShowHideButton = true;
            Settings.Appearance.IsShowLassoSelectButton = true;
            Settings.Appearance.IsShowClearAndMouseButton = true;
            Settings.Appearance.IsShowQuickColorPalette = false;
            Settings.Appearance.QuickColorPaletteDisplayMode = 1;
            Settings.Appearance.EraserDisplayOption = 0;

            Settings.Automation.IsAutoFoldInEasiNote = true;
            Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno = true;
            Settings.Automation.IsAutoFoldInEasiCamera = true;
            Settings.Automation.IsAutoFoldInEasiNote3C = false;
            Settings.Automation.IsAutoFoldInEasiNote3 = false;
            Settings.Automation.IsAutoFoldInEasiNote5C = true;
            Settings.Automation.IsAutoFoldInSeewoPincoTeacher = false;
            Settings.Automation.IsAutoFoldInHiteTouchPro = false;
            Settings.Automation.IsAutoFoldInHiteCamera = false;
            Settings.Automation.IsAutoFoldInWxBoardMain = false;
            Settings.Automation.IsAutoFoldInOldZyBoard = false;
            Settings.Automation.IsAutoFoldInMSWhiteboard = false;
            Settings.Automation.IsAutoFoldInAdmoxWhiteboard = false;
            Settings.Automation.IsAutoFoldInAdmoxBooth = false;
            Settings.Automation.IsAutoFoldInQPoint = false;
            Settings.Automation.IsAutoFoldInYiYunVisualPresenter = false;
            Settings.Automation.IsAutoFoldInMaxHubWhiteboard = false;
            Settings.Automation.IsAutoFoldInPPTSlideShow = false;
            Settings.Automation.IsAutoKillPptService = false;
            Settings.Automation.IsAutoKillEasiNote = false;
            Settings.Automation.IsAutoKillVComYouJiao = false;
            Settings.Automation.IsAutoKillInkCanvas = false;
            Settings.Automation.IsAutoKillICA = false;
            Settings.Automation.IsAutoKillIDT = false;
            Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = false;
            Settings.Automation.IsSaveScreenshotsInDateFolders = false;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = true;
            Settings.Automation.IsAutoSaveStrokesAtClear = true;
            Settings.Automation.IsAutoClearWhenExitingWritingMode = false;
            Settings.Automation.MinimumAutomationStrokeNumber = 0;
            Settings.Automation.AutoDelSavedFiles = AutoDelSavedFilesDays;
            Settings.Automation.AutoDelSavedFilesDaysThreshold = AutoDelSavedFilesDaysThreshold;

            //Settings.PowerPointSettings.IsShowPPTNavigation = true;
            //Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = false;
            //Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = true;
            Settings.PowerPointSettings.PowerPointSupport = true;
            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = false;
            Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = true;
            Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = false;
            Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            Settings.PowerPointSettings.IsNotifyPreviousPage = false;
            Settings.PowerPointSettings.IsNotifyHiddenPage = false;
            Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = false;
            Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = false;
            Settings.PowerPointSettings.IsSupportWPS = false;
            Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview = false;

            Settings.Canvas.InkWidth = 2.5;
            Settings.Canvas.IsShowCursor = false;
            Settings.Canvas.InkStyle = 0;
            Settings.Canvas.HighlighterWidth = 20;
            Settings.Canvas.EraserSize = 1;
            Settings.Canvas.EraserType = 0;
            Settings.Canvas.EraserShapeType = 1;
            Settings.Canvas.HideStrokeWhenSelecting = false;
            Settings.Canvas.ClearCanvasAndClearTimeMachine = false;
            Settings.Canvas.FitToCurve = false;
            Settings.Canvas.UseAdvancedBezierSmoothing = true;
            Settings.Canvas.EnablePressureTouchMode = false;
            Settings.Canvas.DisablePressure = false;
            Settings.Canvas.AutoStraightenLine = true;
            Settings.Canvas.AutoStraightenLineThreshold = 80;
            Settings.Canvas.PauseStraightenLine = false;
            Settings.Canvas.PauseStraightenDelay = 300;
            Settings.Canvas.LineEndpointSnapping = true;
            Settings.Canvas.LineEndpointSnappingThreshold = 15;
            Settings.Canvas.UsingWhiteboard = false;
            Settings.Canvas.HyperbolaAsymptoteOption = 0;

            Settings.Gesture.AutoSwitchTwoFingerGesture = true;
            Settings.Gesture.IsEnableTwoFingerTranslate = true;
            Settings.Gesture.IsEnableTwoFingerZoom = false;
            Settings.Gesture.IsEnableTwoFingerRotation = false;
            Settings.Gesture.IsEnableTwoFingerRotationOnSelection = false;

            Settings.InkToShape.IsInkToShapeEnabled = true;
            Settings.InkToShape.IsInkToShapeNoFakePressureRectangle = false;
            Settings.InkToShape.IsInkToShapeNoFakePressureTriangle = false;
            Settings.InkToShape.IsInkToShapeTriangle = true;
            Settings.InkToShape.IsInkToShapeRectangle = true;
            Settings.InkToShape.IsInkToShapeRounded = true;
            Settings.InkToShape.EnableWinRtHandwritingStrokeBeautify = false;
            Settings.InkToShape.HandwritingCorrectionFontFamily = "Ink Free,KaiTi,Segoe Script";

            Settings.Startup.IsEnableNibMode = false;
            Settings.Startup.IsAutoUpdate = true;
            Settings.Startup.IsAutoUpdateWithSilence = true;
            Settings.Startup.AutoUpdateWithSilenceStartTime = "06:00";
            Settings.Startup.AutoUpdateWithSilenceEndTime = "22:00";
            Settings.Startup.IsFoldAtStartup = false;
        }

        /// <summary>
        /// 将应用设置重置为推荐的默认值，并保存与重新加载配置以应用更改。
        /// </summary>
        /// <remarks>
        /// 如果配置重置受安全密码保护，则会提示用户输入密码；在验证失败时中止重置。方法会暂时停止加载标志以避免触发事件、将“开机启动”切换置为关闭，并在完成后显示一条通知。任何内部异常将被吞噬以保证流程不中断。
        /// </remarks>
        public async void BtnResetToSuggestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender != null && Ink_Canvas.Helpers.SecurityManager.IsPasswordRequiredForResetConfig(Settings))
                {
                    bool ok = await Ink_Canvas.Helpers.SecurityManager.PromptAndVerifyPasswordOrTotpAsync(Settings, this, "重置配置验证", "请输入安全密码或 TOTP 验证码以确认重置配置。");
                    if (!ok) return;
                }
            }
            catch
            {
            }

            try
            {
                isLoaded = false;
                SetSettingsToRecommendation();
                SaveSettingsToFile();
                LoadSettings(isStartup: false, skipAutoUpdateCheck: true);
                isLoaded = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try { ShowNotification("设置已重置为默认推荐设置~"); } catch { }
        }

        private async void SpecialVersionResetToSuggestion_Click()
        {
            await Task.Delay(1000);
            try
            {
                isLoaded = false;
                SetSettingsToRecommendation();
                Settings.Automation.AutoDelSavedFiles = true;
                Settings.Automation.AutoDelSavedFilesDaysThreshold = 15;
                Settings.Automation.AutoSavedStrokesLocation = @"D:\Ink Canvas\AutoSavedStrokes";
                SaveSettingsToFile();
                LoadSettings(isStartup: false, skipAutoUpdateCheck: true);
                isLoaded = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        #endregion


        public void UpdateFloatingBarIcons()
        {
            try
            {
                string currentMode = GetCurrentSelectedMode();

                bool isCursorSolid = currentMode == "cursor";
                bool isPenSolid = currentMode == "pen" || currentMode == "color";
                bool isCircleEraserSolid = currentMode == "eraser";
                bool isStrokeEraserSolid = currentMode == "eraserByStrokes";
                bool isLassoSolid = currentMode == "select";

                void SetIcon(ToolbarImageButton btn, bool isSolid, string solidGeom, string linedGeom)
                {
                    if (btn == null) return;
                    btn.Icon.Geometry = Geometry.Parse(isSolid ? solidGeom : linedGeom);
                }

                if (Settings.Appearance.UseLegacyFloatingBarUI)
                {
                    SetIcon(Cursor_Icon, isCursorSolid, XamlGraphicsIconGeometries.LegacySolidCursorIcon, XamlGraphicsIconGeometries.LegacyLinedCursorIcon);
                    SetIcon(Pen_Icon, isPenSolid, XamlGraphicsIconGeometries.LegacySolidPenIcon, XamlGraphicsIconGeometries.LegacyLinedPenIcon);
                    SetIcon(EraserByStrokes_Icon, isStrokeEraserSolid, XamlGraphicsIconGeometries.LegacySolidEraserStrokeIcon, XamlGraphicsIconGeometries.LegacyLinedEraserStrokeIcon);
                    SetIcon(Eraser_Icon, isCircleEraserSolid, XamlGraphicsIconGeometries.LegacySolidEraserCircleIcon, XamlGraphicsIconGeometries.LegacyLinedEraserCircleIcon);
                    SetIcon(SymbolIconSelect, isLassoSolid, XamlGraphicsIconGeometries.LegacySolidLassoSelectIcon, XamlGraphicsIconGeometries.LegacyLinedLassoSelectIcon);
                }
                else
                {
                    SetIcon(Cursor_Icon, isCursorSolid, XamlGraphicsIconGeometries.SolidCursorIcon, XamlGraphicsIconGeometries.LinedCursorIcon);
                    SetIcon(Pen_Icon, isPenSolid, XamlGraphicsIconGeometries.SolidPenIcon, XamlGraphicsIconGeometries.LinedPenIcon);
                    SetIcon(EraserByStrokes_Icon, isStrokeEraserSolid, XamlGraphicsIconGeometries.SolidEraserStrokeIcon, XamlGraphicsIconGeometries.LinedEraserStrokeIcon);
                    SetIcon(Eraser_Icon, isCircleEraserSolid, XamlGraphicsIconGeometries.SolidEraserCircleIcon, XamlGraphicsIconGeometries.LinedEraserCircleIcon);
                    SetIcon(SymbolIconSelect, isLassoSolid, XamlGraphicsIconGeometries.SolidLassoSelectIcon, XamlGraphicsIconGeometries.LinedLassoSelectIcon);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UpdateFloatingBarIcons 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public string GetCorrectIcon(string iconType, bool isSolid = false)
        {
            if (Settings.Appearance.UseLegacyFloatingBarUI)
            {
                // 使用老版图标
                switch (iconType)
                {
                    case "cursor":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidCursorIcon : XamlGraphicsIconGeometries.LegacyLinedCursorIcon;
                    case "pen":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidPenIcon : XamlGraphicsIconGeometries.LegacyLinedPenIcon;
                    case "eraserStroke":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidEraserStrokeIcon : XamlGraphicsIconGeometries.LegacyLinedEraserStrokeIcon;
                    case "eraserCircle":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidEraserCircleIcon : XamlGraphicsIconGeometries.LegacyLinedEraserCircleIcon;
                    case "lassoSelect":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidLassoSelectIcon : XamlGraphicsIconGeometries.LegacyLinedLassoSelectIcon;
                }
            }
            else
            {
                // 使用新版图标
                switch (iconType)
                {
                    case "cursor":
                        return isSolid ? XamlGraphicsIconGeometries.SolidCursorIcon : XamlGraphicsIconGeometries.LinedCursorIcon;
                    case "pen":
                        return isSolid ? XamlGraphicsIconGeometries.SolidPenIcon : XamlGraphicsIconGeometries.LinedPenIcon;
                    case "eraserStroke":
                        return isSolid ? XamlGraphicsIconGeometries.SolidEraserStrokeIcon : XamlGraphicsIconGeometries.LinedEraserStrokeIcon;
                    case "eraserCircle":
                        return isSolid ? XamlGraphicsIconGeometries.SolidEraserCircleIcon : XamlGraphicsIconGeometries.LinedEraserCircleIcon;
                    case "lassoSelect":
                        return isSolid ? XamlGraphicsIconGeometries.SolidLassoSelectIcon : XamlGraphicsIconGeometries.LinedLassoSelectIcon;
                }
            }
            return "";
        }

        #region 浮动栏按钮显示控制


        internal void UpdateFloatingBarButtonsVisibility()
        {
            try
            {
                UpdateToolbarComponentVisibility();

                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await Task.Delay(100);
                        string selectedToolMode = GetCurrentSelectedMode();
                        if (!string.IsNullOrEmpty(selectedToolMode))
                            SetFloatingBarHighlightPosition(selectedToolMode);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"重新计算高光位置失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新浮动栏按钮可见性时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        /// <summary>
        /// 将当前内存中的 Settings 序列化为格式化的 JSON 并写入应用程序配置文件（位于 App.RootPath 下的 Configs 目录或根设置文件）。
        /// </summary>
        /// <remarks>
        /// 在写入前会确保目标目录/文件具有写入权限（使用 ProcessProtectionManager）。任何写入失败或异常都会被吞掉，调用方不会收到异常抛出。
        /// </remarks>
        public static void SaveSettingsToFile() => SettingsManager.SaveSettingsToFile();

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void HyperlinkSourceToICCRepository_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gitea.bliemhax.com/kriastans/InkCanvasForClass");
            HideSubPanels();
        }

        private void HyperlinkSourceToPresentRepository_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/ChangSakura/Ink-Canvas");
            HideSubPanels();
        }

        private void HyperlinkSourceToOringinalRepository_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/WXRIW/Ink-Canvas");
            HideSubPanels();
        }

        private void UpdatePackageArchitectureSelector_Checked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            if (!(sender is RadioButton radioButton) || radioButton.Tag == null) return;

            var newArch = string.Equals(radioButton.Tag.ToString(), "X64", StringComparison.OrdinalIgnoreCase)
                ? UpdatePackageArchitecture.X64
                : UpdatePackageArchitecture.X86;

            if (Settings.Startup.UpdatePackageArchitecture == newArch)
                return;

            Settings.Startup.UpdatePackageArchitecture = newArch;
            SaveSettingsToFile();
            LogHelper.WriteLogToFile($"Settings | Update package architecture: {newArch}");
        }





        #region 底部按钮水平位置控制


        #endregion

        #region 文件关联管理


        #endregion

    }
}



