using Ink_Canvas.Properties;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class UpdatePage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private enum UpdateUiState
        {
            Idle,
            Checking,
            UpdateAvailable,
            Downloading,
            Downloaded,
            NetworkError
        }

        private class VersionItem
        {
            public string Version { get; set; }
            public string DownloadUrl { get; set; }
            public string ReleaseNotes { get; set; }
        }

        private bool _isLoaded;
        private bool _isChangingUpdateChannelInternally;
        private bool _isChangingUpdatePackageArchInternally;

        private UpdateUiState _state = UpdateUiState.Idle;
        private string _remoteVersion;
        private AutoUpdateHelper.UpdateLineGroup _remoteLineGroup;
        private string _remoteReleaseNotes;

        private List<VersionItem> _versionList = new List<VersionItem>();
        private VersionItem _selectedHistoricalItem;
        private bool _isHistoryLoaded;

        public UpdatePage()
        {
            InitializeComponent();
            Loaded += UpdatePage_Loaded;
        }

        private async void UpdatePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;

            // 复用启动时自动检查的结果，避免二次检查
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && !string.IsNullOrEmpty(mainWindow.AvailableLatestVersion))
            {
                _remoteVersion = mainWindow.AvailableLatestVersion;
                _remoteLineGroup = mainWindow.AvailableLatestLineGroup;
                _remoteReleaseNotes = mainWindow.AvailableLatestReleaseNotes;

                try
                {
                    var statusFile = AutoUpdateHelper.GetUpdateDownloadStatusFilePath(_remoteVersion);
                    if (System.IO.File.Exists(statusFile) &&
                        System.IO.File.ReadAllText(statusFile).Trim().ToLower() == "true")
                    {
                        ApplyState(UpdateUiState.Downloaded);
                    }
                    else
                    {
                        ApplyState(UpdateUiState.UpdateAvailable);
                    }
                }
                catch
                {
                    ApplyState(UpdateUiState.UpdateAvailable);
                }

                if (!string.IsNullOrEmpty(_remoteReleaseNotes))
                    ChangelogViewer.Markdown = _remoteReleaseNotes;
                else
                    ChangelogViewer.Markdown = UpdateStrings.Changelog_SwitchHint;
                return;
            }

            // 没有缓存的检查结果时，仅展示空白；不主动联网，避免打开页面就触发请求
            ApplyState(UpdateUiState.Idle);
            ChangelogViewer.Markdown = UpdateStrings.Changelog_ClickHint;
            await System.Threading.Tasks.Task.CompletedTask;
        }

        #region 更新设置加载

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
                    CardSmartUpdate.IsOn = settings.Startup.IsSmartUpdate;

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

        #endregion

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
                SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = CardSilentUpdate.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置静默更新时出错: {ex.Message}");
            }
        }

        private async void ToggleSwitchIsSmartUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Startup.IsSmartUpdate = CardSmartUpdate.IsOn;
                SettingsManager.SaveSettingsToFile();
                LogHelper.WriteLogToFile($"Settings | Smart update: {CardSmartUpdate.IsOn}");

                if (SettingsManager.Settings.Startup.IsAutoUpdate)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.ResetUpdateCheckRetry();
                        await Dispatcher.InvokeAsync(() => mainWindow.AutoUpdate());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置智慧更新时出错: {ex.Message}");
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
                    UpdateStrings.Channel_PrivacyRequired,
                    UpdateStrings.Channel_PrivacyRequiredTitle,
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
                    UpdateStrings.Channel_TelemetryRequired,
                    UpdateStrings.Channel_TelemetryRequiredTitle,
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

            // 通道切换后强制刷新更新日志和历史版本缓存
            _isHistoryLoaded = false;
            _versionList.Clear();
            VersionComboBox.ItemsSource = null;
            ReleaseNotesViewer.Markdown = "";
            await LoadChangelogAsync();

            if (SettingsManager.Settings.Startup.IsAutoUpdate)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Channel changed to {newChannel}, performing immediate update check");

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ResetUpdateCheckRetry();
                    await System.Threading.Tasks.Task.Run(() =>
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

        #region 更新状态机

        private void ApplyState(UpdateUiState state, string customSubtitle = null)
        {
            _state = state;

            CheckUpdateButton.Visibility = Visibility.Collapsed;
            UpdateNowButton.Visibility = Visibility.Collapsed;
            UpdateLaterButton.Visibility = Visibility.Collapsed;
            SkipVersionButton.Visibility = Visibility.Collapsed;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;

            CheckUpdateButton.IsEnabled = true;

            switch (state)
            {
                case UpdateUiState.Idle:
                    StatusIcon.Icon = SegoeFluentIcons.Completed;
                    StatusTitle.Text = UpdateStrings.Status_UpToDate;
                    StatusSubtitle.Text = customSubtitle ?? BuildLastCheckSubtitle();
                    CheckUpdateButton.Visibility = Visibility.Visible;
                    break;

                case UpdateUiState.Checking:
                    StatusIcon.Icon = SegoeFluentIcons.Sync;
                    StatusTitle.Text = UpdateStrings.Status_Checking;
                    StatusSubtitle.Text = "";
                    CheckUpdateButton.Visibility = Visibility.Visible;
                    CheckUpdateButton.IsEnabled = false;
                    ProgressPanel.Visibility = Visibility.Visible;
                    ProgressText.Text = UpdateStrings.Progress_ConnectingServer;
                    ProgressBar.IsIndeterminate = true;
                    break;

                case UpdateUiState.UpdateAvailable:
                    StatusIcon.Icon = SegoeFluentIcons.Upload;
                    StatusTitle.Text = string.Format(UpdateStrings.Status_NewVersionAvailable, _remoteVersion);
                    StatusSubtitle.Text = customSubtitle ?? string.Format(UpdateStrings.Status_VersionTransition, GetCurrentVersion(), _remoteVersion);
                    UpdateNowButton.Visibility = Visibility.Visible;
                    UpdateLaterButton.Visibility = Visibility.Visible;
                    SkipVersionButton.Visibility = Visibility.Visible;
                    break;

                case UpdateUiState.Downloading:
                    StatusIcon.Icon = SegoeFluentIcons.Download;
                    StatusTitle.Text = UpdateStrings.Status_Downloading;
                    StatusSubtitle.Text = customSubtitle ?? string.Format(UpdateStrings.Status_TargetVersion, _remoteVersion);
                    ProgressPanel.Visibility = Visibility.Visible;
                    ProgressBar.IsIndeterminate = false;
                    break;

                case UpdateUiState.Downloaded:
                    StatusIcon.Icon = SegoeFluentIcons.Download;
                    StatusTitle.Text = UpdateStrings.Status_Downloaded;
                    StatusSubtitle.Text = customSubtitle ?? string.Format(UpdateStrings.Status_WillInstallOnExit, _remoteVersion);
                    CheckUpdateButton.Visibility = Visibility.Visible;
                    break;

                case UpdateUiState.NetworkError:
                    StatusIcon.Icon = SegoeFluentIcons.Error;
                    StatusTitle.Text = UpdateStrings.Status_NetworkError;
                    StatusSubtitle.Text = customSubtitle ?? UpdateStrings.Status_NetworkErrorHint;
                    CheckUpdateButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        private string BuildLastCheckSubtitle()
        {
            string current = GetCurrentVersion();
            return string.Format(UpdateStrings.Status_CurrentVersion, current);
        }

        private static string GetCurrentVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch
            {
                return UpdateStrings.Version_Unknown;
            }
        }

        #endregion

        #region 更新日志

        private async System.Threading.Tasks.Task LoadChangelogAsync()
        {
            try
            {
                ChangelogViewer.Markdown = UpdateStrings.Changelog_Loading;

                // 优先尝试从 GitHub API 获取最新 Release 的 body（带超时，失败则回退到镜像）
                try
                {
                    var apiTask = AutoUpdateHelper.GetAllGithubReleases(SettingsManager.Settings.Startup.UpdateChannel);
                    var completed = await System.Threading.Tasks.Task.WhenAny(apiTask, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(8)));
                    if (completed == apiTask)
                    {
                        var releases = await apiTask;
                        var latest = releases?
                            .OrderByDescending(r => ParseVersionForSort(r.version))
                            .Select(r => (Tuple<string, string, string>)Tuple.Create(r.version, r.downloadUrl, r.releaseNotes))
                            .FirstOrDefault();
                        if (latest != null && !string.IsNullOrWhiteSpace(latest.Item3))
                        {
                            ChangelogViewer.Markdown = latest.Item3;
                            return;
                        }
                        LogHelper.WriteLogToFile("UpdatePage | GitHub API 未返回可用的更新日志，回退到镜像", LogHelper.LogType.Warning);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("UpdatePage | GitHub API 获取更新日志超时，回退到镜像", LogHelper.LogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"UpdatePage | GitHub API 获取更新日志失败，回退到镜像: {ex.Message}", LogHelper.LogType.Warning);
                }

                // 回退到镜像源 UpdateLog.md
                string md = await AutoUpdateHelper.GetUpdateLog(SettingsManager.Settings.Startup.UpdateChannel);
                ChangelogViewer.Markdown = string.IsNullOrEmpty(md) ? UpdateStrings.Changelog_NoData : md;
            }
            catch (Exception ex)
            {
                ChangelogViewer.Markdown = string.Format(UpdateStrings.Changelog_LoadFailed, ex.Message);
            }
        }

        #endregion

        #region 检查 / 下载 / 安装

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyState(UpdateUiState.Checking);
            try
            {
                LogHelper.WriteLogToFile("ManualUpdate | Manual update button clicked");

                string remoteVersion = null;
                string apiReleaseNotes = null;
                AutoUpdateHelper.UpdateLineGroup lineGroup = null;

                if (SettingsManager.Settings.Startup.IsSmartUpdate)
                {
                    var (rv, lg, notes) = await AutoUpdateHelper.CheckForUpdates(
                        SettingsManager.Settings.Startup.UpdateChannel, true, false);
                    remoteVersion = rv;
                    lineGroup = lg;
                    apiReleaseNotes = notes;
                }
                else
                {
                    // 优先通过 GitHub Releases API 获取最新版本
                    try
                    {
                        var releases = await AutoUpdateHelper.GetAllGithubReleases(SettingsManager.Settings.Startup.UpdateChannel);
                        var latest = releases?
                            .OrderByDescending(r => ParseVersionForSort(r.version))
                            .Select(r => Tuple.Create(r.version, r.downloadUrl, r.releaseNotes))
                            .FirstOrDefault();
                        if (latest != null && !string.IsNullOrEmpty(latest.Item1))
                        {
                            var localVersion = new Version(GetCurrentVersion());
                            var remote = ParseVersionForSort(latest.Item1);
                            if (remote > localVersion)
                            {
                                remoteVersion = latest.Item1.TrimStart('v', 'V');
                                apiReleaseNotes = latest.Item3;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"UpdatePage | GitHub API 检查更新失败，回退到 CheckForUpdates: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    // 回退：调用统一的 CheckForUpdates（包含镜像源 txt 方案）
                    if (string.IsNullOrEmpty(remoteVersion))
                    {
                        var (rv, lg, notes) = await AutoUpdateHelper.CheckForUpdates(
                            SettingsManager.Settings.Startup.UpdateChannel, true, false);
                        remoteVersion = rv;
                        lineGroup = lg;
                        if (string.IsNullOrEmpty(apiReleaseNotes)) apiReleaseNotes = notes;
                    }
                }

                if (!string.IsNullOrEmpty(remoteVersion))
                {
                    _remoteVersion = remoteVersion;
                    _remoteLineGroup = lineGroup;
                    _remoteReleaseNotes = apiReleaseNotes;

                    if (!string.IsNullOrEmpty(apiReleaseNotes))
                        ChangelogViewer.Markdown = apiReleaseNotes;

                    ApplyState(UpdateUiState.UpdateAvailable);
                }
                else
                {
                    ApplyState(UpdateUiState.Idle);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Error in CheckUpdateButton_Click: {ex.Message}", LogHelper.LogType.Error);
                ApplyState(UpdateUiState.NetworkError, ex.Message);
            }
        }

        private bool _downloadCancelled;
        private static List<AutoUpdateHelper.UpdateLineGroup> _cachedOrderedGroups;
        private static UpdateChannel _cachedGroupsChannel;

        private static async System.Threading.Tasks.Task<List<AutoUpdateHelper.UpdateLineGroup>> GetOrderedGroupsCachedAsync(UpdateChannel channel)
        {
            if (_cachedOrderedGroups != null && _cachedOrderedGroups.Count > 0 && _cachedGroupsChannel == channel)
                return _cachedOrderedGroups;
            var groups = await AutoUpdateHelper.GetAvailableLineGroupsOrdered(channel);
            _cachedOrderedGroups = groups;
            _cachedGroupsChannel = channel;
            return groups;
        }

        private async System.Threading.Tasks.Task<bool> DownloadWithProgressAsync()
        {
            _downloadCancelled = false;
            var groups = await GetOrderedGroupsCachedAsync(SettingsManager.Settings.Startup.UpdateChannel);
            if (groups == null || groups.Count == 0)
            {
                LogHelper.WriteLogToFile("UpdatePage | 没有可用的下载线路组", LogHelper.LogType.Error);
                return false;
            }
            return await AutoUpdateHelper.DownloadSetupFileWithFallback(_remoteVersion, groups, (percent, text) =>
            {
                if (_downloadCancelled) return;
                Dispatcher.Invoke(() =>
                {
                    if (_state != UpdateUiState.Downloading) return;
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = percent;
                    ProgressText.Text = text;
                });
            });
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_remoteVersion)) return;

            ApplyState(UpdateUiState.Downloading);
            CancelDownloadButton.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressText.Text = UpdateStrings.Progress_PreparingDownload;

            try
            {
                bool ok = await DownloadWithProgressAsync();

                if (!ok)
                {
                    ApplyState(UpdateUiState.NetworkError, UpdateStrings.Msg_UpdateDownloadFailed);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    UpdateStrings.InstallUpdate_Msg,
                    UpdateStrings.InstallUpdate_Title,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    App.IsAppExitByUser = true;
                    AutoUpdateHelper.InstallNewVersionApp(_remoteVersion, true);
                    Application.Current.Shutdown();
                }
                else
                {
                    ApplyState(UpdateUiState.Downloaded);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Error in UpdateNowButton_Click: {ex.Message}", LogHelper.LogType.Error);
                ApplyState(UpdateUiState.NetworkError, ex.Message);
            }
        }

        private async void UpdateLaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_remoteVersion)) return;

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            ApplyState(UpdateUiState.Downloading);
            CancelDownloadButton.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressText.Text = UpdateStrings.Progress_DownloadingBackground;

            try
            {
                bool ok = await DownloadWithProgressAsync();

                if (!ok)
                {
                    ApplyState(UpdateUiState.NetworkError, UpdateStrings.Msg_UpdateDownloadFailed);
                    return;
                }

                SettingsManager.Settings.Startup.IsAutoUpdate = true;
                SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = true;
                SettingsManager.SaveSettingsToFile();
                CardAutoUpdate.IsOn = true;
                CardSilentUpdate.IsOn = true;

                mainWindow.StartSilentUpdateTimer();
                ApplyState(UpdateUiState.Downloaded);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Error in UpdateLaterButton_Click: {ex.Message}", LogHelper.LogType.Error);
                ApplyState(UpdateUiState.NetworkError, ex.Message);
            }
        }

        private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_remoteVersion)) return;

            SettingsManager.Settings.Startup.SkippedVersion = _remoteVersion;
            SettingsManager.SaveSettingsToFile();
            LogHelper.WriteLogToFile($"ManualUpdate | User chose to skip version {_remoteVersion}");

            ApplyState(UpdateUiState.Idle, string.Format(UpdateStrings.Status_SkippedVersion, _remoteVersion));
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _downloadCancelled = true;
            AutoUpdateHelper.RequestCancelDownload();
            ApplyState(UpdateUiState.UpdateAvailable);
        }

        #endregion

        #region 历史版本回滚

        private async void UpdateTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != UpdateTabControl) return;
            if (UpdateTabControl.SelectedItem == HistoryTabItem && !_isHistoryLoaded)
            {
                await LoadHistoryAsync();
            }
        }

        private async void HistoryTabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            // 兼容用户首次切到历史版本 Tab 时再加载
            if (_isHistoryLoaded) return;
            await LoadHistoryAsync();
        }

        private async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            try
            {
                _isHistoryLoaded = true;
                ReleaseNotesViewer.Markdown = UpdateStrings.History_Loading;
                RollbackButton.IsEnabled = false;

                var releases = await AutoUpdateHelper.GetAllGithubReleases(SettingsManager.Settings.Startup.UpdateChannel);
                _versionList = releases
                    .Select(r => new VersionItem { Version = r.version, DownloadUrl = r.downloadUrl, ReleaseNotes = r.releaseNotes })
                    .OrderByDescending(v => ParseVersionForSort(v.Version))
                    .ToList();
                VersionComboBox.ItemsSource = _versionList;

                if (_versionList.Count > 0)
                {
                    VersionComboBox.SelectedIndex = 0;
                    RollbackButton.IsEnabled = true;
                }
                else
                {
                    ReleaseNotesViewer.Markdown = UpdateStrings.History_NoData;
                }
            }
            catch (Exception ex)
            {
                ReleaseNotesViewer.Markdown = string.Format(UpdateStrings.History_LoadFailed, ex.Message);
            }
        }

        private static Version ParseVersionForSort(string version)
        {
            var v = (version ?? "").TrimStart('v', 'V');
            return Version.TryParse(v, out var result) ? result : new Version(0, 0, 0, 0);
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedHistoricalItem = VersionComboBox.SelectedItem as VersionItem;
            if (_selectedHistoricalItem != null)
            {
                ReleaseNotesViewer.Markdown = _selectedHistoricalItem.ReleaseNotes ?? UpdateStrings.History_NoChangelog;
                LogHelper.WriteLogToFile($"HistoryRollback | 用户选择版本: {_selectedHistoricalItem.Version}");
            }
            Keyboard.ClearFocus();
        }

        private async void RollbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedHistoricalItem == null) return;

            int days = await AskPauseDaysAsync();
            if (days < 0)
            {
                LogHelper.WriteLogToFile("HistoryRollback | 用户取消了回滚操作");
                return;
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

            LogHelper.WriteLogToFile($"HistoryRollback | 用户确认回滚，目标版本: {_selectedHistoricalItem.Version}");
            RollbackButton.IsEnabled = false;
            VersionComboBox.IsEnabled = false;
            RollbackProgressPanel.Visibility = Visibility.Visible;
            RollbackProgressBar.Value = 0;
            RollbackProgressText.Text = UpdateStrings.Progress_PreparingDownload;

            bool downloadSuccess = false;
            try
            {
                downloadSuccess = await AutoUpdateHelper.StartManualDownloadAndInstall(
                    _selectedHistoricalItem.Version,
                    SettingsManager.Settings.Startup.UpdateChannel,
                    (percent, text) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            RollbackProgressBar.Value = percent;
                            RollbackProgressText.Text = text;
                        });
                    });
            }
            catch (Exception ex)
            {
                RollbackProgressText.Text = string.Format(UpdateStrings.Rollback_DownloadFailedMsg, ex.Message);
                LogHelper.WriteLogToFile($"HistoryRollback | 下载异常: {ex.Message}", LogHelper.LogType.Error);
            }

            if (downloadSuccess)
            {
                RollbackProgressBar.Value = 100;
                RollbackProgressText.Text = UpdateStrings.Rollback_DownloadComplete;
            }
            else
            {
                RollbackProgressText.Text = UpdateStrings.Rollback_DownloadFailedRetry;
                RollbackButton.IsEnabled = true;
                VersionComboBox.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task<int> AskPauseDaysAsync()
        {
            var dialog = new ContentDialog
            {
                Title = UpdateStrings.PauseAutoUpdate_Title,
                PrimaryButtonText = UpdateStrings.Btn_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel
            };

            var panel = new iNKORE.UI.WPF.Controls.SimpleStackPanel
            {
                Spacing = 16,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var textBlock = new TextBlock
            {
                Text = UpdateStrings.PauseAutoUpdate_SelectDuration,
                FontSize = 14
            };

            var daysComboBox = new ComboBox
            {
                Width = 200,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            for (int i = 0; i <= 7; i++)
            {
                daysComboBox.Items.Add(new ComboBoxItem { Content = string.Format(UpdateStrings.PauseAutoUpdate_Days, i), Tag = i });
            }
            daysComboBox.SelectedIndex = 0;

            panel.Children.Add(textBlock);
            panel.Children.Add(daysComboBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return -1;

            if (daysComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is int days)
                return days;

            return 1;
        }

        #endregion

        #region 维护

        private async void FixVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                UpdateStrings.FixVersion_ConfirmBody,
                UpdateStrings.FixVersion_ConfirmTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            FixVersionButton.IsEnabled = false;
            FixVersionButton.Content = UpdateStrings.FixVersion_InProgress;

            try
            {
                bool result = await AutoUpdateHelper.FixVersion(SettingsManager.Settings.Startup.UpdateChannel);
                if (!result)
                {
                    MessageBox.Show(
                        UpdateStrings.FixVersion_FailedBody,
                        UpdateStrings.FixVersion_FailedTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Error in FixVersionButton_Click: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(
                    string.Format(UpdateStrings.FixVersion_ErrorBody, ex.Message),
                    UpdateStrings.FixVersion_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                FixVersionButton.IsEnabled = true;
                FixVersionButton.Content = BtnStrings.VersionFix;
            }
        }

        #endregion
    }
}