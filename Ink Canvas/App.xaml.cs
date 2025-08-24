using Hardcodet.Wpf.TaskbarNotification;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        Mutex mutex;

        public static string[] StartArgs;
        public static string RootPath = Environment.GetEnvironmentVariable("APPDATA") + "\\Ink Canvas\\";

        // 新增：保存看门狗进程对象
        private static Process watchdogProcess;
        // 新增：标记是否为软件内主动退出
        public static bool IsAppExitByUser;
        // 新增：退出信号文件路径
        private static string watchdogExitSignalFile = Path.Combine(Path.GetTempPath(), "icc_watchdog_exit_" + Process.GetCurrentProcess().Id + ".flag");
        // 新增：崩溃日志文件路径
        private static string crashLogFile = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "Ink Canvas", "crash_logs");
        // 新增：进程ID
        private static int currentProcessId = Process.GetCurrentProcess().Id;
        // 新增：应用启动时间
        internal static DateTime appStartTime { get; private set; }
        // 新增：最后一次错误信息
        private static string lastErrorMessage = string.Empty;
        // 新增：是否已初始化崩溃监听器
        private static bool crashListenersInitialized;

        public App()
        {
            // 配置TLS协议以支持Windows 7
            ConfigureTlsForWindows7();

            // 如果是看门狗子进程，直接进入看门狗主循环并终止主流程
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && args[1] == "--watchdog")
            {
                RunWatchdogIfNeeded();
                Environment.Exit(0);
                return;
            }

            // 启动时优先同步设置，确保CrashAction为最新
            SyncCrashActionFromSettings();

            Startup += App_Startup;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            StartHeartbeatMonitor();

            // 新增：初始化全局异常和进程结束处理
            InitializeCrashListeners();

            // 仅在崩溃后操作为静默重启时才启动看门狗
            if (CrashAction == CrashActionType.SilentRestart)
            {
                StartWatchdogIfNeeded();
            }
            Exit += App_Exit; // 注册退出事件
        }

        // 新增：配置TLS协议以支持Windows 7
        private void ConfigureTlsForWindows7()
        {
            try
            {
                // 检测操作系统版本
                var osVersion = Environment.OSVersion;
                bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

                if (isWindows7)
                {
                    LogHelper.WriteLogToFile("检测到Windows 7系统，配置TLS协议支持");

                    // 启用所有TLS版本以支持Windows 7
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                    // 配置ServicePointManager以支持Windows 7
                    ServicePointManager.DefaultConnectionLimit = 10;
                    ServicePointManager.Expect100Continue = false;
                    ServicePointManager.UseNagleAlgorithm = false;

                    LogHelper.WriteLogToFile("TLS协议配置完成，已启用TLS 1.2/1.1/1.0支持");
                }
                else
                {
                    // 对于更新的Windows版本，不进行任何TLS配置，使用系统默认设置
                    LogHelper.WriteLogToFile($"检测到Windows版本: {osVersion.VersionString}，使用系统默认TLS配置");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"配置TLS协议时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 新增：初始化崩溃监听器
        private void InitializeCrashListeners()
        {
            if (crashListenersInitialized) return;

            try
            {
                // 确保崩溃日志目录存在
                if (!Directory.Exists(crashLogFile))
                {
                    Directory.CreateDirectory(crashLogFile);
                }

                // 注册非UI线程未处理异常处理程序
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // 注册控制台Ctrl+C等终止信号处理
                Console.CancelKeyPress += Console_CancelKeyPress;

                // 注册系统会话结束事件（关机、注销等）
                SystemEvents.SessionEnding += SystemEvents_SessionEnding;

                // 注册进程退出处理程序
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                // 尝试注册Windows关闭消息监听
                SetConsoleCtrlHandler(ConsoleCtrlHandler, true);

                // 如果系统支持，添加Windows Management Instrumentation监听器
                try
                {
                    // 使用反射动态加载和调用WMI
                    TrySetupWmiMonitoring();
                }
                catch (Exception wmiEx)
                {
                    LogHelper.WriteLogToFile($"设置WMI进程监控失败: {wmiEx.Message}", LogHelper.LogType.Warning);
                }

                crashListenersInitialized = true;
                LogHelper.WriteLogToFile("已初始化崩溃监听器");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化崩溃监听器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 新增：动态加载WMI监控（避免直接引用System.Management）
        private void TrySetupWmiMonitoring()
        {
            try
            {
                // 检查System.Management程序集是否可用
                var assemblyName = "System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
                var assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
                    LogHelper.WriteLogToFile("未找到System.Management程序集，跳过WMI监控", LogHelper.LogType.Warning);
                    return;
                }

                // 使用反射创建WMI查询
                var watcherType = assembly.GetType("System.Management.ManagementEventWatcher");
                if (watcherType == null)
                {
                    LogHelper.WriteLogToFile("未找到ManagementEventWatcher类型，跳过WMI监控", LogHelper.LogType.Warning);
                    return;
                }

                // 构建WMI查询字符串
                string queryString = $"SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.ProcessId = {currentProcessId}";

                // 创建ManagementEventWatcher实例
                object watcher = Activator.CreateInstance(watcherType, queryString);

                // 获取EventArrived事件信息
                var eventInfo = watcherType.GetEvent("EventArrived");
                if (eventInfo == null)
                {
                    LogHelper.WriteLogToFile("未找到EventArrived事件，跳过WMI监控", LogHelper.LogType.Warning);
                    return;
                }

                // 创建委托并订阅事件
                Type delegateType = eventInfo.EventHandlerType;
                var handler = Delegate.CreateDelegate(delegateType, this, GetType().GetMethod("WmiEventHandler", BindingFlags.NonPublic | BindingFlags.Instance));
                eventInfo.AddEventHandler(watcher, handler);

                // 启动监听
                var startMethod = watcherType.GetMethod("Start");
                startMethod.Invoke(watcher, null);

                LogHelper.WriteLogToFile("已成功启动WMI进程监控");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"动态加载WMI监控失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // WMI事件处理方法（通过反射调用）
        private void WmiEventHandler(object sender, EventArgs e)
        {
            try
            {
                // 尝试从事件参数中提取信息
                dynamic eventArgs = e;
                dynamic newEvent = eventArgs.NewEvent;
                if (newEvent != null)
                {
                    dynamic targetInstance = newEvent["TargetInstance"];
                    if (targetInstance != null)
                    {
                        string processName = targetInstance["Name"]?.ToString() ?? "未知进程";
                        WriteCrashLog($"WMI检测到进程{processName}(ID:{currentProcessId})已终止");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理WMI事件时出错: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // 新增：Windows控制台控制处理程序
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        private delegate bool ConsoleCtrlDelegate(int ctrlType);

        private static bool ConsoleCtrlHandler(int ctrlType)
        {
            string eventType = "未知控制类型";

            // 使用传统switch语句替代switch表达式
            switch (ctrlType)
            {
                case 0:
                    eventType = "CTRL_C_EVENT";
                    break;
                case 1:
                    eventType = "CTRL_BREAK_EVENT";
                    break;
                case 2:
                    eventType = "CTRL_CLOSE_EVENT";
                    break;
                case 5:
                    eventType = "CTRL_LOGOFF_EVENT";
                    break;
                case 6:
                    eventType = "CTRL_SHUTDOWN_EVENT";
                    break;
                default:
                    eventType = $"未知控制类型({ctrlType})";
                    break;
            }

            WriteCrashLog($"接收到系统控制信号: {eventType}");

            // 返回true表示已处理该事件
            return false;
        }

        // 新增：系统会话结束事件处理
        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            string reason = e.Reason == SessionEndReasons.Logoff ? "用户注销" : "系统关机";
            WriteCrashLog($"系统会话即将结束: {reason}");
            DeviceIdentifier.SaveUsageStatsOnShutdown();
        }

        // 新增：控制台取消事件处理
        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            WriteCrashLog($"接收到控制台中断信号: {e.SpecialKey}");
            e.Cancel = true; // 取消默认处理
        }

        // 新增：处理非UI线程的未处理异常
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                string errorMessage = exception?.ToString() ?? "未知异常";
                lastErrorMessage = errorMessage;

                WriteCrashLog($"捕获到未处理的异常: {errorMessage}");

                if (e.IsTerminating)
                {
                    WriteCrashLog("应用程序即将终止");
                }
            }
            catch (Exception ex)
            {
                // 尝试在最后时刻记录错误
                try
                {
                    File.AppendAllText(
                        Path.Combine(crashLogFile, $"critical_error_{DateTime.Now:yyyyMMdd_HHmmss}.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 记录未处理异常时发生错误: {ex.Message}\r\n"
                    );
                }
                catch { }
            }
        }

        // 新增：处理进程退出事件
        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            TimeSpan runDuration = DateTime.Now - appStartTime;
            string durationText = FormatTimeSpan(runDuration);
            WriteCrashLog($"应用程序退出，运行时长: {durationText}");

            // 如果有最后错误消息，记录到日志
            if (!string.IsNullOrEmpty(lastErrorMessage))
            {
                WriteCrashLog($"最后错误信息: {lastErrorMessage}");
            }
        }

        // 新增：格式化时间跨度
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.Days}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}分钟 {timeSpan.Seconds}秒";
            }
            else
            {
                return $"{timeSpan.Seconds}秒";
            }
        }

        // 新增：记录崩溃日志
        private static void WriteCrashLog(string message)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(crashLogFile))
                {
                    Directory.CreateDirectory(crashLogFile);
                }

                string logFileName = Path.Combine(crashLogFile, $"crash_{DateTime.Now:yyyyMMdd}.log");

                // 收集系统状态信息
                string memoryUsage = (Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)) + " MB";
                string cpuTime = Process.GetCurrentProcess().TotalProcessorTime.ToString();
                string processUptime = FormatTimeSpan(DateTime.Now - Process.GetCurrentProcess().StartTime);

                string statusInfo = $"[内存: {memoryUsage}, CPU时间: {cpuTime}, 运行时长: {processUptime}]";

                // 写入日志
                File.AppendAllText(
                    logFileName,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PID:{currentProcessId}] {message}\r\n{statusInfo}\r\n\r\n"
                );

                // 同时记录到主日志
                LogHelper.WriteLogToFile(message, LogHelper.LogType.Error);
            }
            catch { }
        }

        // 增加字段保存崩溃后操作设置
        public static CrashActionType CrashAction = CrashActionType.SilentRestart;

        // 修正：允许静态调用
        public static void SyncCrashActionFromSettings()
        {
            try
            {
                // 优先从 Settings.json 直接读取
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    int crashAction = 0;
                    try { crashAction = (int)(obj["startup"]["crashAction"] ?? 0); } catch { }
                    CrashAction = (CrashActionType)crashAction;
                }
                // 兜底：从主窗口同步
                else if (Ink_Canvas.MainWindow.Settings != null && Ink_Canvas.MainWindow.Settings.Startup != null)
                {
                    CrashAction = (CrashActionType)Ink_Canvas.MainWindow.Settings.Startup.CrashAction;
                }
            }
            catch { }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Ink_Canvas.MainWindow.ShowNewMessage("抱歉，出现未预期的异常，可能导致 InkCanvasForClass 运行不稳定。\n建议保存墨迹后重启应用。");
            LogHelper.NewLog(e.Exception.ToString());

            // 新增：记录到崩溃日志
            lastErrorMessage = e.Exception.ToString();
            WriteCrashLog($"UI线程未处理异常: {e.Exception}");

            e.Handled = true;

            SyncCrashActionFromSettings(); // 新增：崩溃时同步最新设置

            if (CrashAction == CrashActionType.SilentRestart && !IsAppExitByUser)
            {
                StartupCount.Increment();
                if (StartupCount.GetCount() >= 5)
                {
                    MessageBox.Show("检测到程序已连续重启5次，已停止自动重启。请联系开发者或检查系统环境。", "重启次数过多", MessageBoxButton.OK, MessageBoxImage.Error);
                    StartupCount.Reset();
                    Environment.Exit(1);
                }
                try
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    Process.Start(exePath);
                }
                catch { }
                Environment.Exit(1);
            }
            // CrashActionType.NoAction 时不做处理
        }

        private TaskbarIcon _taskbar;

        void App_Startup(object sender, StartupEventArgs e)
        {
            // 初始化应用启动时间
            appStartTime = DateTime.Now;
            
            /*if (!StoreHelper.IsStoreApp) */
            RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

            LogHelper.NewLog(string.Format("Ink Canvas Starting (Version: {0})", Assembly.GetExecutingAssembly().GetName().Version));

            // 在应用启动时自动释放IACore相关DLL
            try
            {
                Helpers.IACoreDllExtractor.ExtractIACoreDlls();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放IACore DLL时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 记录应用启动（设备标识符）
            DeviceIdentifier.RecordAppLaunch();
            LogHelper.WriteLogToFile($"App | 设备ID: {DeviceIdentifier.GetDeviceId()}");
            LogHelper.WriteLogToFile($"App | 使用频率: {DeviceIdentifier.GetUsageFrequency()}");
            LogHelper.WriteLogToFile($"App | 更新优先级: {DeviceIdentifier.GetUpdatePriority()}");

            bool ret;
            mutex = new Mutex(true, "InkCanvasForClass CE", out ret);

            if (!ret && !e.Args.Contains("-m")) //-m multiple
            {
                LogHelper.NewLog("Detected existing instance");
                MessageBox.Show("已有一个程序实例正在运行");
                LogHelper.NewLog("Ink Canvas automatically closed");
                IsAppExitByUser = true; // 多开时标记为用户主动退出
                // 写入退出信号，确保看门狗不会重启
                try
                {
                    StartupCount.Reset();
                    File.WriteAllText(watchdogExitSignalFile, "exit");
                    if (watchdogProcess != null && !watchdogProcess.HasExited)
                    {
                        watchdogProcess.Kill();
                    }
                }
                catch { }
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
                        using (RegistryKey baseKey = Registry.CurrentUser.OpenSubKey(regPath))
                        {
                            if (baseKey == null)
                            {
                                LogHelper.WriteLogToFile($"注册表路径不存在: {regPath}", LogHelper.LogType.Warning);
                                // 尝试创建路径
                                try
                                {
                                    using (RegistryKey createKey = Registry.CurrentUser.CreateSubKey(regPath, true))
                                    {
                                        if (createKey != null)
                                        {
                                            createKey.SetValue("DisableProtectedView", 1, RegistryValueKind.DWord);
                                            LogHelper.WriteLogToFile($"创建并设置注册表路径: {regPath}");
                                        }
                                    }
                                }
                                catch (Exception createEx)
                                {
                                    LogHelper.WriteLogToFile($"创建注册表路径失败: {createEx.Message}", LogHelper.LogType.Error);
                                }
                                continue;
                            }

                            // 备份路径更改为软件根目录下的saves/RegistryBackups文件夹
                            string backupPath = Path.Combine(RootPath, "saves", "RegistryBackups");
                            LogHelper.WriteLogToFile($"备份路径: {backupPath}");

                            if (!Directory.Exists(backupPath))
                            {
                                Directory.CreateDirectory(backupPath);
                                LogHelper.WriteLogToFile($"创建备份目录: {backupPath}");
                            }

                            string backupFile = Path.Combine(backupPath, $"SecurityBackup_{version}_{DateTime.Now:yyyyMMddHHmmss}.reg");
                            LogHelper.WriteLogToFile($"创建备份文件: {backupFile}");

                            // 使用UTF8编码写入注册表文件
                            using (StreamWriter sw = new StreamWriter(backupFile, false, Encoding.UTF8))
                            {
                                sw.WriteLine("Windows Registry Editor Version 5.00\n");
                                sw.WriteLine();
                                sw.WriteLine($"[{Registry.CurrentUser.Name}\\{regPath}]");

                                foreach (string valueName in baseKey.GetValueNames())
                                {
                                    object value = baseKey.GetValue(valueName);
                                    sw.WriteLine($"\"{valueName}\"=dword:{((int)value):x8}");
                                    LogHelper.WriteLogToFile($"备份注册表值: {valueName} = {value}");
                                }
                            }

                            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(regPath, true))
                            {
                                // 仅在值不存在或不等于1时更新
                                object currentValue = key.GetValue("DisableProtectedView");
                                if (currentValue == null || (int)currentValue != 1)
                                {
                                    key.SetValue("DisableProtectedView", 1, RegistryValueKind.DWord);
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
                    }
                }

                // 处理Office 365的特殊路径
                TryModifyOffice365Registry();
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
                LogHelper.WriteLogToFile(ex.StackTrace);
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (SystemInformation.MouseWheelScrollLines == -1)
                    e.Handled = false;
                else
                    try
                    {
                        ScrollViewerEx SenderScrollViewer = (ScrollViewerEx)sender;
                        SenderScrollViewer.ScrollToVerticalOffset(SenderScrollViewer.VerticalOffset - e.Delta * 10 * SystemInformation.MouseWheelScrollLines / (double)120);
                        e.Handled = true;
                    }
                    catch { }
            }
            catch { }
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
                    SyncCrashActionFromSettings(); // 新增：心跳检测时同步最新设置
                    if (CrashAction == CrashActionType.SilentRestart)
                    {
                        StartupCount.Increment();
                        if (StartupCount.GetCount() >= 5)
                        {
                            MessageBox.Show("检测到程序已连续重启5次，已停止自动重启。请联系开发者或检查系统环境。", "重启次数过多", MessageBoxButton.OK, MessageBoxImage.Error);
                            StartupCount.Reset();
                            Environment.Exit(1);
                        }
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
                    // 主进程异常退出，自动重启前判断崩溃后操作
                    SyncCrashActionFromSettings(); // 新增：同步设置
                    if (CrashAction == CrashActionType.SilentRestart)
                    {
                        StartupCount.Increment();
                        if (StartupCount.GetCount() >= 5)
                        {
                            MessageBox.Show("检测到程序已连续重启5次，已停止自动重启。请联系开发者或检查系统环境。", "重启次数过多", MessageBoxButton.OK, MessageBoxImage.Error);
                            StartupCount.Reset();
                            Environment.Exit(1);
                        }
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        Process.Start(exePath);
                    }
                    // CrashActionType.NoAction 时不重启，直接退出
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
                // 新增：记录应用退出状态
                string exitType = IsAppExitByUser ? "用户主动退出" : "应用程序退出";
                WriteCrashLog($"{exitType}，退出代码: {e.ApplicationExitCode}");

                // 记录应用退出（设备标识符）
                try
                {
                    DeviceIdentifier.RecordAppExit();
                    LogHelper.WriteLogToFile($"App | 应用运行时长: {(DateTime.Now - appStartTime).TotalMinutes:F1}分钟");
                }
                catch (Exception deviceEx)
                {
                    LogHelper.WriteLogToFile($"记录设备标识符退出信息失败: {deviceEx.Message}", LogHelper.LogType.Error);
                }

                if (IsAppExitByUser)
                {
                    // 写入退出信号文件，通知看门狗正常退出
                    StartupCount.Reset();
                    File.WriteAllText(watchdogExitSignalFile, "exit");
                    if (watchdogProcess != null && !watchdogProcess.HasExited)
                    {
                        watchdogProcess.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                // 尝试记录最后的错误
                try
                {
                    LogHelper.WriteLogToFile($"退出处理时发生错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch { }
            }
        }

        /// <summary>
        /// 检查Office是否安装
        /// </summary>
        private bool IsOfficeInstalled()
        {
            try
            {
                // 检查多个可能的注册表路径
                // 1. 检查传统的Office版本
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office"))
                {
                    if (key != null && key.GetSubKeyNames().Any(name => name.Contains(".0")))
                    {
                        LogHelper.WriteLogToFile("检测到传统Office安装");
                        return true;
                    }
                }

                // 2. 检查64位注册表中的Office
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Microsoft\\Office"))
                {
                    if (key != null && key.GetSubKeyNames().Any(name => name.Contains(".0")))
                    {
                        LogHelper.WriteLogToFile("检测到64位注册表中的Office安装");
                        return true;
                    }
                }

                // 3. 检查Office 365/Click-to-Run安装
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\ClickToRun"))
                {
                    if (key != null)
                    {
                        LogHelper.WriteLogToFile("检测到Office 365 Click-to-Run");
                        return true;
                    }
                }

                // 4. 检查Office 365部署配置
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\15.0\\ClickToRun"))
                {
                    if (key != null)
                    {
                        LogHelper.WriteLogToFile("检测到Office 365 (15.0)");
                        return true;
                    }
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\16.0\\ClickToRun"))
                {
                    if (key != null)
                    {
                        LogHelper.WriteLogToFile("检测到Office 365 (16.0)");
                        return true;
                    }
                }

                // 5. 检查Office 365零售订阅信息
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\ClickToRun\\Configuration"))
                {
                    if (key != null)
                    {
                        LogHelper.WriteLogToFile("检测到Office 365配置");
                        return true;
                    }
                }

                LogHelper.WriteLogToFile("未检测到任何Office安装", LogHelper.LogType.Warning);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查Office安装时出错: {ex.Message}", LogHelper.LogType.Error);
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
            MessageBox.Show(message, "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // 检查Office 365的特殊路径
                CheckOffice365Versions(versions);

                // 如果没有找到任何版本，添加默认的Office 365版本号
                if (versions.Count == 0 && IsOffice365Installed())
                {
                    versions.Add("16.0");
                    LogHelper.WriteLogToFile("未找到具体版本，添加默认Office 365版本: 16.0");
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

        /// <summary>
        /// 检测Office 365是否已安装
        /// </summary>
        private bool IsOffice365Installed()
        {
            try
            {
                // 检查多个Office 365特定路径
                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\ClickToRun"))
                {
                    if (key != null)
                        return true;
                }

                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\15.0\\ClickToRun"))
                {
                    if (key != null)
                        return true;
                }

                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\16.0\\ClickToRun"))
                {
                    if (key != null)
                        return true;
                }

                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\ClickToRun\\Configuration"))
                {
                    if (key != null)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查Office 365特有的版本信息
        /// </summary>
        private void CheckOffice365Versions(List<string> versions)
        {
            try
            {
                // 检查Click-to-Run版本路径
                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Office\\ClickToRun\\Configuration"))
                {
                    if (key != null)
                    {
                        var platformVersion = key.GetValue("Platform") as string;
                        var clickToRunVersion = key.GetValue("VersionToReport") as string;

                        if (!string.IsNullOrEmpty(platformVersion))
                        {
                            var majorVersion = platformVersion.Split('.').FirstOrDefault();
                            if (!string.IsNullOrEmpty(majorVersion) && !versions.Contains($"{majorVersion}.0"))
                            {
                                versions.Add($"{majorVersion}.0");
                                LogHelper.WriteLogToFile($"在Office 365配置中找到平台版本: {majorVersion}.0");
                            }
                        }

                        if (!string.IsNullOrEmpty(clickToRunVersion))
                        {
                            var majorVersion = clickToRunVersion.Split('.').FirstOrDefault();
                            if (!string.IsNullOrEmpty(majorVersion) && !versions.Contains($"{majorVersion}.0"))
                            {
                                versions.Add($"{majorVersion}.0");
                                LogHelper.WriteLogToFile($"在Office 365配置中找到报告版本: {majorVersion}.0");
                            }
                        }
                    }
                }

                // 检查安装路径来确认版本
                var possibleVersions = new[] { "15.0", "16.0" }; // Office 2013 (15.0) 和 Office 2016/2019/365 (16.0)
                foreach (var version in possibleVersions)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey($"Software\\Microsoft\\Office\\{version}\\Common\\InstallRoot"))
                    {
                        if (key != null && key.GetValue("Path") != null && !versions.Contains(version))
                        {
                            versions.Add(version);
                            LogHelper.WriteLogToFile($"在InstallRoot中找到Office版本: {version}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查Office 365版本时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 尝试修改Office 365的特殊注册表路径
        /// </summary>
        private void TryModifyOffice365Registry()
        {
            try
            {
                // 准备备份目录
                string backupPath = Path.Combine(RootPath, "saves", "RegistryBackups");
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                    LogHelper.WriteLogToFile($"创建Office 365备份目录: {backupPath}");
                }

                // 检查Office 365 Outlook和PowerPoint的特定路径
                string[] apps = { "outlook", "powerpoint" };

                foreach (var app in apps)
                {
                    // 检查用户级别的注册表
                    string regPath = $"Software\\Microsoft\\Office\\16.0\\{app}\\Security";
                    LogHelper.WriteLogToFile($"检查Office 365特定应用注册表: {regPath}");

                    try
                    {
                        // 先检查是否存在该路径
                        using (var baseKey = Registry.CurrentUser.OpenSubKey(regPath))
                        {
                            // 如果路径存在，先备份
                            if (baseKey != null)
                            {
                                string backupFile = Path.Combine(backupPath, $"SecurityBackup_365_{app}_{DateTime.Now:yyyyMMddHHmmss}.reg");
                                LogHelper.WriteLogToFile($"创建Office 365 {app}备份文件: {backupFile}");

                                // 使用UTF8编码写入注册表文件
                                using (StreamWriter sw = new StreamWriter(backupFile, false, Encoding.UTF8))
                                {
                                    sw.WriteLine("Windows Registry Editor Version 5.00\n");
                                    sw.WriteLine();
                                    sw.WriteLine($"[{Registry.CurrentUser.Name}\\{regPath}]");

                                    foreach (string valueName in baseKey.GetValueNames())
                                    {
                                        object value = baseKey.GetValue(valueName);
                                        sw.WriteLine($"\"{valueName}\"=dword:{((int)value):x8}");
                                        LogHelper.WriteLogToFile($"备份Office 365 {app}注册表值: {valueName} = {value}");
                                    }
                                }
                            }
                        }

                        // 修改或创建注册表项
                        using (var key = Registry.CurrentUser.CreateSubKey(regPath, true))
                        {
                            if (key != null)
                            {
                                object currentValue = key.GetValue("DisableProtectedView");
                                if (currentValue == null || (int)currentValue != 1)
                                {
                                    key.SetValue("DisableProtectedView", 1, RegistryValueKind.DWord);
                                    LogHelper.WriteLogToFile($"Office 365 {app} 注册表值已设置: DisableProtectedView = 1");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"Office 365 {app} 注册表值已存在且无需更改");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"修改 {app} 注册表时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }

                // 尝试通过Office信任中心路径修改
                string trustCenterPath = "Software\\Microsoft\\Office\\16.0\\Common\\Security\\FileValidation";
                LogHelper.WriteLogToFile($"检查信任中心路径: {trustCenterPath}");

                try
                {
                    // 先检查是否存在该路径
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(trustCenterPath))
                    {
                        // 如果路径存在，先备份
                        if (baseKey != null)
                        {
                            string backupFile = Path.Combine(backupPath, $"SecurityBackup_365_TrustCenter_{DateTime.Now:yyyyMMddHHmmss}.reg");
                            LogHelper.WriteLogToFile($"创建信任中心备份文件: {backupFile}");

                            // 使用UTF8编码写入注册表文件
                            using (StreamWriter sw = new StreamWriter(backupFile, false, Encoding.UTF8))
                            {
                                sw.WriteLine("Windows Registry Editor Version 5.00\n");
                                sw.WriteLine();
                                sw.WriteLine($"[{Registry.CurrentUser.Name}\\{trustCenterPath}]");

                                foreach (string valueName in baseKey.GetValueNames())
                                {
                                    object value = baseKey.GetValue(valueName);
                                    sw.WriteLine($"\"{valueName}\"=dword:{((int)value):x8}");
                                    LogHelper.WriteLogToFile($"备份信任中心注册表值: {valueName} = {value}");
                                }
                            }
                        }
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey(trustCenterPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisableEditFromPV", 1, RegistryValueKind.DWord);
                            LogHelper.WriteLogToFile("已禁用受保护视图中的编辑");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"修改信任中心路径时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                // 尝试修改EnableEditWhileViewingPolicy
                string policyPath = "Software\\Policies\\Microsoft\\Office\\16.0\\Common\\Security";
                try
                {
                    // 先检查是否存在该路径
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(policyPath))
                    {
                        // 如果路径存在，先备份
                        if (baseKey != null)
                        {
                            string backupFile = Path.Combine(backupPath, $"SecurityBackup_365_Policy_{DateTime.Now:yyyyMMddHHmmss}.reg");
                            LogHelper.WriteLogToFile($"创建策略备份文件: {backupFile}");

                            // 使用UTF8编码写入注册表文件
                            using (StreamWriter sw = new StreamWriter(backupFile, false, Encoding.UTF8))
                            {
                                sw.WriteLine("Windows Registry Editor Version 5.00\n");
                                sw.WriteLine();
                                sw.WriteLine($"[{Registry.CurrentUser.Name}\\{policyPath}]");

                                foreach (string valueName in baseKey.GetValueNames())
                                {
                                    object value = baseKey.GetValue(valueName);
                                    sw.WriteLine($"\"{valueName}\"=dword:{((int)value):x8}");
                                    LogHelper.WriteLogToFile($"备份策略注册表值: {valueName} = {value}");
                                }
                            }
                        }
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey(policyPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("EnableEditWhileViewingPolicy", 1, RegistryValueKind.DWord);
                            LogHelper.WriteLogToFile("已启用查看时编辑策略");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"修改策略路径时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"修改Office 365注册表时发生未知错误: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
