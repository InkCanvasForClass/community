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
using System.Text;
using System.Collections.Generic;

namespace Ink_Canvas.Helpers
{
    internal class AutoUpdateHelper
    {
        // 定义超时时间为10秒
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        
        public static async Task<string> CheckForUpdates(string proxy = null, UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                LogHelper.WriteLogToFile($"AutoUpdate | Local version: {localVersion}");
                
                string remoteAddress = proxy;
                
                // 根据通道选择URL
                string primaryUrl, fallbackUrl;
                
                if (channel == UpdateChannel.Release)
                {
                    // Release通道版本信息地址
                    primaryUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/beta/AutomaticUpdateVersionControl.txt";
                    fallbackUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/main/AutomaticUpdateVersionControl.txt";
                }
                else
                {
                    // Beta通道版本信息地址
                    primaryUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt";
                    fallbackUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt";
                }
                
                LogHelper.WriteLogToFile($"AutoUpdate | Checking for updates on {channel} channel");
                
                // 先尝试主地址
                remoteAddress += primaryUrl;
                string remoteVersion = await GetRemoteVersion(remoteAddress);

                // 如果主地址失败，尝试备用地址
                if (remoteVersion == null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Primary URL failed, trying fallback URL");
                    remoteVersion = await GetRemoteVersion(proxy + fallbackUrl);
                }

                if (remoteVersion != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Remote version: {remoteVersion}");
                    Version local = new Version(localVersion);
                    Version remote = new Version(remoteVersion);
                    if (remote > local)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | New version available: {remoteVersion}");
                        return remoteVersion;
                    }
                    else 
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Current version is up to date");
                        return null;
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Failed to retrieve remote version from both URLs.", LogHelper.LogType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in CheckForUpdates: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static async Task<string> GetRemoteVersion(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 设置超时时间为10秒
                    client.Timeout = RequestTimeout;
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | Sending HTTP request to: {fileUrl}");
                    
                    // 使用带超时的Task.WhenAny来确保请求不会无限期等待
                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);
                    
                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Request timed out after {RequestTimeout.TotalSeconds} seconds", LogHelper.LogType.Error);
                        return null;
                    }
                    
                    // 请求完成，检查结果
                    HttpResponseMessage response = await downloadTask;
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    // Trim any whitespace, newlines, etc.
                    content = content.Trim();
                    
                    // If the content contains HTML (likely the GitHub view page instead of raw content),
                    // try to extract the version number
                    if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Received HTML content instead of raw version number - trying to extract version");
                        // Try to extract version from GitHub page - look for text content in the file
                        int startPos = content.IndexOf("<table");
                        if (startPos > 0)
                        {
                            int endPos = content.IndexOf("</table>", startPos);
                            if (endPos > startPos)
                            {
                                string tableContent = content.Substring(startPos, endPos - startPos);
                                // Look for the version number pattern (like 1.2.3 or 1.2.3.4)
                                var match = System.Text.RegularExpressions.Regex.Match(tableContent, @"(\d+\.\d+\.\d+(\.\d+)?)");
                                if (match.Success)
                                {
                                    content = match.Groups[1].Value;
                                    LogHelper.WriteLogToFile($"AutoUpdate | Extracted version from HTML: {content}");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"AutoUpdate | Could not extract version from HTML content");
                                    return null;
                                }
                            }
                        }
                    }
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | Response content: {content}");
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP request error: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Request timed out: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Error: {ex.Message}", LogHelper.LogType.Error);
                }

                return null;
            }
        }

        private static string updatesFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AutoUpdate");
        private static string statusFilePath = null;

        public static async Task<bool> DownloadSetupFileAndSaveStatus(string version, string proxy = "", UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                statusFilePath = Path.Combine(updatesFolderPath, $"DownloadV{version}Status.txt");

                if (File.Exists(statusFilePath) && File.ReadAllText(statusFilePath).Trim().ToLower() == "true")
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Setup file already downloaded.");
                    return true;
                }

                // Ensure update directory exists
                if (!Directory.Exists(updatesFolderPath))
                {
                    Directory.CreateDirectory(updatesFolderPath);
                    LogHelper.WriteLogToFile($"AutoUpdate | Created updates directory: {updatesFolderPath}");
                }

                // 根据通道选择下载地址
                string primaryUrl, fallbackUrl;
                
                if (channel == UpdateChannel.Release)
                {
                    // Release通道下载地址
                    primaryUrl = $"{proxy}https://github.com/InkCanvasForClass/community/releases/download/{version}/InkCanvasForClass.CE.{version}.zip";
                    fallbackUrl = $"{proxy}https://bgithub.xyz/InkCanvasForClass/community/releases/download/{version}/InkCanvasForClass.CE.{version}.zip";
                }
                else
                {
                    // Beta通道下载地址
                    primaryUrl = $"{proxy}https://github.com/InkCanvasForClass/community-beta/releases/download/{version}/InkCanvasForClass.CE.{version}.zip";
                    fallbackUrl = $"{proxy}https://bgithub.xyz/InkCanvasForClass/community-beta/releases/download/{version}/InkCanvasForClass.CE.{version}.zip";
                }
                
                LogHelper.WriteLogToFile($"AutoUpdate | Primary download URL: {primaryUrl}");

                SaveDownloadStatus(false);
                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
                LogHelper.WriteLogToFile($"AutoUpdate | Target file path: {zipFilePath}");
                
                // 先尝试主地址下载
                bool downloadSuccess = await DownloadFile(primaryUrl, zipFilePath);
                
                // 如果主地址下载失败，尝试备用地址
                if (!downloadSuccess)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Primary download failed, trying fallback URL: {fallbackUrl}");
                    downloadSuccess = await DownloadFile(fallbackUrl, zipFilePath);
                }
                
                if (downloadSuccess)
                {
                    SaveDownloadStatus(true);
                    LogHelper.WriteLogToFile("AutoUpdate | Setup file successfully downloaded.");
                    return true;
                }
                else
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Failed to download the update file from both URLs.", LogHelper.LogType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error downloading update: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Inner exception: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }

                SaveDownloadStatus(false);
                return false;
            }
        }

        private static async Task<bool> DownloadFile(string fileUrl, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Configure client
                    client.Timeout = TimeSpan.FromMinutes(5); // 下载文件需要更长的超时时间
                    client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | Downloading from: {fileUrl}");
                    
                    // 创建临时文件路径
                    string tempFilePath = destinationPath + ".tmp";
                    
                    // 确保目标目录存在
                    string directory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // 使用带超时的Task.WhenAny来确保请求不会无限期等待
                    var downloadTask = client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    var initialTimeoutTask = Task.Delay(RequestTimeout); // 使用全局定义的10秒超时
                    
                    var completedTask = await Task.WhenAny(downloadTask, initialTimeoutTask);
                    if (completedTask == initialTimeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Initial connection timed out after 30 seconds", LogHelper.LogType.Error);
                        return false;
                    }
                    
                    // 请求完成，检查结果
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    
                    // 获取文件总大小
                    long? totalBytes = response.Content.Headers.ContentLength;
                    LogHelper.WriteLogToFile($"AutoUpdate | File size: {(totalBytes.HasValue ? (totalBytes.Value / 1024.0 / 1024.0).ToString("F2") + " MB" : "Unknown")}");
                    
                    // 创建临时文件流
                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        // 获取下载流
                        using (var downloadStream = await response.Content.ReadAsStreamAsync())
                        {
                            byte[] buffer = new byte[8192]; // 8KB buffer
                            long totalBytesRead = 0;
                            int bytesRead;
                            DateTime lastProgressUpdate = DateTime.Now;
                            
                            // 设置下载超时 - 如果60秒内没有数据传输，则认为下载超时
                            var downloadTimeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                            var readTask = Task.Run(async () => {
                                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalBytesRead += bytesRead;
                                    
                                    // 每5秒更新一次进度
                                    if ((DateTime.Now - lastProgressUpdate).TotalSeconds >= 5)
                                    {
                                        if (totalBytes.HasValue)
                                        {
                                            double percentage = (double)totalBytesRead / totalBytes.Value * 100;
                                            LogHelper.WriteLogToFile($"AutoUpdate | Download progress: {percentage:F1}% ({(totalBytesRead / 1024.0 / 1024.0):F2} MB / {(totalBytes.Value / 1024.0 / 1024.0):F2} MB)");
                                        }
                                        else
                                        {
                                            LogHelper.WriteLogToFile($"AutoUpdate | Downloaded: {(totalBytesRead / 1024.0 / 1024.0):F2} MB");
                                        }
                                        lastProgressUpdate = DateTime.Now;
                                        
                                        // 重置下载超时
                                        downloadTimeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                                    }
                                }
                                return true;
                            });
                            
                            // 等待下载完成或超时
                            if (await Task.WhenAny(readTask, downloadTimeoutTask) == downloadTimeoutTask)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | Download timed out after 60 seconds of inactivity", LogHelper.LogType.Error);
                                return false;
                            }
                            
                            // 确保下载任务完成
                            bool downloadCompleted = await readTask;
                            
                            if (downloadCompleted)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | Download completed: {(totalBytesRead / 1024.0 / 1024.0):F2} MB");
                            }
                        }
                    }
                    
                    // 如果临时文件存在，则将其移动到目标位置
                    if (File.Exists(tempFilePath))
                    {
                        // 如果目标文件已存在，先删除
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }
                        
                        File.Move(tempFilePath, destinationPath);
                        LogHelper.WriteLogToFile($"AutoUpdate | File saved to: {destinationPath}");
                        return true;
                    }
                    
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP request error: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Download timed out: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Error downloading file: {ex.Message}", LogHelper.LogType.Error);
                    if (ex.InnerException != null)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Inner exception: {ex.InnerException.Message}", LogHelper.LogType.Error);
                    }
                }
                
                // 清理临时文件
                try
                {
                    string tempFilePath = destinationPath + ".tmp";
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch { }
                
                return false;
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
                LogHelper.WriteLogToFile($"AutoUpdate | Checking for ZIP file: {zipFilePath}");

                if (!File.Exists(zipFilePath))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP file not found: {zipFilePath}", LogHelper.LogType.Error);
                    return;
                }

                // Verify ZIP file size and validity
                FileInfo fileInfo = new FileInfo(zipFilePath);
                if (fileInfo.Length == 0)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP file is empty, cannot continue", LogHelper.LogType.Error);
                    return;
                }
                LogHelper.WriteLogToFile($"AutoUpdate | ZIP file size: {fileInfo.Length} bytes");

                // 获取当前应用程序路径和进程ID
                string currentAppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                int currentProcessId = Process.GetCurrentProcess().Id;
                string appPath = Assembly.GetExecutingAssembly().Location;
                
                LogHelper.WriteLogToFile($"AutoUpdate | Current application directory: {currentAppDir}");
                LogHelper.WriteLogToFile($"AutoUpdate | Current process ID: {currentProcessId}");
                LogHelper.WriteLogToFile($"AutoUpdate | Silent update mode: {isInSilence}");

                // 创建批处理文件来执行更新操作
                string batchFilePath = Path.Combine(Path.GetTempPath(), "UpdateICC_" + Guid.NewGuid().ToString().Substring(0, 8) + ".bat");
                LogHelper.WriteLogToFile($"AutoUpdate | Creating update batch file: {batchFilePath}");
                
                // 构建批处理文件内容
                StringBuilder batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                
                // 使窗口隐藏（使用VBS脚本运行隐藏窗口）
                batchContent.AppendLine("echo Set objShell = CreateObject(\"WScript.Shell\") > \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine("echo objShell.Run \"cmd /c \"\"\" ^& WScript.Arguments(0) ^& \"\"\"\", 0, True >> \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine($"echo Wscript.Sleep 100 >> \"%temp%\\hideme.vbs\"");
                
                // 创建真正的更新批处理文件
                string updateBatPath = Path.Combine(Path.GetTempPath(), "ICCUpdate_" + Guid.NewGuid().ToString().Substring(0, 8) + ".bat");
                batchContent.AppendLine($"echo @echo off > \"{updateBatPath}\"");
                
                // 写入等待进程退出的代码到更新批处理文件
                batchContent.AppendLine($"echo set PROC_ID={currentProcessId} >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo :CHECK_PROCESS >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo tasklist /fi \"PID eq %PROC_ID%\" ^| find \"%PROC_ID%\" ^> nul >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% == 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     timeout /t 1 /nobreak ^> nul >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto CHECK_PROCESS >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                // 应用程序已关闭，开始更新操作
                batchContent.AppendLine($"echo echo Application closed, starting update process... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo timeout /t 2 /nobreak ^> nul >> \"{updateBatPath}\"");
                
                // 创建临时解压目录
                string extractPath = Path.Combine(updatesFolderPath, $"Extract_{version}");
                batchContent.AppendLine($"echo echo Extracting update files... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{extractPath}\" rd /s /q \"{extractPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo mkdir \"{extractPath}\" >> \"{updateBatPath}\"");
                
                // PowerShell解压ZIP文件（因为批处理不直接支持ZIP解压）
                batchContent.AppendLine($"echo powershell -command \"Expand-Archive -Path '{zipFilePath.Replace("'", "''")}' -DestinationPath '{extractPath.Replace("'", "''")}' -Force\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% neq 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto ERROR_EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                // 复制文件到应用程序目录
                batchContent.AppendLine($"echo echo Copying updated files to application directory... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo xcopy /s /y /e \"{extractPath}\\*\" \"{currentAppDir}\\\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% neq 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto ERROR_EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                // 清理临时文件
                batchContent.AppendLine($"echo echo Cleaning up temporary files... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{extractPath}\" rd /s /q \"{extractPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{zipFilePath}\" del /f /q \"{zipFilePath}\" >> \"{updateBatPath}\"");
                
                // 启动更新后的应用程序
                batchContent.AppendLine($"echo echo Update completed successfully! >> \"{updateBatPath}\"");
                
                // 根据是否为静默更新模式决定是否自动启动应用程序
                if (isInSilence)
                {
                    // 静默更新模式下，自动启动应用程序
                    batchContent.AppendLine($"echo echo 自动启动应用程序... >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo start \"\" \"{appPath}\" >> \"{updateBatPath}\"");
                }
                else
                {
                    // 非静默模式下，检查应用程序是否已经在运行
                    batchContent.AppendLine($"echo :: 检查应用程序是否已经在运行 >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo tasklist /FI \"IMAGENAME eq Ink Canvas.exe\" | find /i \"Ink Canvas.exe\" > nul >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo if %%ERRORLEVEL%% neq 0 ( >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo     echo 启动应用程序... >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo     start \"\" \"{appPath}\" >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo ) else ( >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo     echo 应用程序已经在运行，不再重复启动 >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                }
                
                batchContent.AppendLine($"echo exit /b 0 >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo goto EXIT >> \"{updateBatPath}\"");
                
                // 错误退出处理
                if (isInSilence)
                {
                    // 静默模式下，不显示错误提示
                    batchContent.AppendLine($"echo :ERROR_EXIT >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo echo Update failed! >> \"%temp%\\icc_update_error.log\" >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo exit /b 1 >> \"{updateBatPath}\"");
                }
                else
                {
                    // 非静默模式下，显示错误提示
                    batchContent.AppendLine($"echo :ERROR_EXIT >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo start \"\" cmd /c \"echo Update failed! ^& pause\" >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo exit /b 1 >> \"{updateBatPath}\"");
                }
                
                // 删除批处理文件自身
                batchContent.AppendLine($"echo :EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo del \"{updateBatPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo exit >> \"{updateBatPath}\"");

                // 使用VBS脚本执行更新批处理文件（隐藏窗口）
                batchContent.AppendLine($"wscript \"%temp%\\hideme.vbs\" \"{updateBatPath}\"");
                batchContent.AppendLine("del \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine("exit");
                
                // 写入批处理文件
                File.WriteAllText(batchFilePath, batchContent.ToString());
                LogHelper.WriteLogToFile($"AutoUpdate | Created update batch file");
                
                // 启动批处理文件（隐藏窗口）
                Process.Start(new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                
                LogHelper.WriteLogToFile($"AutoUpdate | Started update batch process with hidden window");
                
                // 应用程序将由用户手动关闭或由MainWindow中的代码关闭
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error preparing update installation: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Inner exception: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }
            }
        }

        private static async Task<long> GetUrlDelay(string url)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var resp = await client.SendAsync(new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url));
                    sw.Stop();
                    if (resp.IsSuccessStatusCode)
                        return sw.ElapsedMilliseconds;
                }
            }
            catch { }
            return -1;
        }

        // 线路组结构体（包含版本、下载、日志地址）
        public class UpdateLineGroup
        {
            public string GroupName { get; set; } // 组名
            public string VersionUrl { get; set; } // 版本检测地址
            public string DownloadUrlFormat { get; set; } // 下载地址格式（带{0}占位符）
            public string LogUrl { get; set; } // 更新日志地址
        }

        // 通道-线路组映射
        private static readonly Dictionary<UpdateChannel, List<UpdateLineGroup>> ChannelLineGroups = new Dictionary<UpdateChannel, List<UpdateLineGroup>>
        {
            { UpdateChannel.Release, new List<UpdateLineGroup>
                {
                    new UpdateLineGroup
                    {
                        GroupName = "GitHub主线",
                        VersionUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/beta/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/beta/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/main/UpdateLog.md"
                    }
                }
            },
            { UpdateChannel.Beta, new List<UpdateLineGroup>
                {
                    new UpdateLineGroup
                    {
                        GroupName = "GitHub主线",
                        VersionUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    }
                }
            }
        };

        // 检测线路组延迟，只检测当前通道下的所有线路组，返回最快组
        private static async Task<UpdateLineGroup> GetFastestLineGroup(UpdateChannel channel)
        {
            var groups = ChannelLineGroups[channel];
            long minDelay = long.MaxValue;
            UpdateLineGroup bestGroup = null;
            foreach (var group in groups)
            {
                var delay = await GetUrlDelay(group.VersionUrl);
                if (delay >= 0 && delay < minDelay)
                {
                    minDelay = delay;
                    bestGroup = group;
                }
            }
            return bestGroup;
        }

        // 新的自动选择线路组的更新检测方法，返回远程版本号和所用线路组
        public static async Task<(string remoteVersion, UpdateLineGroup lineGroup)> CheckForUpdatesWithAutoLine(UpdateChannel channel = UpdateChannel.Release, bool alwaysGetRemote = false)
        {
            try
            {
                string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                LogHelper.WriteLogToFile($"AutoUpdate | Local version: {localVersion}");
                LogHelper.WriteLogToFile($"AutoUpdate | 检测通道 {channel} 下最快线路组...");
                var bestGroup = await GetFastestLineGroup(channel);
                if (bestGroup == null)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均不可用", LogHelper.LogType.Error);
                    return (null, null);
                }
                LogHelper.WriteLogToFile($"AutoUpdate | 选择最快线路组: {bestGroup.GroupName} {bestGroup.VersionUrl}");
                string remoteVersion = await GetRemoteVersion(bestGroup.VersionUrl);
                if (remoteVersion != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Remote version: {remoteVersion}");
                    Version local = new Version(localVersion);
                    Version remote = new Version(remoteVersion);
                    if (remote > local || alwaysGetRemote)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | New version available or alwaysGetRemote: {remoteVersion}");
                        return (remoteVersion, bestGroup);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Current version is up to date");
                        return (null, bestGroup);
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 获取远程版本失败", LogHelper.LogType.Error);
                    return (null, bestGroup);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in CheckForUpdatesWithAutoLine: {ex.Message}", LogHelper.LogType.Error);
                return (null, null);
            }
        }

        // 使用指定线路组下载新版
        public static async Task<bool> DownloadSetupFileWithLineGroup(string version, UpdateLineGroup group)
        {
            try
            {
                string url = string.Format(group.DownloadUrlFormat, version);
                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
                LogHelper.WriteLogToFile($"AutoUpdate | Downloading from: {url}");
                return await DownloadFile(url, zipFilePath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in DownloadSetupFileWithLineGroup: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        // 使用指定线路组获取更新日志
        public static async Task<string> GetUpdateLogWithLineGroup(UpdateLineGroup group)
        {
            return await AutoUpdateHelper.GetRemoteContent(group.LogUrl);
        }

        // 获取远程内容的通用方法（public 以便线路组方法调用）
        public static async Task<string> GetRemoteContent(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 设置超时时间为10秒
                    client.Timeout = RequestTimeout;
                    LogHelper.WriteLogToFile($"AutoUpdate | Sending HTTP request to: {fileUrl}");
                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);
                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Request timed out after {RequestTimeout.TotalSeconds} seconds", LogHelper.LogType.Error);
                        return null;
                    }
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP request error: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Request timed out: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Error: {ex.Message}", LogHelper.LogType.Error);
                }
                return null;
            }
        }

        // 兼容旧接口：获取更新日志（自动选择最快线路组）
        public static async Task<string> GetUpdateLog(UpdateChannel channel = UpdateChannel.Release)
        {
            var group = await GetFastestLineGroup(channel);
            if (group == null) return "# 无法获取更新日志\n\n所有线路均不可用。";
            return await GetUpdateLogWithLineGroup(group);
        }

        // 兼容旧接口：删除更新文件夹
        public static void DeleteUpdatesFolder()
        {
            try
            {
                if (Directory.Exists(updatesFolderPath))
                {
                    // Try to delete all files first in case of locking issues
                    foreach (string file in Directory.GetFiles(updatesFolderPath, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (string dir in Directory.GetDirectories(updatesFolderPath))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    try { Directory.Delete(updatesFolderPath, true); } catch { }
                }
            }
            catch { }
        }

        // 兼容旧接口：版本修复方法，强制下载并安装指定通道的最新版本
        public static async Task<bool> FixVersion(UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Starting version fix for {channel} channel");
                // 获取远程版本号（自动选择最快线路组，始终下载远程版本）
                var (remoteVersion, group) = await CheckForUpdatesWithAutoLine(channel, true);
                if (string.IsNullOrEmpty(remoteVersion) || group == null)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Failed to retrieve remote version for fixing", LogHelper.LogType.Error);
                    return false;
                }
                LogHelper.WriteLogToFile($"AutoUpdate | Remote version for fixing: {remoteVersion}");
                // 无论版本是否为最新，都下载远程版本
                bool downloadResult = await DownloadSetupFileWithLineGroup(remoteVersion, group);
                if (!downloadResult)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Failed to download update for fixing", LogHelper.LogType.Error);
                    return false;
                }
                // 执行安装，非静默模式
                InstallNewVersionApp(remoteVersion, false);
                App.IsAppExitByUser = true;
                Application.Current.Dispatcher.Invoke(() => {
                    Application.Current.Shutdown();
                });
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in FixVersion: {ex.Message}", LogHelper.LogType.Error);
                return false;
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

