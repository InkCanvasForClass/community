using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            this.Loaded += HasNewUpdateWindow_Loaded;
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

        private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("AutoUpdate | Update Now button clicked");
            
            // 设置结果为立即更新
            Result = UpdateResult.UpdateNow;
            
            // 关闭窗口，返回到MainWindow处理后续下载和安装流程
            DialogResult = true;
            Close();
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
                if (this.Height > maxHeight)
                {
                    this.Height = maxHeight;
                    needsAdjustment = true;
                    LogHelper.WriteLogToFile($"AutoUpdate | Adjusted window height to: {this.Height}");
                }
                
                // 如果窗口宽度超过最大允许宽度，调整窗口宽度
                if (this.Width > maxWidth)
                {
                    this.Width = maxWidth;
                    needsAdjustment = true;
                    LogHelper.WriteLogToFile($"AutoUpdate | Adjusted window width to: {this.Width}");
                }
                
                // 如果屏幕分辨率较低，调整更多UI元素
                if (screenHeight < 768 || screenWidth < 1024 || needsAdjustment)
                {
                    // 查找相关控件并调整大小
                    var markdownViewer = this.FindName("markdownContent") as MdXaml.MarkdownScrollViewer;
                    var updateNowButton = this.FindName("UpdateNowButton") as Button;
                    var updateLaterButton = this.FindName("UpdateLaterButton") as Button;
                    var skipVersionButton = this.FindName("SkipVersionButton") as Button;
                    
                    // 查找包含ScrollViewer的边框控件，减小其高度
                    var contentBorders = this.FindVisualChildren<Border>().ToList();
                    foreach (var border in contentBorders)
                    {
                        if (border.Child is ScrollViewer || border.Child is iNKORE.UI.WPF.Modern.Controls.ScrollViewerEx)
                        {
                            // 减小内容显示区域的高度
                            if (border.Height > 180)
                            {
                                border.Height = 160;
                                LogHelper.WriteLogToFile("AutoUpdate | Reduced content area height");
                            }
                            else if (border.Child is iNKORE.UI.WPF.Modern.Controls.ScrollViewerEx scrollViewer && scrollViewer.Height > 160)
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
                if (this.Left < 0) this.Left = 0;
                if (this.Top < 0) this.Top = 0;
                if (this.Left + this.Width > screenWidth) this.Left = screenWidth - this.Width;
                if (this.Top + this.Height > screenHeight) this.Top = screenHeight - this.Height;
                
                LogHelper.WriteLogToFile($"AutoUpdate | Final window size: {this.Width}x{this.Height}");
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
    }
}
