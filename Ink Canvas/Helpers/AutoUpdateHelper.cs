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
using Newtonsoft.Json;

namespace Ink_Canvas.Helpers
{
    internal class AutoUpdateHelper
    {
        // 定义超时时间为10秒
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static string updatesFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AutoUpdate");
        private static string statusFilePath = null;

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
                        VersionUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "kkgithub线路",
                        VersionUrl = "https://kkgithub.com/InkCanvasForClass/community/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://kkgithub.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://kkgithub.com/InkCanvasForClass/community/raw/refs/heads/main/UpdateLog.md"
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
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "kkgithub线路",
                        VersionUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://kkgithub.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    }
                }
            }
        };

        // 检测URL延迟
        private static async Task<long> GetUrlDelay(string url)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var sw = Stopwatch.StartNew();
                    var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    sw.Stop();
                    if (resp.IsSuccessStatusCode)
                        return sw.ElapsedMilliseconds;
                }
            }
            catch { }
            return -1;
        }

        // 检测线路组延迟，返回最快组（保持向后兼容）
        private static async Task<UpdateLineGroup> GetFastestLineGroup(UpdateChannel channel)
        {
            var availableGroups = await GetAvailableLineGroupsOrdered(channel);
            return availableGroups.Count > 0 ? availableGroups[0] : null;
        }

        // 获取所有可用线路组，按延迟排序
        public static async Task<List<UpdateLineGroup>> GetAvailableLineGroupsOrdered(UpdateChannel channel)
        {
            var groups = ChannelLineGroups[channel];
            var availableGroups = new List<(UpdateLineGroup group, long delay)>();
            
            LogHelper.WriteLogToFile($"AutoUpdate | 开始检测通道 {channel} 下所有线路组延迟...");
            
            foreach (var group in groups)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 检测线路组: {group.GroupName} ({group.VersionUrl})");
                var delay = await GetUrlDelay(group.VersionUrl);
                if (delay >= 0)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 延迟: {delay}ms");
                    availableGroups.Add((group, delay));
                }
                else
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 不可用", LogHelper.LogType.Warning);
                }
            }
            
            // 按延迟排序，延迟最小的排在前面
            var orderedGroups = availableGroups
                .OrderBy(x => x.delay)
                .Select(x => x.group)
                .ToList();
            
            if (orderedGroups.Count > 0)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 找到 {orderedGroups.Count} 个可用线路组，按延迟排序:");
                for (int i = 0; i < orderedGroups.Count; i++)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | {i + 1}. {orderedGroups[i].GroupName}");
                }
            }
            else
            {
                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均不可用", LogHelper.LogType.Error);
            }
            
            return orderedGroups;
        }

        // 获取远程版本号
        private static async Task<string> GetRemoteVersion(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = RequestTimeout;
                    LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");
                    
                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);
                    
                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                        return null;
                    }
                    
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    content = content.Trim();
                    
                    // 如果内容包含HTML（可能是GitHub页面而不是原始内容），尝试提取版本号
                    if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 收到HTML内容而不是原始版本号 - 尝试提取版本");
                        int startPos = content.IndexOf("<table");
                        if (startPos > 0)
                        {
                            int endPos = content.IndexOf("</table>", startPos);
                            if (endPos > startPos)
                            {
                                string tableContent = content.Substring(startPos, endPos - startPos);
                                var match = System.Text.RegularExpressions.Regex.Match(tableContent, @"(\d+\.\d+\.\d+(\.\d+)?)");
                                if (match.Success)
                                {
                                    content = match.Groups[1].Value;
                                    LogHelper.WriteLogToFile($"AutoUpdate | 从HTML提取版本: {content}");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"AutoUpdate | 无法从HTML内容提取版本");
                                    return null;
                                }
                            }
                        }
                    }
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | 响应内容: {content}");
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                }

                return null;
            }
        }

        // 主要的更新检测方法（优先检测延迟，失败时自动切换线路组）
        public static async Task<(string remoteVersion, UpdateLineGroup lineGroup)> CheckForUpdates(UpdateChannel channel = UpdateChannel.Release, bool alwaysGetRemote = false)
        {
            try
            {
                string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                LogHelper.WriteLogToFile($"AutoUpdate | 本地版本: {localVersion}");
                LogHelper.WriteLogToFile($"AutoUpdate | 检测通道 {channel} 下最快线路组...");
                
                // 获取所有可用线路组（按延迟排序）
                var availableGroups = await GetAvailableLineGroupsOrdered(channel);
                if (availableGroups.Count == 0)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均不可用", LogHelper.LogType.Error);
                    return (null, null);
                }
                
                // 依次尝试每个线路组，直到成功获取版本信息
                foreach (var group in availableGroups)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 尝试使用线路组获取版本信息: {group.GroupName}");
                    string remoteVersion = await GetRemoteVersion(group.VersionUrl);
                    
                    if (remoteVersion != null)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 成功从线路组 {group.GroupName} 获取远程版本: {remoteVersion}");
                        Version local = new Version(localVersion);
                        Version remote = new Version(remoteVersion);
                        
                        if (remote > local || alwaysGetRemote)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 发现新版本或强制获取: {remoteVersion}");
                            return (remoteVersion, group);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 当前版本已是最新");
                            return (null, group);
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 获取版本失败，尝试下一个线路组", LogHelper.LogType.Warning);
                    }
                }
                
                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均无法获取版本信息", LogHelper.LogType.Error);
                return (null, null);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | CheckForUpdates错误: {ex.Message}", LogHelper.LogType.Error);
                return (null, null);
            }
        }

        // 使用指定线路组下载新版
        public static async Task<bool> DownloadSetupFile(string version, UpdateLineGroup group)
        {
            return await DownloadSetupFileWithFallback(version, new List<UpdateLineGroup> { group });
        }

        // 使用多线路组下载新版（支持自动切换）
        public static async Task<bool> DownloadSetupFileWithFallback(string version, List<UpdateLineGroup> groups)
        {
            try
            {
                statusFilePath = Path.Combine(updatesFolderPath, $"DownloadV{version}Status.txt");

                if (File.Exists(statusFilePath) && File.ReadAllText(statusFilePath).Trim().ToLower() == "true")
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 安装包已下载");
                    return true;
                }

                // 确保更新目录存在
                if (!Directory.Exists(updatesFolderPath))
                {
                    Directory.CreateDirectory(updatesFolderPath);
                    LogHelper.WriteLogToFile($"AutoUpdate | 创建更新目录: {updatesFolderPath}");
                }

                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
                LogHelper.WriteLogToFile($"AutoUpdate | 目标文件路径: {zipFilePath}");

                SaveDownloadStatus(false);

                // 依次尝试每个线路组
                foreach (var group in groups)
                {
                    string url = string.Format(group.DownloadUrlFormat, version);
                    LogHelper.WriteLogToFile($"AutoUpdate | 尝试从线路组 {group.GroupName} 下载: {url}");
                    
                    bool downloadSuccess = await DownloadFile(url, zipFilePath);
                    
                    if (downloadSuccess)
                    {
                        SaveDownloadStatus(true);
                        LogHelper.WriteLogToFile($"AutoUpdate | 从线路组 {group.GroupName} 下载成功");
                        return true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 下载失败，尝试下一个线路组", LogHelper.LogType.Warning);
                    }
                }
                
                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组下载均失败", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 下载更新时出错: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 内部异常: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }

                SaveDownloadStatus(false);
                return false;
            }
        }

        // 下载文件的具体实现
        private static async Task<bool> DownloadFile(string fileUrl, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                    client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                    
                    LogHelper.WriteLogToFile($"AutoUpdate | 开始下载: {fileUrl}");
                    
                    string tempFilePath = destinationPath + ".tmp";
                    
                    string directory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var downloadTask = client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    var initialTimeoutTask = Task.Delay(RequestTimeout);
                    
                    var completedTask = await Task.WhenAny(downloadTask, initialTimeoutTask);
                    if (completedTask == initialTimeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 初始连接超时", LogHelper.LogType.Error);
                        return false;
                    }
                    
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    
                    long? totalBytes = response.Content.Headers.ContentLength;
                    LogHelper.WriteLogToFile($"AutoUpdate | 文件大小: {(totalBytes.HasValue ? (totalBytes.Value / 1024.0 / 1024.0).ToString("F2") + " MB" : "未知")}");
                    
                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (var downloadStream = await response.Content.ReadAsStreamAsync())
                        {
                            byte[] buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;
                            DateTime lastProgressUpdate = DateTime.Now;
                            
                            var downloadTimeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                            var readTask = Task.Run(async () => {
                                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalBytesRead += bytesRead;
                                    
                                    if ((DateTime.Now - lastProgressUpdate).TotalSeconds >= 5)
                                    {
                                        if (totalBytes.HasValue)
                                        {
                                            double percentage = (double)totalBytesRead / totalBytes.Value * 100;
                                            LogHelper.WriteLogToFile($"AutoUpdate | 下载进度: {percentage:F1}% ({(totalBytesRead / 1024.0 / 1024.0):F2} MB / {(totalBytes.Value / 1024.0 / 1024.0):F2} MB)");
                                        }
                                        else
                                        {
                                            LogHelper.WriteLogToFile($"AutoUpdate | 已下载: {(totalBytesRead / 1024.0 / 1024.0):F2} MB");
                                        }
                                        lastProgressUpdate = DateTime.Now;
                                        downloadTimeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                                    }
                                }
                                return true;
                            });
                            
                            if (await Task.WhenAny(readTask, downloadTimeoutTask) == downloadTimeoutTask)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 下载超时（60秒无数据传输）", LogHelper.LogType.Error);
                                return false;
                            }
                            
                            bool downloadCompleted = await readTask;
                            
                            if (downloadCompleted)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 下载完成: {(totalBytesRead / 1024.0 / 1024.0):F2} MB");
                            }
                        }
                    }
                    
                    if (File.Exists(tempFilePath))
                    {
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }
                        
                        File.Move(tempFilePath, destinationPath);
                        LogHelper.WriteLogToFile($"AutoUpdate | 文件保存到: {destinationPath}");
                        return true;
                    }
                    
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 下载超时: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 下载文件时出错: {ex.Message}", LogHelper.LogType.Error);
                    if (ex.InnerException != null)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 内部异常: {ex.InnerException.Message}", LogHelper.LogType.Error);
                    }
                }
                
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

        // 保存下载状态
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
                LogHelper.WriteLogToFile($"AutoUpdate | 保存下载状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 安装新版本应用
        public static void InstallNewVersionApp(string version, bool isInSilence)
        {
            try
            {
                // 在更新前备份设置文件
                try
                {
                    if (MainWindow.Settings.Advanced.IsAutoBackupBeforeUpdate)
                    {
                        string backupDir = Path.Combine(App.RootPath, "Backups");
                        if (!Directory.Exists(backupDir))
                        {
                            Directory.CreateDirectory(backupDir);
                            LogHelper.WriteLogToFile($"创建备份目录: {backupDir}", LogHelper.LogType.Info);
                        }
                        
                        string backupFileName = $"Settings_BeforeUpdate_v{version}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        string backupPath = Path.Combine(backupDir, backupFileName);
                        
                        string settingsJson = JsonConvert.SerializeObject(MainWindow.Settings, Formatting.Indented);
                        File.WriteAllText(backupPath, settingsJson);
                        
                        LogHelper.WriteLogToFile($"更新前自动备份设置成功: {backupPath}", LogHelper.LogType.Info);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("更新前自动备份功能已禁用，跳过备份", LogHelper.LogType.Info);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新前自动备份设置时出错: {ex.Message}", LogHelper.LogType.Error);
                }
                
                string zipFilePath = Path.Combine(updatesFolderPath, $"InkCanvasForClass.CE.{version}.zip");
                LogHelper.WriteLogToFile($"AutoUpdate | 检查ZIP文件: {zipFilePath}");

                if (!File.Exists(zipFilePath))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件未找到: {zipFilePath}", LogHelper.LogType.Error);
                    return;
                }

                FileInfo fileInfo = new FileInfo(zipFilePath);
                if (fileInfo.Length == 0)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件为空，无法继续", LogHelper.LogType.Error);
                    return;
                }
                LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件大小: {fileInfo.Length} 字节");

                string currentAppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                int currentProcessId = Process.GetCurrentProcess().Id;
                string appPath = Assembly.GetExecutingAssembly().Location;
                
                LogHelper.WriteLogToFile($"AutoUpdate | 当前应用程序目录: {currentAppDir}");
                LogHelper.WriteLogToFile($"AutoUpdate | 当前进程ID: {currentProcessId}");
                LogHelper.WriteLogToFile($"AutoUpdate | 静默更新模式: {isInSilence}");

                string batchFilePath = Path.Combine(Path.GetTempPath(), "UpdateICC_" + Guid.NewGuid().ToString().Substring(0, 8) + ".bat");
                LogHelper.WriteLogToFile($"AutoUpdate | 创建更新批处理文件: {batchFilePath}");
                
                StringBuilder batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                
                batchContent.AppendLine("echo Set objShell = CreateObject(\"WScript.Shell\") > \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine("echo objShell.Run \"cmd /c \"\"\" ^& WScript.Arguments(0) ^& \"\"\"\", 0, True >> \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine($"echo Wscript.Sleep 100 >> \"%temp%\\hideme.vbs\"");
                
                string updateBatPath = Path.Combine(Path.GetTempPath(), "ICCUpdate_" + Guid.NewGuid().ToString().Substring(0, 8) + ".bat");
                batchContent.AppendLine($"echo @echo off > \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo set PROC_ID={currentProcessId} >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo :CHECK_PROCESS >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo tasklist /fi \"PID eq %PROC_ID%\" ^| find \"%PROC_ID%\" ^> nul >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% == 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     timeout /t 1 /nobreak ^> nul >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto CHECK_PROCESS >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo echo Application closed, starting update process... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo timeout /t 2 /nobreak ^> nul >> \"{updateBatPath}\"");
                
                string extractPath = Path.Combine(updatesFolderPath, $"Extract_{version}");
                batchContent.AppendLine($"echo echo Extracting update files... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{extractPath}\" rd /s /q \"{extractPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo mkdir \"{extractPath}\" >> \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo powershell -command \"Expand-Archive -Path '{zipFilePath.Replace("'", "''")}' -DestinationPath '{extractPath.Replace("'", "''")}' -Force\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% neq 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto ERROR_EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo echo Copying updated files to application directory... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo xcopy /s /y /e \"{extractPath}\\*\" \"{currentAppDir}\\\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if %%ERRORLEVEL%% neq 0 ( >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo     goto ERROR_EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo ) >> \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo echo Cleaning up temporary files... >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{extractPath}\" rd /s /q \"{extractPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo if exist \"{zipFilePath}\" del /f /q \"{zipFilePath}\" >> \"{updateBatPath}\"");
                
                batchContent.AppendLine($"echo echo Update completed successfully! >> \"{updateBatPath}\"");
                
                if (isInSilence)
                {
                    batchContent.AppendLine($"echo echo 自动启动应用程序... >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo start \"\" \"{appPath}\" >> \"{updateBatPath}\"");
                }
                else
                {
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
                
                if (isInSilence)
                {
                    batchContent.AppendLine($"echo :ERROR_EXIT >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo echo Update failed! >> \"%temp%\\icc_update_error.log\" >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo exit /b 1 >> \"{updateBatPath}\"");
                }
                else
                {
                    batchContent.AppendLine($"echo :ERROR_EXIT >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo start \"\" cmd /c \"echo Update failed! ^& pause\" >> \"{updateBatPath}\"");
                    batchContent.AppendLine($"echo exit /b 1 >> \"{updateBatPath}\"");
                }
                
                batchContent.AppendLine($"echo :EXIT >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo del \"{updateBatPath}\" >> \"{updateBatPath}\"");
                batchContent.AppendLine($"echo exit >> \"{updateBatPath}\"");

                batchContent.AppendLine($"wscript \"%temp%\\hideme.vbs\" \"{updateBatPath}\"");
                batchContent.AppendLine("del \"%temp%\\hideme.vbs\"");
                batchContent.AppendLine("exit");
                
                File.WriteAllText(batchFilePath, batchContent.ToString());
                LogHelper.WriteLogToFile($"AutoUpdate | 创建更新批处理文件完成");
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                
                LogHelper.WriteLogToFile($"AutoUpdate | 启动更新批处理进程（隐藏窗口）");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 准备更新安装时出错: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 内部异常: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }
            }
        }

        // 获取远程内容的通用方法
        public static async Task<string> GetRemoteContent(string fileUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = RequestTimeout;
                    LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");
                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);
                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                        return null;
                    }
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                }
                return null;
            }
        }

        // 使用指定线路组获取更新日志
        public static async Task<string> GetUpdateLogWithLineGroup(UpdateLineGroup group)
        {
            return await GetRemoteContent(group.LogUrl);
        }

        // 获取更新日志（自动选择最快线路组）
        public static async Task<string> GetUpdateLog(UpdateChannel channel = UpdateChannel.Release)
        {
            var group = await GetFastestLineGroup(channel);
            if (group == null) return "# 无法获取更新日志\n\n所有线路均不可用。";
            return await GetUpdateLogWithLineGroup(group);
        }

        // 删除更新文件夹
        public static void DeleteUpdatesFolder()
        {
            try
            {
                if (Directory.Exists(updatesFolderPath))
                {
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

        // 版本修复方法，强制下载并安装指定通道的最新版本
        public static async Task<bool> FixVersion(UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 开始修复版本，通道: {channel}");
                
                // 获取远程版本号（自动选择最快线路组，始终下载远程版本）
                var (remoteVersion, group) = await CheckForUpdates(channel, true);
                if (string.IsNullOrEmpty(remoteVersion) || group == null)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 修复版本时获取远程版本失败", LogHelper.LogType.Error);
                    return false;
                }
                
                LogHelper.WriteLogToFile($"AutoUpdate | 修复版本远程版本: {remoteVersion}");
                
                // 无论版本是否为最新，都下载远程版本
                bool downloadResult = await DownloadSetupFile(remoteVersion, group);
                if (!downloadResult)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 修复版本时下载更新失败", LogHelper.LogType.Error);
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
                LogHelper.WriteLogToFile($"AutoUpdate | FixVersion错误: {ex.Message}", LogHelper.LogType.Error);
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

