using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.IO.Compression;

namespace Ink_Canvas.Helpers
{
    internal class AutoUpdateHelper
    {
        public static async Task<string> CheckForUpdates(string proxy = null)
        {
            try
            {
                string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string remoteAddress = proxy;
                remoteAddress += "https://github.com/awesome-iwb/icc-ce/blob/main/AutomaticUpdateVersionControl.txt";
                string remoteVersion = await GetRemoteVersion(remoteAddress);

                if (remoteVersion != null)
                {
                    Version local = new Version(localVersion);
                    Version remote = new Version(remoteVersion);
                    if (remote > local)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | New version Availble: " + remoteVersion);
                        return remoteVersion;
                    }
                    else return null;
                }
                else
                {
                    LogHelper.WriteLogToFile("Failed to retrieve remote version.", LogHelper.LogType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AutoUpdate | Error: {ex.Message}");
                return null;
            }
        }

        public static async Task<string> GetRemoteVersion(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(fileUrl);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP request error: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Error: {ex.Message}", LogHelper.LogType.Error);
                }

                return null;
            }
        }

        private static string updatesFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ink Canvas Annotation", "AutoUpdate");
        private static string statusFilePath = null;

        public static async Task<bool> DownloadSetupFileAndSaveStatus(string version, string proxy = "")
        {
            try
            {
                statusFilePath = Path.Combine(updatesFolderPath, $"DownloadV{version}Status.txt");

                if (File.Exists(statusFilePath) && File.ReadAllText(statusFilePath).Trim().ToLower() == "true")
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Setup file already downloaded.");
                    return true;
                }

                string downloadUrl = $"{proxy}https://github.com/awesome-iwb/icc-ce/releases/download/{version}/InkCanvasForClass.CE.{version}.zip";

                SaveDownloadStatus(false);
                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
                await DownloadFile(downloadUrl, zipFilePath);
                SaveDownloadStatus(true);

                LogHelper.WriteLogToFile("AutoUpdate | Setup file successfully downloaded.");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error downloading update: {ex.Message}", LogHelper.LogType.Error);

                SaveDownloadStatus(false);
                return false;
            }
        }

        private static async Task DownloadFile(string fileUrl, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(fileUrl);
                    response.EnsureSuccessStatusCode();

                    using (FileStream fileStream = File.Create(destinationPath))
                    {
                        await response.Content.CopyToAsync(fileStream);
                        fileStream.Close();
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"AutoUpdate | HTTP request error: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AutoUpdate | Error: {ex.Message}");
                    throw;
                }
            }
        }

        private static void SaveDownloadStatus(bool isSuccess)
        {
            try
            {
                if (statusFilePath == null) return;

                string directory = Path.GetDirectoryName(statusFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(statusFilePath, isSuccess.ToString());
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error saving download status: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void InstallNewVersionApp(string version, bool isInSilence)
        {
            try
            {
                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");

                if (!File.Exists(zipFilePath))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP file not found: {zipFilePath}", LogHelper.LogType.Error);
                    return;
                }

                // 创建临时解压目录
                string extractPath = Path.Combine(updatesFolderPath, $"Extract_{version}");
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                // 解压ZIP文件
                LogHelper.WriteLogToFile($"AutoUpdate | Extracting ZIP file to: {extractPath}");
                ZipFile.ExtractToDirectory(zipFilePath, extractPath);

                // 获取当前应用程序路径
                string currentAppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                LogHelper.WriteLogToFile($"AutoUpdate | Current application directory: {currentAppDir}");

                // 复制解压的文件到应用程序目录
                LogHelper.WriteLogToFile($"AutoUpdate | Copying files to application directory");
                CopyDirectory(extractPath, currentAppDir);

                // 清理临时文件
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                // 重启应用程序
                LogHelper.WriteLogToFile($"AutoUpdate | Update completed, restarting application");
                RestartApplication();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error installing update: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            // 创建目标目录（如果不存在）
            Directory.CreateDirectory(destinationDir);

            // 复制所有文件
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(destinationDir, fileName);
                try
                {
                    // 如果目标文件存在，先删除
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Copy(filePath, destPath);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Error copying file {fileName}: {ex.Message}", LogHelper.LogType.Error);
                }
            }

            // 递归复制所有子目录
            foreach (string subDirPath in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDirPath);
                string destSubDir = Path.Combine(destinationDir, subDirName);
                CopyDirectory(subDirPath, destSubDir);
            }
        }

        private static void RestartApplication()
        {
            string appPath = Assembly.GetExecutingAssembly().Location;
            Process.Start(appPath);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }

        private static void ExecuteCommandLine(string command)
        {
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    Application.Current.Shutdown();
                    /*process.WaitForExit();
                    int exitCode = process.ExitCode;*/
                }
            }
            catch { }
        }

        public static void DeleteUpdatesFolder()
        {
            try
            {
                if (Directory.Exists(updatesFolderPath))
                {
                    Directory.Delete(updatesFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate clearing| Error deleting updates folder: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }

    internal class AutoUpdateWithSilenceTimeComboBox
    {
        public static ObservableCollection<string> Hours { get; set; } = new ObservableCollection<string>();
        public static ObservableCollection<string> Minutes { get; set; } = new ObservableCollection<string>();

        public static void InitializeAutoUpdateWithSilenceTimeComboBoxOptions(ComboBox startTimeComboBox, ComboBox endTimeComboBox)
        {
            for (int hour = 0; hour <= 23; ++hour)
            {
                Hours.Add(hour.ToString("00"));
            }
            for (int minute = 0; minute <= 59; minute += 20)
            {
                Minutes.Add(minute.ToString("00"));
            }
            startTimeComboBox.ItemsSource = Hours.SelectMany(h => Minutes.Select(m => $"{h}:{m}"));
            endTimeComboBox.ItemsSource = Hours.SelectMany(h => Minutes.Select(m => $"{h}:{m}"));
        }

        public static bool CheckIsInSilencePeriod(string startTime, string endTime)
        {
            if (startTime == endTime) return true;
            DateTime currentTime = DateTime.Now;

            DateTime StartTime = DateTime.ParseExact(startTime, "HH:mm", null);
            DateTime EndTime = DateTime.ParseExact(endTime, "HH:mm", null);
            if (StartTime <= EndTime)
            { // 单日时间段
                return currentTime >= StartTime && currentTime <= EndTime;
            }
            else
            { // 跨越两天的时间段
                return currentTime >= StartTime || currentTime <= EndTime;
            }
        }
    }
}
