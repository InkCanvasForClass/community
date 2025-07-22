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
            try
            {
                downloadSuccess = await AutoUpdateHelper.StartManualDownloadAndInstall(
                    selectedItem.Version,
                    channel,
                    (percent, text) =>
                    {
                        Dispatcher.Invoke(() => {
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
                this.DialogResult = true;
                this.Close();
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
            base.OnClosing(e);
        }
    }
} 