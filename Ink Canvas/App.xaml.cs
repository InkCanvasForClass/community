using Hardcodet.Wpf.TaskbarNotification;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using System.Security;  // 添加SecurityException所需命名空间
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;
using System.Collections.Generic;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        System.Threading.Mutex mutex;

        public static string[] StartArgs = null;
        public static string RootPath = Environment.GetEnvironmentVariable("APPDATA") + "\\Ink Canvas\\";

        // 新增：保存看门狗进程对象
        private static Process watchdogProcess = null;
        // 新增：标记是否为软件内主动退出
        public static bool IsAppExitByUser = false;
        // 新增：退出信号文件路径
        private static string watchdogExitSignalFile = Path.Combine(Path.GetTempPath(), "icc_watchdog_exit_" + System.Diagnostics.Process.GetCurrentProcess().Id + ".flag");

        public App()
        {
            this.Startup += new StartupEventHandler(App_Startup);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            StartHeartbeatMonitor();
            StartWatchdogIfNeeded();
            this.Exit += App_Exit; // 注册退出事件
        }

        // 增加字段保存崩溃后操作设置
        public static CrashActionType CrashAction = CrashActionType.SilentRestart;

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Ink_Canvas.MainWindow.ShowNewMessage("抱歉，出现未预期的异常，可能导致 InkCanvasForClass 运行不稳定。\n建议保存墨迹后重启应用。", true);
            LogHelper.NewLog(e.Exception.ToString());
            e.Handled = true;

            // 修改：仅当非用户主动退出时才触发自动重启
            if (CrashAction == CrashActionType.SilentRestart && !IsAppExitByUser)
            {
                try
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    System.Diagnostics.Process.Start(exePath);
                }
                catch { }
                Environment.Exit(1);
            }
            // CrashActionType.NoAction 时不做处理
        }

        private TaskbarIcon _taskbar;

        void App_Startup(object sender, StartupEventArgs e)
        {
            RunWatchdogIfNeeded();
            /*if (!StoreHelper.IsStoreApp) */RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

            LogHelper.NewLog(string.Format("Ink Canvas Starting (Version: {0})", Assembly.GetExecutingAssembly().GetName().Version.ToString()));

            bool ret;
            mutex = new System.Threading.Mutex(true, "InkCanvasForClass", out ret);

            if (!ret && !e.Args.Contains("-m")) //-m multiple
            {
                LogHelper.NewLog("Detected existing instance");
                MessageBox.Show("已有一个程序实例正在运行");
                LogHelper.NewLog("Ink Canvas automatically closed");
                Environment.Exit(0);
            }

            _taskbar = (TaskbarIcon)FindResource("TaskbarTrayIcon");

            StartArgs = e.Args;

            // 新增：Office注册表检测
            try
            {
                LogHelper.WriteLogToFile("开始Office注册表检测");
                
                // 检查Office安装
                if (!IsOfficeInstalled())
                {
                    LogHelper.WriteLogToFile("未检测到Office安装", LogHelper.LogType.Warning);
                    return;
                }

                // 尝试获取所有可能的Office版本路径
                var officeVersions = GetOfficeVersions();
                if (officeVersions.Count == 0)
                {
                    LogHelper.WriteLogToFile("未找到任何Office版本", LogHelper.LogType.Warning);
                    return;
                }

                foreach (var version in officeVersions)
                {
                    string regPath = $"Software\\Microsoft\\Office\\{version}\\Common\\Security";
                    LogHelper.WriteLogToFile($"正在处理Office版本 {version}, 注册表路径: {regPath}");

                    try
                    {
                        using (Microsoft.Win32.RegistryKey baseKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath))
                        {
                            if (baseKey == null)
                            {
                                LogHelper.WriteLogToFile($"注册表路径不存在: {regPath}", LogHelper.LogType.Warning);
                                continue;
                            }

                            Settings settingsInstance = new Settings();
                            string backupPath = Path.Combine(settingsInstance.Automation.AutoSavedStrokesLocation, "RegistryBackups");
                            LogHelper.WriteLogToFile($"备份路径: {backupPath}");
                            
                            if (!Directory.Exists(backupPath)) 
                            {
                                Directory.CreateDirectory(backupPath);
                                LogHelper.WriteLogToFile($"创建备份目录: {backupPath}");
                            }
                            
                            string backupFile = Path.Combine(backupPath, $"SecurityBackup_{version}_{DateTime.Now:yyyyMMddHHmmss}.reg");
                            LogHelper.WriteLogToFile($"创建备份文件: {backupFile}");
                            
                            // 使用UTF8编码写入注册表文件
                            using (StreamWriter sw = new StreamWriter(backupFile, false, System.Text.Encoding.UTF8))
                            {
                                sw.WriteLine("Windows Registry Editor Version 5.00\n");
                                sw.WriteLine();
                                sw.WriteLine($"[{Microsoft.Win32.Registry.CurrentUser.Name}\\{regPath}]");
                                
                                foreach (string valueName in baseKey.GetValueNames())
                                {
                                    object value = baseKey.GetValue(valueName);
                                    sw.WriteLine($"\"{valueName}\"=dword:{((int)value):x8}");
                                    LogHelper.WriteLogToFile($"备份注册表值: {valueName} = {value}");
                                }
                            }

                            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath, true))
                            {
                                // 仅在值不存在或不等于1时更新
                                object currentValue = key.GetValue("DisableProtectedView");
                                if (currentValue == null || (int)currentValue != 1)
                                {
                                    key.SetValue("DisableProtectedView", 1, Microsoft.Win32.RegistryValueKind.DWord);
                                    LogHelper.WriteLogToFile($"Office {version} 注册表值已设置: DisableProtectedView = 1");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"Office {version} 注册表值已存在且无需更改: DisableProtectedView = 1");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理Office版本 {version} 时出错: {ex.Message}", LogHelper.LogType.Error);
                        continue;
                    }
                }
            }
            catch (SecurityException secEx)
            {
                LogHelper.WriteLogToFile($"安全异常: {secEx.Message}", LogHelper.LogType.Error);
                ShowPermissionError();
            }
            catch (UnauthorizedAccessException authEx)
            {
                LogHelper.WriteLogToFile($"访问被拒绝: {authEx.Message}", LogHelper.LogType.Error);
                ShowPermissionError();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"未知错误: {ex.GetType().FullName} - {ex.Message}", LogHelper.LogType.Error);
                LogHelper.WriteLogToFile(ex.StackTrace, LogHelper.LogType.Info);
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                if (System.Windows.Forms.SystemInformation.MouseWheelScrollLines == -1)
                    e.Handled = false;
                else
                    try
                    {
                        ScrollViewerEx SenderScrollViewer = (ScrollViewerEx)sender;
                        SenderScrollViewer.ScrollToVerticalOffset(SenderScrollViewer.VerticalOffset - e.Delta * 10 * System.Windows.Forms.SystemInformation.MouseWheelScrollLines / (double)120);
                        e.Handled = true;
                    }
                    catch {  }
            }
            catch {  }
        }

        // 新增：用于设置崩溃后操作类型
        public enum CrashActionType
        {
            SilentRestart,
            NoAction
        }

        // 心跳相关
        private static Timer heartbeatTimer;
        private static DateTime lastHeartbeat = DateTime.Now;
        private static Timer watchdogTimer;

        private void StartHeartbeatMonitor()
        {
            // 主线程定时更新心跳
            heartbeatTimer = new Timer(_ => lastHeartbeat = DateTime.Now, null, 0, 1000);
            // 辅助线程检测心跳超时
            watchdogTimer = new Timer(_ =>
            {
                if ((DateTime.Now - lastHeartbeat).TotalSeconds > 10)
                {
                    LogHelper.NewLog("检测到主线程无响应，自动重启。");
                    if (CrashAction == CrashActionType.SilentRestart)
                    {
                        try
                        {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            Process.Start(exePath);
                        }
                        catch { }
                        Environment.Exit(1);
                    }
                }
            }, null, 0, 3000);
        }

        // 看门狗进程
        private void StartWatchdogIfNeeded()
        {
            // 避免递归启动
            if (Environment.GetCommandLineArgs().Contains("--watchdog")) return;
            // 启动看门狗进程
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--watchdog " + Process.GetCurrentProcess().Id + " \"" + watchdogExitSignalFile + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            watchdogProcess = Process.Start(psi);
        }

        // 看门狗主逻辑（在 Main 函数或 App_Startup 入口前加判断）
        public static void RunWatchdogIfNeeded()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 4 && args[1] == "--watchdog")
            {
                int pid = int.Parse(args[2]);
                string exitSignalFile = args[3];
                try
                {
                    var proc = Process.GetProcessById(pid);
                    while (!proc.HasExited)
                    {
                        // 检查退出信号文件
                        if (File.Exists(exitSignalFile))
                        {
                            try { File.Delete(exitSignalFile); } catch { }
                            Environment.Exit(0);
                        }
                        Thread.Sleep(2000);
                    }
                    // 主进程异常退出，自动重启
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    Process.Start(exePath);
                }
                catch { }
                Environment.Exit(0);
            }
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            // 仅在软件内主动退出时关闭看门狗，并写入退出信号
            try
            {
                if (IsAppExitByUser)
                {
                    // 写入退出信号文件，通知看门狗正常退出
                    File.WriteAllText(watchdogExitSignalFile, "exit");
                    if (watchdogProcess != null && !watchdogProcess.HasExited)
                    {
                        watchdogProcess.Kill();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 检查Office是否安装
        /// </summary>
        private bool IsOfficeInstalled()
        {
            try
            {
                // 检查注册表判断Office是否存在
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office"))
                {
                    return key?.GetValueNames().Any(name => name.Contains(".0")) ?? false;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 显示权限不足的错误提示
        /// </summary>
        private void ShowPermissionError()
        {
            const string message = "需要管理员权限才能完成此操作\n请以管理员身份重新启动应用程序";
            LogHelper.WriteLogToFile(message, LogHelper.LogType.Error);
            System.Windows.MessageBox.Show(message, "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 获取所有已安装的Office版本
        /// </summary>
        private List<string> GetOfficeVersions()
        {
            var versions = new List<string>();
            try
            {
                // 检查HKLM
                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains(".0"))
                            {
                                versions.Add(subKeyName);
                                LogHelper.WriteLogToFile($"在HKLM中找到Office版本: {subKeyName}");
                            }
                        }
                    }
                }

                // 检查HKCU
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Office"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains(".0") && !versions.Contains(subKeyName))
                            {
                                versions.Add(subKeyName);
                                LogHelper.WriteLogToFile($"在HKCU中找到Office版本: {subKeyName}");
                            }
                        }
                    }
                }

                // 检查64位注册表
                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Microsoft\\Office"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains(".0") && !versions.Contains(subKeyName))
                            {
                                versions.Add(subKeyName);
                                LogHelper.WriteLogToFile($"在64位注册表中找到Office版本: {subKeyName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取Office版本时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 按版本号排序
            versions.Sort((a, b) =>
            {
                try
                {
                    double va = double.Parse(a.Replace(".0", ""));
                    double vb = double.Parse(b.Replace(".0", ""));
                    return vb.CompareTo(va); // 降序排列，最新版本在前
                }
                catch
                {
                    return 0;
                }
            });

            return versions;
        }
    }
}
