using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using MdXaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ink_Canvas
{
    /// <summary>
    /// HasNewUpdateWindow.xaml 的交互逻辑
    /// </summary>
    /// 


    public partial class HasNewUpdateWindow : Window
    {

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }

        /// <summary>
        /// 应用当前主题设置
        /// </summary>
        private void ApplyCurrentTheme()
        {
            try
            {
                // 根据主窗口的主题设置应用主题
                switch (MainWindow.Settings.Appearance.Theme)
                {
                    case 0: // 浅色主题
                        ThemeManager.SetRequestedTheme(this, ElementTheme.Light);
                        UpdateColorsForLightTheme();
                        break;
                    case 1: // 深色主题
                        ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
                        UpdateColorsForDarkTheme();
                        break;
                    case 2: // 跟随系统
                        if (IsSystemThemeLight())
                        {
                            ThemeManager.SetRequestedTheme(this, ElementTheme.Light);
                            UpdateColorsForLightTheme();
                        }
                        else
                        {
                            ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
                            UpdateColorsForDarkTheme();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 更新为浅色主题颜色
        /// </summary>
        private void UpdateColorsForLightTheme()
        {
            try
            {
                // 更新主要颜色资源
                Resources["UpdateWindowPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb));
                Resources["UpdateWindowPrimaryHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x1d, 0x4e, 0xd8));
                Resources["UpdateWindowPrimaryPressedBrush"] = new SolidColorBrush(Color.FromRgb(0x1e, 0x40, 0xaf));
                Resources["UpdateWindowCardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc));
                Resources["UpdateWindowCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xe5, 0xe7, 0xeb));
                Resources["UpdateWindowTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1f, 0x29, 0x37));
                Resources["UpdateWindowTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6b, 0x72, 0x80));
                Resources["UpdateWindowCloseButtonBrush"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                
                // 更新渐变背景
                var gradient = new LinearGradientBrush();
                gradient.StartPoint = new Point(0, 0);
                gradient.EndPoint = new Point(1, 1);
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x25, 0x63, 0xeb), 0));
                gradient.GradientStops.Add(new GradientStop(Colors.White, 1));
                Resources["HeaderGradient"] = gradient;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新浅色主题颜色时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 更新为深色主题颜色
        /// </summary>
        private void UpdateColorsForDarkTheme()
        {
            try
            {
                // 更新主要颜色资源
                Resources["UpdateWindowPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x3b, 0x82, 0xf6));
                Resources["UpdateWindowPrimaryHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb));
                Resources["UpdateWindowPrimaryPressedBrush"] = new SolidColorBrush(Color.FromRgb(0x1d, 0x4e, 0xd8));
                Resources["UpdateWindowCardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a));
                Resources["UpdateWindowCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
                Resources["UpdateWindowTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xf9, 0xfa, 0xfb));
                Resources["UpdateWindowTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf));
                Resources["UpdateWindowCloseButtonBrush"] = new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf));
                
                // 更新渐变背景
                var gradient = new LinearGradientBrush();
                gradient.StartPoint = new Point(0, 0);
                gradient.EndPoint = new Point(1, 1);
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x3b, 0x82, 0xf6), 0));
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x1f, 0x1f, 0x1f), 1));
                Resources["HeaderGradient"] = gradient;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新深色主题颜色时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 检查系统是否为浅色主题
        /// </summary>
        private bool IsSystemThemeLight()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int value)
                    {
                        return value == 1;
                    }
                }
            }
            catch
            {
                // 如果无法读取注册表，默认返回true（浅色主题）
            }
            return true;
        }

        // 存储更新版本信息
        public string CurrentVersion { get; set; }
        public string NewVersion { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseNotes { get; set; }

        // 更新按钮结果
        public enum UpdateResult
        {
            UpdateNow,
            UpdateLater,
            SkipVersion
        }

        public UpdateResult Result { get; private set; } = UpdateResult.UpdateLater;

        public HasNewUpdateWindow(string currentVersion, string newVersion, string releaseDate, string releaseNotes = null)
        {
            InitializeComponent();

            // 应用当前主题
            ApplyCurrentTheme();

            // 设置版本信息
            CurrentVersion = currentVersion;
            NewVersion = newVersion;
            ReleaseDate = releaseDate;
            ReleaseNotes = releaseNotes;

            // 更新UI
            updateVersionInfo.Text = $"本次更新： {CurrentVersion} -> {NewVersion}";
            updateDateInfo.Text = $"{ReleaseDate}发布更新";

            // 如果有发布说明，设置到Markdown内容中
            if (!string.IsNullOrEmpty(ReleaseNotes))
            {
                markdownContent.Markdown = ReleaseNotes;
            }

            // 自动更新和静默更新设置已移至设置界面，此处不再需要

            // 确保按钮可见且可用
            EnsureButtonsVisibility();

            // 显示窗口动画
            AnimationsHelper.ShowWithFadeIn(this, 0.25);

            // 设置深色模式
            UseImmersiveDarkMode(new WindowInteropHelper(this).Handle, true);

            // 窗口加载完成后再次确保按钮可见
            Loaded += HasNewUpdateWindow_Loaded;
        }

        private void HasNewUpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后再次确保按钮可见
            EnsureButtonsVisibility();

            // 调整窗口大小以适应屏幕分辨率
            AdjustWindowSizeForScreenResolution();
        }

        // 确保按钮可见并启用
        private void EnsureButtonsVisibility()
        {
            // 确保立即更新按钮可见
            UpdateNowButton.Visibility = Visibility.Visible;
            UpdateNowButton.IsEnabled = true;

            // 确保稍后更新按钮可见
            UpdateLaterButton.Visibility = Visibility.Visible;
            UpdateLaterButton.IsEnabled = true;

            // 确保跳过版本按钮可见
            SkipVersionButton.Visibility = Visibility.Visible;
            SkipVersionButton.IsEnabled = true;

            // 强制刷新UI
            UpdateLayout();

            // 记录日志
            LogHelper.WriteLogToFile("AutoUpdate | Update dialog buttons visibility ensured");
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("AutoUpdate | Update Now button clicked");
            // 禁用按钮，显示进度条
            UpdateNowButton.IsEnabled = false;
            UpdateLaterButton.IsEnabled = false;
            SkipVersionButton.IsEnabled = false;
            DownloadProgressPanel.Visibility = Visibility.Visible;
            
            // 重置进度条
            var progressFill = FindName("ProgressFill") as Border;
            if (progressFill != null)
            {
                progressFill.Width = 0;
            }
            DownloadProgressText.Text = "正在准备下载...";

            // 启动多线路下载
            bool downloadSuccess = false;
            try
            {
                // 获取当前通道的所有线路组
                var groups = AutoUpdateHelper.ChannelLineGroups[MainWindow.Settings.Startup.UpdateChannel];
                downloadSuccess = await AutoUpdateHelper.DownloadSetupFileWithFallback(NewVersion, groups, (percent, text) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // 更新自定义进度条
                        progressFill = FindName("ProgressFill") as Border;
                        if (progressFill != null)
                        {
                            progressFill.Width = (percent / 100.0) * 400; // 400是进度条总宽度
                        }
                        DownloadProgressText.Text = text;
                    });
                });
                if (downloadSuccess)
                {
                    // 下载完成后自动安装
                    await DownloadAndInstallVersion(NewVersion, null, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                DownloadProgressText.Text = $"下载失败: {ex.Message}";
                LogHelper.WriteLogToFile($"AutoUpdate | 下载异常: {ex.Message}", LogHelper.LogType.Error);
            }

            if (downloadSuccess)
            {
                // 设置进度条为100%
                progressFill = FindName("ProgressFill") as Border;
                if (progressFill != null)
                {
                    progressFill.Width = 400; // 100%完成
                }
                DownloadProgressText.Text = "下载完成，准备安装...";
                await Task.Delay(800);
                // 设置结果为立即更新
                Result = UpdateResult.UpdateNow;
                DialogResult = true;
                Close();
            }
            else
            {
                DownloadProgressText.Text = "下载失败，请检查网络后重试。";
                UpdateNowButton.IsEnabled = true;
                UpdateLaterButton.IsEnabled = true;
                SkipVersionButton.IsEnabled = true;
            }
        }

        private void UpdateLaterButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("AutoUpdate | Update Later button clicked");

            // 设置结果为稍后更新
            Result = UpdateResult.UpdateLater;

            // 关闭窗口
            DialogResult = true;
            Close();
        }

        private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("AutoUpdate | Skip Version button clicked");

            // 设置结果为跳过该版本
            Result = UpdateResult.SkipVersion;

            // 关闭窗口
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("AutoUpdate | Close button clicked");

            // 设置结果为稍后更新（默认行为）
            Result = UpdateResult.UpdateLater;

            // 关闭窗口
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 开始拖动窗口
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }



        // 根据屏幕分辨率调整窗口大小
        private void AdjustWindowSizeForScreenResolution()
        {
            try
            {
                // 获取主屏幕分辨率
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                LogHelper.WriteLogToFile($"AutoUpdate | Screen resolution: {screenWidth}x{screenHeight}");

                // 始终确保窗口不超过屏幕大小的85%
                double maxHeight = screenHeight * 0.85;
                double maxWidth = screenWidth * 0.85;

                bool needsAdjustment = false;

                // 如果窗口高度超过最大允许高度，调整窗口高度
                if (Height > maxHeight)
                {
                    Height = maxHeight;
                    needsAdjustment = true;
                    LogHelper.WriteLogToFile($"AutoUpdate | Adjusted window height to: {Height}");
                }

                // 如果窗口宽度超过最大允许宽度，调整窗口宽度
                if (Width > maxWidth)
                {
                    Width = maxWidth;
                    needsAdjustment = true;
                    LogHelper.WriteLogToFile($"AutoUpdate | Adjusted window width to: {Width}");
                }

                // 如果屏幕分辨率较低，调整更多UI元素
                if (screenHeight < 768 || screenWidth < 1024 || needsAdjustment)
                {
                    // 查找相关控件并调整大小
                    var markdownViewer = FindName("markdownContent") as MarkdownScrollViewer;
                    var updateNowButton = FindName("UpdateNowButton") as Button;
                    var updateLaterButton = FindName("UpdateLaterButton") as Button;
                    var skipVersionButton = FindName("SkipVersionButton") as Button;

                    // 查找包含ScrollViewer的边框控件，减小其高度
                    var contentBorders = FindVisualChildren<Border>().ToList();
                    foreach (var border in contentBorders)
                    {
                        if (border.Child is ScrollViewer || border.Child is ScrollViewerEx)
                        {
                            // 减小内容显示区域的高度
                            if (border.Height > 180)
                            {
                                border.Height = 160;
                                LogHelper.WriteLogToFile("AutoUpdate | Reduced content area height");
                            }
                            else if (border.Child is ScrollViewerEx scrollViewer && scrollViewer.Height > 160)
                            {
                                scrollViewer.Height = 160;
                                LogHelper.WriteLogToFile("AutoUpdate | Reduced scroll viewer height");
                            }
                        }
                    }

                    // 调整按钮大小
                    if (updateNowButton != null && updateLaterButton != null && skipVersionButton != null)
                    {
                        updateNowButton.Height = 42;
                        updateLaterButton.Height = 42;
                        skipVersionButton.Height = 42;
                        updateNowButton.Padding = new Thickness(15, 8, 15, 8);
                        updateLaterButton.Padding = new Thickness(15, 8, 15, 8);
                        skipVersionButton.Padding = new Thickness(15, 8, 15, 8);
                        LogHelper.WriteLogToFile("AutoUpdate | Reduced button sizes for small screen");
                    }
                }

                // 确保窗口在屏幕范围内
                if (Left < 0) Left = 0;
                if (Top < 0) Top = 0;
                if (Left + Width > screenWidth) Left = screenWidth - Width;
                if (Top + Height > screenHeight) Top = screenHeight - Height;

                LogHelper.WriteLogToFile($"AutoUpdate | Final window size: {Width}x{Height}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error adjusting window size: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 递归查找指定类型的所有子控件
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj = null) where T : DependencyObject
        {
            if (depObj == null)
                depObj = this;

            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        // 多线程分块下载并自动安装
        private async Task<bool> DownloadAndInstallVersion(string version, string downloadUrl, CancellationToken token)
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // 自动更新场景下，downloadUrl为null，直接用主下载目录
                string updatesFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AutoUpdate");
                downloadUrl = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
            }
            LogHelper.WriteLogToFile($"AutoUpdate | 开始安装版本: {version}");
            AutoUpdateHelper.InstallNewVersionApp(version, true);
            App.IsAppExitByUser = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
            return true;
        }
    }
}
