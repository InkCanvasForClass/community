using Hardcodet.Wpf.TaskbarNotification;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using SplashScreen = Ink_Canvas.Windows.SplashScreen;
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
        public static string RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        // 新增：标记是否通过--board参数启动
        public static bool StartWithBoardMode = false;
        // 新增：标记是否通过--show参数启动
        public static bool StartWithShowMode = false;
        // 新增：保存看门狗进程对象
        public static Process watchdogProcess;
        // 新增：标记是否为软件内主动退出
        public static bool IsAppExitByUser;
        // 新增：标记是否启用了UIA置顶功能
        public static bool IsUIAccessTopMostEnabled;
        // 新增：退出信号文件路径
        private static string watchdogExitSignalFile = Path.Combine(Path.GetTempPath(), "icc_watchdog_exit_" + Process.GetCurrentProcess().Id + ".flag");
        // 新增：崩溃日志文件路径
        private static string crashLogFile = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "Crashes");
        // 新增：进程ID
        private static int currentProcessId = Process.GetCurrentProcess().Id;
        // 新增：应用启动时间
        internal static DateTime appStartTime { get; private set; }
        // 新增：最后一次错误信息
        private static string lastErrorMessage = string.Empty;
        // 新增：是否已初始化崩溃监听器
        private static bool crashListenersInitialized;
        // 新增：启动画面相关
        private static SplashScreen _splashScreen;
        private static bool _isSplashScreenShown = false;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        public App()
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID("InkCanvasForClass.CE");
            }
            catch
            {
            }

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

            // 初始化全局异常和进程结束处理
            InitializeCrashListeners();

            // 仅在崩溃后操作为静默重启时才启动看门狗
            // 在更新模式下不启动看门狗，避免干扰更新流程
            args = Environment.GetCommandLineArgs();
            bool isUpdateMode = args.Contains("--update-mode");
            bool isFinalApp = args.Contains("--final-app");

            if (CrashAction == CrashActionType.SilentRestart && !isUpdateMode && !isFinalApp)
            {
                StartWatchdogIfNeeded();
            }
            Exit += App_Exit; // 注册退出事件
        }

        // 配置TLS协议以支持Windows 7
        private void ConfigureTlsForWindows7()
        {
            try
            {
                // 检测操作系统版本
                var osVersion = Environment.OSVersion;
                bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

                if (isWindows7)
                {

                    // 启用所有TLS版本以支持Windows 7
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                    // 配置ServicePointManager以支持Windows 7
                    ServicePointManager.DefaultConnectionLimit = 10;
                    ServicePointManager.Expect100Continue = false;
                    ServicePointManager.UseNagleAlgorithm = false;

                }
                else
                {
                    // 对于更新的Windows版本，不进行任何TLS配置，使用系统默认设置
                }
            }
            catch (Exception)
            {
            }
        }

        // 初始化崩溃监听器
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

        // 动态加载WMI监控
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

        // WMI事件处理方法
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

        // Windows控制台控制处理程序
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

        // 系统会话结束事件处理
        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            string reason = e.Reason == SessionEndReasons.Logoff ? "用户注销" : "系统关机";
            WriteCrashLog($"系统会话即将结束: {reason}");

            // 清理PowerPoint进程守护和悬浮窗拦截器
            try
            {
                // 获取主窗口实例
                var mainWindow = Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // 清理PowerPoint进程守护
                    var method = mainWindow.GetType().GetMethod("StopPowerPointProcessMonitoring",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(mainWindow, null);
                    WriteCrashLog("PowerPoint进程守护已在系统关机时清理");

                    // 清理悬浮窗拦截器
                    var interceptorField = mainWindow.GetType().GetField("_floatingWindowInterceptorManager",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var interceptorManager = interceptorField?.GetValue(mainWindow);
                    if (interceptorManager != null)
                    {
                        var disposeMethod = interceptorManager.GetType().GetMethod("Dispose");
                        disposeMethod?.Invoke(interceptorManager, null);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteCrashLog($"清理资源失败: {ex.Message}");
            }

            DeviceIdentifier.SaveUsageStatsOnShutdown();
        }

        // 控制台取消事件处理
        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            WriteCrashLog($"接收到控制台中断信号: {e.SpecialKey}");
            e.Cancel = true; // 取消默认处理
        }

        // 处理非UI线程的未处理异常
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;

                if (exception is InvalidOperationException invalidOpEx)
                {
                    string exceptionMessage = invalidOpEx.Message ?? "";
                    string exceptionStackTrace = invalidOpEx.StackTrace ?? "";

                    if (exceptionMessage.Contains("调用线程无法访问此对象") ||
                        exceptionMessage.Contains("because another thread owns it") ||
                        exceptionStackTrace.Contains("DynamicRenderer") ||
                        exceptionStackTrace.Contains("CompositionTarget.get_RootVisual"))
                    {
                        LogHelper.WriteLogToFile(
                            $"检测到DynamicRenderer线程访问异常: {invalidOpEx.Message}",
                            LogHelper.LogType.Warning
                        );
                        return;
                    }
                }

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
                    string timeStr = (appStartTime != default(DateTime) && appStartTime != DateTime.MinValue)
                        ? appStartTime.ToString("yyyy-MM-dd-HH-mm-ss")
                        : DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                    File.AppendAllText(
                        Path.Combine(crashLogFile, $"Crash_{timeStr}.txt"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 记录未处理异常时发生错误: {ex.Message}\r\n"
                    );
                }
                catch { }
            }
        }

        // 处理进程退出事件
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

        // 格式化时间跨度
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.Days}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
            }

            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}分钟 {timeSpan.Seconds}秒";
            }

            return $"{timeSpan.Seconds}秒";
        }

        public static void ShowSplashScreen()
        {
            if (_isSplashScreenShown)
            {
                LogHelper.WriteLogToFile("启动画面已经显示，跳过重复显示");
                return;
            }

            try
            {
                LogHelper.WriteLogToFile("开始创建启动画面...");
                _splashScreen = new SplashScreen();
                LogHelper.WriteLogToFile("启动画面对象创建成功，准备显示...");
                _splashScreen.Show();
                _isSplashScreenShown = true;
                LogHelper.WriteLogToFile("启动画面已显示");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示启动画面失败: {ex.Message}", LogHelper.LogType.Error);
                LogHelper.WriteLogToFile($"异常堆栈: {ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        // 关闭启动画面
        public static void CloseSplashScreen()
        {
            if (!_isSplashScreenShown || _splashScreen == null) return;

            try
            {
                _splashScreen.CloseSplashScreen();
                _isSplashScreenShown = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭启动画面失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 设置启动画面进度
        public static void SetSplashProgress(int progress)
        {
            if (_splashScreen != null)
            {
                _splashScreen.SetProgress(progress);
            }
        }

        // 设置启动画面消息
        public static void SetSplashMessage(string message)
        {
            if (_splashScreen != null)
            {
                _splashScreen.SetLoadingMessage(message);
            }
        }

        private static bool ShouldShowSplashScreen()
        {
            try
            {
                // 检查设置文件中的启动动画开关
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    if (obj?["appearance"]?["enableSplashScreen"] != null)
                    {
                        return (bool)obj["appearance"]["enableSplashScreen"];
                    }
                }

                // 如果设置文件不存在或没有该设置，返回默认值false
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查启动动画设置失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        // 记录崩溃日志
        private static void WriteCrashLog(string message)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(crashLogFile))
                {
                    Directory.CreateDirectory(crashLogFile);
                }

                string appStartTimeStr = (appStartTime != default(DateTime) && appStartTime != DateTime.MinValue)
                    ? appStartTime.ToString("yyyy-MM-dd-HH-mm-ss")
                    : DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                string logFileName = Path.Combine(crashLogFile, $"Crash_{appStartTimeStr}.txt");

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

        public static void SyncCrashActionFromSettings()
        {
            try
            {
                // 优先从 Settings.json 直接读取
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    int crashAction = 0;
                    try { crashAction = (int)(obj["startup"]["crashAction"] ?? 0); } catch { }
                    CrashAction = (CrashActionType)crashAction;
                }
                // 从主窗口同步
                else if (Ink_Canvas.MainWindow.Settings != null && Ink_Canvas.MainWindow.Settings.Startup != null)
                {
                    CrashAction = (CrashActionType)Ink_Canvas.MainWindow.Settings.Startup.CrashAction;
                }
            }
            catch { }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // 检查是否是DynamicRenderer线程访问UI对象的已知问题
            if (e.Exception is InvalidOperationException invalidOpEx)
            {
                string exceptionMessage = invalidOpEx.Message ?? "";
                string exceptionStackTrace = invalidOpEx.StackTrace ?? "";

                // 检查是否是DynamicRenderer相关的线程访问问题
                if (exceptionMessage.Contains("调用线程无法访问此对象") ||
                    exceptionMessage.Contains("because another thread owns it") ||
                    exceptionStackTrace.Contains("DynamicRenderer") ||
                    exceptionStackTrace.Contains("CompositionTarget.get_RootVisual"))
                {
                    // 这是WPF InkCanvas的已知问题，DynamicRenderer的后台线程尝试访问UI对象
                    // 这个异常不会影响应用程序功能，可以安全地忽略
                    LogHelper.WriteLogToFile(
                        $"检测到DynamicRenderer线程访问异常（已安全处理）: {invalidOpEx.Message}",
                        LogHelper.LogType.Warning
                    );

                    // 标记为已处理，不显示错误消息，不触发重启
                    e.Handled = true;
                    return;
                }
            }

            Ink_Canvas.MainWindow.ShowNewMessage("抱歉，出现未预期的异常，可能导致 InkCanvasForClass 运行不稳定。\n建议保存墨迹后重启应用。");
            LogHelper.NewLog(e.Exception.ToString());

            // 记录到崩溃日志
            lastErrorMessage = e.Exception.ToString();
            WriteCrashLog($"UI线程未处理异常: {e.Exception}");

            e.Handled = true;

            SyncCrashActionFromSettings(); // 崩溃时同步最新设置

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

        async void App_Startup(object sender, StartupEventArgs e)
        {
            // 初始化应用启动时间
            appStartTime = DateTime.Now;

            // 根据设置决定是否显示启动画面
            if (ShouldShowSplashScreen())
            {
                ShowSplashScreen();
                SetSplashMessage("正在启动 Ink Canvas...");
                SetSplashProgress(20);
                await Task.Delay(500);

                // 强制刷新UI，确保启动画面显示
                Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            }

            System.Threading.Thread.Sleep(500);
            RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

            LogHelper.NewLog(string.Format("Ink Canvas Starting (Version: {0})", Assembly.GetExecutingAssembly().GetName().Version));

            // 检查是否为最终应用启动（更新后的应用）
            bool isFinalApp = e.Args.Contains("--final-app");
            bool skipMutexCheck = e.Args.Contains("--skip-mutex-check");

            // 检查是否通过--board参数启动
            bool hasBoardArg = e.Args.Contains("--board");
            if (hasBoardArg)
            {
                StartWithBoardMode = true;
                LogHelper.WriteLogToFile("App | 检测到--board参数，将直接进入白板模式");
            }

            // 检查是否通过--show参数启动
            bool hasShowArg = e.Args.Contains("--show");
            if (hasShowArg)
            {
                StartWithShowMode = true;
                LogHelper.WriteLogToFile("App | 检测到--show参数，将退出收纳模式并恢复浮动栏");
            }

            // 记录最终应用启动状态
            if (isFinalApp)
            {
                LogHelper.WriteLogToFile("App | 检测到最终应用启动（更新后的应用）");
            }

            // 释放IACore相关DLL
            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在初始化组件...");
                SetSplashProgress(40);
                await Task.Delay(500);
            }
            try
            {
                IACoreDllExtractor.ExtractIACoreDlls();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放IACore DLL时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 释放UIAccess DLL
            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在初始化组件...");
                SetSplashProgress(50);
                await Task.Delay(300);
            }
            try
            {
                UIAccessDllExtractor.ExtractUIAccessDlls();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放UIAccess DLL时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 记录应用启动（设备标识符）
            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在加载配置...");
                SetSplashProgress(60);
                await Task.Delay(500);
            }
            DeviceIdentifier.RecordAppLaunch();
            LogHelper.WriteLogToFile($"App | 设备ID: {DeviceIdentifier.GetDeviceId()}");
            LogHelper.WriteLogToFile($"App | 使用频率: {DeviceIdentifier.GetUsageFrequency()}");
            LogHelper.WriteLogToFile($"App | 更新优先级: {DeviceIdentifier.GetUpdatePriority()}");

            // 处理更新模式启动
            bool isUpdateMode = AutoUpdateHelper.HandleUpdateModeStartup(e.Args);

            // 如果是更新模式，不显示主窗口但保持应用运行
            if (isUpdateMode)
            {
                LogHelper.WriteLogToFile("App | 检测到更新模式，跳过主窗口显示，保持应用运行");
                return;
            }

            // 检查是否存在更新标记文件
            string updateMarkerFile = Path.Combine(RootPath, "update_in_progress.tmp");
            bool isUpdateInProgress = false;

            // 检查是否以更新模式启动
            isUpdateMode = e.Args.Contains("--update-mode");

            // 如果是最终应用启动，立即清理更新标记文件
            if (isFinalApp)
            {
                try
                {
                    if (File.Exists(updateMarkerFile))
                    {
                        File.Delete(updateMarkerFile);
                        LogHelper.WriteLogToFile("App | 最终应用启动，清理更新标记文件");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"App | 清理更新标记文件失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        LogHelper.WriteLogToFile("App | 最终应用启动，删除AutoUpdate文件夹");
                        AutoUpdateHelper.DeleteUpdatesFolder();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"App | 删除AutoUpdate文件夹失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                });
            }

            // 如果不是最终应用启动，才检查更新标记文件
            if (!isFinalApp && File.Exists(updateMarkerFile))
            {
                try
                {
                    string updateProcessIdStr = File.ReadAllText(updateMarkerFile).Trim();
                    if (int.TryParse(updateProcessIdStr, out int updateProcessId))
                    {
                        LogHelper.WriteLogToFile($"App | 检测到更新标记文件，更新进程ID: {updateProcessId}");

                        // 检查更新进程是否还在运行
                        try
                        {
                            Process updateProcess = Process.GetProcessById(updateProcessId);
                            if (!updateProcess.HasExited)
                            {
                                LogHelper.WriteLogToFile("App | 更新进程仍在运行，等待更新完成");
                                isUpdateInProgress = true;

                                // 等待更新进程完成
                                int waitCount = 0;
                                const int maxWaitCount = 10; // 减少等待时间到10秒

                                while (waitCount < maxWaitCount && !updateProcess.HasExited)
                                {
                                    Thread.Sleep(500); // 减少等待间隔到500ms
                                    waitCount++;
                                    LogHelper.WriteLogToFile($"App | 等待更新进程完成... ({waitCount}/{maxWaitCount})");
                                }

                                if (updateProcess.HasExited)
                                {
                                    LogHelper.WriteLogToFile("App | 更新进程已结束");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile("App | 等待更新进程超时，强制清理", LogHelper.LogType.Warning);
                                    // 超时后强制清理标记文件
                                    try
                                    {
                                        if (File.Exists(updateMarkerFile))
                                        {
                                            File.Delete(updateMarkerFile);
                                            LogHelper.WriteLogToFile("App | 强制清理更新标记文件");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.WriteLogToFile($"App | 强制清理更新标记文件失败: {ex.Message}", LogHelper.LogType.Warning);
                                    }
                                }
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("App | 更新进程已结束");
                            }
                        }
                        catch (ArgumentException)
                        {
                            LogHelper.WriteLogToFile("App | 更新进程已不存在");
                        }

                        // 无论更新进程是否还在运行，都清理标记文件
                        try
                        {
                            if (File.Exists(updateMarkerFile))
                            {
                                File.Delete(updateMarkerFile);
                                LogHelper.WriteLogToFile("App | 清理更新标记文件");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"App | 清理更新标记文件失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"App | 读取更新标记文件失败: {ex.Message}", LogHelper.LogType.Warning);
                    // 如果读取失败，也尝试删除标记文件
                    try
                    {
                        if (File.Exists(updateMarkerFile))
                        {
                            File.Delete(updateMarkerFile);
                            LogHelper.WriteLogToFile("App | 清理损坏的更新标记文件");
                        }
                    }
                    catch { }
                }
            }

            // 如果是更新过程、更新模式、最终应用或跳过Mutex检查，跳过Mutex检查
            if (!isUpdateInProgress && !isUpdateMode && !isFinalApp && !skipMutexCheck)
            {
                bool ret;
                mutex = new Mutex(true, "InkCanvasForClass CE", out ret);

                if (!ret && !e.Args.Contains("-m")) //-m multiple
                {
                    LogHelper.NewLog("Detected existing instance");

                    // 检查是否有.icstk文件参数
                    string icstkFile = FileAssociationManager.GetIcstkFileFromArgs(e.Args);
                    if (!string.IsNullOrEmpty(icstkFile))
                    {
                        LogHelper.WriteLogToFile($"检测到已运行实例，尝试通过IPC发送文件: {icstkFile}", LogHelper.LogType.Event);

                        // 尝试通过IPC发送文件路径给已运行实例
                        if (FileAssociationManager.TrySendFileToExistingInstance(icstkFile))
                        {
                            LogHelper.WriteLogToFile("文件路径已通过IPC发送给已运行实例", LogHelper.LogType.Event);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("通过IPC发送文件路径失败", LogHelper.LogType.Warning);
                        }
                    }
                    // 检查是否有--board参数
                    else if (hasBoardArg)
                    {
                        LogHelper.WriteLogToFile("检测到已运行实例且有--board参数，尝试通过IPC发送白板模式命令", LogHelper.LogType.Event);

                        // 尝试通过IPC发送白板模式命令给已运行实例
                        if (FileAssociationManager.TrySendBoardModeCommandToExistingInstance())
                        {
                            LogHelper.WriteLogToFile("白板模式命令已通过IPC发送给已运行实例", LogHelper.LogType.Event);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("通过IPC发送白板模式命令失败", LogHelper.LogType.Warning);
                        }
                    }
                    // 检查是否有--show参数
                    else if (hasShowArg)
                    {
                        LogHelper.WriteLogToFile("检测到已运行实例且有--show参数，尝试通过IPC发送展开浮动栏命令", LogHelper.LogType.Event);

                        // 尝试通过IPC发送展开浮动栏命令给已运行实例
                        if (FileAssociationManager.TrySendShowModeCommandToExistingInstance())
                        {
                            LogHelper.WriteLogToFile("展开浮动栏命令已通过IPC发送给已运行实例", LogHelper.LogType.Event);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("通过IPC发送展开浮动栏命令失败", LogHelper.LogType.Warning);
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("检测到已运行实例，但无文件参数", LogHelper.LogType.Event);
                    }

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
            }
            else
            {
                if (isUpdateMode)
                {
                    LogHelper.WriteLogToFile("App | 更新模式启动，跳过重复运行检测");
                }
                else if (isFinalApp)
                {
                    LogHelper.WriteLogToFile("App | 最终应用启动，跳过重复运行检测");
                }
                else if (skipMutexCheck)
                {
                    LogHelper.WriteLogToFile("App | 跳过Mutex检查模式启动，跳过重复运行检测");
                }
                else
                {
                    LogHelper.WriteLogToFile("App | 更新过程中，跳过重复运行检测");
                }

                // 在特殊模式下，创建一个临时的Mutex以避免其他检查出错
                string mutexName = isFinalApp ? "InkCanvasForClass CE Final" : "InkCanvasForClass CE Update";
                mutex = new Mutex(true, mutexName, out bool tempRet);

                // 额外等待一小段时间确保更新进程完全退出
                Thread.Sleep(1000);
                LogHelper.WriteLogToFile("App | 特殊模式等待完成，继续启动");
            }

            _taskbar = (TaskbarIcon)FindResource("TaskbarTrayIcon");

            StartArgs = e.Args;

            // 在非更新模式下创建主窗口
            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在初始化主界面...");
                SetSplashProgress(80);
                await Task.Delay(500);
            }
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // 主窗口加载完成后关闭启动画面
            mainWindow.Loaded += (s, args) =>
            {
                if (_isSplashScreenShown)
                {
                    SetSplashMessage("完成初始化...");
                    SetSplashProgress(80);
                    Task.Delay(300).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SetSplashMessage("启动完成！");
                            SetSplashProgress(100);
                            // 延迟关闭启动画面，让用户看到完成消息
                            Task.Delay(500).ContinueWith(__ =>
                            {
                                Dispatcher.Invoke(() => CloseSplashScreen());
                            });
                        });
                    });
                }
            };

            mainWindow.Show();

            // 注册.icstk文件关联
            try
            {
                LogHelper.WriteLogToFile("开始注册.icstk文件关联");
                FileAssociationManager.RegisterFileAssociation();
                FileAssociationManager.ShowFileAssociationStatus();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册文件关联时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 启动IPC监听器
            try
            {
                LogHelper.WriteLogToFile("启动IPC监听器");
                FileAssociationManager.StartIpcListener();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动IPC监听器时出错: {ex.Message}", LogHelper.LogType.Error);
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

        // 用于设置崩溃后操作类型
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
        public static void StartWatchdogIfNeeded()
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

        // 看门狗主逻辑
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
                    SyncCrashActionFromSettings(); // 同步设置

                    if (IsUIAccessTopMostEnabled)
                    {
                        Environment.Exit(0);
                    }

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
                // 记录应用退出状态
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
    }
}
