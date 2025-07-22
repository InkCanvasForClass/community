using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        public HistoryRollbackWindow()
        {
            InitializeComponent();
            LoadVersions();
        }

        private async void LoadVersions()
        {
            RollbackButton.IsEnabled = false;
            VersionComboBox.Items.Clear();
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "";
            ReleaseNotesViewer.Markdown = "正在获取历史版本...";
            var releases = await AutoUpdateHelper.GetAllGithubReleases();
            versionList.Clear();
            foreach (var (version, url, notes) in releases)
            {
                versionList.Add(new VersionItem { Version = version, DownloadUrl = url, ReleaseNotes = notes });
            }
            VersionComboBox.ItemsSource = versionList;
            if (versionList.Count > 0)
            {
                VersionComboBox.SelectedIndex = 0;
                RollbackButton.IsEnabled = true;
            }
            else
            {
                ReleaseNotesViewer.Markdown = "未获取到历史版本信息。";
            }
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedItem = VersionComboBox.SelectedItem as VersionItem;
            if (selectedItem != null)
            {
                ReleaseNotesViewer.Markdown = selectedItem.ReleaseNotes ?? "无更新日志";
            }
        }

        private async void RollbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem == null) return;
            RollbackButton.IsEnabled = false;
            VersionComboBox.IsEnabled = false;
            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "正在准备下载...";
            bool downloadSuccess = false;
            try
            {
                downloadSuccess = await DownloadAndInstallVersion(selectedItem.Version, selectedItem.DownloadUrl);
            }
            catch (Exception ex)
            {
                DownloadProgressText.Text = $"下载失败: {ex.Message}";
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

        private async Task<bool> DownloadAndInstallVersion(string version, string downloadUrl)
        {
            string updatesFolderPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "AutoUpdate");
            if (!Directory.Exists(updatesFolderPath))
                Directory.CreateDirectory(updatesFolderPath);
            string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
            string tmpFilePath = zipFilePath + ".tmp";
            long existingLength = 0;
            if (File.Exists(tmpFilePath))
                existingLength = new FileInfo(tmpFilePath).Length;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                    if (existingLength > 0)
                        client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                    using (var response = await client.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength.HasValue
                            ? response.Content.Headers.ContentLength.Value + existingLength
                            : -1L;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(tmpFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = existingLength;
                            int read;
                            var lastUpdate = DateTime.Now;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, read);
                                totalRead += read;
                                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 200)
                                {
                                    int percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                                    DownloadProgressBar.Value = percent;
                                    DownloadProgressText.Text = totalBytes > 0
                                        ? $"已下载 {totalRead / 1024 / 1024.0:F2} MB / {totalBytes / 1024 / 1024.0:F2} MB ({percent}%)"
                                        : $"已下载 {totalRead / 1024 / 1024.0:F2} MB";
                                    lastUpdate = DateTime.Now;
                                }
                            }
                            DownloadProgressBar.Value = 100;
                            DownloadProgressText.Text = "下载完成，正在校验...";
                            await fs.FlushAsync();
                        }
                        if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
                        File.Move(tmpFilePath, zipFilePath);
                    }
                }
                // 下载完成后，调用现有安装流程
                AutoUpdateHelper.InstallNewVersionApp(version, false);
                App.IsAppExitByUser = true;
                Application.Current.Dispatcher.Invoke(() => {
                    Application.Current.Shutdown();
                });
                return true;
            }
            catch (Exception ex)
            {
                if (File.Exists(tmpFilePath)) { /* 不删除，便于断点续传 */ }
                return false;
            }
        }
    }
} 