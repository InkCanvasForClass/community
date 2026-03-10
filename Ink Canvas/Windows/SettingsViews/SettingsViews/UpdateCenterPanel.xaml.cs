using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Ink_Canvas.Helpers.AutoUpdateHelper;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows.SettingsViews
{
    public partial class UpdateCenterPanel : UserControl
    {
        private bool _isLoaded = false;
        private string _currentTab = "Update";
        private List<(string version, string downloadUrl, string releaseNotes)> _historyVersions = new List<(string, string, string)>();
        private string _availableVersion = null;
        private UpdateLineGroup _availableLineGroup = null;

        public UpdateCenterPanel()
        {
            InitializeComponent();
            Loaded += UpdateCenterPanel_Loaded;
        }

        private void UpdateCenterPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                LoadSettings();
                SwitchTab("Update");
                CheckUpdateStatus();
                ApplyTheme();
            }
        }

        private void LoadSettings()
        {
            if (MainWindow.Settings == null) return;

            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var platform = Environment.Is64BitOperatingSystem ? "windows-x64" : "windows-x86";
                CurrentVersionText.Text = $"当前版本: v{version} | {platform}";

                LoadLastCheckTime();

                var toggleSwitchIsAutoUpdate = FindToggleSwitch("ToggleSwitchIsAutoUpdate");
                if (toggleSwitchIsAutoUpdate != null)
                {
                    bool isAutoUpdate = MainWindow.Settings.Startup.IsAutoUpdate;
                    SetToggleSwitchState(toggleSwitchIsAutoUpdate, isAutoUpdate);
                }

                var toggleSwitchIsAutoUpdateWithSilence = FindToggleSwitch("ToggleSwitchIsAutoUpdateWithSilence");
                if (toggleSwitchIsAutoUpdateWithSilence != null)
                {
                    bool isAutoUpdateWithSilence = MainWindow.Settings.Startup.IsAutoUpdateWithSilence;
                    SetToggleSwitchState(toggleSwitchIsAutoUpdateWithSilence, isAutoUpdateWithSilence);
                    AutoUpdateWithSilencePanel.Visibility = MainWindow.Settings.Startup.IsAutoUpdate ? Visibility.Visible : Visibility.Collapsed;
                    AutoUpdateTimePeriodSeparator.Visibility = MainWindow.Settings.Startup.IsAutoUpdate ? Visibility.Visible : Visibility.Collapsed;
                }

                if (AutoUpdateTimePeriodBlock != null)
                {
                    AutoUpdateTimePeriodBlock.Visibility =
                        (MainWindow.Settings.Startup.IsAutoUpdateWithSilence && MainWindow.Settings.Startup.IsAutoUpdate) ?
                        Visibility.Visible : Visibility.Collapsed;
                }

                if (AutoUpdateWithSilenceStartTimeComboBox != null)
                {
                    var startTime = MainWindow.Settings.Startup.AutoUpdateWithSilenceStartTime ?? "06:00";
                    var startHour = startTime.Contains(":") ? startTime.Split(':')[0].Trim().PadLeft(2, '0') : startTime;
                    var startItem = AutoUpdateWithSilenceStartTimeComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == startHour);
                    if (startItem != null)
                    {
                        AutoUpdateWithSilenceStartTimeComboBox.SelectedItem = startItem;
                    }
                }

                if (AutoUpdateWithSilenceEndTimeComboBox != null)
                {
                    var endTime = MainWindow.Settings.Startup.AutoUpdateWithSilenceEndTime ?? "22:00";
                    var endHour = endTime.Contains(":") ? endTime.Split(':')[0].Trim().PadLeft(2, '0') : endTime;
                    var endItem = AutoUpdateWithSilenceEndTimeComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == endHour);
                    if (endItem != null)
                    {
                        AutoUpdateWithSilenceEndTimeComboBox.SelectedItem = endItem;
                    }
                }

                if (MainWindow.Settings.Startup.UpdateChannel == UpdateChannel.Release)
                {
                    UpdateUpdateChannelButtons(UpdateChannel.Release);
                }
                else if (MainWindow.Settings.Startup.UpdateChannel == UpdateChannel.Preview)
                {
                    UpdateUpdateChannelButtons(UpdateChannel.Preview);
                }
                else
                {
                    UpdateUpdateChannelButtons(UpdateChannel.Beta);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCenterPanel 加载设置失败: {ex.Message}");
            }
        }

        private void LoadLastCheckTime()
        {
            try
            {
                var lastCheckTime = DeviceIdentifier.GetLastUpdateCheck();
                if (lastCheckTime != DateTime.MinValue)
                {
                    var now = DateTime.Now;
                    var timeDiff = now - lastCheckTime;

                    if (timeDiff.TotalDays < 1 && lastCheckTime.Date == now.Date)
                    {
                        LastCheckTimeText.Text = $"上次检查时间: 今天, {lastCheckTime:HH:mm}";
                    }
                    else if (timeDiff.TotalDays < 2 && lastCheckTime.Date == now.Date.AddDays(-1))
                    {
                        LastCheckTimeText.Text = $"上次检查时间: 昨天, {lastCheckTime:HH:mm}";
                    }
                    else
                    {
                        LastCheckTimeText.Text = $"上次检查时间: {lastCheckTime:yyyy-MM-dd HH:mm}";
                    }
                    return;
                }
                LastCheckTimeText.Text = "上次检查时间: 从未";
            }
            catch
            {
                LastCheckTimeText.Text = "上次检查时间: 从未";
            }
        }

        private void SaveLastCheckTime()
        {
            try
            {
                DeviceIdentifier.RecordUpdateCheck();
                LoadLastCheckTime();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存上次检查时间失败: {ex.Message}");
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            SwitchTab(tag);
        }

        private void SwitchTab(string tabName)
        {
            _currentTab = tabName;
            bool isDarkTheme = ThemeHelper.IsDarkTheme;
            var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            var unselectedBrush = new SolidColorBrush(Colors.Transparent);

            TabUpdate.Background = tabName == "Update" ? selectedBrush : unselectedBrush;
            var updateText = TabUpdate.Child as TextBlock;
            if (updateText != null)
            {
                updateText.FontWeight = tabName == "Update" ? FontWeights.Bold : FontWeights.Normal;
                updateText.Foreground = ThemeHelper.GetTextPrimaryBrush();
            }

            TabRollback.Background = tabName == "Rollback" ? selectedBrush : unselectedBrush;
            var rollbackText = TabRollback.Child as TextBlock;
            if (rollbackText != null)
            {
                rollbackText.FontWeight = tabName == "Rollback" ? FontWeights.Bold : FontWeights.Normal;
                rollbackText.Foreground = ThemeHelper.GetTextPrimaryBrush();
            }

            TabSettings.Background = tabName == "Settings" ? selectedBrush : unselectedBrush;
            var settingsText = TabSettings.Child as TextBlock;
            if (settingsText != null)
            {
                settingsText.FontWeight = tabName == "Settings" ? FontWeights.Bold : FontWeights.Normal;
                settingsText.Foreground = ThemeHelper.GetTextPrimaryBrush();
            }

            UpdateTabContent.Visibility = tabName == "Update" ? Visibility.Visible : Visibility.Collapsed;
            RollbackTabContent.Visibility = tabName == "Rollback" ? Visibility.Visible : Visibility.Collapsed;
            SettingsTabContent.Visibility = tabName == "Settings" ? Visibility.Visible : Visibility.Collapsed;

            if (tabName == "Rollback" && _historyVersions.Count == 0)
            {
                LoadHistoryVersions();
            }
        }

        private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateStatus(true);
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_availableVersion))
            {
                MessageBox.Show("没有可用的更新版本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                UpdateNowButton.IsEnabled = false;
                UpdateLaterButton.IsEnabled = false;
                SkipVersionButton.IsEnabled = false;

                var updateChannel = UpdateChannel.Release;
                if (MainWindow.Settings?.Startup != null)
                {
                    updateChannel = MainWindow.Settings.Startup.UpdateChannel;
                }

                var groups = _availableLineGroup != null ? new List<UpdateLineGroup> { _availableLineGroup } : AutoUpdateHelper.ChannelLineGroups[updateChannel];

                bool downloadSuccess = await AutoUpdateHelper.DownloadSetupFileWithFallback(_availableVersion, groups, (percent, text) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"下载进度: {percent}% - {text}");
                    }));
                });

                if (downloadSuccess)
                {
                    AutoUpdateHelper.InstallNewVersionApp(_availableVersion, true);
                    App.IsAppExitByUser = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
                else
                {
                    MessageBox.Show("下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateNowButton.IsEnabled = true;
                    UpdateLaterButton.IsEnabled = true;
                    SkipVersionButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"立即更新失败: {ex.Message}");
                MessageBox.Show($"更新过程中发生错误：{ex.Message}", "更新错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateNowButton.IsEnabled = true;
                UpdateLaterButton.IsEnabled = true;
                SkipVersionButton.IsEnabled = true;
            }
        }

        private async void UpdateLaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_availableVersion))
            {
                MessageBox.Show("没有可用的更新版本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                UpdateNowButton.IsEnabled = false;
                UpdateLaterButton.IsEnabled = false;
                SkipVersionButton.IsEnabled = false;

                var updateChannel = UpdateChannel.Release;
                if (MainWindow.Settings?.Startup != null)
                {
                    updateChannel = MainWindow.Settings.Startup.UpdateChannel;
                }

                var groups = _availableLineGroup != null ? new List<AutoUpdateHelper.UpdateLineGroup> { _availableLineGroup } : AutoUpdateHelper.ChannelLineGroups[updateChannel];

                bool downloadSuccess = await AutoUpdateHelper.DownloadSetupFileWithFallback(_availableVersion, groups, (percent, text) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"下载进度: {percent}% - {text}");
                    }));
                });

                if (downloadSuccess)
                {
                    MainWindow.Settings.Startup.IsAutoUpdate = true;
                    MainWindow.Settings.Startup.IsAutoUpdateWithSilence = true;
                    MainWindow.SaveSettingsToFile();

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var field = typeof(MainWindow).GetField("AvailableLatestVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(mainWindow, _availableVersion);
                        }

                        var timerField = typeof(MainWindow).GetField("timerCheckAutoUpdateWithSilence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (timerField != null)
                        {
                            var timer = timerField.GetValue(mainWindow);
                            if (timer != null)
                            {
                                var startMethod = timer.GetType().GetMethod("Start");
                                if (startMethod != null)
                                {
                                    startMethod.Invoke(timer, null);
                                }
                            }
                        }
                    }

                    MessageBox.Show("更新已下载完成，将在软件关闭时自动安装。", "更新已准备就绪", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateNowButton.IsEnabled = true;
                UpdateLaterButton.IsEnabled = true;
                SkipVersionButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"稍后更新失败: {ex.Message}");
                MessageBox.Show($"更新过程中发生错误：{ex.Message}", "更新错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateNowButton.IsEnabled = true;
                UpdateLaterButton.IsEnabled = true;
                SkipVersionButton.IsEnabled = true;
            }
        }

        private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_availableVersion))
            {
                MessageBox.Show("没有可用的更新版本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                MainWindow.Settings.Startup.SkippedVersion = _availableVersion;
                MainWindow.SaveSettingsToFile();

                UpdateAvailablePanel.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = "已跳过该版本";

                MessageBox.Show($"已设置跳过版本 {_availableVersion}，在下次发布新版本之前不会再提示更新。",
                               "已跳过此版本",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"跳过版本失败: {ex.Message}");
                MessageBox.Show($"跳过版本时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckUpdateStatus(bool manualCheck = false)
        {
            UpdateStatusText.Text = "正在检查更新...";
            UpdateAvailablePanel.Visibility = Visibility.Collapsed;
            CheckUpdateButton.IsEnabled = false;
            StartLoadingAnimation();

            Task.Run(async () =>
            {
                try
                {
                    var updateChannel = UpdateChannel.Release;
                    if (MainWindow.Settings?.Startup != null)
                    {
                        updateChannel = MainWindow.Settings.Startup.UpdateChannel;
                    }

                    var (remoteVersion, lineGroup, releaseNotes) = await AutoUpdateHelper.CheckForUpdates(updateChannel, manualCheck, false);

                    if (manualCheck)
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            var field = typeof(MainWindow).GetField("AvailableLatestVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field != null)
                            {
                                field.SetValue(mainWindow, null);
                            }
                        }
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StopLoadingAnimation();

                        if (!string.IsNullOrEmpty(remoteVersion))
                        {
                            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
                            var localVersionStr = localVersion.ToString();
                            var remoteVersionStr = remoteVersion.TrimStart('v', 'V');

                            Version local = new Version(localVersionStr);
                            Version remote = new Version(remoteVersionStr);

                            if (remote > local)
                            {
                                UpdateStatusText.Text = "有可用更新";
                                LatestVersionText.Text = $"版本 {remoteVersion} 现已可用";
                                UpdateAvailablePanel.Visibility = Visibility.Visible;
                                _availableVersion = remoteVersion;
                                _availableLineGroup = lineGroup;
                            }
                            else
                            {
                                UpdateStatusText.Text = "你使用的是最新版本";
                                UpdateAvailablePanel.Visibility = Visibility.Collapsed;
                                _availableVersion = null;
                                _availableLineGroup = null;
                            }
                        }
                        else
                        {
                            UpdateStatusText.Text = "你使用的是最新版本";
                            UpdateAvailablePanel.Visibility = Visibility.Collapsed;
                            _availableVersion = null;
                            _availableLineGroup = null;
                        }

                        if (!string.IsNullOrEmpty(releaseNotes))
                        {
                            UpdateLogViewer.Markdown = releaseNotes;
                        }
                        else
                        {
                            LoadUpdateLogAsFallback(updateChannel);
                        }

                        if (manualCheck)
                        {
                            SaveLastCheckTime();
                        }

                        CheckUpdateButton.IsEnabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StopLoadingAnimation();
                        UpdateStatusText.Text = "检查更新失败";
                        UpdateAvailablePanel.Visibility = Visibility.Collapsed;
                        CheckUpdateButton.IsEnabled = true;
                    }));
                    System.Diagnostics.Debug.WriteLine($"检查更新状态失败: {ex.Message}");
                }
            });
        }

        private async Task LoadUpdateLogAsFallback(UpdateChannel channel)
        {
            try
            {
                var updateLog = await AutoUpdateHelper.GetUpdateLog(channel);
                if (!string.IsNullOrEmpty(updateLog))
                {
                    UpdateLogViewer.Markdown = updateLog;
                }
                else
                {
                    UpdateLogViewer.Markdown = "暂无更新日志";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载更新日志失败: {ex.Message}");
                UpdateLogViewer.Markdown = "加载更新日志失败";
            }
        }

        private async void LoadUpdateLogWithPriority(UpdateChannel channel)
        {
            try
            {
                var (remoteVersion, lineGroup, releaseNotes) = await AutoUpdateHelper.CheckForUpdates(channel, false, false);

                if (!string.IsNullOrEmpty(releaseNotes))
                {
                    UpdateLogViewer.Markdown = releaseNotes;
                }
                else
                {
                    await LoadUpdateLogAsFallback(channel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载更新日志失败: {ex.Message}");
                await LoadUpdateLogAsFallback(channel);
            }
        }

        private async void LoadHistoryVersions()
        {
            try
            {
                HistoryLogViewer.Markdown = "正在加载历史版本...";
                RollbackVersionComboBox.Items.Clear();

                var updateChannel = UpdateChannel.Release;
                if (MainWindow.Settings?.Startup != null)
                {
                    updateChannel = MainWindow.Settings.Startup.UpdateChannel;
                }

                _historyVersions = await AutoUpdateHelper.GetAllGithubReleases(updateChannel);

                if (_historyVersions.Count > 0)
                {
                    var markdown = new System.Text.StringBuilder();
                    markdown.AppendLine("# 历史版本列表\n");

                    foreach (var (version, downloadUrl, releaseNotes) in _historyVersions)
                    {
                        markdown.AppendLine($"## {version}\n");
                        if (!string.IsNullOrEmpty(releaseNotes))
                        {
                            var notes = releaseNotes.Length > 200 ? releaseNotes.Substring(0, 200) + "..." : releaseNotes;
                            markdown.AppendLine(notes);
                        }
                        markdown.AppendLine("\n---\n");
                    }

                    HistoryLogViewer.Markdown = markdown.ToString();

                    foreach (var versionInfo in _historyVersions)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = versionInfo.version,
                            Tag = versionInfo
                        };
                        RollbackVersionComboBox.Items.Add(item);
                    }
                }
                else
                {
                    HistoryLogViewer.Markdown = "未获取到历史版本信息。";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载历史版本失败: {ex.Message}");
                HistoryLogViewer.Markdown = "加载历史版本失败";
            }
        }

        private void RollbackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RollbackVersionComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is ValueTuple<string, string, string> versionInfo)
            {
                var version = versionInfo.Item1;
                var downloadUrl = versionInfo.Item2;
                var releaseNotes = versionInfo.Item3;

                if (!string.IsNullOrEmpty(releaseNotes))
                {
                    HistoryLogViewer.Markdown = $"# {version}\n\n{releaseNotes}";
                }
                else
                {
                    HistoryLogViewer.Markdown = $"# {version}\n\n无更新日志";
                }
            }
        }

        private async void RollbackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RollbackVersionComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is ValueTuple<string, string, string> versionInfo)
                {
                    var version = versionInfo.Item1;
                    var downloadUrl = versionInfo.Item2;
                    var releaseNotes = versionInfo.Item3;

                    var updateChannel = UpdateChannel.Release;
                    if (MainWindow.Settings?.Startup != null)
                    {
                        updateChannel = MainWindow.Settings.Startup.UpdateChannel;
                    }

                    var dialog = new ContentDialog
                    {
                        Title = "暂停自动更新",
                        PrimaryButtonText = "确定",
                        SecondaryButtonText = "取消"
                    };

                    var panel = new iNKORE.UI.WPF.Modern.Controls.SimpleStackPanel
                    {
                        Spacing = 16,
                        Margin = new Thickness(0, 10, 0, 0)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = "请选择在回滚后多久不再接收自动更新：",
                        FontSize = 14,
                        Foreground = ThemeHelper.GetTextPrimaryBrush()
                    };

                    var daysComboBox = new ComboBox
                    {
                        Width = 200,
                        Height = 36,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    for (int i = 0; i <= 7; i++)
                    {
                        daysComboBox.Items.Add(new ComboBoxItem
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
                        int days = 0;
                        if (daysComboBox.SelectedItem is ComboBoxItem selectedItemCombo &&
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
                            LogHelper.WriteLogToFile($"UpdateCenter | 用户选择暂停自动更新 {days} 天，截止日期: {pauseUntilDate:yyyy-MM-dd}");
                        }

                        MainWindow.SaveSettingsToFile();
                        LogHelper.WriteLogToFile($"UpdateCenter | 用户确认回滚，目标版本: {version}");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("UpdateCenter | 用户取消了回滚操作");
                        return;
                    }

                    {
                        RollbackButton.IsEnabled = false;
                        RollbackVersionComboBox.IsEnabled = false;

                        try
                        {
                            bool downloadSuccess = await AutoUpdateHelper.StartManualDownloadAndInstall(
                                version,
                                updateChannel,
                                (percent, text) =>
                                {
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"回滚进度: {percent}% - {text}");
                                    }));
                                }
                            );

                            if (!downloadSuccess)
                            {
                                MessageBox.Show(
                                    "回滚失败，请检查网络连接后重试。",
                                    "回滚失败",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                RollbackButton.IsEnabled = true;
                                RollbackVersionComboBox.IsEnabled = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"回滚过程中发生错误：{ex.Message}",
                                "回滚错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            RollbackButton.IsEnabled = true;
                            RollbackVersionComboBox.IsEnabled = true;
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "请先选择一个要回滚的版本。",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"回滚操作失败: {ex.Message}");
                MessageBox.Show(
                    $"回滚操作失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void FixVersionButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindowSettingsHelper.InvokeMainWindowMethod("FixVersionButton_Click", sender, e);
        }

        private void OptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            switch (tag)
            {
                case "UpdateChannel_Release":
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        MainWindow.Settings.Startup.UpdateChannel = UpdateChannel.Release;
                    }, "UpdateChannelSelector");
                    MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateChannelSelector_Checked",
                        new System.Windows.Controls.RadioButton { Tag = "Release" }, e);
                    UpdateUpdateChannelButtons(UpdateChannel.Release);
                    LoadHistoryVersions();
                    LoadUpdateLogWithPriority(UpdateChannel.Release);
                    break;

                case "UpdateChannel_Preview":
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        MainWindow.Settings.Startup.UpdateChannel = UpdateChannel.Preview;
                    }, "UpdateChannelSelector");
                    MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateChannelSelector_Checked",
                        new System.Windows.Controls.RadioButton { Tag = "Preview" }, e);
                    UpdateUpdateChannelButtons(UpdateChannel.Preview);
                    LoadHistoryVersions();
                    LoadUpdateLogWithPriority(UpdateChannel.Preview);
                    break;

                case "UpdateChannel_Beta":
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        MainWindow.Settings.Startup.UpdateChannel = UpdateChannel.Beta;
                    }, "UpdateChannelSelector");
                    MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateChannelSelector_Checked",
                        new System.Windows.Controls.RadioButton { Tag = "Beta" }, e);
                    UpdateUpdateChannelButtons(UpdateChannel.Beta);
                    LoadHistoryVersions();
                    LoadUpdateLogWithPriority(UpdateChannel.Beta);
                    break;
            }
        }

        private void UpdateUpdateChannelButtons(UpdateChannel selectedChannel)
        {
            try
            {
                bool isDarkTheme = ThemeHelper.IsDarkTheme;
                var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
                var unselectedBrush = new SolidColorBrush(Colors.Transparent);

                if (UpdateChannelReleaseBorder != null)
                {
                    bool isSelected = selectedChannel == UpdateChannel.Release;
                    UpdateChannelReleaseBorder.Background = isSelected ? selectedBrush : unselectedBrush;
                    var textBlock = UpdateChannelReleaseBorder.Child as TextBlock;
                    if (textBlock != null)
                    {
                        textBlock.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                        textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                    }
                }

                if (UpdateChannelPreviewBorder != null)
                {
                    bool isSelected = selectedChannel == UpdateChannel.Preview;
                    UpdateChannelPreviewBorder.Background = isSelected ? selectedBrush : unselectedBrush;
                    var textBlock = UpdateChannelPreviewBorder.Child as TextBlock;
                    if (textBlock != null)
                    {
                        textBlock.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                        textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                    }
                }

                if (UpdateChannelBetaBorder != null)
                {
                    bool isSelected = selectedChannel == UpdateChannel.Beta;
                    UpdateChannelBetaBorder.Background = isSelected ? selectedBrush : unselectedBrush;
                    var textBlock = UpdateChannelBetaBorder.Child as TextBlock;
                    if (textBlock != null)
                    {
                        textBlock.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                        textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新更新通道按钮状态时出错: {ex.Message}");
            }
        }

        private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            bool currentState = GetCurrentSettingValue(tag);
            bool newState = !currentState;
            SetToggleSwitchState(border, newState);

            switch (tag)
            {
                case "IsAutoUpdate":
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoUpdate", newState);
                    AutoUpdateWithSilencePanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    AutoUpdateTimePeriodSeparator.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    if (AutoUpdateTimePeriodBlock != null)
                    {
                        AutoUpdateTimePeriodBlock.Visibility =
                            (MainWindow.Settings.Startup.IsAutoUpdateWithSilence && MainWindow.Settings.Startup.IsAutoUpdate) ?
                            Visibility.Visible : Visibility.Collapsed;
                    }
                    break;

                case "IsAutoUpdateWithSilence":
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoUpdateWithSilence", newState);
                    if (AutoUpdateTimePeriodBlock != null)
                    {
                        AutoUpdateTimePeriodBlock.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;
            }
        }

        private bool GetCurrentSettingValue(string tag)
        {
            if (MainWindow.Settings == null) return false;

            try
            {
                switch (tag)
                {
                    case "IsAutoUpdate":
                        return MainWindow.Settings.Startup?.IsAutoUpdate ?? false;
                    case "IsAutoUpdateWithSilence":
                        return MainWindow.Settings.Startup?.IsAutoUpdateWithSilence ?? false;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private Border FindToggleSwitch(string name)
        {
            return this.FindDescendantByName(name) as Border;
        }

        private void SetToggleSwitchState(Border toggleSwitch, bool isOn)
        {
            if (toggleSwitch == null) return;
            toggleSwitch.Background = isOn
                ? new SolidColorBrush(Color.FromRgb(53, 132, 228))
                : (ThemeHelper.IsDarkTheme ? ThemeHelper.GetButtonBackgroundBrush() : new SolidColorBrush(Color.FromRgb(225, 225, 225)));
            var innerBorder = toggleSwitch.Child as Border;
            if (innerBorder != null)
            {
                innerBorder.HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                innerBorder.Background = new SolidColorBrush(Colors.White);
            }
        }

        private void AutoUpdateWithSilenceStartTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("AutoUpdateWithSilenceStartTimeComboBox", AutoUpdateWithSilenceStartTimeComboBox?.SelectedItem);
        }

        private void AutoUpdateWithSilenceEndTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("AutoUpdateWithSilenceEndTimeComboBox", AutoUpdateWithSilenceEndTimeComboBox?.SelectedItem);
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10)
            {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
            else
            {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }

        public void ApplyTheme()
        {
            try
            {
                ThemeHelper.ApplyThemeToControl(this);

                if (UpdateAvailableBorder != null)
                {
                    UpdateAvailableBorder.Background = ThemeHelper.GetBackgroundPrimaryBrush();
                    UpdateAvailableBorder.BorderBrush = ThemeHelper.GetBorderPrimaryBrush();
                }

                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.Background = ThemeHelper.GetButtonBackgroundBrush();
                    CheckUpdateButton.Foreground = ThemeHelper.GetTextPrimaryBrush();
                    CheckUpdateButton.BorderBrush = ThemeHelper.GetBorderPrimaryBrush();
                }

                if (UpdateNowButton != null)
                {
                    UpdateNowButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    UpdateNowButton.Foreground = Brushes.White;
                }

                if (UpdateLaterButton != null)
                {
                    UpdateLaterButton.Background = ThemeHelper.GetButtonBackgroundBrush();
                    UpdateLaterButton.Foreground = ThemeHelper.GetTextPrimaryBrush();
                    UpdateLaterButton.BorderBrush = ThemeHelper.GetBorderPrimaryBrush();
                }

                if (SkipVersionButton != null)
                {
                    SkipVersionButton.Background = Brushes.Transparent;
                    SkipVersionButton.Foreground = ThemeHelper.GetTextSecondaryBrush();
                    SkipVersionButton.BorderThickness = new Thickness(0);
                }

                if (RollbackButton != null)
                {
                    RollbackButton.Background = ThemeHelper.GetButtonBackgroundBrush();
                    RollbackButton.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }

                if (FixVersionButton != null)
                {
                    FixVersionButton.Background = ThemeHelper.GetButtonBackgroundBrush();
                    FixVersionButton.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }

                SwitchTab(_currentTab);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCenterPanel 应用主题时出错: {ex.Message}");
            }
        }

        private void StartLoadingAnimation()
        {
            try
            {
                if (LoadingSpinner != null)
                {
                    LoadingSpinner.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动加载动画失败: {ex.Message}");
            }
        }

        private void StopLoadingAnimation()
        {
            try
            {
                if (LoadingSpinner != null)
                {
                    LoadingSpinner.IsActive = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止加载动画失败: {ex.Message}");
            }
        }
    }
}

