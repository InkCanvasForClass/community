using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class UpdatePage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;
        private bool _isChangingUpdateChannelInternally = false;
        private bool _isChangingUpdatePackageArchInternally = false;

        public UpdatePage()
        {
            InitializeComponent();
            Loaded += UpdatePage_Loaded;
        }

        private void UpdatePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Startup != null)
                {
                    CardAutoUpdate.IsOn = settings.Startup.IsAutoUpdate;
                    CardSilentUpdate.IsOn = settings.Startup.IsAutoUpdateWithSilence;

                    AutoUpdateWithSilenceTimeComboBox.InitializeAutoUpdateWithSilenceTimeComboBoxOptions(
                        AutoUpdateWithSilenceStartTimeComboBox, AutoUpdateWithSilenceEndTimeComboBox);
                    AutoUpdateWithSilenceStartTimeComboBox.SelectedItem = settings.Startup.AutoUpdateWithSilenceStartTime;
                    AutoUpdateWithSilenceEndTimeComboBox.SelectedItem = settings.Startup.AutoUpdateWithSilenceEndTime;

                    foreach (var item in UpdateChannelSelector.Items)
                    {
                        if (item is ComboBoxItem cbi && cbi.Tag != null &&
                            string.Equals(cbi.Tag.ToString(), settings.Startup.UpdateChannel.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateChannelSelector.SelectedItem = cbi;
                            break;
                        }
                    }

                    _isChangingUpdatePackageArchInternally = true;
                    try
                    {
                        string wantTag = settings.Startup.UpdatePackageArchitecture == UpdatePackageArchitecture.X64 ? "X64" : "X86";
                        foreach (var item in UpdatePackageArchitectureSelector.Items)
                        {
                            if (item is ComboBoxItem cbi && cbi.Tag != null &&
                                string.Equals(cbi.Tag.ToString(), wantTag, StringComparison.OrdinalIgnoreCase))
                            {
                                UpdatePackageArchitectureSelector.SelectedItem = cbi;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        _isChangingUpdatePackageArchInternally = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载更新设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        #region 自动更新事件处理

        private void ToggleSwitchIsAutoUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardAutoUpdate.IsOn;
                SettingsManager.Settings.Startup.IsAutoUpdate = newState;

                if (!newState)
                {
                    SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = false;
                    CardSilentUpdate.IsOn = false;
                }

                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置自动更新时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchIsAutoUpdateWithSilence_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardSilentUpdate.IsOn;
                SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = newState;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置静默更新时出错: {ex.Message}");
            }
        }

        private void AutoUpdateWithSilenceStartTimeComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Startup.AutoUpdateWithSilenceStartTime =
                    (string)AutoUpdateWithSilenceStartTimeComboBox.SelectedItem;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置静默更新开始时间时出错: {ex.Message}");
            }
        }

        private void AutoUpdateWithSilenceEndTimeComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Startup.AutoUpdateWithSilenceEndTime =
                    (string)AutoUpdateWithSilenceEndTimeComboBox.SelectedItem;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置静默更新结束时间时出错: {ex.Message}");
            }
        }

        #endregion

        #region 更新通道和架构事件处理

        private void UpdatePackageArchitectureSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingUpdatePackageArchInternally) return;
            if (!(UpdatePackageArchitectureSelector.SelectedItem is ComboBoxItem cbi) || cbi.Tag == null) return;

            var newArch = string.Equals(cbi.Tag.ToString(), "X64", StringComparison.OrdinalIgnoreCase)
                ? UpdatePackageArchitecture.X64
                : UpdatePackageArchitecture.X86;

            if (SettingsManager.Settings.Startup.UpdatePackageArchitecture == newArch)
                return;

            SettingsManager.Settings.Startup.UpdatePackageArchitecture = newArch;
            SettingsManager.SaveSettingsToFile();
            LogHelper.WriteLogToFile($"Settings | Update package architecture: {newArch}");
        }

        private async void UpdateChannelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingUpdateChannelInternally) return;
            if (!(UpdateChannelSelector.SelectedItem is ComboBoxItem cbi) || cbi.Tag == null) return;

            var oldChannel = SettingsManager.Settings.Startup.UpdateChannel;
            string channel = cbi.Tag.ToString();
            UpdateChannel newChannel = channel == "Beta" ? UpdateChannel.Beta
                : channel == "Preview" ? UpdateChannel.Preview
                : UpdateChannel.Release;

            if (SettingsManager.Settings.Startup.UpdateChannel == newChannel)
                return;

            bool isTestChannel = newChannel == UpdateChannel.Preview || newChannel == UpdateChannel.Beta;

            if (isTestChannel && !SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy)
            {
                MessageBox.Show(
                    "加入预览 / 测试通道前，请先在关于页面勾选\u201C我已阅读并同意 privacy 中的隐私说明\u201D。",
                    "需要同意隐私说明",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SettingsManager.Settings.Startup.UpdateChannel = oldChannel;
                RevertChannelSelection(oldChannel);
                SettingsManager.SaveSettingsToFile();
                LogHelper.WriteLogToFile("Settings | User not accepted privacy, reverted update channel");
                return;
            }

            if (isTestChannel && SettingsManager.Settings.Startup.TelemetryUploadLevel == TelemetryUploadLevel.None)
            {
                var result = MessageBox.Show(
                    "加入预览 / 测试通道需要开启匿名基础数据上传。\n\n是否立即开启匿名基础数据上传？",
                    "需要开启匿名使用数据上传",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    SettingsManager.Settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.Basic;
                    SettingsManager.SaveSettingsToFile();
                    LogHelper.WriteLogToFile("Settings | Telemetry enabled (Basic) for preview/beta update channel");
                }
                else
                {
                    SettingsManager.Settings.Startup.UpdateChannel = oldChannel;
                    RevertChannelSelection(oldChannel);
                    SettingsManager.SaveSettingsToFile();
                    LogHelper.WriteLogToFile("Settings | User declined telemetry, reverted update channel");
                    return;
                }
            }

            SettingsManager.Settings.Startup.UpdateChannel = newChannel;
            DeviceIdentifier.UpdateUsageChannel(newChannel);
            LogHelper.WriteLogToFile($"Settings | Update channel changed to {SettingsManager.Settings.Startup.UpdateChannel}");
            SettingsManager.SaveSettingsToFile();

            if (SettingsManager.Settings.Startup.IsAutoUpdate)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Channel changed to {newChannel}, performing immediate update check");

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ResetUpdateCheckRetry();
                    await System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            Dispatcher.Invoke(() =>
                            {
                                mainWindow.AutoUpdate();
                            });
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | Error during channel switch update check: {ex.Message}", LogHelper.LogType.Error);
                        }
                    });
                }
            }
            else
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Channel changed to {newChannel}, but auto-update is disabled");
            }
        }

        private void RevertChannelSelection(UpdateChannel targetChannel)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isChangingUpdateChannelInternally = true;
                try
                {
                    string targetTag = targetChannel.ToString();
                    foreach (var item in UpdateChannelSelector.Items)
                    {
                        if (item is ComboBoxItem cbi && cbi.Tag != null &&
                            string.Equals(cbi.Tag.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateChannelSelector.SelectedItem = cbi;
                            break;
                        }
                    }
                }
                finally
                {
                    _isChangingUpdateChannelInternally = false;
                }
            }), DispatcherPriority.Normal);
        }

        #endregion

        #region 手动操作事件处理

        private async void ManualUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ManualUpdateButton.IsEnabled = false;
            ManualUpdateButton.Content = "正在检查更新...";

            try
            {
                LogHelper.WriteLogToFile("ManualUpdate | Manual update button clicked");

                var (remoteVersion, lineGroup, apiReleaseNotes) = await AutoUpdateHelper.CheckForUpdates(SettingsManager.Settings.Startup.UpdateChannel, true, false);

                if (remoteVersion != null)
                {
                    LogHelper.WriteLogToFile($"ManualUpdate | Found new version: {remoteVersion}");

                    string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

                    HasNewUpdateWindow updateWindow = new HasNewUpdateWindow(currentVersion, remoteVersion, "", apiReleaseNotes);
                    updateWindow.Owner = Application.Current.MainWindow;
                    bool? dialogResult = updateWindow.ShowDialog();

                    if (dialogResult != true)
                    {
                        LogHelper.WriteLogToFile("ManualUpdate | Update dialog closed without selection");
                        return;
                    }

                    var mainWindow = Application.Current.MainWindow as MainWindow;

                    switch (updateWindow.Result)
                    {
                        case HasNewUpdateWindow.UpdateResult.UpdateNow:
                            LogHelper.WriteLogToFile("ManualUpdate | User chose to update now");
                            MessageBox.Show("开始下载更新，请稍候...", "正在更新", MessageBoxButton.OK, MessageBoxImage.Information);

                            bool isDownloadSuccessful = mainWindow != null
                                && await mainWindow.DownloadUpdateWithFallback(remoteVersion, lineGroup, SettingsManager.Settings.Startup.UpdateChannel);

                            if (isDownloadSuccessful)
                            {
                                MessageBoxResult result = MessageBox.Show("更新已下载完成，点击确定后将关闭软件并安装新版本！", "安装更新", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                                if (result == MessageBoxResult.OK)
                                {
                                    App.IsAppExitByUser = true;
                                    AutoUpdateHelper.InstallNewVersionApp(remoteVersion, true);
                                    Application.Current.Shutdown();
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile("ManualUpdate | User cancelled update installation");
                                }
                            }
                            else
                            {
                                MessageBox.Show("更新下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;

                        case HasNewUpdateWindow.UpdateResult.UpdateLater:
                            LogHelper.WriteLogToFile("ManualUpdate | User chose to update later");

                            isDownloadSuccessful = mainWindow != null
                                && await mainWindow.DownloadUpdateWithFallback(remoteVersion, lineGroup, SettingsManager.Settings.Startup.UpdateChannel);

                            if (isDownloadSuccessful)
                            {
                                LogHelper.WriteLogToFile("ManualUpdate | Update downloaded successfully, will install when application closes");
                                SettingsManager.Settings.Startup.IsAutoUpdate = true;
                                SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = true;

                                if (mainWindow != null)
                                    mainWindow.StartSilentUpdateTimer();

                                MessageBox.Show("更新已下载完成，将在软件关闭时自动安装。", "更新已准备就绪", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("ManualUpdate | Update download failed", LogHelper.LogType.Error);
                                MessageBox.Show("更新下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;

                        case HasNewUpdateWindow.UpdateResult.SkipVersion:
                            LogHelper.WriteLogToFile($"ManualUpdate | User chose to skip version {remoteVersion}");
                            SettingsManager.Settings.Startup.SkippedVersion = remoteVersion;
                            SettingsManager.SaveSettingsToFile();
                            MessageBox.Show($"已设置跳过版本 {remoteVersion}，在下次发布新版本之前不会再提示更新。",
                                           "已跳过此版本",
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Information);
                            break;
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("ManualUpdate | No updates available");
                    MessageBox.Show("当前已是最新版本！", "无可用更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Error in ManualUpdateButton_Click: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(
                    $"手动更新过程中发生错误: {ex.Message}",
                    "更新错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ManualUpdateButton.IsEnabled = true;
                ManualUpdateButton.Content = "手动更新";
            }
        }

        private async void FixVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "此操作将下载当前选择通道的最新版本并安装，软件将自动关闭并更新。\n\n确定要执行版本修复吗？",
                "版本修复确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                FixVersionButton.IsEnabled = false;
                FixVersionButton.Content = "正在修复...";

                try
                {
                    bool result = await AutoUpdateHelper.FixVersion(SettingsManager.Settings.Startup.UpdateChannel);

                    if (!result)
                    {
                        MessageBox.Show(
                            "版本修复失败，可能是网络问题或当前已是最新版本。",
                            "修复失败",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        FixVersionButton.IsEnabled = true;
                        FixVersionButton.Content = "版本修复";
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Error in FixVersionButton_Click: {ex.Message}", LogHelper.LogType.Error);
                    MessageBox.Show(
                        $"版本修复过程中发生错误: {ex.Message}",
                        "修复错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    FixVersionButton.IsEnabled = true;
                    FixVersionButton.Content = "版本修复";
                }
            }
        }

        private void HistoryRollbackButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new HistoryRollbackWindow(SettingsManager.Settings.Startup.UpdateChannel);
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }

        #endregion
    }
}
