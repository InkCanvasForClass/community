using H.NotifyIcon;
using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using WpfUiCompat.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.UI.Accessibility;
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

        public void ReleaseMutexForRestart()
        {
            try
            {
                if (mutex != null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                    mutex = null;
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "释放互斥体失败（重启时）", LogHelper.LogType.Warning);
            }
        }

        public static string[] StartArgs;

        /// <summary>从文件关联双击打开的插件关联文件路径；插件加载完成后派发给注册该扩展名的插件。</summary>
        internal static string PendingPluginFilePath;
        public static string RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        /// <summary>
        /// 从 DispatcherOperation 提取回调委托的可读方法名。
        /// WPF 内部把委托存在私有字段里，反射枚举拿；失败退回 Priority 类别。
        /// </summary>
        private static string ExtractDispatcherOpName(System.Windows.Threading.DispatcherOperation op)
        {
            try
            {
                if (op == null)
                    return "<null>";
                // 反射枚举私有字段找 Delegate 类型字段（_callback / _method 等）。
                foreach (var field in op.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                {
                    var value = field.GetValue(op);
                    if (value is Delegate del && del.Method != null)
                        return $"{del.Method.Name} ({op.Priority})";
                }
                return $"{op.Priority}";
            }
            catch
            {
                try { return $"{op.Priority}"; }
                catch { return "<error>"; }
            }
        }
        // 新增：版本字符串（在 App_Startup 中计算赋值，形如 "1.7.18.0 (sha)"）
        public static string AppVersion = "";

        // 新增：标记是否通过--board参数启动
        public static bool StartWithBoardMode = false;
        // 新增：标记是否通过--show参数启动
        public static bool StartWithShowMode = false;
        // 当前启动模式由 Settings.json 在主窗口创建前确定。
        public static StartupMode CurrentStartupMode { get; private set; } = StartupMode.Default;
        public static bool IsDefaultStartupMode => CurrentStartupMode == StartupMode.Default;
        public static bool IsFasterStartupMode => CurrentStartupMode == StartupMode.Faster;
        public static bool IsFastestStartupMode => CurrentStartupMode == StartupMode.Fastest;
        // 新增：保存看门狗进程对象
        public static Process watchdogProcess;
        // 新增：标记是否为软件内主动退出
        public static bool IsAppExitByUser;
        // 新增：插件事件服务引用，App_Exit 时向插件广播 AppExiting
        private static Plugins.EventService _pluginEventService;
        // 新增：标记是否正在触发安装更新（用于跳过某些交互确认）
        public static bool IsUpdateInstalling;
        // 新增：标记是否启用了UIA置顶功能
        public static bool IsUIAccessTopMostEnabled;
        // 新增：标记是否正在显示 OOBE（首次启动向导），看门狗在此期间不判定为卡死/假死
        public static bool IsOobeShowing;
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
        private volatile bool powerPointShutdownCleanupCompleted;
        // 新增：是否已初始化崩溃监听器
        private static bool crashListenersInitialized;
        private static readonly object cpuUsageLock = new object();
        private static bool hasCpuUsageSample;
        private static ulong previousSystemIdleTime;
        private static ulong previousSystemKernelTime;
        private static ulong previousSystemUserTime;
        private static TimeSpan previousProcessTotalProcessorTime;
        private static DateTime previousCpuSampleTime = DateTime.MinValue;
        private static double? lastSystemCpuUsagePercent;
        private static double? lastProcessCpuUsagePercent;
        private UnhookWinEventSafeHandle processDestroyHook = new UnhookWinEventSafeHandle();
        private IntPtr monitoredMainWindowHandle = IntPtr.Zero;
        private bool mainWindowDestroyedLogged;
        private WINEVENTPROC processDestroyHookCallback;
        // 新增：启动画面相关
        private static SplashScreen _splashScreen;
        private static bool _isSplashScreenShown = false;
        // _pendingLocalizedResourceSet removed - using Strings.LoadAllToResources
        private static readonly Stopwatch startupStopwatch = new Stopwatch();
        private static readonly Stopwatch splashStopwatch = new Stopwatch();

        //[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        //private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        public App()
        {
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // 注意：此处显式禁用 Switch.System.Windows.Input.Stylus.EnablePointerSupport。
            // 启用该开关会让 WPF 使用 WM_POINTER 触摸栈，导致 DragMove() 和 DoDragDrop()
            // （gong-wpf-dragdrop 库内部使用）的模态消息循环无法接收触摸释放消息。
            // 在模拟触摸屏（UU 远程、spacedesk 等远程控制软件注入的虚拟触摸）下，
            // 拖动操作会进入假死状态，直到呼出鼠标或点击其他窗口生成真实鼠标消息才解除。
            // AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.EnablePointerSupport", true);

            try
            {
                PInvoke.SetCurrentProcessExplicitAppUserModelID("InkCanvasForClass.CE");
            }
            catch
            {
            }

            // 配置TLS协议以支持Windows 7
            ConfigureTlsForWindows7();

            // Dispatcher 长任务监控（诊断用）：
            // OperationStarted = 操作真正开始执行，记录执行时长（Completed-Started），
            // 排除 Background 优先级排队等待的虚高（posted→completed 含排队）。
            try
            {
                Dispatcher.Hooks.OperationStarted += (s, e) =>
                {
                    var sw = Stopwatch.StartNew();
                    e.Operation.Completed += (_, __) =>
                    {
                        sw.Stop();
                        if (sw.ElapsedMilliseconds > 20)
                        {
                            var name = ExtractDispatcherOpName(e.Operation);
                            var elapsed = sw.Elapsed.TotalMilliseconds;
                            Debug.WriteLine($"Dispatcher Exec:{elapsed:F1}ms {name}");
                        }
                    };
                };
            }
            catch
            {
                // 监控失败不影响启动
            }

            // 如果是看门狗子进程，直接进入看门狗主循环并终止主流程
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && args[1] == "--watchdog")
            {
                RunWatchdogIfNeeded();
                Environment.Exit(0);
                return;
            }

            if (args.Contains("--enable-uia-topmost-helper"))
            {
                // 检查是否为原进程令牌模式（通过 --uia-source-pid 参数判断）
                uint sourcePid = 0;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--uia-source-pid", StringComparison.OrdinalIgnoreCase)
                        && uint.TryParse(args[i + 1], out uint parsedPid))
                    {
                        sourcePid = parsedPid;
                        break;
                    }
                }

                if (sourcePid != 0)
                {
                    Environment.Exit(UIAccessHelper.LaunchNormalUserWithUIAccessFromElevatedHelper_ProcessToken(sourcePid) ? 0 : 1);
                }
                else
                {
                    Environment.Exit(UIAccessHelper.LaunchNormalUserWithUIAccessFromElevatedHelper() ? 0 : 1);
                }
                return;
            }

            // CrashAction 的值将在 App_Startup 中通过缓存的 Settings.json 同步，
            // 构造函数中先用默认值（ShowCrashWindow），LoadSettings 运行后会被覆盖。

            // 注意：Exit 事件在 Application.Shutdown() 或 Application.Run() 正常返回时触发，
            // 用于释放 mutex、清理 IpcIACoreClient、卸载插件、写看门狗退出信号、记录设备退出等。
            // 若不挂载此事件，App_Exit 中已实现的所有清理与看门狗通知逻辑都不会执行，
            // 软件正常关闭后会被看门狗误判为崩溃并触发重复重启。
            Startup += App_Startup;
            SessionEnding += App_SessionEnding;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            Exit += App_Exit;
            StartHeartbeatMonitor();

            // 初始化全局异常和进程结束处理
            InitializeCrashListeners();

            // 看门狗必须在 App_Startup 读取 Settings.json 并同步 CrashAction 后启动。
            // 构造函数阶段仍使用默认 CrashAction，不能在此处做判断。
        }

        /// <summary>
        /// 统一注册所有插件服务到 PluginManager。
        /// </summary>
        private void RegisterPluginServices(MainWindow mainWindow)
        {
            var host = Plugins.PluginManager.Instance;

            // 外部演示源服务需要回注到 MainWindow：翻页条事件要能路由到当前激活的演示源。
            var presentationSourceService = new Plugins.Services.PresentationSourceService(mainWindow);
            mainWindow.AttachPresentationSourceService(presentationSourceService);

            // 保存事件服务引用，App_Exit 时向插件广播 AppExiting。
            _pluginEventService = new Plugins.EventService(mainWindow);

            var services = new (Type iface, object impl)[]
            {
                (typeof(Plugins.IAppRestartService),      new Plugins.AppRestartService()),
                (typeof(Plugins.IWindowService),          new Plugins.WindowService(mainWindow)),
                (typeof(Plugins.IPowerPointService),      new Plugins.PowerPointService(mainWindow)),
                // 复用 _pluginEventService 实例：App_Exit 广播 AppExiting 时，插件经 DI 拿到的就是同一实例
                (typeof(Plugins.IEventService),           _pluginEventService),
                (typeof(Plugins.ISettingsService),        new Plugins.SettingsService()),
                (typeof(Plugins.IHotkeyService),          new Plugins.HotkeyService(mainWindow.GlobalHotkeyManagerInstance)),
                (typeof(Plugins.INotificationService),    new Plugins.NotificationService(mainWindow)),
                (typeof(Plugins.IFileAssociationService),      new Plugins.FileAssociationService()),
                (typeof(Plugins.IWindowOverviewService),        new Plugins.WindowOverviewService(mainWindow.WindowOverviewModel)),
                (typeof(Plugins.ICanvasCompositionService),      new Plugins.CanvasCompositionService(mainWindow)),
                (typeof(Plugins.IPresentationSourceService),     presentationSourceService),
                (typeof(Plugins.ICanvasInkService),        new Plugins.CanvasInkService(mainWindow)),
                (typeof(Plugins.ICanvasElementService),    new Plugins.CanvasElementService(mainWindow)),
                (typeof(Plugins.IRecognitionService),      new Plugins.RecognitionService()),
                (typeof(Plugins.ITrayService),            new Plugins.TrayService(this)),
                (typeof(Plugins.IPluginUriService),       new Plugins.UriService(host)),
                (typeof(Plugins.IScreenshotService),      new Plugins.ScreenshotService(mainWindow)),
                (typeof(Plugins.IClipboardService),       new Plugins.ClipboardService()),
                (typeof(Plugins.IAppInfoService),         new Plugins.AppInfoService()),
                (typeof(Plugins.IScreenInfoService),      new Plugins.ScreenInfoService()),
                (typeof(Plugins.IUpdateService),          new Plugins.UpdateService()),
                (typeof(Plugins.IConfigProfileService),   new Plugins.ConfigProfileService()),
                (typeof(Plugins.IQuoteService),           new Plugins.QuoteService(mainWindow)),
                (typeof(Plugins.INameRosterService),      new Plugins.NameRosterService()),
                (typeof(Plugins.IInkEffectService),       new Plugins.InkEffectService(mainWindow)),
                (typeof(Plugins.ICameraService),          new Plugins.CameraService()),
                (typeof(Plugins.ISystemInfoService),      new Plugins.SystemInfoService()),
                (typeof(Plugins.IBackupService),          new Plugins.BackupService()),
                (typeof(Plugins.IFileDialogService),      new Plugins.FileDialogService(mainWindow)),
                (typeof(Plugins.IThemeService),           new Plugins.ThemeService()),
                (typeof(Plugins.IAnnouncementService),    new Plugins.AnnouncementService()),
                (typeof(Plugins.ICanvasAppearanceService),       new Plugins.CanvasAppearanceService(mainWindow)),
                (typeof(Plugins.IFocusInteractionService),       new Plugins.FocusInteractionService(mainWindow)),
                (typeof(Plugins.ICanvasLayerService),            new Plugins.CanvasLayerService(mainWindow)),
                (typeof(Plugins.ICanvasToolService),             new Plugins.CanvasToolService(mainWindow)),
                (typeof(Plugins.ICanvasLineConversionService),   new Plugins.CanvasLineConversionService(mainWindow)),
                (typeof(Plugins.ICanvasViewportService),         new Plugins.CanvasViewportService(mainWindow)),
                (typeof(Plugins.IUndoService),                   new Plugins.UndoService(mainWindow)),
                (typeof(Plugins.IWhiteboardDocumentService),     new Plugins.WhiteboardDocumentService(mainWindow)),
            };

            foreach (var (iface, impl) in services)
            {
                try
                {
                    host.RegisterService(iface, impl);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"Failed to register plugin service {iface.Name}: {ex.Message}", LogHelper.LogType.Error);
                }
            }
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
                    // Win7 专用旧式网络栈配置；net10 不再支持 Win7，此分支仅为历史遗留兼容而保留
#pragma warning disable SYSLIB0014
                    // 启用所有TLS版本以支持Windows 7
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                    // 配置ServicePointManager以支持Windows 7
                    ServicePointManager.DefaultConnectionLimit = 10;
                    ServicePointManager.Expect100Continue = false;
                    ServicePointManager.UseNagleAlgorithm = false;
#pragma warning restore SYSLIB0014
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

                PHANDLER_ROUTINE handlerRoutine = new PHANDLER_ROUTINE(ConsoleCtrlHandler);
                // 尝试注册Windows关闭消息监听
                PInvoke.SetConsoleCtrlHandler(handlerRoutine, true);

                try
                {
                    TrySetupTerminationMonitoring();
                }
                catch (Exception monitorEx)
                {
                    LogHelper.WriteLogToFile($"设置终止监控失败: {monitorEx.Message}", LogHelper.LogType.Warning);
                }

                crashListenersInitialized = true;
                LogHelper.WriteLogToFile("已初始化崩溃监听器");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化崩溃监听器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void TrySetupTerminationMonitoring()
        {
            try
            {
                processDestroyHookCallback = OnWinEventMainWindowDestroyed;

                // 等主窗口句柄可用后再开始监听
                Dispatcher.BeginInvoke(new Action(BindMainWindowLifecycle), DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化终止监控失败: {ex.GetType().FullName}: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void BindMainWindowLifecycle()
        {
            try
            {
                if (Current?.MainWindow == null)
                {
                    return;
                }

                Current.MainWindow.SourceInitialized -= MainWindow_SourceInitialized;
                Current.MainWindow.SourceInitialized += MainWindow_SourceInitialized;
            }
            catch (Exception)
            {
            }
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                if (!(sender is Window window))
                {
                    return;
                }

                monitoredMainWindowHandle = new WindowInteropHelper(window).Handle;
                if (monitoredMainWindowHandle == IntPtr.Zero)
                {
                    return;
                }

                RegisterMainWindowDestroyHook();
            }
            catch (Exception)
            {
            }
        }

        private void RegisterMainWindowDestroyHook()
        {
            if (!processDestroyHook.IsInvalid || monitoredMainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            processDestroyHook = PInvoke.SetWinEventHook(
                EVENT_OBJECT_DESTROY,
                EVENT_OBJECT_DESTROY,
                null,
                processDestroyHookCallback,
                (uint)currentProcessId,
                0,
                WINEVENT_OUTOFCONTEXT);

            if (!processDestroyHook.IsInvalid)
            {
                return;
            }
        }

        private void OnWinEventMainWindowDestroyed(HWINEVENTHOOK hWinEventHook, uint eventType, HWND hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EVENT_OBJECT_DESTROY || mainWindowDestroyedLogged)
            {
                return;
            }

            if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
            {
                return;
            }

            if (hwnd != monitoredMainWindowHandle || hwnd == IntPtr.Zero)
            {
                return;
            }

            mainWindowDestroyedLogged = true;
        }

        private void CleanupTerminationMonitoring()
        {
            try
            {
                if (!processDestroyHook.IsInvalid)
                {
                    PInvoke.UnhookWinEvent(new HWINEVENTHOOK(processDestroyHook.DangerousGetHandle()));
                    processDestroyHook = new UnhookWinEventSafeHandle();
                }
            }
            catch
            {
            }
        }

        // Windows控制台控制处理程序
        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        //[StructLayout(LayoutKind.Sequential)]
        //private struct FILETIME
        //{
        //    public uint dwLowDateTime;
        //    public uint dwHighDateTime;
        //}

        //private delegate bool ConsoleCtrlDelegate(int ctrlType);
        //private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const int OBJID_WINDOW = 0;
        private const int CHILDID_SELF = 0;

        //[DllImport("user32.dll")]
        //private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private static BOOL ConsoleCtrlHandler(uint ctrlType)
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

            if (!powerPointShutdownCleanupCompleted)
            {
                WriteCrashLog("PowerPoint模块等待WPF会话结束事件清理");
            }

            DeviceIdentifier.SaveUsageStatsOnShutdown();
        }

        private void App_SessionEnding(object sender, System.Windows.SessionEndingCancelEventArgs e)
        {
            CleanupPowerPointModuleForShutdown();
        }

        private void CleanupPowerPointModuleForShutdown()
        {
            if (powerPointShutdownCleanupCompleted) return;

            try
            {
                if (Current?.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UnloadPPTModuleForShutdown();
                    powerPointShutdownCleanupCompleted = true;
                    WriteCrashLog("PowerPoint模块已在系统关机时清理");
                }
            }
            catch (Exception ex)
            {
                WriteCrashLog($"清理资源失败: {ex.Message}");
            }
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

                if (exception is System.Runtime.InteropServices.COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    if (hr == 0x80004005 || hr == 0x8001010E || hr == 0x800706B5 ||
                        hr == 0x800706BA || hr == 0x8001010A || hr == 0x80010001 ||
                        hr == 0x80010108 || hr == 0x8001010D || hr == 0x800706BE)
                    {
                        LogHelper.WriteLogToFile(
                            $"非UI线程检测到PPT/WPS COM对象异常（已安全处理）: HR=0x{hr:X8}, {comEx.Message}",
                            LogHelper.LogType.Warning
                        );
                        return;
                    }
                }

                if (exception is System.Runtime.InteropServices.InvalidComObjectException)
                {
                    LogHelper.WriteLogToFile(
                        $"非UI线程检测到无效COM对象异常（已安全处理）: {exception.Message}",
                        LogHelper.LogType.Warning
                    );
                    return;
                }

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
                catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
            }
        }

        // 处理进程退出事件
        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CleanupTerminationMonitoring();
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

        private static void UpdateCpuUsageSnapshot()
        {
            try
            {
                if (!PInvoke.GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
                {
                    return;
                }

                DateTime sampleTime = DateTime.UtcNow;
                ulong currentIdleTime = ToUInt64(idleTime);
                ulong currentKernelTime = ToUInt64(kernelTime);
                ulong currentUserTime = ToUInt64(userTime);
                TimeSpan currentProcessCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

                lock (cpuUsageLock)
                {
                    if (hasCpuUsageSample)
                    {
                        ulong idleDelta = currentIdleTime - previousSystemIdleTime;
                        ulong kernelDelta = currentKernelTime - previousSystemKernelTime;
                        ulong userDelta = currentUserTime - previousSystemUserTime;
                        ulong totalDelta = kernelDelta + userDelta;

                        if (totalDelta > 0)
                        {
                            ulong busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
                            lastSystemCpuUsagePercent = Math.Clamp(busyDelta * 100d / totalDelta, 0d, 100d);
                        }

                        double elapsedSeconds = (sampleTime - previousCpuSampleTime).TotalSeconds;
                        double processCpuSeconds = (currentProcessCpuTime - previousProcessTotalProcessorTime).TotalSeconds;
                        if (elapsedSeconds > 0 && Environment.ProcessorCount > 0 && processCpuSeconds >= 0)
                        {
                            lastProcessCpuUsagePercent = Math.Clamp(processCpuSeconds / (elapsedSeconds * Environment.ProcessorCount) * 100d, 0d, 100d);
                        }
                    }

                    previousSystemIdleTime = currentIdleTime;
                    previousSystemKernelTime = currentKernelTime;
                    previousSystemUserTime = currentUserTime;
                    previousProcessTotalProcessorTime = currentProcessCpuTime;
                    previousCpuSampleTime = sampleTime;
                    hasCpuUsageSample = true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private static ulong ToUInt64(FILETIME fileTime)
        {
            return ((ulong)(uint)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
        }

        private static string FormatCpuUsagePercent(double? cpuUsagePercent)
        {
            return cpuUsagePercent.HasValue
                ? cpuUsagePercent.Value.ToString("F1", CultureInfo.InvariantCulture) + "%"
                : "采样不足";
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
                splashScreenStartTime = DateTime.Now;
                splashStopwatch.Restart();
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

        private static bool IsLaunchByFileOrUri(string[] args)
        {
            if (args == null || args.Length == 0) return false;
            foreach (string a in args)
            {
                if (string.IsNullOrWhiteSpace(a)) continue;
                string t = a.Trim();
                if (t.StartsWith("icc:", StringComparison.OrdinalIgnoreCase)) return true;
                if (Path.GetExtension(t).Equals(".icstk", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 记录崩溃日志
        private static void WriteCrashLog(string message)
        {
            try
            {
                UpdateCpuUsageSnapshot();

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
                var currentProcess = Process.GetCurrentProcess();
                string memoryUsage = (currentProcess.WorkingSet64 / (1024 * 1024)) + " MB";
                string cpuTime = currentProcess.TotalProcessorTime.ToString();
                string processUptime = FormatTimeSpan(DateTime.Now - currentProcess.StartTime);
                string systemCpuUsage = FormatCpuUsagePercent(lastSystemCpuUsagePercent);
                string processCpuUsage = FormatCpuUsagePercent(lastProcessCpuUsagePercent);

                string statusInfo = $"[内存: {memoryUsage}, CPU时间: {cpuTime}, 进程CPU占用: {processCpuUsage}, 系统CPU占用: {systemCpuUsage}, 运行时长: {processUptime}]";

                // 写入日志
                File.AppendAllText(
                    logFileName,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PID:{currentProcessId}] {message}\r\n{statusInfo}\r\n\r\n"
                );

                // 同时记录到主日志
                LogHelper.WriteLogToFile(message, LogHelper.LogType.Error);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        // 增加字段保存崩溃后操作设置
        public static CrashActionType CrashAction = CrashActionType.ShowCrashWindow;

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
                    int crashAction = 2;
                    try { crashAction = (int)(obj["startup"]["crashAction"] ?? 2); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    CrashAction = (CrashActionType)crashAction;
                }
                // 从主窗口同步
                else if (Ink_Canvas.MainWindow.Settings != null && Ink_Canvas.MainWindow.Settings.Startup != null)
                {
                    CrashAction = (CrashActionType)Ink_Canvas.MainWindow.Settings.Startup.CrashAction;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private static void SyncCrashActionFromParsed(dynamic parsedSettings)
        {
            try
            {
                int crashAction = 2;
                try { crashAction = (int)(parsedSettings?["startup"]?["crashAction"] ?? 2); } catch { }
                CrashAction = (CrashActionType)crashAction;
            }
            catch { }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is System.Runtime.InteropServices.COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x80004005 || hr == 0x8001010E || hr == 0x800706B5 ||
                    hr == 0x800706BA || hr == 0x8001010A || hr == 0x80010001 ||
                    hr == 0x80010108 || hr == 0x8001010D || hr == 0x800706BE)
                {
                    LogHelper.WriteLogToFile(
                        $"检测到PPT/WPS COM对象异常（已安全处理）: HR=0x{hr:X8}, {comEx.Message}",
                        LogHelper.LogType.Warning
                    );
                    e.Handled = true;
                    return;
                }
            }

            if (e.Exception is System.Runtime.InteropServices.InvalidComObjectException)
            {
                LogHelper.WriteLogToFile(
                    $"检测到无效COM对象异常（已安全处理）: {e.Exception.Message}",
                    LogHelper.LogType.Warning
                );
                e.Handled = true;
                return;
            }

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

            Ink_Canvas.MainWindow.ShowNewMessage(MainWindowStrings.Main_App_UnexpectedError);
            LogHelper.NewLog(e.Exception.ToString());

            // 记录到崩溃日志
            lastErrorMessage = e.Exception.ToString();
            WriteCrashLog($"UI线程未处理异常: {e.Exception}");

            e.Handled = true;

            SyncCrashActionFromSettings(); // 崩溃时同步最新设置

            if (CrashAction == CrashActionType.ShowCrashWindow)
            {
                try
                {
                    new CrashWindow
                    {
                        CrashInfo = lastErrorMessage
                    }.ShowDialog();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                return;
            }

            if (CrashAction == CrashActionType.SilentRestart && !IsAppExitByUser)
            {
                TryRestartWithBreaker($"UI线程未处理异常: {e.Exception.GetType().Name}");
            }
            // CrashActionType.NoAction 时不做处理
        }

        private TaskbarIcon _taskbar;

        /// <summary>
        /// 处理应用启动流程：根据命令行与设置显示启动画面、初始化组件与遥测、处理更新相关逻辑、单实例检查并在必要时通过 IPC 与已运行实例通信，最终创建并显示主窗口并启动文件关联与 IPC 监听器。
        /// </summary>
        /// <param name="sender">事件的发送者（通常为 Application 对象）。</param>
        /// <param name="e">启动事件参数；其 Args 可包含控制启动流程的标志，例如:
        /// - "--final-app"：表示这是更新后的最终应用启动（会清理更新标记等）
        /// - "--update-mode"：表示以更新模式启动（跳过主窗口显示）
        /// - "--board"：直接进入白板模式
        /// - "--show"：退出收纳模式并恢复浮动栏
        /// - "--skip-mutex-check"：跳过单实例互斥检查
        /// - "-m"：允许多实例启动
        /// 另外也可能包含以 "icc:" 开头的 URI 参数或 .icstk 文件路径用于启动时的 IPC 交互。</param>
        async void App_Startup(object sender, StartupEventArgs e)
        {
            appStartTime = DateTime.Now;

            // ARM64 渲染模式自适应：Surface Pro X 等 ARM64 设备走 WARP 软件光栅，
            // WPF 默认按 GPU 路径会触发无效 GPU 句柄的探测耗时与偶发 fallback。
            // 提前显式声明 SoftwareOnly，让 InkCanvas/浮动栏/墨迹预览的渲染直接走软件，
            // 减少启动抖动与首帧延迟。x86/x64 不动。
            // 注意：RenderOptions 位于 System.Windows.Media，RenderMode enum 位于
            // System.Windows.Interop；本文件已 using System.Windows.Interop，
            // 引用 RenderMode 直接走短名即可。
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                LogHelper.WriteLogToFile("App | ARM64 detected, RenderMode=SoftwareOnly");
            }

            appStartupStartTime = DateTime.Now;
            startupStopwatch.Restart();

            // 启动阶段跳过昂贵的 StackTrace 采集
            LogHelper.SuppressCallerInfo = true;

            // 一次性读取并解析 Settings.json，避免重复 I/O + dynamic 反序列化
            dynamic parsedSettings = ReadSettingsJsonOnce();

            // 从缓存设置同步 CrashAction（替代原构造函数中的 SyncCrashActionFromSettings）
            SyncCrashActionFromParsed(parsedSettings);
            CurrentStartupMode = GetStartupModeFromParsed(parsedSettings);
            LogHelper.WriteLogToFile($"App | 启动模式: {CurrentStartupMode}");

            // 照片矫正加速模式：CUDA 模式下设置 OPENCV_OPENCL_DEVICE 强制使用 NVIDIA OpenCL 平台。
            // 必须在 OpenCV 第一次创建 UMat 之前设置才能生效（OpenCL 上下文进程级别单初始化）。
            ApplyPhotoCorrectionAccelerationFromParsed(parsedSettings);

            TryApplyPreferredLanguageFromParsedSettings(parsedSettings);

            // 应用 Windows 系统个性化强调色到全局主题管理器，使强调按钮与弹窗确认按钮自动匹配系统强调色
            ThemeHelper.ApplySystemAccentColor();

            // 根据设置决定是否显示启动画面（复用已解析的设置对象）
            if (ShouldShowSplashScreenFromParsed(parsedSettings) && !IsLaunchByFileOrUri(e.Args))
            {
                // 注入缓存 JSON 给 SplashScreen，避免其构造期间重复读取 + 解析 Settings.json
                SplashScreen.CachedSettingsJson = CachedSettingsJson;
                ShowSplashScreen();
                SetSplashMessage("正在启动 Ink Canvas...");
                SetSplashProgress(25);

                // 强制刷新UI，确保启动画面显示。
                // 注意：在 App 构造阶段同步调用 Dispatcher.Invoke 会让当前线程等待自身调度，
                // 形成可见卡顿甚至死锁。此处直接返回，依靠 Splash 自身 Loaded 事件完成首帧渲染即可。
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            if (IsDefaultStartupMode)
            {
                await Task.Delay(100);
            }

            RootPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string versionString = version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
            if (informationalVersion != null)
            {
                string infoVersion = informationalVersion.InformationalVersion;
                int lastDotIndex = infoVersion.LastIndexOf('.');
                if (lastDotIndex >= 0 && lastDotIndex < infoVersion.Length - 7)
                {
                    versionString += " (" + infoVersion.Substring(lastDotIndex + 1) + ")";
                }
            }
            AppVersion = versionString;
            LogHelper.NewLog(string.Format("Ink Canvas Starting (Version: {0})", versionString));

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


            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在加载配置...");
                SetSplashProgress(50);
                if (IsDefaultStartupMode)
                {
                    await Task.Delay(100);
                }
            }

            // 处理更新模式启动
            bool isUpdateMode = AutoUpdateHelper.HandleUpdateModeStartup(e.Args);

            // 配置已加载后再启动看门狗，避免使用默认的 CrashAction 覆盖用户设置。
            if (!isUpdateMode && !isFinalApp && CrashAction == CrashActionType.SilentRestart)
            {
                StartWatchdogIfNeeded();
            }

            // 如果是更新模式，不显示主窗口但保持应用运行
            if (isUpdateMode)
            {
                LogHelper.WriteLogToFile("App | 检测到更新模式，跳过主窗口显示，保持应用运行");
                return;
            }

            // 检查是否存在更新标记文件
            string updateMarkerFile = Path.Combine(RootPath, "update_in_progress.tmp");
            bool isUpdateInProgress = false;

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

                _ = Task.Run(async () =>
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
                    catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
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
                    // 检查是否有插件关联文件参数（文件关联打开）
                    else if (!string.IsNullOrEmpty(FileAssociationManager.GetPluginFileFromArgs(e.Args)))
                    {
                        string pluginFile = FileAssociationManager.GetPluginFileFromArgs(e.Args);
                        LogHelper.WriteLogToFile($"检测到已运行实例，尝试通过IPC发送插件关联文件: {pluginFile}", LogHelper.LogType.Event);

                        if (FileAssociationManager.TrySendPluginFileToExistingInstance(pluginFile))
                        {
                            LogHelper.WriteLogToFile("插件关联文件路径已通过IPC发送给已运行实例", LogHelper.LogType.Event);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("通过IPC发送插件关联文件路径失败", LogHelper.LogType.Warning);
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
                    // 检查是否有URI参数
                    else if (e.Args.Any(a => a.StartsWith("icc:", StringComparison.OrdinalIgnoreCase)))
                    {
                        string uriArg = e.Args.FirstOrDefault(a => a.StartsWith("icc:", StringComparison.OrdinalIgnoreCase));
                        LogHelper.WriteLogToFile($"检测到已运行实例且有URI参数: {uriArg}", LogHelper.LogType.Event);

                        // 尝试通过IPC发送URI命令给已运行实例
                        if (FileAssociationManager.TrySendUriCommandToExistingInstance(uriArg))
                        {
                            LogHelper.WriteLogToFile("URI命令已通过IPC发送给已运行实例", LogHelper.LogType.Event);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("通过IPC发送URI命令失败", LogHelper.LogType.Warning);
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
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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

                // 默认模式沿用 1.7.19.4 的等待时序；优化模式保留当前短等待。
                await Task.Delay(IsDefaultStartupMode ? 1000 : 100);
                LogHelper.WriteLogToFile("App | 特殊模式等待完成，继续启动");
            }

            _taskbar = (TaskbarIcon)FindResource("TaskbarTrayIcon");

            StartArgs = e.Args;

            // 文件关联双击打开插件关联文件：记录待打开路径，等插件加载完成后派发给注册该扩展名的插件。
            PendingPluginFilePath = FileAssociationManager.GetPluginFileFromArgs(e.Args);
            if (!string.IsNullOrEmpty(PendingPluginFilePath))
                LogHelper.WriteLogToFile($"App | 检测到插件文件关联启动，待插件加载后打开: {PendingPluginFilePath}", LogHelper.LogType.Event);

            // 在非更新模式下创建主窗口
            if (_isSplashScreenShown)
            {
                SetSplashMessage("正在初始化主界面...");
                SetSplashProgress(75);
            }
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // 最快模式将插件服务注册延迟到首帧显示之后，其他模式在显示主窗口前注册。
            if (!IsFastestStartupMode)
            {
                RegisterPluginServices(mainWindow);
            }

            // 主窗口加载完成后关闭启动画面
            mainWindow.Loaded += (s, args) =>
            {
                isStartupComplete = true;
                startupCompleteHeartbeat = DateTime.Now;

                // 启动完成，恢复日志调用栈采集
                LogHelper.SuppressCallerInfo = false;

                // 启动成功，重置崩溃重启计数器
                StartupCount.Reset();

                if (_isSplashScreenShown && splashStopwatch.IsRunning)
                {
                    LogHelper.WriteLogToFile($"启动完成心跳已记录，启动画面显示时长: {splashStopwatch.Elapsed.TotalSeconds:F2}秒");
                }
                else
                {
                    LogHelper.WriteLogToFile($"启动完成心跳已记录");
                }
                LogHelper.WriteLogToFile($"启动时长: {startupStopwatch.Elapsed.TotalSeconds:F2}秒");

                if (_isSplashScreenShown)
                {
                    SetSplashMessage("启动完成！");
                    SetSplashProgress(100);
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 延迟关闭启动画面，让用户看到完成消息
                            Task.Delay(100).ContinueWith(__ =>
                            {
                                Dispatcher.Invoke(() => CloseSplashScreen());
                            });
                        });
                    });
                }
            };

            mainWindow.Show();
            MemoryBreakdownHelper.StartAutomaticDumpMonitor();

            if (IsFastestStartupMode)
            {
                _ = RunFastestStartupPostRenderTasksAsync(mainWindow);
                _ = Dispatcher.BeginInvoke(new Action(() => _taskbar?.ForceCreate()), DispatcherPriority.ContextIdle);
            }
            else if (IsDefaultStartupMode)
            {
                WindowTopmostManager.Initialize(mainWindow);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(600);
                    Dispatcher.Invoke(() => _taskbar?.ForceCreate());
                });
            }
            else
            {
                WindowTopmostManager.Initialize(mainWindow, skipScan: true);
                _ = Task.Run(() => Dispatcher.Invoke(() => _taskbar?.ForceCreate()));
            }

            // 处理启动时的URI参数
            string startupUriArg = e.Args.FirstOrDefault(a => a.StartsWith("icc:", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(startupUriArg))
            {
                LogHelper.WriteLogToFile($"App | 处理启动URI参数: {startupUriArg}", LogHelper.LogType.Event);
                // 延迟一点执行，确保窗口初始化完成
                _ = Task.Delay(1000).ContinueWith(_ =>
                {
                    mainWindow.Dispatcher.Invoke(() =>
                    {
                        mainWindow.HandleUriCommand(startupUriArg);
                    });
                });
            }

            _ = RunDeferredStartupTasksAsync();
        }

        private async Task RunFastestStartupPostRenderTasksAsync(MainWindow mainWindow)
        {
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                await Task.Delay(1000);

                RegisterPluginServices(mainWindow);
                WindowTopmostManager.Initialize(mainWindow, skipScan: true);
                LogHelper.WriteLogToFile("App | 最快启动模式的应用级延迟任务已开始");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"App | 最快启动模式延迟初始化失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async Task RunDeferredStartupTasksAsync()
        {
            try
            {
                await Task.Delay(IsFastestStartupMode ? 1200 : 400);

                try
                {
                    await IACoreDllExtractor.ExtractIACoreDllsAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"释放IACore DLL时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                try
                {
                    var shapeMode = ShapeRecognitionRouter.FromSettingsInt(
                        Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.Settings?.InkToShape?.ShapeRecognitionEngine ?? 0);
                    if (!ShapeRecognitionRouter.ResolveUseWinRt(shapeMode) && IpcIACoreClient.Instance.IsHelperExecutableAvailable)
                    {
                        LogHelper.WriteLogToFile("启动 IACore IPC 辅助进程");
                        bool ipcStarted = IpcIACoreClient.Instance.Start();
                        LogHelper.WriteLogToFile($"IACore IPC 辅助进程{(ipcStarted ? "启动成功" : "启动失败")}");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"启动 IACore IPC 辅助进程时出错: {ex.Message}", LogHelper.LogType.Error);
                }

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

                try
                {
                    LogHelper.WriteLogToFile("启动IPC监听器");
                    FileAssociationManager.StartIpcListener();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"启动IPC监听器时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                try
                {
                    LogHelper.WriteLogToFile("初始化上传帮助类");
                    Helpers.UploadHelper.Initialize();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"初始化上传帮助类时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                try
                {
                    LogHelper.WriteLogToFile("开始加载插件");
                    PluginManager.Instance.InitializeAdvancedServices(PluginMarketService.Instance);
                    await PluginManager.Instance.LoadAllAsync();

                    // 主窗口在 Window_Loaded 阶段已先构建浮动栏；插件随后才在延迟任务中注册组件。
                    // 插件加载完成后必须重建一次，确保启动时立即显示插件工具栏项。
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (Current.MainWindow is MainWindow window)
                            window.RebuildToolbar();
                    }, DispatcherPriority.Loaded);

                    try
                    {
                        PluginManager.Instance.StartIpc();
                        LogHelper.WriteLogToFile("插件 IPC 总线已启动");
                    }
                    catch (Exception ipcEx)
                    {
                        LogHelper.WriteLogToFile($"启动插件 IPC 总线失败: {ipcEx.Message}", LogHelper.LogType.Warning);
                    }

                    // 文件关联双击插件关联文件：插件已加载（相关插件在 Initialize 注册了 "open" URI 处理器），
                    // 把路径派发给注册该扩展名的插件。不走 HandleUriCommand，避免受「启用 URI 协议」设置门禁。
                    if (!string.IsNullOrEmpty(PendingPluginFilePath))
                    {
                        try
                        {
                            string pending = PendingPluginFilePath;
                            PendingPluginFilePath = null;
                            bool handled = Helpers.FileAssociationManager.DispatchPluginFileOpen(pending);
                            if (handled)
                                LogHelper.WriteLogToFile($"App | 已派发插件关联文件: {pending}", LogHelper.LogType.Event);
                            else
                                LogHelper.WriteLogToFile($"App | 派发插件关联文件失败（插件未加载或未注册处理器）: {pending}", LogHelper.LogType.Warning);
                        }
                        catch (Exception pluginFileEx)
                        {
                            LogHelper.WriteLogToFile($"App | 派发插件关联文件失败: {pluginFileEx.Message}", LogHelper.LogType.Error);
                        }
                    }

                    LogHelper.WriteLogToFile(string.Format("插件加载完成，共加载 {0} 个插件", PluginManager.Instance.Plugins.Count));
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(string.Format("加载插件时出错: {0}", ex.Message), LogHelper.LogType.Error);
                }

                try
                {
                    DeviceIdentifier.RecordAppLaunch();
                    var systemVersion = DeviceIdentifier.GetSystemVersion();
                    LogHelper.WriteLogToFile($"App | 系统版本: {systemVersion}");
                    LogHelper.WriteLogToFile($"App | 设备ID: {DeviceIdentifier.GetDeviceId()}");
                    LogHelper.WriteLogToFile($"App | 使用频率: {DeviceIdentifier.GetUsageFrequency()}");
                    LogHelper.WriteLogToFile($"App | 更新优先级: {DeviceIdentifier.GetUpdatePriority()}");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"App | 初始化设备统计与遥测标签失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动阶段任务执行失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static dynamic _cachedParsedSettings;
        /// <summary>
        /// 缓存 Settings.json 原始文本，供 LoadSettings 复用，避免启动阶段重复磁盘 I/O。
        /// </summary>
        internal static string CachedSettingsJson { get; private set; }

        /// <summary>
        /// 使用已规范化的设置内容更新启动缓存，确保同一启动流程中的后续设置加载不会再次使用旧配置。
        /// </summary>
        internal static void UpdateCachedSettingsJson(string json)
        {
            CachedSettingsJson = json;
            _cachedParsedSettings = JsonConvert.DeserializeObject(json);
        }

        /// <summary>
        /// 一次性读取并缓存 Settings.json 的解析结果，避免启动阶段重复 I/O + dynamic 反序列化。
        /// 同时缓存原始 JSON 文本供 LoadSettings 使用。
        /// </summary>
        private static dynamic ReadSettingsJsonOnce()
        {
            if (_cachedParsedSettings != null) return _cachedParsedSettings;
            try
            {
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    UpdateCachedSettingsJson(json);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"读取 Settings.json 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return _cachedParsedSettings;
        }

        private static StartupMode GetStartupModeFromParsed(dynamic parsedSettings)
        {
            try
            {
                string startupModeValue = parsedSettings?["startup"]?["startupMode"]?.ToString();
                if (!string.IsNullOrWhiteSpace(startupModeValue))
                {
                    if (int.TryParse(startupModeValue, out int startupMode) &&
                        Enum.IsDefined(typeof(StartupMode), startupMode))
                    {
                        return (StartupMode)startupMode;
                    }

                    return StartupMode.Default;
                }

                // 旧版二态设置迁移：关闭对应当前普通顺序（更快），开启对应当前快速顺序（最快）。
                if (parsedSettings?["startup"]?["enableFastStartup"] != null)
                {
                    return (bool)parsedSettings["startup"]["enableFastStartup"]
                        ? StartupMode.Fastest
                        : StartupMode.Faster;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查启动模式设置失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            return StartupMode.Default;
        }

        /// <summary>
        /// 根据 Settings.json 中的 photoCorrectionAcceleration 设置 OPENCV_OPENCL_DEVICE 环境变量。
        /// CUDA 模式（值=2）下强制使用 NVIDIA OpenCL 平台（NVIDIA OpenCL 底层由 CUDA 驱动）。
        /// 必须在 OpenCV 第一次创建 UMat 之前调用——OpenCL 上下文一旦初始化无法重置。
        /// </summary>
        private static void ApplyPhotoCorrectionAccelerationFromParsed(dynamic parsedSettings)
        {
            try
            {
                int mode = 0; // 默认 CPU
                var v = parsedSettings?["automation"]?["photoCorrectionAcceleration"];
                if (v != null) mode = (int)v;
                if (mode == 2) // CUDA = NVIDIA OpenCL
                {
                    Environment.SetEnvironmentVariable("OPENCV_OPENCL_DEVICE", "NVIDIA:GPU:");
                    LogHelper.WriteLogToFile(
                        "App | 照片矫正加速: CUDA 模式，已设置 OPENCV_OPENCL_DEVICE=NVIDIA:GPU: 强制 NVIDIA OpenCL 平台",
                        LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"App | 应用照片矫正加速设置失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void TryApplyPreferredLanguageFromParsedSettings(dynamic parsedSettings)
        {
            try
            {
                string preferredLanguage = parsedSettings?["appearance"]?["language"]?.ToString();
                if (!string.IsNullOrWhiteSpace(preferredLanguage))
                {
                    LocalizationHelper.TrySetCulture(preferredLanguage);
                    // TrySetCulture → CurrentCulture setter 内部已调用 SyncCommonResources()，无需重复调用
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动时预加载语言失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static bool ShouldShowSplashScreenFromParsed(dynamic parsedSettings)
        {
            try
            {
                if (parsedSettings?["appearance"]?["enableSplashScreen"] != null)
                {
                    return (bool)parsedSettings["appearance"]["enableSplashScreen"];
                }
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查启动动画设置失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
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
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        // 用于设置崩溃后操作类型
        public enum CrashActionType
        {
            SilentRestart,
            NoAction,
            ShowCrashWindow
        }

        /// <summary>
        /// 尝试通过熔断机制静默重启应用：先检查是否达到重启上限，未达则启动新进程并退出当前进程。
        /// 重启上限（5次）内启动新进程；达到上限时弹出提示、重置计数并以非零码退出。
        /// 重启前会通知看门狗退出（写入退出信号文件），避免看门狗二次触发导致双进程启动。
        /// </summary>
        /// <param name="restartReason">用于日志记录的重启原因描述。</param>
        private static void TryRestartWithBreaker(string restartReason)
        {
            StartupCount.Increment();
            int count = StartupCount.GetCount();
            LogHelper.WriteLogToFile($"熔断计数: {count}/5 — {restartReason}", LogHelper.LogType.Warning);

            if (count >= 5)
            {
                MessageBox.Show(
                    UpdateStrings.Msg_RestartLimit,
                    UpdateStrings.Msg_RestartLimitTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StartupCount.Reset();
                Environment.Exit(1);
                return;
            }

            try
            {
                // 通知看门狗退出，防止看门狗检测到进程退出后二次触发重启
                if (!string.IsNullOrEmpty(watchdogExitSignalFile))
                {
                    try { File.WriteAllText(watchdogExitSignalFile, "restart"); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }

                // 杀掉看门狗进程，避免竞态
                try
                {
                    if (watchdogProcess != null && !watchdogProcess.HasExited)
                    {
                        watchdogProcess.Kill();
                        watchdogProcess = null;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(exePath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"熔断重启启动新进程失败: {ex.Message}", LogHelper.LogType.Error);
            }

            Environment.Exit(1);
        }

        // 心跳相关
        private static DispatcherTimer heartbeatTimer;
        private static DateTime lastHeartbeat = DateTime.Now;
        private static Timer watchdogTimer;
        private static bool isStartupComplete = false;
        private static DateTime startupCompleteHeartbeat = DateTime.MinValue;
        private static DateTime splashScreenStartTime = DateTime.MinValue;
        private static DateTime appStartupStartTime = DateTime.MinValue;
        private static volatile bool isAppExiting = false;

        /// <summary>
        /// [调试用] 停止心跳计时器，模拟主线程无响应，下一次守护检查将触发心跳超时重启。
        /// </summary>
        internal static void DebugStopHeartbeat()
        {
            try
            {
                heartbeatTimer?.Stop();
                LogHelper.WriteLogToFile("[Debug] 心跳计时器已手动停止，等待守护检查检测超时", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Debug] 停止心跳计时器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动并管理应用的心跳与守护检查定时器，监测启动阶段与主线程是否无响应，并在符合配置的情况下尝试静默重启应用。
        /// </summary>
        /// <remarks>
        /// - 启动一个每秒更新心跳时间戳的调度定时器和一个每3秒运行的守护定时器。  
        /// - 守护定时器在首次运行的启动阶段若检测到超过两分钟未完成启动，会根据 CrashAction 配置尝试静默重启。  
        /// - 在启动完成后若检测到主线程超过10秒无响应，会根据 CrashAction 配置尝试静默重启。  
        /// - 对连续重启次数有保护：若重启计数达到或超过5次，会弹出提示并停止自动重启（重置重启计数并退出进程）。  
        /// - 在 OOBE（首次引导）展示期间不执行守护检查。  
        /// - 该方法会产生外部可观察的副作用：可能启动新进程并调用 Environment.Exit 终止当前进程，或显示消息框。
        /// - 重启前会等待1秒以确保旧进程资源已释放，避免多实例竞争条件。
        /// </remarks>
        private void StartHeartbeatMonitor()
        {
            UpdateCpuUsageSnapshot();

            heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            heartbeatTimer.Tick += (_, __) =>
            {
                lastHeartbeat = DateTime.Now;
                UpdateCpuUsageSnapshot();
            };
            heartbeatTimer.Start();

            watchdogTimer = new Timer(_ =>
            {
                if (isAppExiting)
                    return;
                if (IsOobeShowing)
                    return;

                if (!isStartupComplete && appStartupStartTime != DateTime.MinValue)
                {
                    DateTime startTime = _isSplashScreenShown && splashScreenStartTime != DateTime.MinValue
                        ? splashScreenStartTime
                        : appStartupStartTime;
                    TimeSpan elapsedSinceStart = DateTime.Now - startTime;
                    if (elapsedSinceStart.TotalMinutes >= 2)
                    {
                        string timeType = _isSplashScreenShown ? "启动画面已显示" : "应用启动开始";
                        string restartReason = $"检测到启动假死：{timeType}{elapsedSinceStart.TotalMinutes:F2}分钟，但未收到启动完成心跳，自动重启。";
                        LogHelper.WriteLogToFile(restartReason, LogHelper.LogType.Error);
                        WriteCrashLog(restartReason);
                        SyncCrashActionFromSettings();
                        if (CrashAction == CrashActionType.SilentRestart)
                        {
                            TryRestartWithBreaker(restartReason);
                        }
                        return;
                    }
                }

                if (isStartupComplete)
                {
                    var now = DateTime.Now;
                    var sinceHeartbeat = now - lastHeartbeat;
                    var sinceStartupComplete = startupCompleteHeartbeat == DateTime.MinValue
                        ? TimeSpan.Zero
                        : now - startupCompleteHeartbeat;

                    if (sinceStartupComplete.TotalSeconds < 30)
                    {
                        return;
                    }

                    if (sinceHeartbeat.TotalSeconds > 10)
                    {
                        string restartReason = $"检测到主线程无响应，自动重启。心跳超时 {sinceHeartbeat.TotalSeconds:F1} 秒。";
                        LogHelper.NewLog(restartReason);
                        WriteCrashLog(restartReason);
                        SyncCrashActionFromSettings();
                        if (CrashAction == CrashActionType.SilentRestart)
                        {
                            TryRestartWithBreaker(restartReason);
                        }
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

        /// <summary>
        /// 作为守护进程监视指定的主进程，并在主进程异常退出时根据配置执行重启或退出操作。
        /// </summary>
        /// <remarks>
        /// 该方法期望命令行参数格式为："--watchdog &lt;pid&gt; &lt;exitSignalFile&gt;"（args[1..3]）。
        /// - 每 2 秒检查一次指定的主进程是否仍在运行；同时检测退出信号文件，若存在则删除该文件并以代码 0 退出守护进程。  
        /// - 当主进程退出时，会同步崩溃处理设置（SyncCrashActionFromSettings）。若启用了 UIA 顶层访问（IsUIAccessTopMostEnabled），守护进程直接退出。  
        /// - 若崩溃动作为 SilentRestart，则增加启动计数并：当连续重启计数达到 5 次及以上时弹出错误对话框、重置计数并以代码 1 退出；否则启动新的主进程实例。  
        /// 方法对内部异常静默处理，并在完成后确保进程退出。
        /// </remarks>
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
                            try { File.Delete(exitSignalFile); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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
                        TryRestartWithBreaker("看门狗检测到主进程异常退出");
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                Environment.Exit(0);
            }
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            isAppExiting = true;

            // 在卸载插件前广播 AppExiting，让插件有机会清理自身资源。
            try
            {
                _pluginEventService?.OnAppExiting();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"广播插件 AppExiting 失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            try { heartbeatTimer?.Stop(); } catch { }
            try { watchdogTimer?.Change(Timeout.Infinite, Timeout.Infinite); watchdogTimer?.Dispose(); } catch { }
            MemoryBreakdownHelper.StopAutomaticDumpMonitor();

            CleanupTerminationMonitoring();

            try
            {
                IpcIACoreClient.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "释放 IpcIACoreClient 失败", LogHelper.LogType.Warning);
            }

            try
            {
                if (mutex != null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                    mutex = null;
                }
            }
            catch { }

            // 卸载所有插件
            try
            {
                LogHelper.WriteLogToFile("正在卸载插件...");
                PluginManager.Instance.UnloadAll();
                LogHelper.WriteLogToFile("插件卸载完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"卸载插件时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 仅在软件内主动退出时关闭看门狗，并写入退出信号
            try
            {
                // 记录应用退出状态
                string exitType = IsAppExitByUser ? "用户主动退出" : "应用程序退出";
                WriteCrashLog($"{exitType}，退出代码: {e.ApplicationExitCode}");

                // 停止性能监测并保存运行记录
                try
                {
                    PerformanceMonitorHelper.StopAndSave();
                }
                catch (Exception perfEx)
                {
                    LogHelper.WriteLogToFile($"保存性能监测数据失败: {perfEx.Message}", LogHelper.LogType.Warning);
                }

                // 实时笔迹详细调试日志独立写盘（仅 Debug 页开启过时才会落盘）
                try
                {
                    RealtimeInkPerformanceMonitor.StopAndSave();
                }
                catch (Exception inkPerfEx)
                {
                    LogHelper.WriteLogToFile($"保存实时笔迹调试日志失败: {inkPerfEx.Message}", LogHelper.LogType.Warning);
                }

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
                catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
            }
        }
    }
}
