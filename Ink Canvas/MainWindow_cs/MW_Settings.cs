using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        private readonly object _hitokotoPrefetchSyncRoot = new object();
        private Task<string> _hitokotoPrefetchTask;
        private string _hitokotoPrefetchRequestUrl;
        private DispatcherTimer _chickenSoupAutoRotationTimer;

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

        private HttpClient GetHitokotoHttpClientOrNull(bool writeLog = true)
        {
            try
            {
                return HitokotoHttpClient.Value as HttpClient;
            }
            catch (Exception initEx)
            {
                if (writeLog)
                {
                    LogHelper.WriteLogToFile($"一言 HTTP 客户端初始化失败: {initEx.Message}", LogHelper.LogType.Warning);
                }
                return null;
            }
        }

        private string BuildHitokotoRequestUrl()
        {
            var cats = Settings.Appearance.HitokotoCategories;
            if (cats == null || cats.Count == 0)
                cats = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l" };

            var urlBuilder = new StringBuilder("https://v1.hitokoto.cn/?encode=text");
            foreach (var category in cats)
            {
                urlBuilder.Append($"&c={category}");
            }

            return urlBuilder.ToString();
        }

        private async Task<string> FetchHitokotoTextCoreAsync(HttpClient client, string requestUrl)
        {
            var response = await client.GetAsync(requestUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        private Task<string> EnsureHitokotoPrefetchTask(HttpClient client, string requestUrl)
        {
            Task<string> task;
            var created = false;

            lock (_hitokotoPrefetchSyncRoot)
            {
                if (_hitokotoPrefetchTask == null
                    || _hitokotoPrefetchTask.IsCanceled
                    || _hitokotoPrefetchTask.IsFaulted
                    || !string.Equals(_hitokotoPrefetchRequestUrl, requestUrl, StringComparison.Ordinal))
                {
                    _hitokotoPrefetchRequestUrl = requestUrl;
                    _hitokotoPrefetchTask = FetchHitokotoTextCoreAsync(client, requestUrl);
                    created = true;
                }

                task = _hitokotoPrefetchTask;
            }

            if (created)
            {
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        LogHelper.WriteLogToFile($"一言预取失败: {t.Exception?.GetBaseException().Message}", LogHelper.LogType.Warning);
                    }
                }, TaskScheduler.Default);
            }

            return task;
        }

        private bool HasUsableHitokotoPrefetchTask(string requestUrl)
        {
            lock (_hitokotoPrefetchSyncRoot)
            {
                return _hitokotoPrefetchTask != null
                    && !_hitokotoPrefetchTask.IsFaulted
                    && !_hitokotoPrefetchTask.IsCanceled
                    && string.Equals(_hitokotoPrefetchRequestUrl, requestUrl, StringComparison.Ordinal);
            }
        }

        private void StartHitokotoPrefetch(string requestUrl)
        {
            var client = GetHitokotoHttpClientOrNull(false);
            if (client == null)
            {
                return;
            }

            _ = EnsureHitokotoPrefetchTask(client, requestUrl);
        }

        private async Task<string> ConsumePrefetchedHitokotoTextAsync(HttpClient client, string requestUrl)
        {
            var task = EnsureHitokotoPrefetchTask(client, requestUrl);
            string text;
            try
            {
                text = await task.ConfigureAwait(true);
            }
            finally
            {
                lock (_hitokotoPrefetchSyncRoot)
                {
                    if (ReferenceEquals(_hitokotoPrefetchTask, task))
                    {
                        _hitokotoPrefetchTask = null;
                    }
                }
            }

            StartHitokotoPrefetch(requestUrl);
            return text;
        }

        /// <summary>
        /// 根据当前外观设置更新白板水印的名言文本。
        /// </summary>
        /// <remarks>
        /// 汇总所有启用的来源（预设来源 + 自定义方案），从中随机选取一个：
        /// 若选中预设为 osu/mottos/gaokao/phigros，从对应数组中随机选择一条；
        /// 若选中预设为 hitokoto，则异步请求 Hitokoto API，并在请求中显示占位提示，成功时将返回文本设为水印，失败时记录警告日志并设置可读的失败提示文本；
        /// 若选中的是自定义方案，则按行拆分其 Content 并随机选取一行。
        /// 当启用列表为空时直接返回，不修改当前文本。
        /// </remarks>
        internal async Task UpdateChickenSoupTextAsync()
        {
            try
            {
                if (!Settings.Appearance.EnableChickenSoupInWhiteboardMode)
                {
                    return;
                }

                // 汇总所有启用的方案
                var enabledSchemes = new List<TipsScheme>();

                var enabledPresets = Settings.Appearance.EnabledPresetTipsSources;
                foreach (var preset in ChickenSoup.GetPresetSchemes())
                {
                    if (enabledPresets != null && enabledPresets.Contains(preset.PresetId))
                    {
                        enabledSchemes.Add(preset);
                    }
                }

                var customSchemes = Settings.Appearance.CustomTipsSchemes;
                if (customSchemes != null)
                {
                    foreach (var custom in customSchemes)
                    {
                        if (custom != null && custom.IsEnabled)
                        {
                            enabledSchemes.Add(custom);
                        }
                    }
                }

                if (enabledSchemes.Count == 0)
                {
                    return;
                }

                var rnd = new Random();
                var selected = enabledSchemes[rnd.Next(enabledSchemes.Count)];
                var hasHitokotoEnabled = enabledSchemes.Any(i => i.IsPreset && i.PresetId == "hitokoto");
                string hitokotoRequestUrl = null;
                var hadExistingHitokotoPrefetch = false;

                if (hasHitokotoEnabled)
                {
                    hitokotoRequestUrl = BuildHitokotoRequestUrl();
                    hadExistingHitokotoPrefetch = HasUsableHitokotoPrefetchTask(hitokotoRequestUrl);
                    StartHitokotoPrefetch(hitokotoRequestUrl);
                }

                // Hitokoto 预设走 HTTP API
                if (selected.IsPreset && selected.PresetId == "hitokoto")
                {
                    var client = GetHitokotoHttpClientOrNull();
                    if (client == null)
                    {
                        BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_HttpUnavailable;
                        return;
                    }

                    var requestUrl = hitokotoRequestUrl ?? BuildHitokotoRequestUrl();
                    if (!hadExistingHitokotoPrefetch)
                    {
                        BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_Loading;
                    }

                    try
                    {
                        var text = await ConsumePrefetchedHitokotoTextAsync(client, requestUrl).ConfigureAwait(true);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            BlackBoardWaterMark.Text = text;
                        }
                        else
                        {
                            BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_NoContent;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"一言 API 请求失败: {ex.Message}", LogHelper.LogType.Warning);
                        BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_Unavailable;
                    }
                    return;
                }

                // 其它预设来源
                if (selected.IsPreset && !string.IsNullOrEmpty(selected.PresetId))
                {
                    var tips = ChickenSoup.GetTipsFromPreset(selected.PresetId);
                    if (tips != null && tips.Length > 0)
                    {
                        BlackBoardWaterMark.Text = tips[rnd.Next(tips.Length)];
                    }
                    return;
                }

                // 自定义方案
                if (!selected.IsPreset)
                {
                    if (string.IsNullOrWhiteSpace(selected.Content))
                    {
                        return;
                    }
                    var lines = selected.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0)
                    {
                        return;
                    }
                    BlackBoardWaterMark.Text = lines[rnd.Next(lines.Length)];
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新白板名言时出错: {ex.Message}", LogHelper.LogType.Warning);
                if (BlackBoardWaterMark != null)
                {
                    try { BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_Unavailable; } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                }
            }
        }

        /// <summary>
        /// 根据设置应用白板名言的位置。
        /// </summary>
        internal void ApplyChickenSoupPosition()
        {
            if (BlackBoardWaterMark == null) return;

            var position = Settings.Appearance.ChickenSoupPosition ?? "TopRight";
            const double margin = 25;

            // 清除旧的 Canvas 附加属性
            System.Windows.Controls.Canvas.SetLeft(BlackBoardWaterMark, double.NaN);
            System.Windows.Controls.Canvas.SetTop(BlackBoardWaterMark, double.NaN);
            System.Windows.Controls.Canvas.SetRight(BlackBoardWaterMark, double.NaN);
            System.Windows.Controls.Canvas.SetBottom(BlackBoardWaterMark, double.NaN);

            switch (position)
            {
                case "TopLeft":
                    System.Windows.Controls.Canvas.SetLeft(BlackBoardWaterMark, margin);
                    System.Windows.Controls.Canvas.SetTop(BlackBoardWaterMark, margin);
                    break;
                case "BottomRight":
                    System.Windows.Controls.Canvas.SetRight(BlackBoardWaterMark, margin);
                    System.Windows.Controls.Canvas.SetBottom(BlackBoardWaterMark, margin);
                    break;
                case "BottomLeft":
                    System.Windows.Controls.Canvas.SetLeft(BlackBoardWaterMark, margin);
                    System.Windows.Controls.Canvas.SetBottom(BlackBoardWaterMark, margin);
                    break;
                case "TopRight":
                default:
                    System.Windows.Controls.Canvas.SetRight(BlackBoardWaterMark, margin);
                    System.Windows.Controls.Canvas.SetTop(BlackBoardWaterMark, margin);
                    break;
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
        /// 启动白板名言自动轮换计时器。
        /// </summary>
        internal void StartChickenSoupAutoRotation()
        {
            if (!Settings.Appearance.EnableChickenSoupInWhiteboardMode) return;
            if (!Settings.Appearance.EnableChickenSoupAutoRotation) return;

            if (_chickenSoupAutoRotationTimer == null)
            {
                _chickenSoupAutoRotationTimer = new DispatcherTimer();
                _chickenSoupAutoRotationTimer.Tick += async (s, e) => await UpdateChickenSoupTextAsync();
            }

            _chickenSoupAutoRotationTimer.Interval = TimeSpan.FromSeconds(Settings.Appearance.ChickenSoupAutoRotationInterval);
            _chickenSoupAutoRotationTimer.Start();
        }

        /// <summary>
        /// 停止白板名言自动轮换计时器。
        /// </summary>
        internal void StopChickenSoupAutoRotation()
        {
            if (_chickenSoupAutoRotationTimer != null)
            {
                _chickenSoupAutoRotationTimer.Stop();
            }
        }

        /// <summary>
        /// 重启白板名言自动轮换计时器。
        /// </summary>
        internal void RestartChickenSoupAutoRotation()
        {
            StopChickenSoupAutoRotation();
            StartChickenSoupAutoRotation();
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
            var page = Application.Current.Windows.OfType<Window>()
                .SelectMany(w => FindVisualChildren<iNKORE.UI.WPF.Modern.Controls.NavigationView>(w))
                .SelectMany(nv => FindVisualChildren<Windows.SettingsViews.Pages.ToolbarAppearancePage>(nv))
                .FirstOrDefault();
            if (page == null) return;

            var comboBox = page.FindName("ComboBoxFloatingBarImg") as ComboBox;
            if (comboBox == null) return;

            // 保留前12个内置图标选项，移除所有自定义图标选项
            while (comboBox.Items.Count > 12)
            {
                comboBox.Items.RemoveAt(comboBox.Items.Count - 1);
            }

            // 添加自定义图标选项
            foreach (var customIcon in Settings.Appearance.CustomFloatingBarImgs)
            {
                comboBox.Items.Add(new ComboBoxItem { Content = customIcon.Name });
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        /// <summary>
        /// 更新PPT UI管理器设置的通用方法
        /// </summary>
        public void UpdatePPTUIManagerSettings()
        {
            if (_pptUIManager != null && IsInPPTPresentationMode)
            {
                var ppt = Settings.PowerPointSettings;

                // 计算有效值：位置 i 若 UseGlobalSettings=true，则采用全局字段值，否则采用位置自身字段值
                // 偏移按位置类型区分：侧边(左侧/右侧)用全局侧边偏移，底部(左下/右下)用全局底部偏移
                _pptUIManager.PPTLSButtonPosition = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTLSButtonPosition;
                _pptUIManager.PPTRSButtonPosition = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTRSButtonPosition;
                _pptUIManager.PPTLBButtonPosition = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTLBButtonPosition;
                _pptUIManager.PPTRBButtonPosition = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTRBButtonPosition;

                _pptUIManager.PPTLSButtonOpacity = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLSButtonOpacity;
                _pptUIManager.PPTRSButtonOpacity = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRSButtonOpacity;
                _pptUIManager.PPTLBButtonOpacity = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLBButtonOpacity;
                _pptUIManager.PPTRBButtonOpacity = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRBButtonOpacity;

                _pptUIManager.PPTLSButtonScale = ppt.PPTLSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLSButtonScale;
                _pptUIManager.PPTRSButtonScale = ppt.PPTRSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRSButtonScale;
                _pptUIManager.PPTLBButtonScale = ppt.PPTLBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLBButtonScale;
                _pptUIManager.PPTRBButtonScale = ppt.PPTRBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRBButtonScale;

                // 计算有效的 PPTButtonsDisplayOption：UseGlobalSettings 的位由 PPTGlobalButtonEnabled 决定
                string str = ppt.PPTButtonsDisplayOption.ToString("D4");
                if (str.Length < 4) str = "2222";
                char[] c = str.ToCharArray();
                // display option index: 0=LB, 1=RB, 2=LS, 3=RS
                if (ppt.PPTLBUseGlobalSettings) c[0] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
                if (ppt.PPTRBUseGlobalSettings) c[1] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
                if (ppt.PPTLSUseGlobalSettings) c[2] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
                if (ppt.PPTRSUseGlobalSettings) c[3] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
                _pptUIManager.PPTButtonsDisplayOption = int.Parse(new string(c));

                _pptUIManager.PPTSButtonsOption = ppt.PPTSButtonsOption;
                _pptUIManager.PPTBButtonsOption = ppt.PPTBButtonsOption;
                _pptUIManager.EnablePPTButtonPageClickable = ppt.EnablePPTButtonPageClickable;
                _pptUIManager.EnablePPTButtonLongPressPageTurn = ppt.EnablePPTButtonLongPressPageTurn;
                _pptUIManager.PPTNavBarScale = ppt.PPTNavBarScale;

                // 有效的显示页码 / 黑色背景通过 PPTSButtonsOption / PPTBButtonsOption 间接传递（原有机制）
                // 这里同步各位置的 ShowPageNumber / BlackBackground 字段（UseGlobalSettings 时用全局值覆盖位置字段，保证下游读取一致）
                if (ppt.PPTLSUseGlobalSettings) ppt.PPTLSShowPageNumber = ppt.PPTGlobalShowPageNumber;
                if (ppt.PPTRSUseGlobalSettings) ppt.PPTRSShowPageNumber = ppt.PPTGlobalShowPageNumber;
                if (ppt.PPTLBUseGlobalSettings) ppt.PPTLBShowPageNumber = ppt.PPTGlobalShowPageNumber;
                if (ppt.PPTRBUseGlobalSettings) ppt.PPTRBShowPageNumber = ppt.PPTGlobalShowPageNumber;
                if (ppt.PPTLSUseGlobalSettings) ppt.PPTLSBlackBackground = ppt.PPTGlobalBlackBackground;
                if (ppt.PPTRSUseGlobalSettings) ppt.PPTRSBlackBackground = ppt.PPTGlobalBlackBackground;
                if (ppt.PPTLBUseGlobalSettings) ppt.PPTLBBlackBackground = ppt.PPTGlobalBlackBackground;
                if (ppt.PPTRBUseGlobalSettings) ppt.PPTRBBlackBackground = ppt.PPTGlobalBlackBackground;

                _pptUIManager.UpdateNavigationPanelsVisibility();
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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



        private void EraserTypeTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            if (sender is TabControl tabControl)
            {
                Settings.Canvas.EraserShapeType = tabControl.SelectedIndex;
                SaveSettingsToFile();
                CheckEraserTypeTab();
                ApplyAdvancedEraserShape();
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
        }


        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void PenWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(PenWidthSlider, PenWidthText, "{0:0.0}");
            UpdateSliderText(BoardPenWidthSlider, BoardPenWidthText, "{0:0.0}");
            if (!isLoaded) return;
            if (_isUpdatingSliders) return;

            var value = ((Slider)sender).Value;

            _isUpdatingSliders = true;
            if (sender == BoardPenWidthSlider) PenWidthSlider.Value = value;
            else if (sender == PenWidthSlider) BoardPenWidthSlider.Value = value;
            _isUpdatingSliders = false;

            if (penType == 0)
            {
                drawingAttributes.Height = value / 2;
                drawingAttributes.Width = value / 2;
                Settings.Canvas.InkWidth = value / 2;
            }
            else if (penType == 1)
            {
                drawingAttributes.Height = value;
                drawingAttributes.Width = value / 2;
                Settings.Canvas.HighlighterWidth = value;
            }
            else if (penType == 2)
            {
                drawingAttributes.Width = value;
                drawingAttributes.Height = value;
                Settings.Canvas.LaserPenWidth = value;
            }
            SaveSettingsToFile();
        }

        /// <summary>
        /// 将画笔不透明度更新为滑块的当前值，并保存到设置中。
        /// </summary>
        /// <remarks>
        /// 使用滑块的当前值作为 alpha 通道更新 drawingAttributes.Color，同时将该值写入对应的设置项并持久化配置文件。
        /// </remarks>
        private void PenAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(PenAlphaSlider, PenAlphaText, "{0:0}");
            UpdateSliderText(BoardPenAlphaSlider, BoardPenAlphaText, "{0:0}");
            if (!isLoaded) return;
            if (_isUpdatingSliders) return;

            var value = ((Slider)sender).Value;

            _isUpdatingSliders = true;
            if (sender == BoardPenAlphaSlider) PenAlphaSlider.Value = value;
            else if (sender == PenAlphaSlider) BoardPenAlphaSlider.Value = value;
            _isUpdatingSliders = false;

            var NowR = drawingAttributes.Color.R;
            var NowG = drawingAttributes.Color.G;
            var NowB = drawingAttributes.Color.B;
            drawingAttributes.Color = Color.FromArgb((byte)value, NowR, NowG, NowB);

            if (penType == 0)
            {
                Settings.Canvas.InkAlpha = value;
            }
            else if (penType == 1)
            {
                Settings.Canvas.HighlighterAlpha = value;
            }
            else if (penType == 2)
            {
                Settings.Canvas.LaserPenAlpha = (int)value;
            }
            SaveSettingsToFile();
        }

        private void LaserPenFadeTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(LaserPenFadeTimeSlider, LaserPenFadeTimeText, "{0:0}s");
            UpdateSliderText(BoardLaserPenFadeTimeSlider, BoardLaserPenFadeTimeText, "{0:0}s");
            if (!isLoaded) return;
            if (_isUpdatingSliders) return;
            Settings.Canvas.InkFadeTime = (int)((Slider)sender).Value * 1000;
            if (_inkFadeManager != null)
            {
                _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
            }
            SaveSettingsToFile();
        }

        private void LaserPenFadeSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(LaserPenFadeSpeedSlider, LaserPenFadeSpeedText, "{0:0.0}x");
            UpdateSliderText(BoardLaserPenFadeSpeedSlider, BoardLaserPenFadeSpeedText, "{0:0.0}x");
            if (!isLoaded) return;
            if (_isUpdatingSliders) return;
            var val = Math.Round(((Slider)sender).Value, 1);
            Settings.Canvas.InkFadeSpeedMultiplier = val;
            if (_inkFadeManager != null)
            {
                _inkFadeManager.UpdateFadeSpeedMultiplier(val);
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
            Settings.Advanced.IsEnableAvoidFullScreenHelper = OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows11;
            Settings.Advanced.IsEnableForceFullScreen = false;
            Settings.Advanced.IsEnableDPIChangeDetection = false;
            Settings.Advanced.IsEnableResolutionChangeDetection = false;
            Settings.Advanced.EnableMultiScreenSupport = true;
            Settings.Advanced.FollowMouseForScreenSelection = true;

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
            Settings.Automation.IsAutoKillPPTService = false;
            Settings.Automation.IsAutoKillEasiNote = false;
            Settings.Automation.IsAutoKillVComYouJiao = false;
            Settings.Automation.IsAutoKillInkCanvas = false;
            Settings.Automation.IsAutoKillICA = false;
            Settings.Automation.IsAutoKillIDT = false;
            Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = false;
            Settings.Automation.IsSaveScreenshotsInDateFolders = false;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = true;
            Settings.Automation.IsAutoSaveScreenshotAtClear = true;
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
            Settings.PowerPointSettings.ShowPPTEnhancedPreviewLoadingAnimation = true;

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
            Settings.Canvas.MergeInkSmoothingWithUndo = false;
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
            Settings.InkToShape.HandwritingLanguageOverrideLcid = 0;
            Settings.InkToShape.HandwritingBeautifyDebounceMs = 2000;

            Settings.Startup.IsEnableNibMode = false;
            Settings.Startup.IsAutoUpdate = true;
            Settings.Startup.IsAutoUpdateWithSilence = true;
            Settings.Startup.AutoUpdateWithSilenceStartTime = "06:00";
            Settings.Startup.AutoUpdateWithSilenceEndTime = "22:00";
            Settings.Startup.IsFoldAtStartup = false;
            Settings.Startup.EnableFastStartup = false;
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
                    bool ok = await Ink_Canvas.Helpers.SecurityManager.PromptAndVerifyPasswordOrTotpAsync(Settings, this, Properties.MainWindowStrings.Main_Settings_ResetVerify, Properties.MainWindowStrings.Main_Settings_ResetVerifyMessage);
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

                // 确保工具栏配置也被重置为默认值
                var configName = SettingsManager.Settings?.ToolbarConfigName ?? "default";
                ToolbarRegistry.SaveConfigFile(configName, ToolbarRegistry.CreateDefaultLayout());

                LoadSettings(isStartup: false, skipAutoUpdateCheck: true);

                // 重置后重建工具栏
                RebuildToolbar();

                isLoaded = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try { ShowNotification(Properties.MainWindowStrings.Main_Settings_ResetDone); } catch { }
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

                // 确保工具栏配置也被重置为默认值
                var configName = SettingsManager.Settings?.ToolbarConfigName ?? "default";
                ToolbarRegistry.SaveConfigFile(configName, ToolbarRegistry.CreateDefaultLayout());

                LoadSettings(isStartup: false, skipAutoUpdateCheck: true);

                // 重置后重建工具栏
                RebuildToolbar();

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

        public double QuickPanelUnfoldedMargin => 8.0;
        public double QuickPanelFoldedMargin => -60.0;

        public void ApplySidePanelSettings()
        {
            LeftSidePanel?.ApplySettings();
            RightSidePanel?.ApplySettings();
            ApplyQuickPanelLayoutSettings();
        }

        public void ApplyQuickPanelLayoutSettings()
        {
            if (LeftQuickPanelBorder != null)
            {
                LeftQuickPanelBorder.CornerRadius = new CornerRadius(6);
                LeftQuickPanelBorder.ClipToBounds = false;
            }
            if (LeftQuickPanelShadow != null)
            {
                LeftQuickPanelShadow.Opacity = 0.3;
            }

            if (RightQuickPanelBorder != null)
            {
                RightQuickPanelBorder.CornerRadius = new CornerRadius(6);
                RightQuickPanelBorder.ClipToBounds = false;
            }
            if (RightQuickPanelShadow != null)
            {
                RightQuickPanelShadow.Opacity = 0.3;
            }

            // Update unfolded/folded margins in real-time
            if (LeftUnFoldButtonQuickPanel != null)
            {
                var leftMargin = LeftUnFoldButtonQuickPanel.Margin;
                double newLeft = LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible ? QuickPanelUnfoldedMargin : QuickPanelFoldedMargin;
                LeftUnFoldButtonQuickPanel.Margin = new Thickness(newLeft, leftMargin.Top, leftMargin.Right, leftMargin.Bottom);
            }
            if (RightUnFoldButtonQuickPanel != null)
            {
                var rightMargin = RightUnFoldButtonQuickPanel.Margin;
                double newRight = RightUnFoldButtonQuickPanel.Visibility == Visibility.Visible ? QuickPanelUnfoldedMargin : QuickPanelFoldedMargin;
                RightUnFoldButtonQuickPanel.Margin = new Thickness(rightMargin.Left, rightMargin.Top, newRight, rightMargin.Bottom);
            }

            if (LeftSidePanel != null)
            {
                var leftMargin = LeftSidePanel.Margin;
                double newLeft = LeftSidePanel.Visibility == Visibility.Visible ? -10.0 : QuickPanelFoldedMargin;
                LeftSidePanel.Margin = new Thickness(newLeft, leftMargin.Top, leftMargin.Right, leftMargin.Bottom);
            }
            if (RightSidePanel != null)
            {
                var rightMargin = RightSidePanel.Margin;
                double newRight = RightSidePanel.Visibility == Visibility.Visible ? -10.0 : QuickPanelFoldedMargin;
                RightSidePanel.Margin = new Thickness(rightMargin.Left, rightMargin.Top, newRight, rightMargin.Bottom);
            }
        }

    }
}



