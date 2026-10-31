using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers
{
    public partial class ROTPPTManager
    {
        #region WPS Process Management
        private void RecordWpsProcessForManagement()
        {
            if (!IsSupportWPS || PPTApplication == null) return;

            try
            {
                Process wpsProcess = null;

                // 方法1：通过应用程序路径检测
                if (PPTApplication.Path.Contains("Kingsoft\\WPS Office\\") ||
                    PPTApplication.Path.Contains("WPS Office\\"))
                {
                    uint processId;
                    GetWindowThreadProcessId((IntPtr)PPTApplication.HWND, out processId);
                    wpsProcess = Process.GetProcessById((int)processId);
                }

                // 方法2：通过前台窗口检测
                if (wpsProcess == null)
                {
                    var foregroundWpsWindow = GetForegroundWpsWindow();
                    if (foregroundWpsWindow != null)
                    {
                        wpsProcess = Process.GetProcessById((int)foregroundWpsWindow.ProcessId);
                    }
                }

                // 方法3：通过进程名检测
                if (wpsProcess == null)
                {
                    var wpsProcesses = GetWpsProcesses();
                    if (wpsProcesses.Count > 0)
                    {
                        wpsProcess = wpsProcesses.First();
                    }
                }

                if (wpsProcess != null)
                {
                    _wpsProcess = wpsProcess;
                    _hasWpsProcessId = true;
                    _wpsProcessRecordTime = DateTime.Now;
                    _wpsProcessCheckCount = 0;
                    StartWpsProcessCheckTimer();
                }
                else
                {
                    LogHelper.WriteLogToFile("未能检测到WPS进程", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"记录WPS进程失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void StartWpsProcessCheckTimer()
        {
            if (!IsSupportWPS) return;

            if (_wpsProcessCheckTimer != null)
            {
                _wpsProcessCheckTimer.Stop();
                _wpsProcessCheckTimer.Dispose();
            }

            // 增加检查间隔到2秒，减少性能开销
            _wpsProcessCheckTimer = new Timer(2000);
            _wpsProcessCheckTimer.Elapsed += OnWpsProcessCheckTimerElapsed;
            _wpsProcessCheckTimer.Start();
        }

        private void OnWpsProcessCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!IsSupportWPS)
            {
                StopWpsProcessCheckTimer();
                return;
            }

            try
            {
                if (_wpsProcess == null || !_hasWpsProcessId)
                {
                    StopWpsProcessCheckTimer();
                    return;
                }

                _wpsProcess.Refresh();
                _wpsProcessCheckCount++;

                if (_wpsProcess.HasExited)
                {
                    StopWpsProcessCheckTimer();
                    return;
                }


                // 检查前台WPS窗口是否存在
                bool isForegroundWpsWindowActive = IsForegroundWpsWindowStillActiveOptimized();

                if (isForegroundWpsWindowActive)
                {
                    if (_wpsProcessCheckCount % 5 == 0) // 每10秒记录一次日志
                    {
                    }
                    return;
                }

                // 多重验证确保准确性
                if (!PerformMultipleWpsWindowChecks())
                {
                    return;
                }

                // 前台窗口已消失，准备结束WPS进程
                LogHelper.WriteLogToFile("多重验证确认WPS窗口已消失，准备结束WPS进程", LogHelper.LogType.Event);

                // 安全结束WPS进程
                SafeTerminateWpsProcess();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WPS 进程检测失败: {ex}", LogHelper.LogType.Error);
                StopWpsProcessCheckTimer();
            }
        }

        /// <summary>
        /// 前台WPS窗口检测
        /// </summary>
        private bool IsForegroundWpsWindowStillActiveOptimized()
        {
            try
            {
                // 快速检查：直接检查前台窗口
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                // 获取前台窗口的进程ID
                uint processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);

                // 如果前台窗口就是我们监控的WPS进程，则认为仍然活跃
                if (processId == _wpsProcess?.Id)
                {
                    return true;
                }

                // 检查是否为WPS相关窗口
                var windowInfo = GetWindowInfo(foregroundWindow);
                return IsWpsWindow(windowInfo);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WPS窗口检测失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 多重验证WPS窗口状态，确保查杀准确性
        /// </summary>
        private bool PerformMultipleWpsWindowChecks()
        {
            try
            {
                // 第一重验证：等待1秒后再次检查
                Thread.Sleep(1000);
                if (IsForegroundWpsWindowStillActiveOptimized())
                {
                    return false;
                }

                // 第二重验证：检查所有WPS进程的窗口
                var wpsProcesses = GetWpsProcesses();
                foreach (var process in wpsProcesses)
                {
                    if (process.Id == _wpsProcess?.Id) continue; // 跳过当前监控的进程

                    var windows = GetWpsWindowsByProcess(process.Id);
                    if (windows.Any(w => w.IsVisible && !w.IsMinimized))
                    {
                        return false;
                    }
                }

                // 第三重验证：检查任务栏中的WPS窗口
                if (HasWpsWindowInTaskbar())
                {
                    return false;
                }

                LogHelper.WriteLogToFile("多重验证完成：确认WPS窗口已全部消失", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"多重验证失败: {ex}", LogHelper.LogType.Error);
                return false; // 出错时保守处理，不进行查杀
            }
        }

        /// <summary>
        /// 检查任务栏中是否有WPS窗口
        /// </summary>
        private bool HasWpsWindowInTaskbar()
        {
            try
            {
                var allWindows = new List<WpsWindowInfo>();

                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (IsWindow(hWnd) && IsWindowVisible(hWnd))
                        {
                            var windowInfo = GetWindowInfo(hWnd);
                            if (IsWpsWindow(windowInfo) && !string.IsNullOrEmpty(windowInfo.Title))
                            {
                                allWindows.Add(windowInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogException(ex);
                    }
                    return true;
                }, IntPtr.Zero);

                return allWindows.Count > 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查任务栏WPS窗口失败: {ex}", LogHelper.LogType.Error);
                return true; // 出错时保守处理，认为仍有窗口
            }
        }

        /// <summary>
        /// 安全地结束WPS进程 - 通过释放PPTCOM对象
        /// </summary>
        private void SafeTerminateWpsProcess()
        {
            try
            {
                if (_wpsProcess == null || _wpsProcess.HasExited)
                {
                    StopWpsProcessCheckTimer();
                    return;
                }

                LogHelper.WriteLogToFile($"开始通过释放PPTCOM对象安全结束WPS进程 (PID: {_wpsProcess.Id})", LogHelper.LogType.Event);

                // 第一步：释放 pptActWindow 对象（SlideShowWindow）
                SlideShowWindow pptActWindow = null;
                try
                {
                    if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                    {
                        if (PPTApplication.SlideShowWindows?.Count > 0)
                        {
                            pptActWindow = PPTApplication.SlideShowWindows[1];
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"获取SlideShowWindow对象时发生异常: {ex}", LogHelper.LogType.Warning);
                }

                if (pptActWindow != null)
                {
                    Marshal.ReleaseComObject(pptActWindow);
                    pptActWindow = null;
                }

                // 第二步：释放 pptActDoc 对象（CurrentPresentation）
                Presentation pptActDoc = CurrentPresentation;
                if (pptActDoc != null)
                {
                    Marshal.ReleaseComObject(pptActDoc);
                    pptActDoc = null;
                    CurrentPresentation = null;
                }

                // 第三步：释放 pptApp 对象（PPTApplication）
                if (PPTApplication != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(PPTApplication);
                        PPTApplication = null;
                    }
                    catch (Exception)
                    {
                        PPTApplication = null;
                    }
                }

                // 第四步：强制垃圾回收及等待终结器执行
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 等待一段时间让COM对象完全释放
                Thread.Sleep(1000);

                // 检查进程是否已经结束
                try
                {
                    _wpsProcess.Refresh();
                    if (_wpsProcess.HasExited)
                    {
                        LogHelper.WriteLogToFile("WPS进程已通过COM对象释放成功结束", LogHelper.LogType.Event);
                        StopWpsProcessCheckTimer();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"检查WPS进程状态失败: {ex}", LogHelper.LogType.Warning);
                }

                // 备用方案：如果COM对象释放后进程仍未结束，尝试关闭
                try
                {
                    LogHelper.WriteLogToFile("COM对象释放后进程仍在运行，尝试关闭", LogHelper.LogType.Warning);
                    _wpsProcess.CloseMainWindow();
                    if (_wpsProcess.WaitForExit(3000)) // 等待3秒
                    {
                        LogHelper.WriteLogToFile("WPS进程已关闭", LogHelper.LogType.Event);
                        StopWpsProcessCheckTimer();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"关闭WPS进程失败: {ex}", LogHelper.LogType.Warning);
                }

                // 最后备用方案：强制结束进程
                try
                {
                    if (!_wpsProcess.HasExited)
                    {
                        LogHelper.WriteLogToFile("所有方法都失败，强制结束WPS进程", LogHelper.LogType.Warning);
                        _wpsProcess.Kill();
                        LogHelper.WriteLogToFile("WPS进程已强制结束", LogHelper.LogType.Event);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"强制结束WPS进程失败: {ex}", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"安全结束WPS进程时发生异常: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                // 确保清理状态
                if (CurrentSlide != null && Marshal.IsComObject(CurrentSlide))
                {
                    try { Marshal.ReleaseComObject(CurrentSlide); } catch (Exception ex) { LogService.LogException(ex); }
                }
                if (CurrentSlides != null && Marshal.IsComObject(CurrentSlides))
                {
                    try { Marshal.ReleaseComObject(CurrentSlides); } catch (Exception ex) { LogService.LogException(ex); }
                }
                if (CurrentPresentation != null && Marshal.IsComObject(CurrentPresentation))
                {
                    try { Marshal.ReleaseComObject(CurrentPresentation); } catch (Exception ex) { LogService.LogException(ex); }
                }
                if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                {
                    try { Marshal.ReleaseComObject(PPTApplication); } catch (Exception ex) { LogService.LogException(ex); }
                }

                CurrentSlide = null;
                CurrentSlides = null;
                CurrentPresentation = null;
                PPTApplication = null;
                SlidesCount = 0;
                StopWpsProcessCheckTimer();

                bool wasConnected = _cachedIsConnected;
                _cachedIsConnected = false;
                _cachedIsInSlideShow = false;
                if (wasConnected)
                {
                    PPTConnectionChanged?.Invoke(false);
                }

                LogHelper.WriteLogToFile("WPS进程结束后已清理所有COM对象并重启连接检查", LogHelper.LogType.Event);
            }
        }



        private void StopWpsProcessCheckTimer()
        {
            if (_wpsProcessCheckTimer != null)
            {
                _wpsProcessCheckTimer.Stop();
                _wpsProcessCheckTimer.Dispose();
                _wpsProcessCheckTimer = null;
            }

            _wpsProcess = null;
            _hasWpsProcessId = false;
            _wpsProcessRecordTime = DateTime.MinValue;
            _wpsProcessCheckCount = 0;
            _lastForegroundWpsWindow = null;
            _lastWindowCheckTime = DateTime.MinValue;
        }
        #endregion

        #region WPS Window Detection
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private class WpsWindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ClassName { get; set; }
            public bool IsVisible { get; set; }
            public bool IsMinimized { get; set; }
            public bool IsMaximized { get; set; }
            public RECT Rect { get; set; }
            public uint ProcessId { get; set; }
            public string ProcessName { get; set; }
        }

        private WpsWindowInfo GetForegroundWpsWindow()
        {
            try
            {
                var foregroundHwnd = GetForegroundWindow();
                if (foregroundHwnd != IntPtr.Zero && IsWindow(foregroundHwnd))
                {
                    var windowInfo = GetWindowInfo(foregroundHwnd);
                    if (IsWpsWindow(windowInfo))
                    {
                        return windowInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取前台WPS窗口失败: {ex}", LogHelper.LogType.Error);
            }
            return null;
        }

        private WpsWindowInfo GetWindowInfo(IntPtr hWnd)
        {
            var windowInfo = new WpsWindowInfo
            {
                Handle = hWnd,
                IsVisible = IsWindowVisible(hWnd),
                IsMinimized = IsIconic(hWnd),
                IsMaximized = IsZoomed(hWnd)
            };

            // 获取窗口标题
            var windowTitle = new StringBuilder(256);
            GetWindowText(hWnd, windowTitle, 256);
            windowInfo.Title = windowTitle.ToString().Trim();

            // 获取窗口类名
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);
            windowInfo.ClassName = className.ToString().Trim();

            // 获取窗口位置
            GetWindowRect(hWnd, out RECT rect);
            windowInfo.Rect = rect;

            // 获取进程ID
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            windowInfo.ProcessId = processId;

            // 获取进程名
            windowInfo.ProcessName = "";
            try
            {
                var proc = Process.GetProcessById((int)processId);
                windowInfo.ProcessName = proc.ProcessName.ToLower();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            return windowInfo;
        }

        private bool IsWpsWindow(WpsWindowInfo windowInfo)
        {
            if (string.IsNullOrEmpty(windowInfo.Title) && string.IsNullOrEmpty(windowInfo.ClassName))
                return false;

            var title = windowInfo.Title.ToLower();
            var className = windowInfo.ClassName.ToLower();
            var processName = windowInfo.ProcessName ?? "";

            // WPS相关关键词
            var wpsKeywords = new[] { "wps", "wpp", "kingsoft", "金山", "wps演示", "wps presentation", "wps office", "kingsoft office" };
            // 微软Office相关进程名
            var msOfficeProcess = new[] { "powerpnt", "excel", "word", "onenote", "outlook", "microsoftoffice", "office" };

            // 只要进程名是微软Office，直接排除
            if (msOfficeProcess.Any(keyword => processName.Contains(keyword)))
                return false;

            // 只要进程名是WPS/WPP/Kingsoft，直接通过
            if (wpsKeywords.Any(keyword => processName.Contains(keyword)))
                return true;

            // 标题或类名包含WPS相关关键词
            bool hasWpsTitle = wpsKeywords.Any(keyword => title.Contains(keyword));
            bool hasWpsClass = wpsKeywords.Any(keyword => className.Contains(keyword));
            bool isWpsClass = className.Contains("wps") || className.Contains("kingsoft") || className.Contains("wpp");
            bool hasValidSize = (windowInfo.Rect.Right - windowInfo.Rect.Left) > 0 && (windowInfo.Rect.Bottom - windowInfo.Rect.Top) > 0;

            return (hasWpsTitle || hasWpsClass || isWpsClass) && hasValidSize;
        }

        private List<Process> GetWpsProcesses()
        {
            var wpsProcesses = new List<Process>();
            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var process in allProcesses)
                {
                    try
                    {
                        var pname = process.ProcessName.ToLower();

                        // 精确的WPS进程名匹配，避免误杀
                        var exactWpsNames = new[] { "wps", "wpp", "et", "wpspdf", "wpsoffice" };
                        var microsoftOfficeNames = new[] { "powerpnt", "excel", "word", "onenote", "outlook", "winword", "msaccess" };

                        // 排除微软Office进程
                        if (microsoftOfficeNames.Any(name => pname.Contains(name)))
                        {
                            continue;
                        }

                        // 精确匹配WPS进程名
                        bool isWpsProcess = exactWpsNames.Any(name => pname.Equals(name) || pname.StartsWith(name + "."));

                        // 额外验证：检查进程路径
                        if (isWpsProcess)
                        {
                            try
                            {
                                var processPath = process.MainModule?.FileName?.ToLower() ?? "";
                                if (processPath.Contains("kingsoft") || processPath.Contains("wps office"))
                                {
                                    wpsProcesses.Add(process);
                                }
                            }
                            catch
                            {
                                // 无法访问进程路径时，基于进程名判断
                                if (exactWpsNames.Contains(pname))
                                {
                                    wpsProcesses.Add(process);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"检查进程{process.ProcessName}失败: {ex}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取WPS进程失败: {ex}", LogHelper.LogType.Error);
            }

            return wpsProcesses;
        }

        private bool IsForegroundWpsWindowStillActive()
        {
            try
            {
                var currentTime = DateTime.Now;
                var currentForegroundWindow = GetForegroundWpsWindow();

                // 检查窗口状态是否发生变化
                if (_lastForegroundWpsWindow != null && currentForegroundWindow != null)
                {
                    if (_lastForegroundWpsWindow.Handle != currentForegroundWindow.Handle ||
                        _lastForegroundWpsWindow.Title != currentForegroundWindow.Title)
                    {
                    }
                }
                else if (_lastForegroundWpsWindow == null && currentForegroundWindow != null)
                {
                }
                else if (_lastForegroundWpsWindow != null && currentForegroundWindow == null)
                {
                }

                // 更新记录
                _lastForegroundWpsWindow = currentForegroundWindow;
                _lastWindowCheckTime = currentTime;

                if (currentForegroundWindow != null)
                {
                    if (IsWindow(currentForegroundWindow.Handle) && IsWindowVisible(currentForegroundWindow.Handle))
                    {
                        return true;
                    }
                }

                // 检查所有WPS进程的活跃窗口
                var wpsProcesses = GetWpsProcesses();
                foreach (var process in wpsProcesses)
                {
                    var windows = GetWpsWindowsByProcess(process.Id);
                    if (windows.Any(w => w.IsVisible && !w.IsMinimized && w.Handle == GetForegroundWindow()))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查前台WPS窗口状态失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        private List<WpsWindowInfo> GetWpsWindowsByProcess(int processId)
        {
            var wpsWindows = new List<WpsWindowInfo>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (!IsWindow(hWnd)) return true;

                        uint windowProcessId;
                        GetWindowThreadProcessId(hWnd, out windowProcessId);

                        if ((int)windowProcessId == processId)
                        {
                            var windowInfo = GetWindowInfo(hWnd);
                            if (IsWpsWindow(windowInfo))
                            {
                                wpsWindows.Add(windowInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"枚举窗口时出错: {ex}", LogHelper.LogType.Error);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取WPS窗口失败: {ex}", LogHelper.LogType.Error);
            }

            return wpsWindows;
        }
        #endregion

        #region Window Handle Methods
        /// <summary>
        /// 获取PPT窗口句柄
        /// </summary>
        /// <returns>窗口句柄，如果获取失败返回 IntPtr.Zero</returns>
        public IntPtr GetPPTHwnd()
        {
            IntPtr ret = IntPtr.Zero;

            // 方法1: 尝试从 SlideShowWindow 获取
            ret = GetPPTHwndFromSlideShowWindow(_pptSlideShowWindow);

            if (ret == IntPtr.Zero)
            {
                // 方法2: 通过窗口标题匹配获取（备用方法）
                try
                {
                    if (_pptActivePresentation != null && PPTApplication != null)
                    {
                        dynamic pres = _pptActivePresentation;
                        string fullName = pres.FullName;
                        string appName = PPTApplication.Name;
                        ret = GetPPTHwndWin32(fullName, appName);
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex);
                }
            }

            return ret;
        }

        /// <summary>
        /// 从 SlideShowWindow 对象获取窗口句柄
        /// </summary>
        private IntPtr GetPPTHwndFromSlideShowWindow(object pptSlideShowWindowObj)
        {
            IntPtr hwnd = IntPtr.Zero;
            if (pptSlideShowWindowObj == null) return IntPtr.Zero;

            try
            {
                // 尝试强类型转换
                Microsoft.Office.Interop.PowerPoint.SlideShowWindow slideWindow =
                    pptSlideShowWindowObj as Microsoft.Office.Interop.PowerPoint.SlideShowWindow;

                if (slideWindow != null)
                {
                    int hwndVal = slideWindow.HWND;
                    hwnd = new IntPtr(hwndVal);
                }
                else
                {
                    // 如果强类型转换失败，尝试使用dynamic
                    try
                    {
                        dynamic ssw = pptSlideShowWindowObj;
                        int hwndVal = ssw.HWND;
                        hwnd = new IntPtr(hwndVal);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            return hwnd;
        }

        /// <summary>
        /// 通过窗口标题匹配获取PPT窗口句柄（备用方法）
        /// </summary>
        private IntPtr GetPPTHwndWin32(string presFullName, string appName)
        {
            try
            {
                // 步骤 A: 基础参数校验
                if (string.IsNullOrWhiteSpace(presFullName) || string.IsNullOrWhiteSpace(appName))
                {
                    return IntPtr.Zero;
                }

                // 步骤 B: 提取关键信息 (应用类型 & 文件名)
                string targetAppKeyword;
                if (appName.IndexOf("WPS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetAppKeyword = "WPS";
                }
                else if (appName.IndexOf("PowerPoint", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetAppKeyword = "PowerPoint";
                }
                else
                {
                    // 既不是 WPS 也不是 PowerPoint，视为不支持
                    return IntPtr.Zero;
                }

                // 从路径中安全提取文件名（包含扩展名），如 "myppt.pptx"
                string targetFileName = System.IO.Path.GetFileName(presFullName);
                if (string.IsNullOrWhiteSpace(targetFileName))
                {
                    return IntPtr.Zero;
                }

                // 步骤 C: 枚举窗口并查找匹配项
                List<IntPtr> candidates = new List<IntPtr>();

                // 调用 EnumWindows，使用 Lambda 表达式直接嵌入回调逻辑
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // [安全过滤] 1. 忽略不可见窗口
                        if (!IsWindowVisible(hWnd)) return true;

                        // [安全获取] 2. 获取窗口标题长度
                        int length = GetWindowTextLength(hWnd);
                        if (length == 0) return true;

                        // [安全获取] 3. 获取窗口标题文本
                        StringBuilder sb = new StringBuilder(length + 1);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();

                        if (string.IsNullOrWhiteSpace(title)) return true;

                        // [核心匹配] 4. 判断标题是否同时包含 "文件名" 和 "应用关键字"
                        bool hasFileName = title.IndexOf(targetFileName, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool hasAppKey = title.IndexOf(targetAppKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

                        if (hasFileName && hasAppKey)
                        {
                            candidates.Add(hWnd);
                        }

                        // 继续枚举其他窗口
                        return true;
                    }
                    catch
                    {
                        // 回调内部容错，忽略单个窗口获取信息的错误，继续枚举
                        return true;
                    }
                }, IntPtr.Zero);

                // 步骤 D: 结果判定
                // 只有当匹配到的窗口数量 唯一 (Count == 1) 时才返回句柄
                // 0 个表示没找到，>1 个表示有歧义（无法确定是哪一个），均视为失败
                if (candidates.Count == 1)
                {
                    return candidates[0];
                }
                else if (candidates.Count > 1)
                {
                }

                return IntPtr.Zero;
            }
            catch (Exception)
            {
                // 发生任何不可预知的异常（如Path解析错误等），返回安全值
                return IntPtr.Zero;
            }
        }
        #endregion

    }
}
