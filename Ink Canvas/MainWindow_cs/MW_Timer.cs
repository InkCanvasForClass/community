using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 时间视图模型类，用于绑定显示时间和日期
    /// </summary>
    public class TimeViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// 当前时间字符串
        /// </summary>
        private string _nowTime;
        /// <summary>
        /// 当前日期字符串
        /// </summary>
        private string _nowDate;

        /// <summary>
        /// 当前时间属性
        /// </summary>
        public string nowTime
        {
            get => _nowTime;
            set
            {
                if (_nowTime != value)
                {
                    _nowTime = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前日期属性
        /// </summary>
        public string nowDate
        {
            get => _nowDate;
            set
            {
                if (_nowDate != value)
                {
                    _nowDate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        /// <summary>
        /// 进程终止定时器
        /// </summary>
        private Timer timerKillProcess = new Timer();
        /// <summary>
        /// 统一的主窗口定时器
        /// </summary>
        private Timer _unifiedMainWindowTimer;
        /// <summary>
        /// 可用的最新版本号
        /// </summary>
        private string AvailableLatestVersion;
        /// <summary>
        /// 静默更新检查定时器
        /// </summary>
        private Timer timerCheckAutoUpdateWithSilence = new Timer();
        /// <summary>
        /// 更新检查重试定时器
        /// </summary>
        private Timer timerCheckAutoUpdateRetry = new Timer();
        /// <summary>
        /// 避免书写时触发二次关闭二级菜单导致动画不连续
        /// </summary>
        private bool isHidingSubPanelsWhenInking;
        /// <summary>
        /// 更新检查重试计数
        /// </summary>
        private int updateCheckRetryCount = 0;
        /// <summary>
        /// 最大更新检查重试次数
        /// </summary>
        private const int MAX_UPDATE_CHECK_RETRIES = 6;
        /// <summary>
        /// 时间显示定时器
        /// </summary>
        private Timer timerDisplayTime = new Timer();
        /// <summary>
        /// 日期显示定时器
        /// </summary>
        private Timer timerDisplayDate = new Timer();
        /// <summary>
        /// NTP时间同步定时器
        /// </summary>
        private Timer timerNtpSync = new Timer();
        /// <summary>
        /// 时间视图模型实例
        /// </summary>
        private TimeViewModel nowTimeVM = new TimeViewModel();
        /// <summary>
        /// 缓存的网络时间
        /// </summary>
        private DateTime cachedNetworkTime = DateTime.Now;
        /// <summary>
        /// 上次NTP同步时间
        /// </summary>
        private DateTime lastNtpSyncTime = DateTime.MinValue;
        /// <summary>
        /// 上次显示的时间字符串
        /// </summary>
        private string lastDisplayedTime = "";
        /// <summary>
        /// 是否使用网络时间
        /// </summary>
        private bool useNetworkTime = false;
        /// <summary>
        /// 网络时间与本地时间的偏移量
        /// </summary>
        private TimeSpan networkTimeOffset = TimeSpan.Zero;
        /// <summary>
        /// 记录上次的本地时间，用于检测时间跳跃
        /// </summary>
        private DateTime lastLocalTime = DateTime.Now;
        /// <summary>
        /// 防止重复NTP同步的标志
        /// </summary>
        private bool isNtpSyncing = false;
        /// <summary>
        /// 橡皮擦自动切换回批注模式的计时器
        /// </summary>
        private DispatcherTimer _eraserAutoSwitchBackTimer;
        /// <summary>
        /// 异步获取网络时间
        /// </summary>
        /// <returns>返回网络时间，如果获取失败则返回本地时间</returns>
        /// <remarks>
        /// 使用NTP协议从国家授时中心服务器获取网络时间
        /// </remarks>
        private async Task<DateTime> GetNetworkTimeAsync()
        {
            try
            {
                const string ntpServer = "ntp.ntsc.ac.cn";
                var ntpData = new byte[48];
                ntpData[0] = 0x1B;
                var addresses = await Dns.GetHostAddressesAsync(ntpServer);
                var ipEndPoint = new IPEndPoint(addresses[0], 123);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.ReceiveTimeout = 5000;
                    socket.Connect(ipEndPoint);
                    await Task.Factory.FromAsync(socket.BeginSend(ntpData, 0, ntpData.Length, SocketFlags.None, null, socket), socket.EndSend);
                    await Task.Factory.FromAsync(socket.BeginReceive(ntpData, 0, ntpData.Length, SocketFlags.None, null, socket), socket.EndReceive);
                }
                const byte serverReplyTime = 40;
                ulong intPart = BitConverter.ToUInt32(ntpData.Skip(serverReplyTime).Take(4).Reverse().ToArray(), 0);
                ulong fractPart = BitConverter.ToUInt32(ntpData.Skip(serverReplyTime + 4).Take(4).Reverse().ToArray(), 0);
                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);
                return networkDateTime.ToLocalTime();
            }
            catch (Exception)
            {
                return DateTime.Now;
            }
        }

        /// <summary>
        /// 初始化所有定时器
        /// </summary>
        /// <remarks>
        /// 初始化以下定时器：
        /// 1. timerKillProcess: 进程终止定时器，每2秒执行一次
        /// 2. _unifiedMainWindowTimer: 统一的主窗口定时器，每500毫秒执行一次
        /// 3. timerCheckAutoUpdateWithSilence: 静默更新检查定时器，每10分钟执行一次
        /// 4. timerCheckAutoUpdateRetry: 更新检查重试定时器，每10分钟执行一次
        /// 5. timerDisplayTime: 时间显示定时器，每秒执行一次
        /// 6. timerDisplayDate: 日期显示定时器，每小时执行一次
        /// 7. timerNtpSync: NTP时间同步定时器，每2小时执行一次
        /// 同时初始化定时保存墨迹定时器
        /// </remarks>
        private void InitTimers()
        {
            timerKillProcess.Elapsed += TimerKillProcess_Elapsed;
            timerKillProcess.Interval = 2000;
            _unifiedMainWindowTimer = new Timer(500);
            _unifiedMainWindowTimer.Elapsed += OnUnifiedMainWindowTimerElapsed;
            _unifiedMainWindowTimer.AutoReset = true;
            timerCheckAutoUpdateWithSilence.Elapsed += timerCheckAutoUpdateWithSilence_Elapsed;
            timerCheckAutoUpdateWithSilence.Interval = 1000 * 60 * 10;
            timerCheckAutoUpdateRetry.Elapsed += timerCheckAutoUpdateRetry_Elapsed;
            timerCheckAutoUpdateRetry.Interval = 1000 * 60 * 10;
            WaterMarkTime.DataContext = nowTimeVM;
            WaterMarkDate.DataContext = nowTimeVM;
            timerDisplayTime.Elapsed += TimerDisplayTime_Elapsed;
            timerDisplayTime.Interval = 1000;
            timerDisplayTime.Start();
            timerDisplayDate.Elapsed += TimerDisplayDate_Elapsed;
            timerDisplayDate.Interval = 1000 * 60 * 60 * 1;
            timerDisplayDate.Start();
            timerNtpSync.Elapsed += async (s, e) => await TimerNtpSync_ElapsedAsync();
            timerNtpSync.Interval = 1000 * 60 * 60 * 2; // 每2小时同步一次
            timerNtpSync.Start();
            timerKillProcess.Start();
            nowTimeVM.nowDate = DateTime.Now.ToString("yyyy'年'MM'月'dd'日' dddd");
            nowTimeVM.nowTime = DateTime.Now.ToString("tt hh'时'mm'分'ss'秒'");

            // 程序启动时立即进行一次NTP同步
            Task.Run(async () =>
            {
                try
                {
                    await TimerNtpSync_ElapsedAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"程序启动时NTP同步失败: {ex.Message}", LogHelper.LogType.Error);
                }
            });

            // 初始化定时保存墨迹定时器
            InitAutoSaveStrokesTimer();

            // 初始化橡皮擦自动切换回批注模式计时器
            InitEraserAutoSwitchBackTimer();
        }

        /// <summary>
        /// 统一主窗口定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 调用timerCheckAutoFold_Elapsed方法处理自动收纳逻辑
        /// </remarks>
        private void OnUnifiedMainWindowTimerElapsed(object sender, ElapsedEventArgs e)
        {
            timerCheckAutoFold_Elapsed(sender, e);
        }

        /// <summary>
        /// 初始化定时保存墨迹定时器
        /// </summary>
        /// <remarks>
        /// 初始化DispatcherTimer实例并绑定AutoSaveStrokesTimer_Tick事件处理方法
        /// 然后调用UpdateAutoSaveStrokesTimer方法根据设置更新定时器状态
        /// </remarks>
        private void InitAutoSaveStrokesTimer()
        {
            if (autoSaveStrokesTimer == null)
            {
                autoSaveStrokesTimer = new DispatcherTimer();
                autoSaveStrokesTimer.Tick += AutoSaveStrokesTimer_Tick;
            }

            // 根据设置更新定时器间隔和启动状态
            UpdateAutoSaveStrokesTimer();
        }

        /// <summary>
        /// 更新定时保存墨迹定时器状态
        /// </summary>
        /// <remarks>
        /// 根据Settings.Automation.IsEnableAutoSaveStrokes设置决定是否启用定时器
        /// 如果启用，则根据Settings.Automation.AutoSaveStrokesIntervalMinutes设置定时器间隔
        /// 最小间隔为1分钟
        /// </remarks>
        private void UpdateAutoSaveStrokesTimer()
        {
            if (autoSaveStrokesTimer == null) return;

            autoSaveStrokesTimer.Stop();

            if (Settings.Automation.IsEnableAutoSaveStrokes)
            {
                int intervalMinutes = Settings.Automation.AutoSaveStrokesIntervalMinutes;
                if (intervalMinutes < 1) intervalMinutes = 1; // 最小间隔1分钟
                autoSaveStrokesTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);
                autoSaveStrokesTimer.Start();
            }
        }

        /// <summary>
        /// 定时保存墨迹定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 当定时器触发时，检查画布是否可见且有墨迹
        /// 如果满足条件，则调用SaveInkCanvasStrokes方法进行静默保存
        /// </remarks>
        private void AutoSaveStrokesTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 只有在画布可见且有墨迹时才保存
                if (inkCanvas.Visibility == Visibility.Visible && inkCanvas.Strokes.Count > 0)
                {
                    // 静默保存
                    SaveInkCanvasStrokes(false, false);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// NTP同步定时器事件处理方法
        /// </summary>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 异步执行NTP时间同步，包括以下步骤：
        /// 1. 防止重复同步（使用isNtpSyncing标志）
        /// 2. 添加10秒超时机制
        /// 3. 调用GetNetworkTimeAsync获取网络时间
        /// 4. 计算网络时间与本地时间的偏移量
        /// 5. 如果时间差超过3分钟，则使用网络时间
        /// 6. 处理异常情况，确保即使同步失败也能恢复到使用本地时间
        /// </remarks>
        private async Task TimerNtpSync_ElapsedAsync()
        {
            // 防止重复同步
            if (isNtpSyncing) return;

            isNtpSyncing = true;
            try
            {

                // 添加超时机制，最多等待10秒
                var timeoutTask = Task.Delay(10000);
                var ntpTask = GetNetworkTimeAsync();

                var completedTask = await Task.WhenAny(ntpTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    cachedNetworkTime = DateTime.Now;
                    lastNtpSyncTime = DateTime.Now;
                    useNetworkTime = false;
                    networkTimeOffset = TimeSpan.Zero;
                    return;
                }

                DateTime networkTime = await ntpTask;
                DateTime localTime = DateTime.Now;

                cachedNetworkTime = networkTime;
                lastNtpSyncTime = localTime;

                // 计算网络时间与本地时间的偏移量
                networkTimeOffset = networkTime - localTime;

                // 如果时间差超过3分钟，则使用网络时间
                useNetworkTime = Math.Abs(networkTimeOffset.TotalMinutes) > 3.0;

            }
            catch (Exception ex)
            {
                // NTP同步失败时，保持使用本地时间
                cachedNetworkTime = DateTime.Now;
                lastNtpSyncTime = DateTime.Now;
                useNetworkTime = false;
                networkTimeOffset = TimeSpan.Zero;

                LogHelper.WriteLogToFile($"NTP同步失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                isNtpSyncing = false;
            }
        }

        /// <summary>
        /// 优化后的时间显示方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 处理时间显示逻辑，包括以下步骤：
        /// 1. 获取当前本地时间
        /// 2. 检测系统时间是否发生重大跳跃（超过3分钟），如果是则触发NTP同步
        /// 3. 如果启用网络时间且偏移量已计算，则应用偏移量
        /// 4. 格式化时间字符串
        /// 5. 只有当时间字符串发生变化时才更新UI，避免不必要的UI刷新
        /// 6. 使用BeginInvoke异步更新UI，避免阻塞
        /// </remarks>
        private void TimerDisplayTime_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime localTime = DateTime.Now;
            DateTime displayTime = localTime; // 默认使用本地时间

            // 检测系统时间是否发生重大跳跃（超过2分钟）
            TimeSpan timeJump = localTime - lastLocalTime;
            double timeJumpMinutes = Math.Abs(timeJump.TotalMinutes);

            if (timeJumpMinutes > 3 && !isNtpSyncing)
            {
                // 系统时间发生重大变化（超过3分钟），立即触发NTP同步
                // 使用异步方式触发NTP同步，避免阻塞主线程
                Task.Run(async () =>
                {
                    try
                    {
                        await TimerNtpSync_ElapsedAsync();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"时间跳跃触发的NTP同步失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                });
            }
            lastLocalTime = localTime;

            // 如果启用网络时间且偏移量已计算，则应用偏移量
            if (useNetworkTime && networkTimeOffset != TimeSpan.Zero)
            {
                displayTime = localTime + networkTimeOffset;
            }

            // 格式化时间字符串
            string timeString = displayTime.ToString("tt hh'时'mm'分'ss'秒'");


            // 只有当时间字符串发生变化时才更新UI，避免不必要的UI刷新
            if (timeString != lastDisplayedTime)
            {
                lastDisplayedTime = timeString;

                // 使用BeginInvoke异步更新UI，避免阻塞
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    nowTimeVM.nowTime = timeString;
                }));
            }
        }

        /// <summary>
        /// 日期显示定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 更新nowTimeVM的nowDate属性，设置为当前日期的格式化字符串
        /// 格式为：yyyy年MM月dd日 星期几
        /// </remarks>
        private void TimerDisplayDate_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 使用BeginInvoke异步更新UI，避免阻塞
            Dispatcher.BeginInvoke(new Action(() =>
            {
                nowTimeVM.nowDate = DateTime.Now.ToString("yyyy'年'MM'月'dd'日' dddd");
            }));
        }

        /// <summary>
        /// 进程终止定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 根据设置终止指定的进程，包括PPTService、EasiNote、HiteAnnotation等
        /// 对于每个终止的进程，会显示相应的通知
        /// 对于HiteAnnotation进程，还会根据设置决定是否自动进入批注状态
        /// </remarks>
        private void TimerKillProcess_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // 希沃相关： easinote swenserver RemoteProcess EasiNote.MediaHttpService smartnote.cloud EasiUpdate smartnote EasiUpdate3 EasiUpdate3Protect SeewoP2P CefSharp.BrowserSubprocess SeewoUploadService
                var arg = "/F";
                if (Settings.Automation.IsAutoKillPptService)
                {
                    var processes = Process.GetProcessesByName("PPTService");
                    if (processes.Length > 0) arg += " /IM PPTService.exe";
                    processes = Process.GetProcessesByName("SeewoIwbAssistant");
                    if (processes.Length > 0) arg += " /IM SeewoIwbAssistant.exe" + " /IM Sia.Guard.exe";
                }

                if (Settings.Automation.IsAutoKillEasiNote)
                {
                    var processes = Process.GetProcessesByName("EasiNote");
                    if (processes.Length > 0) arg += " /IM EasiNote.exe";
                    var seewoStartProcesses = Process.GetProcessesByName("SeewoStart");
                    if (seewoStartProcesses.Length > 0) arg += " /IM SeewoStart.exe";
                }

                if (Settings.Automation.IsAutoKillHiteAnnotation)
                {
                    var processes = Process.GetProcessesByName("HiteAnnotation");
                    if (processes.Length > 0) arg += " /IM HiteAnnotation.exe";
                }

                if (Settings.Automation.IsAutoKillVComYouJiao)
                {
                    var processes = Process.GetProcessesByName("VcomTeach");
                    if (processes.Length > 0) arg += " /IM VcomTeach.exe" + " /IM VcomDaemon.exe" + " /IM VcomRender.exe";
                }

                if (Settings.Automation.IsAutoKillICA)
                {
                    var processesAnnotation = Process.GetProcessesByName("Ink Canvas Annotation");
                    var processesArtistry = Process.GetProcessesByName("Ink Canvas Artistry");
                    if (processesAnnotation.Length > 0) arg += " /IM \"Ink Canvas Annotation.exe\"";
                    if (processesArtistry.Length > 0) arg += " /IM \"Ink Canvas Artistry.exe\"";
                }

                if (Settings.Automation.IsAutoKillInkCanvas)
                {
                    var processes = Process.GetProcessesByName("Ink Canvas");
                    if (processes.Length > 0) arg += " /IM \"Ink Canvas.exe\"";
                }

                if (Settings.Automation.IsAutoKillIDT)
                {
                    var processes = Process.GetProcessesByName("Inkeys");
                    if (processes.Length > 0) arg += " /IM \"Inkeys.exe\"";
                }

                if (Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                {
                    //由于希沃桌面2.0提供的桌面批注是64位应用程序，32位程序无法访问，目前暂不做精准匹配，只匹配进程名称，后面会考虑封装一套基于P/Invoke和WMI的综合进程识别方案。
                    var processes = Process.GetProcessesByName("DesktopAnnotation");
                    if (processes.Length > 0) arg += " /IM DesktopAnnotation.exe";
                }

                if (arg != "/F")
                {
                    using (var p = new Process())
                    {
                        p.StartInfo = new ProcessStartInfo("taskkill", arg);
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.Start();
                    }

                    if (arg.Contains("EasiNote"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“希沃白板 5”已自动关闭");
                        });
                    }

                    if (arg.Contains("HiteAnnotation"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“鸿合屏幕书写”已自动关闭");
                            if (Settings.Automation.IsAutoKillHiteAnnotation && Settings.Automation.IsAutoEnterAnnotationAfterKillHite)
                            {
                                // 检查是否处于收纳状态，如果是则先展开浮动栏
                                if (isFloatingBarFolded)
                                {
                                    // 先展开浮动栏，然后进入批注状态
                                    // UnFoldFloatingBar 方法内部会根据设置自动进入批注模式
                                    UnFoldFloatingBar(null);
                                }
                                else
                                {
                                    // 如果已经展开，直接进入批注状态
                                    PenIcon_Click(null, null);
                                }
                            }
                        });
                    }

                    if (arg.Contains("Ink Canvas Annotation") || arg.Contains("Ink Canvas Artistry"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNewMessage("“ICA”已自动关闭");
                        });
                    }

                    if (arg.Contains("\"Ink Canvas.exe\""))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“Ink Canvas”已自动关闭");
                        });
                    }

                    if (arg.Contains("Inkeys"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“智绘教Inkeys”已自动关闭");
                        });
                    }

                    if (arg.Contains("VcomTeach"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“优教授课端”已自动关闭");
                        });
                    }

                    if (arg.Contains("DesktopAnnotation"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("“希沃桌面2.0 桌面批注”已自动关闭");
                        });
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }


        private bool foldFloatingBarByUser, // 保持收纳操作不受自动收纳的控制
            unfoldFloatingBarByUser; // 允许用户在希沃软件内进行展开操作

        /// <summary>
        /// 检测是否为批注窗口（窗口标题为空且高度小于500像素）
        /// </summary>
        /// <returns>如果是批注窗口返回true，否则返回false</returns>
        private bool IsAnnotationWindow()
        {
            var windowTitle = ForegroundWindowInfo.WindowTitle();
            var windowRect = ForegroundWindowInfo.WindowRect();
            var windowProcessName = ForegroundWindowInfo.ProcessName();

            // 检测希沃白板五的批注面板
            // 希沃白板五的批注面板通常具有以下特征：
            // 1. 窗口标题为空或包含特定关键词
            // 2. 窗口高度较小（批注工具栏）
            // 3. 窗口宽度适中（工具栏宽度）
            if (windowProcessName == "BoardService" || windowProcessName == "seewoPincoTeacher")
            {
                // 检测希沃白板五的批注工具栏
                // 批注工具栏通常高度在50-200像素之间，宽度在200-800像素之间
                if (windowRect.Height >= 50 && windowRect.Height <= 200 &&
                    windowRect.Width >= 200 && windowRect.Width <= 800)
                {
                    return true;
                }

                // 检测希沃白板五的二级菜单面板
                // 二级菜单面板通常高度在100-400像素之间，宽度在150-400像素之间
                if (windowRect.Height >= 100 && windowRect.Height <= 400 &&
                    windowRect.Width >= 150 && windowRect.Width <= 400)
                {
                    return true;
                }
            }

            // 检测鸿合软件的批注面板
            if (windowProcessName == "HiteCamera" || windowProcessName == "HiteTouchPro" || windowProcessName == "HiteLightBoard")
            {
                // 鸿合软件的批注面板特征
                if (windowRect.Height >= 50 && windowRect.Height <= 300 &&
                    windowRect.Width >= 200 && windowRect.Width <= 600)
                {
                    return true;
                }
            }

            // 原有的检测逻辑（保持向后兼容）
            return windowTitle.Length == 0 && windowRect.Height < 500;
        }

        /// <summary>
        /// 检查是否存在应当被收纳应用的全屏窗口
        /// </summary>
        /// <returns>如果存在应当被收纳应用的全屏窗口返回true，否则返回false</returns>
        private bool HasFullScreenWindowOfAutoFoldApps()
        {
            if (_windowOverviewModel == null) return false;

            try
            {
                var fullScreenWindows = _windowOverviewModel.GetFullScreenWindows();
                if (fullScreenWindows == null || fullScreenWindows.Count == 0) return false;

                foreach (var window in fullScreenWindows)
                {
                    var windowProcessName = window.ProcessName;
                    var windowRect = window.Rect;

                    if (windowProcessName == "EasiNote")
                    {
                        if (window.ProcessPath != "Unknown")
                        {
                            try
                            {
                                var versionInfo = FileVersionInfo.GetVersionInfo(window.ProcessPath);
                                string version = versionInfo.FileVersion;
                                string prodName = versionInfo.ProductName;

                                if (version.StartsWith("5.") && Settings.Automation.IsAutoFoldInEasiNote)
                                {
                                    return true;
                                }
                                else if (version.StartsWith("3.") && Settings.Automation.IsAutoFoldInEasiNote3)
                                {
                                    return true;
                                }
                                else if (prodName.Contains("3C") && Settings.Automation.IsAutoFoldInEasiNote3C)
                                {
                                    return true;
                                }
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }
                    else if (Settings.Automation.IsAutoFoldInEasiCamera && windowProcessName == "EasiCamera")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInEasiNote5C && windowProcessName == "EasiNote5C")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInSeewoPincoTeacher &&
                             (windowProcessName == "BoardService" || windowProcessName == "seewoPincoTeacher"))
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInHiteCamera && windowProcessName == "HiteCamera")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInHiteTouchPro && windowProcessName == "HiteTouchPro")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInWxBoardMain && windowProcessName == "WxBoardMain")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInMSWhiteboard &&
                             (windowProcessName == "MicrosoftWhiteboard" || windowProcessName == "msedgewebview2"))
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInHiteLightBoard && windowProcessName == "HiteLightBoard")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInAdmoxWhiteboard && windowProcessName == "Amdox.WhiteBoard")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInAdmoxBooth && windowProcessName == "Amdox.Booth")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInQPoint && windowProcessName == "QPoint")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInYiYunVisualPresenter && windowProcessName == "YiYunVisualPresenter")
                    {
                        return true;
                    }
                    else if (Settings.Automation.IsAutoFoldInMaxHubWhiteboard && windowProcessName == "WhiteBoard")
                    {
                        if (window.ProcessPath != "Unknown")
                        {
                            try
                            {
                                var versionInfo = FileVersionInfo.GetVersionInfo(window.ProcessPath);
                                var version = versionInfo.FileVersion;
                                var prodName = versionInfo.ProductName;
                                if (version.StartsWith("6.") && prodName == "WhiteBoard")
                                {
                                    return true;
                                }
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }
                }

                if (Settings.Automation.IsAutoFoldInOldZyBoard &&
                    (WinTabWindowsChecker.IsWindowExisted("WhiteBoard - DrawingWindow") ||
                     WinTabWindowsChecker.IsWindowExisted("InstantAnnotationWindow")))
                {
                    var oldZyWindows = _windowOverviewModel.Windows.Where(w =>
                        (w.Title.Contains("WhiteBoard - DrawingWindow") || w.Title.Contains("InstantAnnotationWindow")) &&
                        w.IsFullScreen).ToList();
                    if (oldZyWindows.Count > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查全屏窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 使用窗口预览模型检测前台窗口是否符合自动收纳要求（仅用于检测，不执行任何操作）
        /// </summary>
        /// <returns>如果符合自动收纳要求返回true，否则返回false</returns>
        private bool CheckShouldAutoFoldByWindowPreview()
        {
            if (_windowOverviewModel == null) return false;

            try
            {
                // 从窗口预览模型中获取窗口列表（已按ZOrder排序，最上层在前）
                var windows = _windowOverviewModel.Windows;
                if (windows == null || windows.Count == 0) return false;

                // 获取前台窗口（ZOrder最小的窗口，即最上层）
                var foregroundWindow = windows.FirstOrDefault();
                if (foregroundWindow == null) return false;

                var windowProcessName = foregroundWindow.ProcessName;
                var windowTitle = foregroundWindow.Title;
                var windowRect = foregroundWindow.Rect;

                // 检查EasiNote
                if (windowProcessName == "EasiNote")
                {
                    if (foregroundWindow.ProcessPath != "Unknown")
                    {
                        try
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(foregroundWindow.ProcessPath);
                            string version = versionInfo.FileVersion;
                            string prodName = versionInfo.ProductName;

                            if (version.StartsWith("5.") && Settings.Automation.IsAutoFoldInEasiNote)
                            {
                                bool isAnnotationWindow = windowTitle.Length == 0 && windowRect.Height < 500;
                                if (Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno && isAnnotationWindow)
                                {
                                    return true;
                                }
                                else if (!isAnnotationWindow)
                                {
                                    return true;
                                }
                            }
                            else if (version.StartsWith("3.") && Settings.Automation.IsAutoFoldInEasiNote3)
                            {
                                return true;
                            }
                            else if (prodName.Contains("3C") && Settings.Automation.IsAutoFoldInEasiNote3C &&
                                     windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                                     windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                            {
                                return true;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }
                // 检查EasiCamera
                else if (Settings.Automation.IsAutoFoldInEasiCamera && windowProcessName == "EasiCamera" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查EasiNote5C
                else if (Settings.Automation.IsAutoFoldInEasiNote5C && windowProcessName == "EasiNote5C" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查SeewoPinco
                else if (Settings.Automation.IsAutoFoldInSeewoPincoTeacher &&
                         (windowProcessName == "BoardService" || windowProcessName == "seewoPincoTeacher"))
                {
                    return true;
                }
                // 检查HiteCamera
                else if (Settings.Automation.IsAutoFoldInHiteCamera && windowProcessName == "HiteCamera" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查HiteTouchPro
                else if (Settings.Automation.IsAutoFoldInHiteTouchPro && windowProcessName == "HiteTouchPro" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查WxBoardMain
                else if (Settings.Automation.IsAutoFoldInWxBoardMain && windowProcessName == "WxBoardMain" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查MSWhiteboard
                else if (Settings.Automation.IsAutoFoldInMSWhiteboard &&
                         (windowProcessName == "MicrosoftWhiteboard" || windowProcessName == "msedgewebview2"))
                {
                    return true;
                }
                // 检查OldZyBoard
                else if (Settings.Automation.IsAutoFoldInOldZyBoard &&
                         (WinTabWindowsChecker.IsWindowExisted("WhiteBoard - DrawingWindow") ||
                          WinTabWindowsChecker.IsWindowExisted("InstantAnnotationWindow")))
                {
                    return true;
                }
                // 检查HiteLightBoard
                else if (Settings.Automation.IsAutoFoldInHiteLightBoard && windowProcessName == "HiteLightBoard" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查AdmoxWhiteboard
                else if (Settings.Automation.IsAutoFoldInAdmoxWhiteboard && windowProcessName == "Amdox.WhiteBoard" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查AdmoxBooth
                else if (Settings.Automation.IsAutoFoldInAdmoxBooth && windowProcessName == "Amdox.Booth" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查QPoint
                else if (Settings.Automation.IsAutoFoldInQPoint && windowProcessName == "QPoint" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查YiYunVisualPresenter
                else if (Settings.Automation.IsAutoFoldInYiYunVisualPresenter && windowProcessName == "YiYunVisualPresenter" &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    return true;
                }
                // 检查MaxHubWhiteboard
                else if (Settings.Automation.IsAutoFoldInMaxHubWhiteboard && windowProcessName == "WhiteBoard" &&
                         WinTabWindowsChecker.IsWindowExisted("白板书写") &&
                         windowRect.Height >= SystemParameters.WorkArea.Height - 16 &&
                         windowRect.Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (foregroundWindow.ProcessPath != "Unknown")
                    {
                        try
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(foregroundWindow.ProcessPath);
                            var version = versionInfo.FileVersion;
                            var prodName = versionInfo.ProductName;
                            if (version.StartsWith("6.") && prodName == "WhiteBoard")
                            {
                                return true;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"窗口预览模型检测失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 自动收纳定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 检查是否需要自动收纳浮动栏，包括以下逻辑：
        /// 1. 检查是否有全屏窗口需要收纳
        /// 2. 检查是否有应用程序需要自动收纳
        /// 3. 对于EasiNote应用，根据版本和窗口类型决定是否收纳
        /// 4. 对于其他应用程序，根据设置决定是否收纳
        /// 5. 当没有需要收纳的应用程序时，根据设置决定是否展开浮动栏
        /// </remarks>
        private void timerCheckAutoFold_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isFloatingBarChangingHideMode) return;
            try
            {
                bool hasFullScreen = HasFullScreenWindowOfAutoFoldApps();
                bool shouldAutoFold = CheckShouldAutoFoldByWindowPreview();
                var windowProcessName = ForegroundWindowInfo.ProcessName();
                var windowTitle = ForegroundWindowInfo.WindowTitle();

                Thickness currentMargin = new Thickness();
                Dispatcher.Invoke(() =>
                {
                    currentMargin = ViewboxFloatingBar.Margin;
                });

                if (hasFullScreen)
                {
                    if (!isFloatingBarFolded)
                    {
                        FoldFloatingBar_MouseUp(null, null);
                    }
                    else if (currentMargin.Left > -50 && !isFloatingBarChangingHideMode)
                    {
                        FoldFloatingBar_MouseUp(null, null);
                    }
                    return;
                }

                if (shouldAutoFold)
                {
                    if (windowProcessName == "EasiNote")
                    {
                        if (ForegroundWindowInfo.ProcessPath() != "Unknown")
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(ForegroundWindowInfo.ProcessPath());
                            string version = versionInfo.FileVersion;

                            if (version.StartsWith("5.") && Settings.Automation.IsAutoFoldInEasiNote)
                            {
                                bool isAnnotationWindow = windowTitle.Length == 0 && ForegroundWindowInfo.WindowRect().Height < 500;
                                if (Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno && isAnnotationWindow)
                                {
                                    if (!isFloatingBarFolded)
                                    {
                                        FoldFloatingBar_MouseUp(null, null);
                                    }
                                }
                                else if (!isAnnotationWindow)
                                {
                                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded)
                                    {
                                        FoldFloatingBar_MouseUp(null, null);
                                    }
                                    else if (unfoldFloatingBarByUser)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    // 处理其他目标软件
                    else if (!unfoldFloatingBarByUser && !isFloatingBarFolded)
                    {
                        FoldFloatingBar_MouseUp(null, null);
                    }
                    return;
                }

                if (!WinTabWindowsChecker.IsWindowExisted("幻灯片放映", false))
                {
                    if (isFloatingBarFolded && !foldFloatingBarByUser)
                    {
                        // 检查是否启用了软件退出后保持收纳模式
                        if (Settings.Automation.KeepFoldAfterSoftwareExit)
                        {
                            unfoldFloatingBarByUser = false;
                        }
                        else
                        {
                            UnFoldFloatingBar_MouseUp(new object(), null);
                            unfoldFloatingBarByUser = false;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 静默更新检查定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 处理静默更新的检查和安装逻辑，包括以下步骤：
        /// 1. 停止计时器，避免重复触发
        /// 2. 检查是否有可用的更新版本
        /// 3. 检查是否启用了静默更新
        /// 4. 检查更新文件是否已下载
        /// 5. 如果未下载，尝试使用多线路组下载更新文件
        /// 6. 检查是否在静默更新时间段内
        /// 7. 检查应用程序状态，确保可以安全更新
        /// 8. 如果可以安全更新，执行更新安装并关闭应用程序
        /// 9. 如果不能安全更新，重新启动计时器，稍后再检查
        /// 10. 处理异常情况，确保计时器能够重新启动
        /// </remarks>
        private void timerCheckAutoUpdateWithSilence_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 停止计时器，避免重复触发
            timerCheckAutoUpdateWithSilence.Stop();

            try
            {
                // 检查是否有可用的更新
                if (string.IsNullOrEmpty(AvailableLatestVersion))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | No available update version found");
                    return;
                }

                // 检查是否启用了静默更新
                if (!Settings.Startup.IsAutoUpdateWithSilence)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Silent update is disabled");
                    return;
                }

                // 检查更新文件是否已下载
                string updatesFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AutoUpdate");
                string statusFilePath = Path.Combine(updatesFolderPath, $"DownloadV{AvailableLatestVersion}Status.txt");

                if (!File.Exists(statusFilePath) || File.ReadAllText(statusFilePath).Trim().ToLower() != "true")
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Update file not downloaded yet");

                    // 尝试下载更新文件，使用多线路组下载功能
                    Task.Run(async () =>
                    {
                        bool isDownloadSuccessful = false;

                        try
                        {
                            // 如果主要线路组可用，直接使用
                            if (AvailableLatestLineGroup != null)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 使用主要线路组下载: {AvailableLatestLineGroup.GroupName}");
                                isDownloadSuccessful = await AutoUpdateHelper.DownloadSetupFile(AvailableLatestVersion, AvailableLatestLineGroup);
                            }

                            // 如果主要线路组不可用或下载失败，获取所有可用线路组
                            if (!isDownloadSuccessful)
                            {
                                LogHelper.WriteLogToFile("AutoUpdate | 主要线路组不可用或下载失败，获取所有可用线路组");
                                var availableGroups = await AutoUpdateHelper.GetAvailableLineGroupsOrdered(Settings.Startup.UpdateChannel);
                                if (availableGroups.Count > 0)
                                {
                                    LogHelper.WriteLogToFile($"AutoUpdate | 使用 {availableGroups.Count} 个可用线路组进行下载");
                                    isDownloadSuccessful = await AutoUpdateHelper.DownloadSetupFileWithFallback(AvailableLatestVersion, availableGroups);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 下载更新时出错: {ex.Message}", LogHelper.LogType.Error);
                        }

                        if (isDownloadSuccessful)
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will check again for installation");
                            // 重新启动计时器，下次检查时安装
                            timerCheckAutoUpdateWithSilence.Start();
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Failed to download update", LogHelper.LogType.Error);
                        }
                    });

                    return;
                }

                // 检查是否在静默更新时间段内
                bool isInSilencePeriod = AutoUpdateWithSilenceTimeComboBox.CheckIsInSilencePeriod(
                    Settings.Startup.AutoUpdateWithSilenceStartTime,
                    Settings.Startup.AutoUpdateWithSilenceEndTime);

                if (!isInSilencePeriod)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Not in silence update time period");
                    // 重新启动计时器，稍后再检查
                    timerCheckAutoUpdateWithSilence.Start();
                    return;
                }

                // 检查应用程序状态，确保可以安全更新 
                // 空闲状态的判定为不处于批注模式和画板模式
                bool canSafelyUpdate = false;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 判断是否处于批注模式（inkCanvas.EditingMode == InkCanvasEditingMode.Ink）
                        // 判断是否处于画板模式（!Topmost）
                        if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink && Topmost)
                        {
                            // 检查是否有未保存的内容或正在进行的操作
                            if (!isHidingSubPanelsWhenInking)
                            {
                                canSafelyUpdate = true;
                                LogHelper.WriteLogToFile("AutoUpdate | Application is in a safe state for update - not in ink or board mode");
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("AutoUpdate | Application is currently performing operations");
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Application is in ink or board mode, cannot update now");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Error checking application state: {ex.Message}", LogHelper.LogType.Error);
                    }
                });

                if (canSafelyUpdate)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Installing update now");

                    // 设置为用户主动退出，避免被看门狗判定为崩溃
                    App.IsAppExitByUser = true;

                    // 执行更新安装
                    AutoUpdateHelper.InstallNewVersionApp(AvailableLatestVersion, true);

                    // 关闭应用程序
                    Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
                else
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Cannot safely update now, will try again later");
                    // 重新启动计时器，稍后再检查
                    timerCheckAutoUpdateWithSilence.Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in silent update check: {ex.Message}", LogHelper.LogType.Error);
                // 出错时重新启动计时器，稍后再检查
                timerCheckAutoUpdateWithSilence.Start();
            }
        }

        /// <summary>
        /// 检查更新失败重试定时器事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 异步处理更新检查失败后的重试逻辑，包括以下步骤：
        /// 1. 停止计时器，避免重复触发
        /// 2. 检查是否启用了自动更新
        /// 3. 增加重试计数
        /// 4. 检查是否超过最大重试次数
        /// 5. 清除之前的更新状态
        /// 6. 使用当前选择的更新通道检查更新
        /// 7. 如果检查成功，重置重试计数并停止重试定时器
        /// 8. 如果检查失败，重新启动定时器，10分钟后再次尝试
        /// 9. 处理异常情况，确保定时器能够重新启动
        /// </remarks>
        private async void timerCheckAutoUpdateRetry_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 停止定时器，避免重复触发
            timerCheckAutoUpdateRetry.Stop();

            try
            {
                // 检查是否启用了自动更新
                if (!Settings.Startup.IsAutoUpdate)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Auto update is disabled, stopping retry timer");
                    return;
                }

                // 增加重试计数
                updateCheckRetryCount++;
                LogHelper.WriteLogToFile($"AutoUpdate | Retry check attempt {updateCheckRetryCount}/{MAX_UPDATE_CHECK_RETRIES}");

                // 检查是否超过最大重试次数
                if (updateCheckRetryCount > MAX_UPDATE_CHECK_RETRIES)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Maximum retry attempts reached, stopping retry timer", LogHelper.LogType.Warning);
                    return;
                }

                // 执行更新检查
                LogHelper.WriteLogToFile("AutoUpdate | Retrying update check after failure");

                // 清除之前的更新状态
                AvailableLatestVersion = null;
                AvailableLatestLineGroup = null;

                // 使用当前选择的更新通道检查更新
                var (remoteVersion, lineGroup, apiReleaseNotes) = await AutoUpdateHelper.CheckForUpdates(Settings.Startup.UpdateChannel);
                AvailableLatestVersion = remoteVersion;
                AvailableLatestLineGroup = lineGroup;

                if (AvailableLatestVersion != null)
                {
                    // 检查更新成功，重置重试计数
                    updateCheckRetryCount = 0;
                    LogHelper.WriteLogToFile($"AutoUpdate | Retry successful, found new version: {AvailableLatestVersion}");

                    // 停止重试定时器，因为已经找到了更新
                    return;
                }
                else
                {
                    // 检查更新仍然失败，继续重试
                    LogHelper.WriteLogToFile($"AutoUpdate | Retry {updateCheckRetryCount} failed, will retry in 10 minutes");

                    // 重新启动定时器，10分钟后再次尝试
                    timerCheckAutoUpdateRetry.Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in retry check: {ex.Message}", LogHelper.LogType.Error);

                // 出错时也重新启动定时器，稍后再检查
                if (updateCheckRetryCount <= MAX_UPDATE_CHECK_RETRIES)
                {
                    timerCheckAutoUpdateRetry.Start();
                }
            }
        }

        /// <summary>
        /// 重置更新检查重试状态方法
        /// </summary>
        /// <remarks>
        /// 重置更新检查的重试状态，包括以下步骤：
        /// 1. 停止重试定时器
        /// 2. 重置重试计数为0
        /// 3. 记录日志
        /// 4. 处理异常情况
        /// </remarks>
        public void ResetUpdateCheckRetry()
        {
            try
            {
                // 停止重试定时器
                timerCheckAutoUpdateRetry.Stop();

                // 重置重试计数
                updateCheckRetryCount = 0;

                LogHelper.WriteLogToFile("AutoUpdate | Update check retry state reset");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error resetting retry state: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化橡皮擦自动切换回批注模式计时器
        /// </summary>
        private void InitEraserAutoSwitchBackTimer()
        {
            if (_eraserAutoSwitchBackTimer == null)
            {
                _eraserAutoSwitchBackTimer = new DispatcherTimer();
                _eraserAutoSwitchBackTimer.Tick += EraserAutoSwitchBackTimer_Tick;
            }
        }

        /// <summary>
        /// 启动橡皮擦自动切换回批注模式计时器
        /// </summary>
        public void StartEraserAutoSwitchBackTimer()
        {
            try
            {
                if (!Settings.Canvas.EnableEraserAutoSwitchBack) return;
                if (_eraserAutoSwitchBackTimer == null) InitEraserAutoSwitchBackTimer();

                // 停止之前的计时器
                _eraserAutoSwitchBackTimer.Stop();

                // 设置计时器间隔
                int delaySeconds = Settings.Canvas.EraserAutoSwitchBackDelaySeconds;
                if (delaySeconds < 1) delaySeconds = 10; // 最小1秒
                _eraserAutoSwitchBackTimer.Interval = TimeSpan.FromSeconds(delaySeconds);

                // 启动计时器
                _eraserAutoSwitchBackTimer.Start();

                LogHelper.WriteLogToFile($"橡皮擦自动切换计时器已启动，延迟 {delaySeconds} 秒", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动橡皮擦自动切换计时器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止橡皮擦自动切换回批注模式计时器
        /// </summary>
        public void StopEraserAutoSwitchBackTimer()
        {
            try
            {
                if (_eraserAutoSwitchBackTimer != null)
                {
                    _eraserAutoSwitchBackTimer.Stop();
                    LogHelper.WriteLogToFile("橡皮擦自动切换计时器已停止", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止橡皮擦自动切换计时器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 橡皮擦自动切换回批注模式计时器事件处理
        /// </summary>
        private void EraserAutoSwitchBackTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 检查是否仍然在橡皮擦模式
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint &&
                    inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    StopEraserAutoSwitchBackTimer();
                    return;
                }

                // 检查设置是否仍然启用
                if (!Settings.Canvas.EnableEraserAutoSwitchBack)
                {
                    StopEraserAutoSwitchBackTimer();
                    return;
                }

                // 切换到批注模式
                Dispatcher.Invoke(() =>
                {
                    PenIcon_Click(null, null);
                    StopEraserAutoSwitchBackTimer();
                    LogHelper.WriteLogToFile("橡皮擦自动切换回批注模式", LogHelper.LogType.Event);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"橡皮擦自动切换计时器事件处理失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}