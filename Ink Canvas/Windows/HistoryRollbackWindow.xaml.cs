using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// Added for OrderByDescending

namespace Ink_Canvas
{
    public partial class HistoryRollbackWindow : Window
    {
        private class VersionItem
        {
            public string Version { get; set; }
            public string DownloadUrl { get; set; }
            public string ReleaseNotes { get; set; }
        }

        private List<VersionItem> versionList = new List<VersionItem>();
        private VersionItem selectedItem;
        private UpdateChannel channel = UpdateChannel.Release;
        private CancellationTokenSource downloadCts = null;

        public HistoryRollbackWindow(UpdateChannel channel = UpdateChannel.Release)
        {
            InitializeComponent();
            this.channel = channel;

            // 设置窗口置顶
            this.Topmost = true;

            // 应用当前主题
            ApplyCurrentTheme();

            // 隐藏主窗口
            HideMainWindow();

            LoadVersions();
        }

        /// <summary>
        /// 隐藏主窗口
        /// </summary>
        private void HideMainWindow()
        {
            try
            {
                // 获取主窗口实例
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // 隐藏主窗口
                    mainWindow.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏主窗口时出错: {ex.Message}", LogHelper.LogType.Error);
            }
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
                Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb));
                Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1f, 0x29, 0x37));
                Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6b, 0x72, 0x80));
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
                Resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x3b, 0x82, 0xf6));
                Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xf9, 0xfa, 0xfb));
                Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf));
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

        private async void LoadVersions()
        {
            LogHelper.WriteLogToFile($"HistoryRollback | 开始加载历史版本，通道: {channel}");
            RollbackButton.IsEnabled = false;
            VersionComboBox.Items.Clear();
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "";
            ReleaseNotesViewer.Markdown = "正在获取历史版本...";
            var releases = await AutoUpdateHelper.GetAllGithubReleases(channel);
            versionList.Clear();
            foreach (var (version, url, notes) in releases)
            {
                versionList.Add(new VersionItem { Version = version, DownloadUrl = url, ReleaseNotes = notes });
            }
            // 按版本号数字降序排列
            versionList = versionList.OrderByDescending(v => ParseVersionForSort(v.Version)).ToList();
            VersionComboBox.ItemsSource = versionList;
            if (versionList.Count > 0)
            {
                VersionComboBox.SelectedIndex = 0;
                RollbackButton.IsEnabled = true;
                LogHelper.WriteLogToFile($"HistoryRollback | 加载到 {versionList.Count} 个历史版本");
            }
            else
            {
                ReleaseNotesViewer.Markdown = "未获取到历史版本信息。";
                LogHelper.WriteLogToFile("HistoryRollback | 未获取到历史版本信息", LogHelper.LogType.Warning);
            }
        }

        // 辅助方法：解析版本号用于排序
        private Version ParseVersionForSort(string version)
        {
            var v = version.TrimStart('v', 'V');
            Version result;
            if (Version.TryParse(v, out result))
                return result;
            return new Version(0, 0, 0, 0);
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedItem = VersionComboBox.SelectedItem as VersionItem;
            if (selectedItem != null)
            {
                ReleaseNotesViewer.Markdown = selectedItem.ReleaseNotes ?? "无更新日志";
                LogHelper.WriteLogToFile($"HistoryRollback | 用户选择版本: {selectedItem.Version}");
            }
            // 取消聚焦，防止父级自动滚动
            Keyboard.ClearFocus();
        }

        private async void RollbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem == null) return;

            var dialog = new ContentDialog
            {
                Title = "暂停自动更新",
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消"
            };

            var panel = new iNKORE.UI.WPF.Controls.SimpleStackPanel
            {
                Spacing = 16,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var textBlock = new TextBlock
            {
                Text = "请选择在回滚后多久不再接收自动更新：",
                FontSize = 14,
                Foreground = (Brush)Resources["TextPrimaryBrush"]
            };

            var daysComboBox = new System.Windows.Controls.ComboBox
            {
                Width = 200,
                Height = 36,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            for (int i = 0; i <= 7; i++)
            {
                daysComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
                {
                    Content = $"{i} 天",
                    Tag = i
                });
            }

            daysComboBox.SelectedIndex = 0;

            panel.Children.Add(textBlock);
            panel.Children.Add(daysComboBox);
            dialog.Content = panel;

            var dialogResult = await dialog.ShowAsync();

            if (dialogResult == ContentDialogResult.Primary)
            {
                int days = 1;
                if (daysComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItemCombo &&
                    selectedItemCombo.Tag != null &&
                    int.TryParse(selectedItemCombo.Tag.ToString(), out int selectedDays))
                {
                    days = selectedDays;
                }

                if (days == 0)
                {
                    MainWindow.Settings.Startup.AutoUpdatePauseUntilDate = "";
                }
                else
                {
                    DateTime pauseUntilDate = DateTime.Now.AddDays(days);
                    MainWindow.Settings.Startup.AutoUpdatePauseUntilDate = pauseUntilDate.ToString("yyyy-MM-dd");
                    LogHelper.WriteLogToFile($"HistoryRollback | 用户选择暂停自动更新 {days} 天，截止日期: {pauseUntilDate:yyyy-MM-dd}");
                }

                MainWindow.SaveSettingsToFile();

                LogHelper.WriteLogToFile($"HistoryRollback | 用户选择暂停自动更新 {days} 天");
            }
            else
            {
                LogHelper.WriteLogToFile("HistoryRollback | 用户取消了回滚操作");
                return;
            }

            LogHelper.WriteLogToFile($"HistoryRollback | 用户确认回滚，目标版本: {selectedItem.Version}");
            RollbackButton.IsEnabled = false;
            VersionComboBox.IsEnabled = false;
            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "正在准备下载...";

            bool downloadSuccess = false;
            try
            {
                downloadSuccess = await AutoUpdateHelper.StartManualDownloadAndInstall(
                    selectedItem.Version,
                    channel,
                    (percent, text) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadProgressBar.Value = percent;
                            DownloadProgressText.Text = text;
                        });
                    }
                );
            }
            catch (Exception ex)
            {
                DownloadProgressText.Text = $"下载失败: {ex.Message}";
                LogHelper.WriteLogToFile($"HistoryRollback | 下载异常: {ex.Message}", LogHelper.LogType.Error);
            }

            if (downloadSuccess)
            {
                DownloadProgressBar.Value = 100;
                DownloadProgressText.Text = "下载完成，准备安装...";
                await Task.Delay(800);
                DialogResult = true;
                Close();
            }
            else
            {
                DownloadProgressText.Text = "下载失败，请检查网络后重试。";
                RollbackButton.IsEnabled = true;
                VersionComboBox.IsEnabled = true;
            }
        }



        protected override void OnClosing(CancelEventArgs e)
        {
            downloadCts?.Cancel();

            try
            {
                // 获取主窗口实例
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // 重新显示主窗口
                    mainWindow.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示主窗口时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            base.OnClosing(e);
        }


    }
}