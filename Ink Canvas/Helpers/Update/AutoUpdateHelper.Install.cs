using Ink_Canvas.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers
{
    internal partial class AutoUpdateHelper
    {

        /// <summary>
        /// 安装指定版本的更新包并启动新版本进程以完成替换，然后退出当前应用程序。
        /// </summary>
        /// <remarks>
        /// 该方法会临时将 App.IsUpdateInstalling 置为 true、尝试关闭进程保护（并在结束时还原）、在必要时备份当前设置、解压更新 ZIP、启动解压后的新可执行文件（以更新模式传递旧进程 ID、解压路径和目标路径等参数），并在新进程启动后关闭当前进程。方法会记录日志并在遇到错误时安全退出相应步骤，但不会抛出异常给调用方以外的上下文。</remarks>
        /// <param name="version">要安装的版本号，用于定位更新包文件名（与 <see cref="GetLocalUpdateZipFilePath"/> 一致；选择 x64 包时为 InkCanvasForClass.CE.{version}-x64.zip）。</param>
        /// <param name="isInSilence">指示是否以静默模式启动新版本（影响传递给新进程的参数和可能的用户提示）。</param>
        public static void InstallNewVersionApp(string version, bool isInSilence)
        {
            bool wasProcessProtectionEnabled = false;
            try
            {
                wasProcessProtectionEnabled = ProcessProtectionManager.Enabled;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            try
            {
                App.IsUpdateInstalling = true;
                if (wasProcessProtectionEnabled)
                {
                    try { ProcessProtectionManager.SetEnabled(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }

                // 在更新前备份设置文件
                try
                {
                    if (MainWindow.Settings.Advanced.IsAutoBackupBeforeUpdate)
                    {
                        string backupDir = Path.Combine(App.RootPath, "Backups");
                        if (!Directory.Exists(backupDir))
                        {
                            Directory.CreateDirectory(backupDir);
                            LogHelper.WriteLogToFile($"创建备份目录: {backupDir}");
                        }

                        string backupFileName = $"Settings_BeforeUpdate_v{version}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                        string backupPath = Path.Combine(backupDir, backupFileName);

                        string settingsJson = JsonConvert.SerializeObject(MainWindow.Settings, Formatting.Indented);
                        File.WriteAllText(backupPath, settingsJson);

                        LogHelper.WriteLogToFile($"更新前自动备份设置成功: {backupPath}");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("更新前自动备份功能已禁用，跳过备份");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新前自动备份设置时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                string zipFilePath = GetLocalUpdateZipFilePath(version);
                LogHelper.WriteLogToFile($"AutoUpdate | 检查ZIP文件: {zipFilePath}");

                if (!File.Exists(zipFilePath))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件未找到: {zipFilePath}", LogHelper.LogType.Error);
                    return;
                }

                FileInfo fileInfo = new FileInfo(zipFilePath);
                if (fileInfo.Length == 0)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | ZIP文件为空，无法继续", LogHelper.LogType.Error);
                    return;
                }
                LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件大小: {fileInfo.Length} 字节");

                // 单文件发布时 AppContext.BaseDirectory 指向临时解压目录，需要用 ProcessPath 获取 exe 真实所在目录
                string currentAppDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                int currentProcessId = Process.GetCurrentProcess().Id;

                LogHelper.WriteLogToFile($"AutoUpdate | 当前应用程序目录: {currentAppDir}");
                LogHelper.WriteLogToFile($"AutoUpdate | 当前进程ID: {currentProcessId}");
                LogHelper.WriteLogToFile($"AutoUpdate | 静默更新模式: {isInSilence}");

                // 创建解压目录
                string extractPath = Path.Combine(updatesFolderPath, $"Extract_{version}");
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                        LogHelper.WriteLogToFile($"AutoUpdate | 清理已存在的解压目录: {extractPath}");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 清理解压目录失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                try
                {
                    Directory.CreateDirectory(extractPath);
                    LogHelper.WriteLogToFile($"AutoUpdate | 创建解压目录: {extractPath}");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 创建解压目录失败: {ex.Message}", LogHelper.LogType.Error);
                    return;
                }

                // 解压ZIP文件
                try
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 开始解压ZIP文件到: {extractPath}");
                    SafeZipExtractor.ExtractZipSafely(zipFilePath, extractPath, overwrite: true);
                    LogHelper.WriteLogToFile("AutoUpdate | ZIP文件解压完成");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 解压ZIP文件失败: {ex.Message}", LogHelper.LogType.Error);
                    return;
                }

                // 查找解压后的主程序文件
                string newAppPath = null;
                string[] possibleExeNames = { "InkCanvasForClass.exe", "Ink Canvas.exe", "InkCanvas.exe" };

                foreach (string exeName in possibleExeNames)
                {
                    string testPath = Path.Combine(extractPath, exeName);
                    if (File.Exists(testPath))
                    {
                        newAppPath = testPath;
                        LogHelper.WriteLogToFile($"AutoUpdate | 找到新版本主程序: {newAppPath}");
                        break;
                    }
                }

                if (string.IsNullOrEmpty(newAppPath))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 在解压目录中未找到主程序文件", LogHelper.LogType.Error);
                    return;
                }

                // 启动新版本进程
                try
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 准备启动新版本进程: {newAppPath}");

                    // 启动新版本进程（以更新模式）
                    // 只在路径包含空格时才加引号，避免Windows命令行解析将引号后的参数吞入
                    string escExtract = extractPath.TrimEnd('\\').Contains(" ") ? $"\"{extractPath.TrimEnd('\\')}\"" : extractPath.TrimEnd('\\');
                    string escTarget = currentAppDir.TrimEnd('\\').Contains(" ") ? $"\"{currentAppDir.TrimEnd('\\')}\"" : currentAppDir.TrimEnd('\\');
                    string arguments = $"--update-mode --old-process-id={currentProcessId} --extract-path={escExtract} --target-path={escTarget} --is-silence={isInSilence}";

                    LogHelper.WriteLogToFile($"AutoUpdate | 启动新进程的命令行: {newAppPath} {arguments}");

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = newAppPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };

                    Process.Start(startInfo);
                    LogHelper.WriteLogToFile("AutoUpdate | 新版本进程启动命令已执行");

                    // 等待一小段时间确保新进程启动
                    Thread.Sleep(2000);

                    // 关闭当前旧软件进程
                    LogHelper.WriteLogToFile("AutoUpdate | 关闭当前旧软件进程");
                    App.IsAppExitByUser = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 启动新版本进程时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 准备更新安装时出错: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 内部异常: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }
            }
            finally
            {
                // 确保无论更新成功还是失败，都恢复标志位和进程保护状态
                App.IsUpdateInstalling = false;
                try
                {
                    ProcessProtectionManager.SetEnabled(wasProcessProtectionEnabled);
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex);
                }
            }
        }

        // 解析命令行参数值，正确处理带引号和不带引号的情况
        private static string ParseQuotedArgValue(string arg, string prefix)
        {
            string raw = arg.Substring(prefix.Length);
            if (raw.Length >= 2 && raw[0] == '"')
            {
                int closingQuote = raw.IndexOf('"', 1);
                if (closingQuote > 0)
                {
                    return raw.Substring(1, closingQuote - 1);
                }
            }
            return raw.Trim('"');
        }

        // 处理更新模式的启动参数
        public static bool HandleUpdateModeStartup(string[] args)
        {
            try
            {
                // 检查是否以更新模式启动
                if (args.Contains("--update-mode"))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 检测到更新模式启动");

                    // 解析命令行参数
                    int oldProcessId = -1;
                    string extractPath = null;
                    string targetPath = null;
                    bool isSilence = false;

                    // 记录所有参数用于调试
                    LogHelper.WriteLogToFile($"AutoUpdate | 接收到的命令行参数: {string.Join(" ", args)}");

                    for (int i = 0; i < args.Length; i++)
                    {
                        string arg = args[i];
                        LogHelper.WriteLogToFile($"AutoUpdate | 处理参数 {i}: {arg}");

                        if (arg.StartsWith("--old-process-id="))
                        {
                            string processIdStr = arg.Substring("--old-process-id=".Length);
                            if (int.TryParse(processIdStr, out int pid))
                            {
                                oldProcessId = pid;
                                LogHelper.WriteLogToFile($"AutoUpdate | 解析到老进程ID: {oldProcessId}");
                            }
                        }
                        else if (arg.StartsWith("--extract-path="))
                        {
                            extractPath = ParseQuotedArgValue(arg, "--extract-path=");
                            LogHelper.WriteLogToFile($"AutoUpdate | 解析到解压路径: {extractPath}");
                        }
                        else if (arg.StartsWith("--target-path="))
                        {
                            targetPath = ParseQuotedArgValue(arg, "--target-path=");
                            LogHelper.WriteLogToFile($"AutoUpdate | 解析到目标路径: {targetPath}");
                        }
                        else if (arg.StartsWith("--is-silence="))
                        {
                            string silenceStr = arg.Substring("--is-silence=".Length);
                            if (bool.TryParse(silenceStr, out bool silence))
                            {
                                isSilence = silence;
                                LogHelper.WriteLogToFile($"AutoUpdate | 解析到静默模式: {isSilence}");
                            }
                        }
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | 更新参数 - 老进程ID: {oldProcessId}, 解压路径: {extractPath}, 目标路径: {targetPath}, 静默模式: {isSilence}");

                    if (oldProcessId > 0 && !string.IsNullOrEmpty(extractPath) && !string.IsNullOrEmpty(targetPath))
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 参数验证通过，启动更新任务");
                        // 启动更新任务
                        Task.Run(async () => await PerformUpdate(oldProcessId, extractPath, targetPath, isSilence));
                        return true; // 返回true表示是更新模式
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | 参数验证失败 - 老进程ID: {oldProcessId}, 解压路径: {extractPath}, 目标路径: {targetPath}", LogHelper.LogType.Error);
                    return false;
                }
                return false; // 返回false表示不是更新模式
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 处理更新模式启动时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        // 执行实际的更新操作
        private static async Task PerformUpdate(int oldProcessId, string extractPath, string targetPath, bool isSilence)
        {
            try
            {
                LogHelper.WriteLogToFile("AutoUpdate | 开始执行更新操作");

                // 等待老进程完全退出
                LogHelper.WriteLogToFile($"AutoUpdate | 等待老进程 {oldProcessId} 退出");
                int waitCount = 0;
                const int maxWaitCount = 30; // 最多等待30秒

                while (waitCount < maxWaitCount)
                {
                    try
                    {
                        Process oldProcess = Process.GetProcessById(oldProcessId);
                        if (oldProcess.HasExited)
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | 老进程已退出");
                            break;
                        }
                        LogHelper.WriteLogToFile($"AutoUpdate | 老进程仍在运行，等待中... ({waitCount + 1}/{maxWaitCount})");
                        Thread.Sleep(1000);
                        waitCount++;
                    }
                    catch (ArgumentException)
                    {
                        // 进程不存在，说明已经退出
                        LogHelper.WriteLogToFile("AutoUpdate | 老进程已退出（进程不存在）");
                        break;
                    }
                }

                if (waitCount >= maxWaitCount)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 等待老进程退出超时，尝试强制结束", LogHelper.LogType.Warning);
                    try
                    {
                        Process oldProcess = Process.GetProcessById(oldProcessId);
                        oldProcess.Kill();
                        Thread.Sleep(2000); // 等待进程完全结束
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 强制结束老进程失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                // 确保目标目录存在
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                    LogHelper.WriteLogToFile($"AutoUpdate | 创建目标目录: {targetPath}");
                }

                // 复制文件到目标目录
                LogHelper.WriteLogToFile($"AutoUpdate | 开始复制文件从 {extractPath} 到 {targetPath}");

                try
                {
                    // 使用递归复制方法，支持重试机制
                    bool copySuccess = await CopyDirectoryWithRetryAsync(extractPath, targetPath);
                    if (copySuccess)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 文件复制完成");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 文件复制失败，部分文件可能无法覆盖", LogHelper.LogType.Error);

                        if (!isSilence)
                        {
                            MessageBox.Show(MainWindowStrings.Main_Update_FilesInUse, MainWindowStrings.Main_Update_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 文件复制失败: {ex.Message}", LogHelper.LogType.Error);

                    if (!isSilence)
                    {
                        MessageBox.Show(string.Format(MainWindowStrings.Main_Update_CopyError, ex.Message), MainWindowStrings.Main_Update_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                // 清理临时文件
                try
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 清理临时文件");

                    // 删除解压目录
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                        LogHelper.WriteLogToFile($"AutoUpdate | 删除解压目录: {extractPath}");
                    }

                    // 删除ZIP文件
                    string zipFile = Path.Combine(updatesFolderPath, "InkCanvasForClass.CE.*.zip");
                    string[] zipFiles = Directory.GetFiles(updatesFolderPath, "InkCanvasForClass.CE.*.zip");
                    foreach (string zip in zipFiles)
                    {
                        try
                        {
                            File.Delete(zip);
                            LogHelper.WriteLogToFile($"AutoUpdate | 删除ZIP文件: {zip}");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 删除ZIP文件失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 清理临时文件时出错: {ex.Message}", LogHelper.LogType.Warning);
                }

                LogHelper.WriteLogToFile("AutoUpdate | 更新操作完成");

                // 启动更新后的应用程序
                string newAppPath = Path.Combine(targetPath, "InkCanvasForClass.exe");
                if (File.Exists(newAppPath))
                {
                    try
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 准备启动更新后的应用程序: {newAppPath}");

                        // 获取当前更新进程ID
                        int currentUpdateProcessId = Process.GetCurrentProcess().Id;
                        LogHelper.WriteLogToFile($"AutoUpdate | 当前更新进程ID: {currentUpdateProcessId}");

                        // 创建一个临时标记文件，用于新进程检测更新状态
                        string updateMarkerFile = Path.Combine(targetPath, "update_in_progress.tmp");
                        File.WriteAllText(updateMarkerFile, currentUpdateProcessId.ToString());
                        LogHelper.WriteLogToFile($"AutoUpdate | 创建更新标记文件: {updateMarkerFile}");

                        // 启动更新后的应用程序（标记为最终应用，不受相同进程影响）
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = newAppPath,
                            Arguments = "--final-app --skip-mutex-check",
                            WorkingDirectory = targetPath,
                            UseShellExecute = false
                        };

                        Process newProcess = Process.Start(startInfo);
                        LogHelper.WriteLogToFile($"AutoUpdate | 最终应用程序启动成功，PID: {newProcess?.Id}，已标记为最终应用");

                        // 等待一小段时间确保最终应用程序启动
                        Thread.Sleep(2000);

                        // 结束当前更新进程
                        LogHelper.WriteLogToFile("AutoUpdate | 更新流程完成，结束更新进程");

                        // 强制结束当前更新进程
                        try
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | 强制结束更新进程");

                            // 标记为应用主动退出，避免看门狗重启
                            App.IsAppExitByUser = true;

                            // 写入退出信号文件，确保看门狗不会重启
                            try
                            {
                                string watchdogExitSignalFile = Path.Combine(Path.GetTempPath(), "icc_watchdog_exit_" + Process.GetCurrentProcess().Id + ".flag");
                                File.WriteAllText(watchdogExitSignalFile, "exit");
                                LogHelper.WriteLogToFile("AutoUpdate | 已写入看门狗退出信号文件");
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 写入看门狗退出信号文件失败: {ex.Message}", LogHelper.LogType.Warning);
                            }

                            Environment.Exit(0);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 结束当前更新进程失败: {ex.Message}", LogHelper.LogType.Error);
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 启动更新后的应用程序失败: {ex.Message}", LogHelper.LogType.Error);

                        if (!isSilence)
                        {
                            MessageBox.Show(string.Format(MainWindowStrings.Main_Update_LaunchFailed, ex.Message), MainWindowStrings.Main_Update_LaunchFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 更新后的应用程序文件不存在: {newAppPath}", LogHelper.LogType.Error);

                    if (!isSilence)
                    {
                        MessageBox.Show(string.Format(MainWindowStrings.Main_Update_LaunchNotFound, newAppPath), MainWindowStrings.Main_Update_LaunchNotFoundTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 执行更新操作时出错: {ex.Message}", LogHelper.LogType.Error);

                if (!isSilence)
                {
                    MessageBox.Show(string.Format(MainWindowStrings.Main_Update_Failed, ex.Message), MainWindowStrings.Main_Update_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 异步复制目录的辅助方法（带重试机制）
        private static async Task<bool> CopyDirectoryWithRetryAsync(string sourceDir, string destinationDir, bool overwriteAllFiles = false)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();
            bool allFilesCopied = true;

            // 如果目标目录不存在，则创建它
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // 复制文件
            foreach (FileInfo file in dir.GetFiles())
            {
                // 只覆盖指定的文件，跳过其他文件
                if (!overwriteAllFiles && !UpdateFilesToOverwrite.Contains(file.Name))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 跳过文件（不在覆盖列表中）: {file.Name}");
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDir, file.Name);
                bool fileCopied = false;

                LogHelper.WriteLogToFile($"AutoUpdate | 开始覆盖文件: {file.Name}");

                // 重试机制，最多重试3次
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        // 如果目标文件存在，先尝试删除
                        if (File.Exists(targetFilePath))
                        {
                            try
                            {
                                File.Delete(targetFilePath);
                            }
                            catch (IOException)
                            {
                                // 文件可能正在使用，等待一下再重试
                                if (retry < 2)
                                {
                                    Thread.Sleep(1000);
                                    continue;
                                }
                            }
                        }

                        await Task.Run(() => file.CopyTo(targetFilePath));
                        fileCopied = true;
                        LogHelper.WriteLogToFile($"AutoUpdate | 文件覆盖成功: {file.Name}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 复制文件失败 (重试 {retry + 1}/3) {file.FullName} -> {targetFilePath}: {ex.Message}", LogHelper.LogType.Warning);

                        if (retry < 2)
                        {
                            Thread.Sleep(1000); // 等待1秒后重试
                        }
                    }
                }

                if (!fileCopied)
                {
                    allFilesCopied = false;
                    LogHelper.WriteLogToFile($"AutoUpdate | 文件复制最终失败: {file.FullName}", LogHelper.LogType.Error);
                }
            }

            // 递归复制子目录
            foreach (DirectoryInfo subDir in dirs)
            {
                if (!overwriteAllFiles && !UpdateDirectoriesToOverwrite.Contains(subDir.Name))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 跳过目录（不在覆盖列表中）: {subDir.Name}");
                    continue;
                }

                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                bool subDirCopied = await CopyDirectoryWithRetryAsync(subDir.FullName, newDestinationDir, overwriteAllFiles: true);
                if (!subDirCopied)
                {
                    allFilesCopied = false;
                }
            }

            return allFilesCopied;
        }

        // 异步复制目录的辅助方法（原版本，保留兼容性）
        private static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool overwriteAllFiles = false)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // 如果目标目录不存在，则创建它
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // 复制文件
            foreach (FileInfo file in dir.GetFiles())
            {
                // 只覆盖指定的文件，跳过其他文件
                if (!overwriteAllFiles && !UpdateFilesToOverwrite.Contains(file.Name))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 跳过文件（不在覆盖列表中）: {file.Name}");
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDir, file.Name);
                try
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 开始覆盖文件: {file.Name}");

                    // 如果目标文件存在且正在使用，先删除
                    if (File.Exists(targetFilePath))
                    {
                        File.Delete(targetFilePath);
                    }

                    await Task.Run(() => file.CopyTo(targetFilePath));
                    LogHelper.WriteLogToFile($"AutoUpdate | 文件覆盖成功: {file.Name}");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 复制文件失败 {file.FullName} -> {targetFilePath}: {ex.Message}", LogHelper.LogType.Warning);
                    // 继续复制其他文件，不中断整个过程
                }
            }

            // 递归复制子目录
            foreach (DirectoryInfo subDir in dirs)
            {
                if (!overwriteAllFiles && !UpdateDirectoriesToOverwrite.Contains(subDir.Name))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 跳过目录（不在覆盖列表中）: {subDir.Name}");
                    continue;
                }

                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, overwriteAllFiles: true);
            }
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
                        try { File.Delete(file); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                    foreach (string dir in Directory.GetDirectories(updatesFolderPath))
                    {
                        try { Directory.Delete(dir, true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                    try { Directory.Delete(updatesFolderPath, true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        // 版本修复方法，强制下载并安装指定通道的最新版本
        public static async Task<bool> FixVersion(UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 开始修复版本，通道: {channel}");

                // 获取远程版本号（始终下载远程版本，版本修复模式）
                var (remoteVersion, preferredGroup, _) = await CheckForUpdates(channel, true, true);
                if (string.IsNullOrEmpty(remoteVersion))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 修复版本时获取远程版本失败", LogHelper.LogType.Error);
                    return false;
                }

                LogHelper.WriteLogToFile($"AutoUpdate | 修复版本远程版本: {remoteVersion}");

                var availableGroups = await GetAvailableLineGroupsOrdered(channel);
                if (availableGroups.Count == 0)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 修复版本时无可用线路组", LogHelper.LogType.Error);
                    return false;
                }

                if (preferredGroup != null)
                {
                    availableGroups.RemoveAll(g => g.GroupName == preferredGroup.GroupName);
                    availableGroups.Insert(0, preferredGroup);
                    LogHelper.WriteLogToFile($"AutoUpdate | 修复版本下载优先使用线路组: {preferredGroup.GroupName}");
                }

                // 无论版本是否为最新，都下载远程版本
                bool downloadResult = await DownloadSetupFileWithFallback(remoteVersion, availableGroups);
                if (!downloadResult)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 修复版本时下载更新失败", LogHelper.LogType.Error);
                    return false;
                }

                // 执行安装，静默模式
                InstallNewVersionApp(remoteVersion, true);
                App.IsAppExitByUser = true;
                Application.Current.Dispatcher.Invoke(() =>
                {
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

        /// <summary>
        /// 启动手动指定版本的多线路多线程下载并自动安装（用于历史版本回滚等场景）
        /// </summary>
        public static async Task<bool> StartManualDownloadAndInstall(string version, UpdateChannel channel, Action<double, string> progressCallback = null)
        {
            try
            {
                // 先检测并排序所有可用线路组
                var groups = await GetAvailableLineGroupsOrdered(channel);
                bool downloadSuccess = await DownloadSetupFileWithFallback(version, groups, progressCallback);
                if (!downloadSuccess)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 手动下载版本{version}失败");
                    return false;
                }
                LogHelper.WriteLogToFile($"AutoUpdate | 手动安装版本: {version}");
                InstallNewVersionApp(version, true);
                App.IsAppExitByUser = true;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 手动下载或安装异常: {ex.Message}", LogHelper.LogType.Error);
                progressCallback?.Invoke(0, $"下载异常: {ex.Message}");
                return false;
            }
        }
    }
}
