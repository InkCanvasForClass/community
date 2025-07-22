using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq; // Added for OrderByDescending
using System.ComponentModel;
using System.Threading;

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
        private VersionItem selectedItem = null;
        private UpdateChannel channel = UpdateChannel.Release;
        private CancellationTokenSource downloadCts = null;

        public HistoryRollbackWindow(UpdateChannel channel = UpdateChannel.Release)
        {
            InitializeComponent();
            this.channel = channel;
            LoadVersions();
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
                LogHelper.WriteLogToFile($"HistoryRollback | 未获取到历史版本信息", LogHelper.LogType.Warning);
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
            LogHelper.WriteLogToFile($"HistoryRollback | 用户点击回滚，目标版本: {selectedItem.Version}");
            RollbackButton.IsEnabled = false;
            VersionComboBox.IsEnabled = false;
            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "正在准备下载...";
            bool downloadSuccess = false;
            downloadCts = new CancellationTokenSource();
            try
            {
                downloadSuccess = await DownloadAndInstallVersion(selectedItem.Version, selectedItem.DownloadUrl, downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                DownloadProgressText.Text = "下载已取消。";
                LogHelper.WriteLogToFile($"HistoryRollback | 用户取消下载", LogHelper.LogType.Info);
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
                LogHelper.WriteLogToFile($"HistoryRollback | 版本 {selectedItem.Version} 下载并准备安装成功");
                this.DialogResult = true;
                this.Close();
            }
            else if (!downloadCts.IsCancellationRequested)
            {
                DownloadProgressText.Text = "下载失败，请检查网络后重试。";
                LogHelper.WriteLogToFile($"HistoryRollback | 版本 {selectedItem?.Version} 下载失败", LogHelper.LogType.Error);
                RollbackButton.IsEnabled = true;
                VersionComboBox.IsEnabled = true;
            }
        }

        private async Task<bool> DownloadAndInstallVersion(string version, string downloadUrl, CancellationToken token)
        {
            LogHelper.WriteLogToFile($"HistoryRollback | 开始下载版本: {version}, url: {downloadUrl}");
            string updatesFolderPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "AutoUpdate");
            if (!Directory.Exists(updatesFolderPath))
                Directory.CreateDirectory(updatesFolderPath);
            string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
            bool downloadSuccess = false;
            try
            {
                downloadSuccess = await AutoUpdateHelper.DownloadFile(
                    downloadUrl,
                    zipFilePath,
                    (percent, text) =>
                    {
                        Dispatcher.Invoke(() => {
                            DownloadProgressBar.Value = percent;
                            DownloadProgressText.Text = text;
                        });
                    }
                );
                if (!downloadSuccess)
                {
                    LogHelper.WriteLogToFile($"HistoryRollback | 多线程下载失败");
                    return false;
                }
                // 下载完成后，调用现有安装流程
                LogHelper.WriteLogToFile($"HistoryRollback | 开始安装版本: {version}");
                AutoUpdateHelper.InstallNewVersionApp(version, false);
                App.IsAppExitByUser = true;
                Application.Current.Dispatcher.Invoke(() => {
                    Application.Current.Shutdown();
                });
                return true;
            }
            catch (OperationCanceledException)
            {
                LogHelper.WriteLogToFile($"HistoryRollback | 用户取消下载", LogHelper.LogType.Info);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HistoryRollback | 下载或安装异常: {ex.Message}", LogHelper.LogType.Error);
                Dispatcher.Invoke(() => {
                    DownloadProgressText.Text = $"下载异常: {ex.Message}";
                });
                return false;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            downloadCts?.Cancel();
            base.OnClosing(e);
        }
    }
} 