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

namespace Ink_Canvas
{
    public class TimeViewModel : INotifyPropertyChanged
    {
        private string _nowTime;
        private string _nowDate;

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        private Timer timerCheckPPT = new Timer();
        private Timer timerKillProcess = new Timer();
        private Timer timerCheckAutoFold = new Timer();
        private string AvailableLatestVersion;
        private Timer timerCheckAutoUpdateWithSilence = new Timer();
        private Timer timerCheckAutoUpdateRetry = new Timer();
        private bool isHidingSubPanelsWhenInking; // 避免书写时触发二次关闭二级菜单导致动画不连续
        private int updateCheckRetryCount = 0;
        private const int MAX_UPDATE_CHECK_RETRIES = 6;
        private Timer timerDisplayTime = new Timer();
        private Timer timerDisplayDate = new Timer();
        private Timer timerNtpSync = new Timer();

        private TimeViewModel nowTimeVM = new TimeViewModel();
        private DateTime cachedNetworkTime = DateTime.Now;
        private DateTime lastNtpSyncTime = DateTime.MinValue;
        private string lastDisplayedTime = "";
        private bool useNetworkTime = false;
        private TimeSpan networkTimeOffset = TimeSpan.Zero;
        private DateTime lastLocalTime = DateTime.Now; // 记录上次的本地时间，用于检测时间跳跃
        private bool isNtpSyncing = false; // 防止重复NTP同步的标志 

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

        // 修改InitTimers方法中的初始时间和日期格式
        private void InitTimers()
        {
            // PPT检查现在由PPTManager处理，不再需要定时器
            // timerCheckPPT.Elapsed += TimerCheckPPT_Elapsed;
            // timerCheckPPT.Interval = 500;
            timerKillProcess.Elapsed += TimerKillProcess_Elapsed;
            timerKillProcess.Interval = 2000;
            timerCheckAutoFold.Elapsed += timerCheckAutoFold_Elapsed;
            timerCheckAutoFold.Interval = 500;
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
        }

        // NTP同步定时器事件处理
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

        // 优化后的时间显示方法，仅在NTP同步时计算网络时间偏移
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

        // 修改TimerDisplayDate_Elapsed方法中的日期格式
        private void TimerDisplayDate_Elapsed(object sender, ElapsedEventArgs e)
        {
            nowTimeVM.nowDate = DateTime.Now.ToString("yyyy'年'MM'月'dd'日' dddd");
        }

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
                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo("taskkill", arg);
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    p.Start();

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
            catch { }
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

        private void timerCheckAutoFold_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isFloatingBarChangingHideMode) return;
            try
            {
                var windowProcessName = ForegroundWindowInfo.ProcessName();
                var windowTitle = ForegroundWindowInfo.WindowTitle();
                //LogHelper.WriteLogToFile("windowTitle | " + windowTitle + " | windowProcessName | " + windowProcessName);

                if (windowProcessName == "EasiNote")
                {
                    // 检测到有可能是EasiNote5或者EasiNote3/3C
                    if (ForegroundWindowInfo.ProcessPath() != "Unknown")
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(ForegroundWindowInfo.ProcessPath());
                        string version = versionInfo.FileVersion;
                        string prodName = versionInfo.ProductName;
                        Trace.WriteLine(ForegroundWindowInfo.ProcessPath());
                        Trace.WriteLine(version);
                        Trace.WriteLine(prodName);
                        if (version.StartsWith("5.") && Settings.Automation.IsAutoFoldInEasiNote)
                        { // EasiNote5
                            // 检查是否是桌面批注窗口
                            bool isAnnotationWindow = windowTitle.Length == 0 && ForegroundWindowInfo.WindowRect().Height < 500;

                            // 如果启用了忽略桌面批注窗口功能，且当前是批注窗口
                            if (Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno && isAnnotationWindow)
                            {
                                // 强制保持收纳状态
                                if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                            }
                            else if (!isAnnotationWindow)
                            {
                                // 非批注窗口时正常收纳
                                if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                            }
                        }
                        else if (version.StartsWith("3.") && Settings.Automation.IsAutoFoldInEasiNote3)
                        { // EasiNote3
                            if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                        }
                        else if (prodName.Contains("3C") && Settings.Automation.IsAutoFoldInEasiNote3C &&
                                   ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                                   ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                        { // EasiNote3C
                            if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                        }
                    }
                    // EasiCamera
                }
                else if (Settings.Automation.IsAutoFoldInEasiCamera && windowProcessName == "EasiCamera" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    // 检测到批注窗口时保持收纳状态
                    if (IsAnnotationWindow())
                    {
                        // 批注窗口打开时，如果当前是展开状态则收纳
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    else
                    {
                        // 非批注窗口时正常处理
                        if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    // EasiNote5C
                }
                else if (Settings.Automation.IsAutoFoldInEasiNote5C && windowProcessName == "EasiNote5C" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // SeewoPinco
                }
                else if (Settings.Automation.IsAutoFoldInSeewoPincoTeacher && (windowProcessName == "BoardService" || windowProcessName == "seewoPincoTeacher"))
                {
                    // 检测到希沃白板五的批注窗口时保持收纳状态
                    if (IsAnnotationWindow())
                    {
                        // 批注窗口打开时，如果当前是展开状态则收纳
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    else
                    {
                        // 非批注窗口时正常处理
                        if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    // HiteCamera
                }
                else if (Settings.Automation.IsAutoFoldInHiteCamera && windowProcessName == "HiteCamera" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    // 检测到批注窗口时保持收纳状态
                    if (IsAnnotationWindow())
                    {
                        // 批注窗口打开时，如果当前是展开状态则收纳
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    else
                    {
                        // 非批注窗口时正常处理
                        if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    // HiteTouchPro
                }
                else if (Settings.Automation.IsAutoFoldInHiteTouchPro && windowProcessName == "HiteTouchPro" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    // 检测到批注窗口时保持收纳状态
                    if (IsAnnotationWindow())
                    {
                        // 批注窗口打开时，如果当前是展开状态则收纳
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    else
                    {
                        // 非批注窗口时正常处理
                        if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    // WxBoardMain
                }
                else if (Settings.Automation.IsAutoFoldInWxBoardMain && windowProcessName == "WxBoardMain" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // MSWhiteboard
                }
                else if (Settings.Automation.IsAutoFoldInMSWhiteboard && (windowProcessName == "MicrosoftWhiteboard" ||
                                                                            windowProcessName == "msedgewebview2"))
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // OldZyBoard
                }
                else if (Settings.Automation.IsAutoFoldInOldZyBoard && // 中原旧白板
                        (WinTabWindowsChecker.IsWindowExisted("WhiteBoard - DrawingWindow")
                         || WinTabWindowsChecker.IsWindowExisted("InstantAnnotationWindow")))
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // HiteLightBoard
                }
                else if (Settings.Automation.IsAutoFoldInHiteLightBoard && windowProcessName == "HiteLightBoard" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    // 检测到批注窗口时保持收纳状态
                    if (IsAnnotationWindow())
                    {
                        // 批注窗口打开时，如果当前是展开状态则收纳
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    else
                    {
                        // 非批注窗口时正常处理
                        if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                    // AdmoxWhiteboard
                }
                else if (Settings.Automation.IsAutoFoldInAdmoxWhiteboard && windowProcessName == "Amdox.WhiteBoard" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // AdmoxBooth
                }
                else if (Settings.Automation.IsAutoFoldInAdmoxBooth && windowProcessName == "Amdox.Booth" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // QPoint
                }
                else if (Settings.Automation.IsAutoFoldInQPoint && windowProcessName == "QPoint" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // YiYunVisualPresenter
                }
                else if (Settings.Automation.IsAutoFoldInYiYunVisualPresenter && windowProcessName == "YiYunVisualPresenter" &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    // MaxHubWhiteboard
                }
                else if (Settings.Automation.IsAutoFoldInMaxHubWhiteboard && windowProcessName == "WhiteBoard" &&
                           WinTabWindowsChecker.IsWindowExisted("白板书写") &&
                           ForegroundWindowInfo.WindowRect().Height >= SystemParameters.WorkArea.Height - 16 &&
                           ForegroundWindowInfo.WindowRect().Width >= SystemParameters.WorkArea.Width - 16)
                {
                    if (ForegroundWindowInfo.ProcessPath() != "Unknown")
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(ForegroundWindowInfo.ProcessPath());
                        var version = versionInfo.FileVersion; var prodName = versionInfo.ProductName;
                        if (version.StartsWith("6.") && prodName == "WhiteBoard") if (!unfoldFloatingBarByUser && !isFloatingBarFolded) FoldFloatingBar_MouseUp(null, null);
                    }
                }
                else if (WinTabWindowsChecker.IsWindowExisted("幻灯片放映", false))
                {
                    // 处于幻灯片放映状态
                    if (!Settings.Automation.IsAutoFoldInPPTSlideShow && isFloatingBarFolded && !foldFloatingBarByUser)
                        UnFoldFloatingBar_MouseUp(new object(), null);
                }
                else
                {
                    // 检查是否启用了软件退出后保持收纳模式
                    if (Settings.Automation.KeepFoldAfterSoftwareExit)
                    {
                        // 如果启用了保持收纳模式，则不自动展开浮动栏
                        unfoldFloatingBarByUser = false;
                    }
                    else
                    {
                        // 原有的逻辑：软件退出后自动展开浮动栏
                        if (isFloatingBarFolded && !foldFloatingBarByUser) UnFoldFloatingBar_MouseUp(new object(), null);
                        unfoldFloatingBarByUser = false;
                    }
                }
            }
            catch { }
        }

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

        // 检查更新失败重试定时器事件处理
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

        // 重置更新检查重试状态
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
    }
}
