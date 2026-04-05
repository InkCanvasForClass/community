using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers
{
    public class ROTPPTManager : IPPTLinkManager
    {
        #region Events
        public event Action<object> SlideShowBegin;
        public event Action<object> SlideShowNextSlide;
        public event Action<object> SlideShowEnd;
        public event Action<object> PresentationOpen;
        public event Action<object> PresentationClose;
        public event Action<bool> PPTConnectionChanged;
        public event Action<bool> SlideShowStateChanged;
        #endregion

        #region Properties
        public dynamic PPTApplication { get; private set; }
        public dynamic CurrentPresentation { get; private set; }
        public dynamic CurrentSlides { get; private set; }
        public dynamic CurrentSlide { get; private set; }
        public int SlidesCount { get; private set; }
        public bool IsConnected
        {
            get
            {
                try
                {
                    if (PPTApplication == null) return false;
                    if (!Marshal.IsComObject(PPTApplication)) return false;

                    // 尝试访问一个简单的属性来验证连接是否有效
                    var _ = PPTApplication.Name;
                    return true;
                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    // 如果COM对象已失效，返回false
                    if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706B5)
                    {
                        return false;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }
        public bool IsInSlideShow
        {
            get
            {
                object slideShowWindows = null;
                object slideShowWindow = null;
                object view = null;
                try
                {
                    if (PPTApplication == null || !Marshal.IsComObject(PPTApplication)) return false;

                    slideShowWindows = PPTApplication.SlideShowWindows;
                    if (slideShowWindows == null) return false;

                    dynamic ssw = slideShowWindows;
                    if (ssw.Count == 0) return false;

                    try
                    {
                        slideShowWindow = ssw[1];
                        if (slideShowWindow == null) return false;

                        dynamic sswObj = slideShowWindow;
                        view = sswObj.View;
                        if (view == null) return false;

                        return true;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x8001010E || hr == 0x80004005)
                        {
                            DisconnectFromPPT();
                        }
                        return false;
                    }
                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    if (hr == 0x8001010E || hr == 0x80004005)
                    {
                        DisconnectFromPPT();
                    }
                    LogHelper.WriteLogToFile($"检查PPT放映状态失败: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                    return false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"检查PPT放映状态时发生意外错误: {ex}", LogHelper.LogType.Warning);
                    return false;
                }
                finally
                {
                    SafeReleaseComObject(view);
                    SafeReleaseComObject(slideShowWindow);
                    SafeReleaseComObject(slideShowWindows);
                }
            }
        }
        public bool IsSupportWPS { get; set; } = false;
        public bool SkipAnimationsWhenNavigating { get; set; } = false;
        #endregion

        #region Private Fields
        private Thread _monitoringThread;
        private bool _shouldStop = false;
        private readonly object _monitoringLock = new object();
        private Timer _wpsProcessCheckTimer;
        private Process _wpsProcess;
        private bool _isModuleUnloading = false;
        private bool _hasWpsProcessId;
        private DateTime _wpsProcessRecordTime = DateTime.MinValue;
        private int _wpsProcessCheckCount;
        private WpsWindowInfo _lastForegroundWpsWindow;
        private DateTime _lastWindowCheckTime = DateTime.MinValue;
        private bool _lastSlideShowState;
        private readonly object _lockObject = new object();
        private bool _disposed;

        private dynamic _pptActivePresentation;
        private dynamic _pptSlideShowWindow;
        private int _polling = 0;
        private bool _forcePolling = true;
        private bool _bindingEvents = false;
        private DateTime _updateTime;
        private int _lastPolledSlideNumber = -1;
        #endregion

        #region Constructor & Initialization
        public ROTPPTManager()
        {
        }

        public void StartMonitoring()
        {
            if (_disposed) return;

            lock (_monitoringLock)
            {
                if (_monitoringThread != null && _monitoringThread.IsAlive)
                {
                    return;
                }

                _shouldStop = false;
                _monitoringThread = new Thread(PptComService)
                {
                    IsBackground = true,
                    Name = "PPTMonitoringThread"
                };
                _monitoringThread.Start();
                LogHelper.WriteLogToFile("PPT监控已启动", LogHelper.LogType.Trace);
            }
        }

        public void StopMonitoring()
        {
            lock (_monitoringLock)
            {
                _shouldStop = true;

                if (_monitoringThread != null && _monitoringThread.IsAlive)
                {
                    // 等待线程退出，最多等待2秒
                    if (!_monitoringThread.Join(2000))
                    {
                        LogHelper.WriteLogToFile("等待监控线程退出超时", LogHelper.LogType.Warning);
                    }
                }

                DisconnectFromPPT();
                LogHelper.WriteLogToFile("PPT监控已停止", LogHelper.LogType.Trace);
            }
        }

        public void ReloadConnection()
        {
            if (_disposed) return;

            LogHelper.WriteLogToFile("[ROT] 执行热重载：强制断开并重新连接", LogHelper.LogType.Event);

            lock (_monitoringLock)
            {
                _shouldStop = true;

                if (_monitoringThread != null && _monitoringThread.IsAlive)
                {
                    if (!_monitoringThread.Join(2000))
                    {
                        LogHelper.WriteLogToFile("等待监控线程退出超时（热重载）", LogHelper.LogType.Warning);
                    }
                }

                DisconnectFromPPT();
                _monitoringThread = null;
                _shouldStop = false;
                _isModuleUnloading = false;
            }

            StartMonitoring();
        }
        #endregion

        #region Connection Management
        private void PptComService()
        {
            LogHelper.WriteLogToFile("PPT Monitor ReStarted", LogHelper.LogType.Trace);

            _bindingEvents = false;
            _lastPolledSlideNumber = -1;
            _polling = 0;

            int tempTotalPage = -1;

            try
            {
                while (!_shouldStop && !_isModuleUnloading)
                {
                    object bestApp = PPTROTConnectionHelper.GetAnyActivePowerPoint(PPTApplication, out int bestPriority, out int targetPriority);
                    bool needRebind = false;

                    if (bestApp == null)
                    {
                        var appFallback = TryConnectToPowerPoint();
                        if (appFallback != null)
                        {
                            bestApp = appFallback;
                            bestPriority = 1;
                        }
                    }

                    if (PPTApplication == null && bestApp != null)
                    {
                        needRebind = true;
                    }
                    else if (PPTApplication != null && bestApp != null && bestPriority > targetPriority)
                    {
                        if (!PPTROTConnectionHelper.AreComObjectsEqual(PPTApplication, bestApp))
                        {
                            needRebind = true;
                        }
                    }

                    if (needRebind)
                    {
                        bool wait = (PPTApplication != null);
                        DisconnectFromPPT();

                        if (bestApp != null)
                        {
                            if (wait) Thread.Sleep(1000);

                            PPTApplication = bestApp;

                            try
                            {
                                _pptActivePresentation = PPTApplication.ActivePresentation;
                                _updateTime = DateTime.Now;

                                try
                                {
                                    _pptSlideShowWindow = _pptActivePresentation.SlideShowWindow;
                                    tempTotalPage = GetTotalSlideIndex(_pptActivePresentation);
                                }
                                catch
                                {
                                    tempTotalPage = -1;
                                }

                                if (tempTotalPage == -1)
                                {
                                    _lastPolledSlideNumber = -1;
                                    _polling = 0;
                                }
                                else
                                {
                                    try
                                    {
                                        _lastPolledSlideNumber = GetCurrentSlideIndex(_pptSlideShowWindow);

                                        if (GetCurrentSlideIndex(_pptSlideShowWindow) >= GetTotalSlideIndex(_pptActivePresentation)) _polling = 1;
                                        else _polling = 0;
                                    }
                                    catch
                                    {
                                        _lastPolledSlideNumber = -1;
                                        _polling = 1;
                                    }
                                }

                                ConnectToPPT(null);

                                try
                                {
                                    dynamic pptAppDynamic = PPTApplication;
                                    LogHelper.WriteLogToFile($"成功绑定! {pptAppDynamic.Name}", LogHelper.LogType.Trace);
                                }
                                catch
                                {
                                    LogHelper.WriteLogToFile("成功绑定!", LogHelper.LogType.Trace);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"绑定失败: {ex.Message}", LogHelper.LogType.Warning);
                                DisconnectFromPPT();
                            }
                        }
                    }
                    else
                    {
                        if (bestApp != null && (PPTApplication == null || !PPTROTConnectionHelper.AreComObjectsEqual(PPTApplication, bestApp)))
                        {
                            PPTROTConnectionHelper.SafeReleaseComObject(bestApp);
                            bestApp = null;
                        }
                        else if (bestApp == null && PPTApplication != null)
                        {
                            LogHelper.WriteLogToFile("检测到PPT已关闭，断开连接", LogHelper.LogType.Trace);
                            DisconnectFromPPT();
                        }
                    }

                    if (PPTApplication != null && _pptActivePresentation != null)
                    {
                        dynamic activePresentation = null;
                        dynamic slideShowWindow = null;

                        try
                        {
                            // 检查COM对象是否仍然有效
                            if (!System.Runtime.InteropServices.Marshal.IsComObject(PPTApplication))
                            {
                                DisconnectFromPPT();
                                continue;
                            }

                            try
                            {
                                var _ = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(PPTApplication);
                                System.Runtime.InteropServices.Marshal.Release(_);
                            }
                            catch (System.Runtime.InteropServices.InvalidComObjectException)
                            {
                                // COM对象已失效
                                DisconnectFromPPT();
                                continue;
                            }

                            activePresentation = PPTApplication.ActivePresentation;

                            if (!PPTROTConnectionHelper.AreComObjectsEqual(_pptActivePresentation, activePresentation))
                            {
                                LogHelper.WriteLogToFile("检测到演示文稿切换，断开连接", LogHelper.LogType.Trace);
                                DisconnectFromPPT();
                                continue;
                            }
                        }
                        catch (System.Runtime.InteropServices.InvalidComObjectException)
                        {
                            // COM对象已失效
                            LogHelper.WriteLogToFile("检测到COM对象失效，断开连接", LogHelper.LogType.Trace);
                            DisconnectFromPPT();
                            continue;
                        }
                        catch (COMException ex) when ((uint)ex.ErrorCode == 0x8001010A)
                        {
                            LogHelper.WriteLogToFile("PowerPoint 忙，稍后重试", LogHelper.LogType.Trace);
                        }
                        catch (COMException comEx)
                        {
                            // COM异常，对象可能已失效
                            var hr = (uint)comEx.HResult;
                            LogHelper.WriteLogToFile($"检查演示文稿状态COM异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                            DisconnectFromPPT();
                            continue;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"检查演示文稿状态失败: {ex.Message}", LogHelper.LogType.Warning);
                            DisconnectFromPPT();
                            continue;
                        }
                        finally
                        {
                            PPTROTConnectionHelper.SafeReleaseComObject(activePresentation);
                            activePresentation = null;
                        }

                        bool isSlideShowActive = false;
                        try
                        {
                            activePresentation = PPTApplication.ActivePresentation;

                            dynamic slideShowWindows = PPTApplication.SlideShowWindows;
                            int count = 0;
                            if (slideShowWindows != null)
                            {
                                count = slideShowWindows.Count;
                            }

                            if (activePresentation != null && count > 0)
                            {
                                isSlideShowActive = true;

                                dynamic activeSlideShowWindow = null;

                                try
                                {
                                    for (int i = 1; i <= count; i++)
                                    {
                                        try
                                        {
                                            dynamic ssw = slideShowWindows[i];
                                            if (PPTROTConnectionHelper.IsSlideShowWindowActive(ssw))
                                            {
                                                activeSlideShowWindow = ssw;
                                                break;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                catch { }

                                if (activeSlideShowWindow == null)
                                {
                                    try
                                    {
                                        activeSlideShowWindow = activePresentation.SlideShowWindow;
                                    }
                                    catch { }
                                }

                                if (activeSlideShowWindow != null)
                                {
                                    slideShowWindow = activeSlideShowWindow;
                                    if (_pptSlideShowWindow == null || !PPTROTConnectionHelper.IsValidSlideShowWindow(_pptSlideShowWindow))
                                    {
                                        if (!PPTROTConnectionHelper.AreComObjectsEqual(_pptSlideShowWindow, slideShowWindow))
                                        {
                                            bool isNewWindow = _pptSlideShowWindow == null;
                                            PPTROTConnectionHelper.SafeReleaseComObject(_pptSlideShowWindow);
                                            _pptSlideShowWindow = slideShowWindow;

                                            if (isNewWindow)
                                            {
                                                try
                                                {
                                                    OnSlideShowBegin(_pptSlideShowWindow);
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogHelper.WriteLogToFile($"手动触发SlideShowBegin失败: {ex.Message}", LogHelper.LogType.Warning);
                                                }
                                            }
                                        }
                                    }
                                }

                                PPTROTConnectionHelper.SafeReleaseComObject(slideShowWindows);
                            }
                            else
                            {
                                PPTROTConnectionHelper.SafeReleaseComObject(slideShowWindows);
                            }
                        }
                        catch (COMException ex) when ((uint)ex.ErrorCode == 0x8001010A)
                        {
                            LogHelper.WriteLogToFile("PowerPoint 忙，稍后重试", LogHelper.LogType.Trace);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"发现窗口失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                        finally
                        {
                            PPTROTConnectionHelper.SafeReleaseComObject(activePresentation);
                            activePresentation = null;

                            if (!PPTROTConnectionHelper.AreComObjectsEqual(_pptSlideShowWindow, slideShowWindow))
                            {
                                PPTROTConnectionHelper.SafeReleaseComObject(slideShowWindow);
                                slideShowWindow = null;
                            }
                        }

                        if (isSlideShowActive)
                        {
                            if ((DateTime.Now - _updateTime).TotalMilliseconds > 3000 || _forcePolling)
                            {

                                try
                                {
                                    slideShowWindow = _pptActivePresentation.SlideShowWindow;

                                    if (slideShowWindow != null)
                                    {
                                        tempTotalPage = GetTotalSlideIndex(_pptActivePresentation);
                                    }
                                    else
                                    {
                                        tempTotalPage = -1;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tempTotalPage = -1;
                                    LogHelper.WriteLogToFile($"获取总页数失败: {ex.Message}", LogHelper.LogType.Warning);
                                }
                                finally
                                {
                                    PPTROTConnectionHelper.SafeReleaseComObject(slideShowWindow);
                                    slideShowWindow = null;
                                }

                                if (tempTotalPage == -1)
                                {
                                    _lastPolledSlideNumber = -1;
                                    _polling = 0;
                                }
                                else
                                {
                                    try
                                    {
                                        int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);
                                        int lastPage = _lastPolledSlideNumber;

                                        if (currentPage >= GetTotalSlideIndex(_pptActivePresentation)) _polling = 1;
                                        else _polling = 0;

                                        if (lastPage != -1 && currentPage != lastPage)
                                        {
                                            try
                                            {
                                                SlideShowNextSlide?.Invoke(_pptSlideShowWindow);
                                            }
                                            catch (Exception ex)
                                            {
                                                LogHelper.WriteLogToFile($"触发轮询模式幻灯片切换事件失败: {ex.Message}", LogHelper.LogType.Warning);
                                            }
                                        }

                                        _lastPolledSlideNumber = currentPage;
                                    }
                                    catch (Exception ex)
                                    {
                                        _lastPolledSlideNumber = -1;
                                        _polling = 1;
                                        LogHelper.WriteLogToFile($"获取当前页数失败: {ex}", LogHelper.LogType.Warning);
                                    }
                                }

                                _updateTime = DateTime.Now;
                            }

                            if (_polling != 0)
                            {
                                try
                                {
                                    int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);

                                    if (_lastPolledSlideNumber != -1 && currentPage != _lastPolledSlideNumber)
                                    {
                                        try
                                        {
                                            SlideShowNextSlide?.Invoke(_pptSlideShowWindow);
                                        }
                                        catch (Exception ex)
                                        {
                                            LogHelper.WriteLogToFile($"触发轮询模式幻灯片切换事件失败: {ex.Message}", LogHelper.LogType.Warning);
                                        }
                                    }

                                    _lastPolledSlideNumber = currentPage;
                                    UpdateCurrentPresentationInfo();
                                    _polling = 2;
                                }
                                catch
                                {
                                    _lastPolledSlideNumber = -1;
                                }
                            }
                        }
                        else
                        {
                            if (_lastSlideShowState)
                            {
                                LogHelper.WriteLogToFile("轮询检测到放映已结束", LogHelper.LogType.Trace);

                                PPTROTConnectionHelper.SafeReleaseComObject(_pptSlideShowWindow);
                                _pptSlideShowWindow = null;
                                _lastPolledSlideNumber = -1;
                                _polling = 1;
                                SlidesCount = 0;

                                _lastSlideShowState = false;
                                SlideShowStateChanged?.Invoke(false);

                                if (_pptActivePresentation != null)
                                {
                                    try
                                    {
                                        OnSlideShowEnd(_pptActivePresentation);
                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.WriteLogToFile($"触发SlideShowEnd事件失败: {ex.Message}", LogHelper.LogType.Warning);
                                    }
                                }
                            }
                            else
                            {
                                _lastPolledSlideNumber = -1;
                                SlidesCount = 0;
                            }
                        }
                    }
                    else
                    {
                        _lastPolledSlideNumber = -1;
                        SlidesCount = 0;
                    }

                    if (_shouldStop || _isModuleUnloading)
                    {
                        LogHelper.WriteLogToFile("收到停止信号，退出循环", LogHelper.LogType.Trace);
                        break;
                    }

                    Thread.Sleep(500);
                }
            }
            catch (InvalidComObjectException ex)
            {
                LogHelper.WriteLogToFile($"PptComService异常: COM对象已失效 - {ex.Message}", LogHelper.LogType.Error);
                DisconnectFromPPT();
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                LogHelper.WriteLogToFile($"PptComService异常: COM异常 (HR: 0x{hr:X8}) - {comEx.Message}", LogHelper.LogType.Error);
                DisconnectFromPPT();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PptComService异常: {ex.Message}", LogHelper.LogType.Error);
                DisconnectFromPPT();
            }
        }

        private void CheckAndConnectToPPT()
        {
            if (_isModuleUnloading) return;

            lock (_lockObject)
            {
                try
                {
                    if (_isModuleUnloading) return;

                    object bestApp = PPTROTConnectionHelper.GetAnyActivePowerPoint(PPTApplication, out int bestPriority, out int targetPriority);
                    bool needRebind = false;

                    LogHelper.WriteLogToFile($"ROT扫描结果: now={targetPriority}, best={bestPriority}, bestApp={(bestApp != null ? "found" : "null")}", LogHelper.LogType.Trace);

                    if (PPTApplication == null && bestApp != null)
                    {
                        needRebind = true;
                    }
                    else if (PPTApplication != null && bestApp != null && bestPriority > targetPriority)
                    {
                        if (!PPTROTConnectionHelper.AreComObjectsEqual(PPTApplication, bestApp))
                        {
                            needRebind = true;
                        }
                    }

                    if (needRebind)
                    {
                        LogHelper.WriteLogToFile($"需要重新绑定: bestPriority={bestPriority}, targetPriority={targetPriority}", LogHelper.LogType.Trace);

                        bool wait = (PPTApplication != null);
                        DisconnectFromPPT();

                        if (bestApp != null)
                        {
                            if (wait) Thread.Sleep(1000);

                            try
                            {
                                LogHelper.WriteLogToFile("使用dynamic类型连接", LogHelper.LogType.Trace);
                                PPTApplication = bestApp;
                                ConnectToPPT(null);
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"连接失败: {ex.Message}", LogHelper.LogType.Warning);
                                PPTROTConnectionHelper.SafeReleaseComObject(bestApp);
                            }
                        }
                    }
                    else
                    {
                        if (bestApp != null && (PPTApplication == null || !PPTROTConnectionHelper.AreComObjectsEqual(PPTApplication, bestApp)))
                        {
                            PPTROTConnectionHelper.SafeReleaseComObject(bestApp);
                            bestApp = null;
                        }
                    }

                    if (PPTApplication != null)
                    {
                        // 即使 _pptActivePresentation 为 null，也要检查（可能在轮询模式下需要初始化）
                        if (_pptActivePresentation == null)
                        {
                            try
                            {
                                _pptActivePresentation = PPTApplication.ActivePresentation;
                                if (_pptActivePresentation != null)
                                {
                                    LogHelper.WriteLogToFile("轮询模式：初始化_pptActivePresentation", LogHelper.LogType.Trace);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"轮询模式：无法获取ActivePresentation: {ex.Message}", LogHelper.LogType.Trace);
                            }
                        }

                        if (_pptActivePresentation != null)
                        {
                            CheckPresentationAndSlideShowState();
                        }
                    }
                }
                catch (InvalidComObjectException)
                {
                    // COM对象已失效，断开连接
                    LogHelper.WriteLogToFile("检测到COM对象失效，断开连接", LogHelper.LogType.Trace);
                    DisconnectFromPPT();
                }
                catch (COMException comEx)
                {
                    // COM异常，记录并断开连接
                    var hr = (uint)comEx.HResult;
                    LogHelper.WriteLogToFile($"PPT连接检查COM异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                    DisconnectFromPPT();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PptComService异常: {ex.Message}", LogHelper.LogType.Error);
                    if (PPTApplication != null)
                    {
                        DisconnectFromPPT();
                    }
                }
            }
        }

        private void CheckPresentationAndSlideShowState()
        {
            try
            {
                // 检查COM对象是否仍然有效
                if (PPTApplication == null || !System.Runtime.InteropServices.Marshal.IsComObject(PPTApplication))
                {
                    return;
                }

                try
                {
                    var _ = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(PPTApplication);
                    System.Runtime.InteropServices.Marshal.Release(_);
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    // COM对象已失效
                    DisconnectFromPPT();
                    return;
                }

                dynamic activePresentation = null;
                dynamic slideShowWindow = null;

                try
                {
                    activePresentation = PPTApplication.ActivePresentation;

                    if (activePresentation != null && _pptActivePresentation != null && !PPTROTConnectionHelper.AreComObjectsEqual(_pptActivePresentation, activePresentation))
                    {
                        LogHelper.WriteLogToFile("检测到演示文稿切换，断开连接", LogHelper.LogType.Trace);
                        DisconnectFromPPT();
                        return;
                    }
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    // COM对象已失效
                    LogHelper.WriteLogToFile("检测到COM对象失效，断开连接", LogHelper.LogType.Trace);
                    DisconnectFromPPT();
                    return;
                }
                catch (COMException ex) when ((uint)ex.HResult == 0x8001010A)
                {
                    LogHelper.WriteLogToFile("PowerPoint 忙，稍后重试", LogHelper.LogType.Trace);
                    return;
                }
                catch (COMException comEx)
                {
                    // COM异常，对象可能已失效
                    var hr = (uint)comEx.HResult;
                    LogHelper.WriteLogToFile($"检查演示文稿状态COM异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                    DisconnectFromPPT();
                    return;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"检查演示文稿状态失败: {ex.Message}，继续使用轮询模式", LogHelper.LogType.Warning);
                    activePresentation = null;
                }
                finally
                {
                    if (activePresentation != null && (_pptActivePresentation == null || !PPTROTConnectionHelper.AreComObjectsEqual(_pptActivePresentation, activePresentation)))
                    {
                        SafeReleaseComObject(activePresentation);
                    }
                }

                bool isSlideShowActive = false;
                try
                {
                    if (activePresentation == null)
                    {
                        try
                        {
                            activePresentation = PPTApplication.ActivePresentation;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"访问ActivePresentation失败: {ex.Message}，继续使用轮询模式", LogHelper.LogType.Warning);
                        }
                    }

                    if (activePresentation != null)
                    {
                        try
                        {
                            dynamic slideShowWindows = PPTApplication.SlideShowWindows;
                            if (slideShowWindows != null && slideShowWindows.Count > 0)
                            {
                                isSlideShowActive = true;
                                LogHelper.WriteLogToFile($"检测到放映模式，轮询模式={_forcePolling}", LogHelper.LogType.Trace);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"检查SlideShowWindows失败: {ex.Message}", LogHelper.LogType.Trace);
                        }
                    }

                    if (isSlideShowActive && activePresentation != null)
                    {
                        dynamic pres = activePresentation;
                        slideShowWindow = pres.SlideShowWindow;
                        if (_pptSlideShowWindow == null || !PPTROTConnectionHelper.IsValidSlideShowWindow(_pptSlideShowWindow))
                        {
                            if (!PPTROTConnectionHelper.AreComObjectsEqual(_pptSlideShowWindow, slideShowWindow))
                            {
                                SafeReleaseComObject(_pptSlideShowWindow);
                                _pptSlideShowWindow = slideShowWindow;
                                LogHelper.WriteLogToFile("发现窗口，成功设置 slideshowwindow", LogHelper.LogType.Trace);
                            }
                        }
                    }
                }
                catch (COMException ex) when ((uint)ex.HResult == 0x8001010A)
                {
                    LogHelper.WriteLogToFile("PowerPoint 忙，稍后重试", LogHelper.LogType.Trace);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"发现窗口失败: {ex}", LogHelper.LogType.Warning);
                }
                finally
                {
                    if (activePresentation != null && (_pptActivePresentation == null || !PPTROTConnectionHelper.AreComObjectsEqual(_pptActivePresentation, activePresentation)))
                    {
                        SafeReleaseComObject(activePresentation);
                    }
                    if (slideShowWindow != null && !PPTROTConnectionHelper.AreComObjectsEqual(_pptSlideShowWindow, slideShowWindow))
                    {
                        SafeReleaseComObject(slideShowWindow);
                    }
                }

                if (isSlideShowActive)
                {
                    if ((DateTime.Now - _updateTime).TotalMilliseconds > 3000 || _forcePolling)
                    {
                        LogHelper.WriteLogToFile($"轮询", LogHelper.LogType.Trace);

                        try
                        {
                            dynamic pres = _pptActivePresentation;
                            if (pres == null)
                            {
                                LogHelper.WriteLogToFile("_pptActivePresentation为null，无法轮询", LogHelper.LogType.Warning);
                            }
                            else
                            {
                                slideShowWindow = pres.SlideShowWindow;

                                int tempTotalPage = -1;
                                if (slideShowWindow != null)
                                {
                                    tempTotalPage = GetTotalSlideIndex(_pptActivePresentation);
                                }

                                if (tempTotalPage == -1)
                                {
                                    SlidesCount = 0;
                                    _polling = 0;
                                }
                                else
                                {
                                    try
                                    {
                                        if (_pptSlideShowWindow == null)
                                        {
                                            LogHelper.WriteLogToFile("轮询: _pptSlideShowWindow为null", LogHelper.LogType.Warning);
                                        }
                                        else
                                        {
                                            int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);
                                            SlidesCount = tempTotalPage;
                                            if (currentPage >= tempTotalPage) _polling = 1;
                                            else _polling = 0;

                                            if (_lastPolledSlideNumber != -1 && currentPage != _lastPolledSlideNumber)
                                            {
                                                try
                                                {
                                                    LogHelper.WriteLogToFile($"轮询模式检测到页码变化: {_lastPolledSlideNumber} -> {currentPage}，触发事件", LogHelper.LogType.Trace);
                                                    SlideShowNextSlide?.Invoke(_pptSlideShowWindow);
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogHelper.WriteLogToFile($"触发轮询模式幻灯片切换事件失败: {ex.Message}", LogHelper.LogType.Warning);
                                                }
                                            }

                                            _lastPolledSlideNumber = currentPage;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        SlidesCount = 0;
                                        _polling = 1;
                                        LogHelper.WriteLogToFile($"获取当前页数失败: {ex}", LogHelper.LogType.Warning);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SlidesCount = 0;
                            LogHelper.WriteLogToFile($"获取总页数失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                        finally
                        {
                            SafeReleaseComObject(slideShowWindow);
                        }

                        _updateTime = DateTime.Now;
                    }

                    if (_polling != 0)
                    {
                        try
                        {
                            int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);

                            if (_lastPolledSlideNumber != -1 && currentPage != _lastPolledSlideNumber)
                            {
                                try
                                {
                                    LogHelper.WriteLogToFile($"轮询模式检测到页码变化: {_lastPolledSlideNumber} -> {currentPage}，触发事件", LogHelper.LogType.Trace);
                                    SlideShowNextSlide?.Invoke(_pptSlideShowWindow);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"触发轮询模式幻灯片切换事件失败: {ex.Message}", LogHelper.LogType.Warning);
                                }
                            }

                            _lastPolledSlideNumber = currentPage;
                            UpdateCurrentPresentationInfo();
                            _polling = 2;
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    if (_lastSlideShowState)
                    {
                        LogHelper.WriteLogToFile("轮询检测到放映已结束", LogHelper.LogType.Trace);

                        PPTROTConnectionHelper.SafeReleaseComObject(_pptSlideShowWindow);
                        _pptSlideShowWindow = null;
                        _lastPolledSlideNumber = -1;
                        _polling = 1;
                        SlidesCount = 0;

                        _lastSlideShowState = false;
                        SlideShowStateChanged?.Invoke(false);

                        if (_pptActivePresentation != null)
                        {
                            try
                            {
                                OnSlideShowEnd(_pptActivePresentation);
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"触发SlideShowEnd事件失败: {ex.Message}", LogHelper.LogType.Warning);
                            }
                        }
                    }
                    else
                    {
                        SlidesCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查演示文稿和放映状态失败: {ex}", LogHelper.LogType.Warning);
            }
        }

        private void CheckSlideShowState()
        {
            try
            {
                if (!IsConnected) return;

                var currentSlideShowState = IsInSlideShow;
                if (currentSlideShowState != _lastSlideShowState)
                {
                    _lastSlideShowState = currentSlideShowState;
                    SlideShowStateChanged?.Invoke(currentSlideShowState);

                    if (!currentSlideShowState)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查PPT放映状态异常: {ex}", LogHelper.LogType.Error);
            }
        }

        private Microsoft.Office.Interop.PowerPoint.Application TryConnectToPowerPoint()
        {
            try
            {
                var pptApp = PPTROTConnectionHelper.TryConnectViaROT(IsSupportWPS);
                return pptApp;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ROT连接PowerPoint异常: {ex}", LogHelper.LogType.Error);
                return null;
            }
        }

        private Microsoft.Office.Interop.PowerPoint.Application TryConnectToWPS()
        {
            try
            {
                var wpsApp = PPTROTConnectionHelper.TryConnectViaROT(true);
                return wpsApp;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ROT连接WPS异常: {ex}", LogHelper.LogType.Error);
                return null;
            }
        }

        private void ConnectToPPT(object pptApp)
        {
            try
            {
                if (pptApp != null)
                {
                    PPTApplication = pptApp;
                }

                try
                {
                    dynamic pptAppDynamic = PPTApplication;
                    try
                    {
                        _pptActivePresentation = pptAppDynamic.ActivePresentation;
                        _updateTime = DateTime.Now;
                        _lastPolledSlideNumber = -1;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"访问ActivePresentation失败: {ex.Message}，继续使用轮询模式", LogHelper.LogType.Warning);
                        _pptActivePresentation = null;
                        _updateTime = DateTime.Now;
                        _lastPolledSlideNumber = -1;
                    }

                    int tempTotalPage = -1;
                    if (_pptActivePresentation != null)
                    {
                        try
                        {
                            _pptSlideShowWindow = _pptActivePresentation.SlideShowWindow;
                            tempTotalPage = GetTotalSlideIndex(_pptActivePresentation);
                        }
                        catch
                        {
                            tempTotalPage = -1;
                        }
                    }
                    else
                    {
                        tempTotalPage = -1;
                    }

                    if (tempTotalPage == -1)
                    {
                        SlidesCount = 0;
                        _polling = 0;
                    }
                    else
                    {
                        try
                        {
                            if (_pptSlideShowWindow != null)
                            {
                                int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);
                                SlidesCount = tempTotalPage;
                                if (currentPage >= tempTotalPage) _polling = 1;
                                else _polling = 0;
                                _lastPolledSlideNumber = currentPage;
                            }
                            else
                            {
                                SlidesCount = tempTotalPage;
                                _polling = 0;
                                _lastPolledSlideNumber = -1;
                            }
                        }
                        catch
                        {
                            SlidesCount = 0;
                            _polling = 1;
                        }
                    }

                    try
                    {
                        if (PPTApplication != null)
                        {
                            try
                            {
                                Microsoft.Office.Interop.PowerPoint.Application pptAppForEvents = PPTApplication as Microsoft.Office.Interop.PowerPoint.Application;

                                if (pptAppForEvents != null)
                                {
                                    pptAppForEvents.SlideShowNextSlide += new EApplication_SlideShowNextSlideEventHandler(OnSlideShowNextSlide);
                                    pptAppForEvents.SlideShowBegin += new EApplication_SlideShowBeginEventHandler(OnSlideShowBegin);
                                    pptAppForEvents.SlideShowEnd += new EApplication_SlideShowEndEventHandler(OnSlideShowEndForComEvent);

                                    try
                                    {
                                        pptAppForEvents.PresentationBeforeClose += new EApplication_PresentationBeforeCloseEventHandler(OnPresentationBeforeClose);
                                    }
                                    catch
                                    {
                                        LogHelper.WriteLogToFile("无法注册PresentationBeforeClose事件", LogHelper.LogType.Warning);
                                    }

                                    _bindingEvents = true;

                                    LogHelper.WriteLogToFile("PPT事件注册成功", LogHelper.LogType.Trace);
                                }
                                else
                                {
                                    _bindingEvents = false;
                                    _forcePolling = true;
                                    LogHelper.WriteLogToFile("无法转换为强类型Application，使用轮询模式", LogHelper.LogType.Trace);
                                }
                            }
                            catch (Exception ex)
                            {
                                _bindingEvents = false;
                                _forcePolling = true;
                                LogHelper.WriteLogToFile($"事件注册失败: {ex.Message}，使用轮询模式", LogHelper.LogType.Trace);
                            }
                        }
                        else
                        {
                            _bindingEvents = false;
                            _forcePolling = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _bindingEvents = false;
                        _forcePolling = true;
                        LogHelper.WriteLogToFile($"无法注册事件: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    _bindingEvents = false;
                    _forcePolling = true;

                    if (_pptActivePresentation != null)
                    {
                        UpdateCurrentPresentationInfo();
                    }

                    PPTConnectionChanged?.Invoke(true);

                    try
                    {
                        dynamic pptAppDynamic2 = PPTApplication;
                        LogHelper.WriteLogToFile($"成功绑定! {pptAppDynamic2.Name}", LogHelper.LogType.Event);
                    }
                    catch
                    {
                        LogHelper.WriteLogToFile("成功绑定!", LogHelper.LogType.Event);
                    }

                    TryRaiseSlideShowBeginOnConnect();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"ConnectToPPT内部异常: {ex.Message}", LogHelper.LogType.Warning);
                    if (_pptActivePresentation == null && PPTApplication == null)
                    {
                        DisconnectFromPPT();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"连接PPT应用程序失败: {ex}", LogHelper.LogType.Error);
                PPTApplication = null;
            }
        }

        private void TryRaiseSlideShowBeginOnConnect()
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null)
                    return;

                if (!Marshal.IsComObject(PPTApplication))
                    return;

                dynamic app = PPTApplication;
                dynamic slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows == null || slideShowWindows.Count == 0)
                    return;

                dynamic slideShowWindow = slideShowWindows[1];
                if (slideShowWindow == null)
                    return;

                SlideShowBegin?.Invoke(slideShowWindow);
                LogHelper.WriteLogToFile("检测到放映已在进行中，热重载", LogHelper.LogType.Trace);
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                LogHelper.WriteLogToFile($"TryRaiseSlideShowBeginOnConnect COM 异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"TryRaiseSlideShowBeginOnConnect 异常: {ex}", LogHelper.LogType.Trace);
            }
        }

        private void UnbindEvents()
        {
            try
            {
                if (_bindingEvents && PPTApplication != null)
                {
                    try
                    {
                        Microsoft.Office.Interop.PowerPoint.Application app = PPTApplication as Microsoft.Office.Interop.PowerPoint.Application;

                        if (app != null)
                        {
                            app.SlideShowNextSlide -= new EApplication_SlideShowNextSlideEventHandler(OnSlideShowNextSlide);
                            app.SlideShowBegin -= new EApplication_SlideShowBeginEventHandler(OnSlideShowBegin);
                            app.SlideShowEnd -= new EApplication_SlideShowEndEventHandler(OnSlideShowEndForComEvent);
                            app.PresentationBeforeClose -= new EApplication_PresentationBeforeCloseEventHandler(OnPresentationBeforeClose);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"取消PPT事件注册失败: {ex.Message}", LogHelper.LogType.Trace);
                    }

                    _bindingEvents = false;
                }
            }
            catch { }
        }

        private static bool IsObjectNull(object comObject)
        {
            return ReferenceEquals(comObject, null);
        }

        private void DisconnectFromPPT()
        {
            object pptApplication = PPTApplication;
            object activePresentation = _pptActivePresentation;
            object slideShowWindow = _pptSlideShowWindow;
            object currentPresentation = CurrentPresentation;
            object currentSlides = CurrentSlides;
            object currentSlide = CurrentSlide;

            if (IsObjectNull(pptApplication) && IsObjectNull(activePresentation) && IsObjectNull(slideShowWindow) &&
                IsObjectNull(currentPresentation) && IsObjectNull(currentSlides) && IsObjectNull(currentSlide))
            {
                return;
            }

            _isModuleUnloading = true;

            try
            {
                UnbindEvents();

                if (_pptActivePresentation != null)
                {
                    try
                    {
                        PresentationClose?.Invoke(_pptActivePresentation);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"触发PresentationClose事件失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                SafeReleaseComObject(_pptSlideShowWindow, "_pptSlideShowWindow");
                SafeReleaseComObject(_pptActivePresentation, "_pptActivePresentation");
                SafeReleaseComObject(CurrentSlide, "CurrentSlide");
                SafeReleaseComObject(CurrentSlides, "CurrentSlides");
                SafeReleaseComObject(CurrentPresentation, "CurrentPresentation");

                if (PPTApplication != null)
                {
                    try
                    {
                        if (Marshal.IsComObject(PPTApplication))
                        {
                            try
                            {
                                var unk = Marshal.GetIUnknownForObject(PPTApplication);
                                Marshal.Release(unk);

                                try
                                {
                                    Marshal.FinalReleaseComObject(PPTApplication);
                                }
                                catch
                                {
                                    try
                                    {
                                        int refCount = Marshal.ReleaseComObject(PPTApplication);
                                        while (refCount > 0)
                                        {
                                            refCount = Marshal.ReleaseComObject(PPTApplication);
                                        }
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            catch (InvalidComObjectException)
                            {
                                LogHelper.WriteLogToFile("PPTApplication COM对象已失效，跳过释放", LogHelper.LogType.Trace);
                            }
                            catch (COMException comEx) when (IsIgnorableDisconnectComException(comEx))
                            {
                                LogHelper.WriteLogToFile(
                                    $"PPTApplication COM对象在断开连接时已不可用，跳过释放 (HR: 0x{(uint)comEx.HResult:X8})",
                                    LogHelper.LogType.Trace);
                            }
                        }
                    }
                    catch (InvalidComObjectException)
                    {
                        LogHelper.WriteLogToFile("PPTApplication COM对象已失效，跳过释放", LogHelper.LogType.Trace);
                    }
                    catch (COMException comEx) when (IsIgnorableDisconnectComException(comEx))
                    {
                        LogHelper.WriteLogToFile(
                            $"释放PPTApplication时检测到COM对象已不可用，跳过释放 (HR: 0x{(uint)comEx.HResult:X8})",
                            LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"释放PPTApplication时发生异常: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                PPTApplication = null;
                _pptActivePresentation = null;
                _pptSlideShowWindow = null;
                CurrentPresentation = null;
                CurrentSlides = null;
                CurrentSlide = null;
                SlidesCount = 0;
                _polling = 0;
                _forcePolling = true;
                _bindingEvents = false;

                PPTConnectionChanged?.Invoke(false);

                LogHelper.WriteLogToFile("已断开PPT连接，并显式释放所有COM对象", LogHelper.LogType.Event);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Thread.Sleep(2000);
                        _isModuleUnloading = false;

                        LogHelper.WriteLogToFile("PPT联动模块已允许重新连接", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"重新启用PPT联动模块失败: {ex}", LogHelper.LogType.Error);
                        _isModuleUnloading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"断开PPT连接失败: {ex}", LogHelper.LogType.Error);
                _isModuleUnloading = false;
            }
        }

        /// <summary>
        /// 安全释放COM对象
        /// </summary>
        private void SafeReleaseComObject(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            catch { }
        }

        private void SafeReleaseComObject(object comObject, string objectName)
        {
            try
            {
                if (comObject != null)
                {
                    // 检查是否为有效的COM对象
                    if (!Marshal.IsComObject(comObject))
                    {
                        return; // 不是COM对象，无需释放
                    }

                    // 检查COM对象是否仍然有效
                    try
                    {
                        // 尝试访问对象以验证其有效性
                        var _ = Marshal.GetIUnknownForObject(comObject);
                        Marshal.Release(_);
                    }
                    catch (InvalidComObjectException)
                    {
                        // COM对象已失效，直接返回
                        LogHelper.WriteLogToFile($"COM对象 {objectName} 已失效，跳过释放", LogHelper.LogType.Trace);
                        return;
                    }
                    catch (COMException comEx) when (IsIgnorableDisconnectComException(comEx))
                    {
                        LogHelper.WriteLogToFile(
                            $"COM对象 {objectName} 在释放前已不可用，跳过释放 (HR: 0x{(uint)comEx.HResult:X8})",
                            LogHelper.LogType.Trace);
                        return;
                    }

                    int refCount = Marshal.ReleaseComObject(comObject);
                    LogHelper.WriteLogToFile($"已释放COM对象 {objectName}，引用计数: {refCount}", LogHelper.LogType.Trace);
                }
            }
            catch (InvalidComObjectException)
            {
                // COM对象已失效，这是正常的，无需记录错误
                LogHelper.WriteLogToFile($"COM对象 {objectName} 已失效，跳过释放", LogHelper.LogType.Trace);
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                var logType = IsIgnorableDisconnectComException(comEx) ? LogHelper.LogType.Trace : LogHelper.LogType.Warning;
                LogHelper.WriteLogToFile($"释放COM对象 {objectName} 时COM异常: {comEx.Message} (HR: 0x{hr:X8})", logType);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放COM对象 {objectName} 时发生异常: {ex}", LogHelper.LogType.Warning);
            }
        }

        private static bool IsIgnorableDisconnectComException(COMException comEx)
        {
            var hr = (uint)comEx.HResult;
            return hr == 0x800706BA || // RPC server unavailable
                   hr == 0x80010108 || // object disconnected from clients
                   hr == 0x8001010D || // server died
                   hr == 0x800706BE;   // remote procedure call failed
        }

        private void UpdateCurrentPresentationInfo()
        {
            object activeWindow = null;
            object view = null;
            object selection = null;
            object slideRange = null;

            try
            {
                if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                {
                    try
                    {
                        if (_pptActivePresentation != null)
                        {
                            try
                            {
                                // 检查COM对象是否仍然有效
                                if (!Marshal.IsComObject(_pptActivePresentation))
                                {
                                    CurrentPresentation = null;
                                    CurrentSlides = null;
                                }
                                else
                                {
                                    try
                                    {
                                        var _ = Marshal.GetIUnknownForObject(_pptActivePresentation);
                                        Marshal.Release(_);
                                    }
                                    catch (InvalidComObjectException)
                                    {
                                        CurrentPresentation = null;
                                        CurrentSlides = null;
                                        return;
                                    }

                                    dynamic pres = _pptActivePresentation;
                                    CurrentPresentation = pres;
                                    CurrentSlides = pres.Slides;
                                }
                            }
                            catch (InvalidComObjectException)
                            {
                                LogHelper.WriteLogToFile($"访问演示文稿属性失败: COM对象已失效", LogHelper.LogType.Warning);
                                CurrentPresentation = null;
                                CurrentSlides = null;
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"访问演示文稿属性失败: {ex.Message}", LogHelper.LogType.Warning);
                                CurrentPresentation = null;
                                CurrentSlides = null;
                            }

                            if (CurrentSlides != null)
                            {
                                try
                                {
                                    var slideCount = CurrentSlides.Count;
                                    if (slideCount > 0)
                                    {
                                        SlidesCount = slideCount;
                                    }
                                    else
                                    {
                                        SlidesCount = 0;
                                        LogHelper.WriteLogToFile("PPT演示文稿页数为0，可能为空演示文稿", LogHelper.LogType.Warning);
                                    }
                                }
                                catch (COMException comEx)
                                {
                                    var hr = (uint)comEx.HResult;
                                    SlidesCount = 0;
                                    LogHelper.WriteLogToFile($"读取PPT页数失败: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                                }

                                try
                                {
                                    if (IsInSlideShow && _pptSlideShowWindow != null)
                                    {
                                        try
                                        {
                                            dynamic ssw = _pptSlideShowWindow;
                                            view = ssw.View;
                                            if (view != null)
                                            {
                                                dynamic viewObj = view;
                                                CurrentSlide = viewObj.Slide;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogHelper.WriteLogToFile($"获取SlideShowWindow的Slide失败: {ex.Message}", LogHelper.LogType.Trace);
                                        }
                                    }
                                    else
                                    {
                                        activeWindow = PPTApplication.ActiveWindow;
                                        if (activeWindow != null)
                                        {
                                            dynamic aw = activeWindow;
                                            selection = aw.Selection;
                                            if (selection != null)
                                            {
                                                dynamic sel = selection;
                                                slideRange = sel.SlideRange;
                                                if (slideRange != null)
                                                {
                                                    dynamic sr = slideRange;
                                                    int slideNumber = sr.SlideNumber;
                                                    if (slideNumber > 0 && slideNumber <= SlidesCount)
                                                    {
                                                        CurrentSlide = CurrentSlides[slideNumber];
                                                    }
                                                }
                                            }
                                        }

                                        if (CurrentSlide == null && SlidesCount > 0)
                                        {
                                            CurrentSlide = CurrentSlides[1];
                                        }
                                    }
                                }
                                catch (COMException comEx)
                                {
                                    var hr = (uint)comEx.HResult;
                                    if (hr != 0x8001010E && hr != 0x80004005)
                                    {
                                        LogHelper.WriteLogToFile($"获取当前幻灯片失败: {comEx.Message}", LogHelper.LogType.Warning);
                                    }

                                    if (SlidesCount > 0)
                                    {
                                        CurrentSlide = CurrentSlides[1];
                                    }
                                }
                            }
                            else
                            {
                                CurrentPresentation = null;
                                CurrentSlides = null;
                                CurrentSlide = null;
                                SlidesCount = 0;
                            }
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x8001010E || hr == 0x80004005)
                        {
                            CurrentPresentation = null;
                            CurrentSlides = null;
                            CurrentSlide = null;
                            SlidesCount = 0;
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"访问活动演示文稿失败: {comEx}", LogHelper.LogType.Warning);
                        }
                    }
                }
                else
                {
                    CurrentPresentation = null;
                    CurrentSlides = null;
                    CurrentSlide = null;
                    SlidesCount = 0;
                }
            }
            catch (InvalidComObjectException)
            {
                LogHelper.WriteLogToFile($"更新演示文稿信息失败: COM对象已失效", LogHelper.LogType.Warning);
                CurrentPresentation = null;
                CurrentSlides = null;
                CurrentSlide = null;
                SlidesCount = 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新演示文稿信息失败: {ex}", LogHelper.LogType.Error);
                CurrentPresentation = null;
                CurrentSlides = null;
                CurrentSlide = null;
                SlidesCount = 0;
            }
            finally
            {
                SafeReleaseComObject(slideRange);
                SafeReleaseComObject(selection);
                SafeReleaseComObject(view);
                SafeReleaseComObject(activeWindow);
            }
        }
        #endregion

        #region Event Handlers
        private void OnPresentationOpen(Presentation pres)
        {
            try
            {
                if (PPTApplication != null && pres != null)
                {
                    try
                    {
                        _pptActivePresentation = PPTApplication.ActivePresentation;
                        _updateTime = DateTime.Now;
                    }
                    catch { }
                }

                UpdateCurrentPresentationInfo();
                PresentationOpen?.Invoke(pres);
                LogHelper.WriteLogToFile($"演示文稿已打开: {pres?.Name}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPresentationBeforeClose(Presentation pres, ref bool cancel)
        {
            try
            {
                LogHelper.WriteLogToFile("PresentationBeforeClose事件触发", LogHelper.LogType.Trace);

                if (_bindingEvents && PPTApplication != null)
                {
                    try
                    {
                        Microsoft.Office.Interop.PowerPoint.Application app = PPTApplication as Microsoft.Office.Interop.PowerPoint.Application;

                        if (app != null)
                        {
                            app.SlideShowNextSlide -= new EApplication_SlideShowNextSlideEventHandler(OnSlideShowNextSlide);
                            app.SlideShowBegin -= new EApplication_SlideShowBeginEventHandler(OnSlideShowBegin);
                            app.SlideShowEnd -= new EApplication_SlideShowEndEventHandler(OnSlideShowEndForComEvent);
                            app.PresentationBeforeClose -= new EApplication_PresentationBeforeCloseEventHandler(OnPresentationBeforeClose);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"取消PPT事件注册失败: {ex.Message}", LogHelper.LogType.Trace);
                    }

                    _bindingEvents = false;
                }

                DisconnectFromPPT();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿关闭前事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowBegin(object wn)
        {
            try
            {
                _updateTime = DateTime.Now;
                _pptSlideShowWindow = wn;
                _lastPolledSlideNumber = -1; // 重置页码跟踪

                try
                {
                    if (_pptActivePresentation != null)
                    {
                        int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);
                        int totalPage = GetTotalSlideIndex(_pptActivePresentation);

                        if (currentPage >= totalPage) _polling = 1;
                        else _polling = 0;

                        SlidesCount = totalPage;
                        _lastPolledSlideNumber = currentPage; // 初始化页码跟踪
                    }
                }
                catch
                {
                    _polling = 1;
                    _lastPolledSlideNumber = -1;
                }

                UpdateCurrentPresentationInfo();

                if (!_lastSlideShowState)
                {
                    _lastSlideShowState = true;
                    SlideShowStateChanged?.Invoke(true);
                }

                SlideShowBegin?.Invoke(wn);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映开始事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowNextSlide(SlideShowWindow wn)
        {
            try
            {
                _updateTime = DateTime.Now;

                try
                {
                    if (_pptActivePresentation != null && _pptSlideShowWindow != null)
                    {
                        int currentPage = GetCurrentSlideIndex(_pptSlideShowWindow);
                        int totalPage = GetTotalSlideIndex(_pptActivePresentation);

                        if (currentPage >= totalPage) _polling = 1;
                        else _polling = 0;

                        _lastPolledSlideNumber = currentPage; // 更新页码跟踪
                    }
                }
                catch
                {
                    _polling = 1;
                }

                UpdateCurrentPresentationInfo();
                SlideShowNextSlide?.Invoke(wn);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片切换事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowEnd(object pres)
        {
            try
            {
                _updateTime = DateTime.Now;
                SlidesCount = 0;
                _lastPolledSlideNumber = -1;
                _polling = 1;

                PPTROTConnectionHelper.SafeReleaseComObject(_pptSlideShowWindow);
                _pptSlideShowWindow = null;

                if (IsSupportWPS && PPTApplication != null)
                {
                    RecordWpsProcessForManagement();
                }

                UpdateCurrentPresentationInfo();

                if (_lastSlideShowState)
                {
                    _lastSlideShowState = false;
                    SlideShowStateChanged?.Invoke(false);
                }

                SlideShowEnd?.Invoke(pres);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映结束事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowEndForComEvent(Presentation pres)
        {
            OnSlideShowEnd(pres);
        }
        #endregion

        #region Public Methods
        public bool TryNavigateToSlide(int slideNumber)
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            object windows = null;
            object window = null;
            object windowView = null;
            try
            {
                if (!IsConnected || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                if (IsInSlideShow)
                {
                    slideShowWindows = PPTApplication.SlideShowWindows;
                    if (slideShowWindows != null)
                    {
                        dynamic ssw = slideShowWindows;
                        if (ssw.Count >= 1)
                        {
                            slideShowWindow = ssw[1];
                            if (slideShowWindow != null)
                            {
                                dynamic sswObj = slideShowWindow;
                                view = sswObj.View;
                                if (view != null)
                                {
                                    dynamic viewObj = view;
                                    viewObj.GotoSlide(slideNumber);
                                    return true;
                                }
                            }
                        }
                    }
                }
                else if (CurrentPresentation != null)
                {
                    windows = CurrentPresentation.Windows;
                    if (windows != null)
                    {
                        dynamic win = windows;
                        if (win.Count >= 1)
                        {
                            window = win[1];
                            if (window != null)
                            {
                                dynamic winObj = window;
                                windowView = winObj.View;
                                if (windowView != null)
                                {
                                    dynamic viewObj = windowView;
                                    viewObj.GotoSlide(slideNumber);
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"跳转到幻灯片{slideNumber}失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"跳转到幻灯片{slideNumber}失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(windowView);
                SafeReleaseComObject(window);
                SafeReleaseComObject(windows);
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public bool TryNavigateNext()
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                // 在新线程中执行翻页操作，避免等待动画完成
                new Thread(() =>
                {
                    try
                    {
                        object slideShowWindows = PPTApplication.SlideShowWindows;
                        if (slideShowWindows != null)
                        {
                            dynamic ssw = slideShowWindows;
                            object slideShowWindow = ssw[1];
                            if (slideShowWindow != null)
                            {
                                dynamic sswObj = slideShowWindow;
                                try
                                {
                                    sswObj.Activate();
                                }
                                catch { }
                                try
                                {
                                    object view = sswObj.View;
                                    if (view != null)
                                    {
                                        dynamic viewObj = view;
                                        viewObj.Next();
                                    }
                                }
                                catch { }
                                SafeReleaseComObject(slideShowWindow);
                            }
                            SafeReleaseComObject(slideShowWindows);
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x8001010E || hr == 0x80004005)
                        {
                            DisconnectFromPPT();
                        }
                        LogHelper.WriteLogToFile($"切换到下一页失败: {comEx.Message}", LogHelper.LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"切换到下一页失败: {ex}", LogHelper.LogType.Error);
                    }
                }).Start();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动下一页线程失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        public bool TryNavigatePrevious()
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                // 在新线程中执行翻页操作，避免等待动画完成
                new Thread(() =>
                {
                    try
                    {
                        object slideShowWindows = PPTApplication.SlideShowWindows;
                        if (slideShowWindows != null)
                        {
                            dynamic ssw = slideShowWindows;
                            object slideShowWindow = ssw[1];
                            if (slideShowWindow != null)
                            {
                                dynamic sswObj = slideShowWindow;
                                try
                                {
                                    sswObj.Activate();
                                }
                                catch { }
                                try
                                {
                                    object view = sswObj.View;
                                    if (view != null)
                                    {
                                        dynamic viewObj = view;
                                        viewObj.Previous();
                                    }
                                }
                                catch { }
                                SafeReleaseComObject(slideShowWindow);
                            }
                            SafeReleaseComObject(slideShowWindows);
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x8001010E || hr == 0x80004005)
                        {
                            DisconnectFromPPT();
                        }
                        LogHelper.WriteLogToFile($"切换到上一页失败: {comEx.Message}", LogHelper.LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"切换到上一页失败: {ex}", LogHelper.LogType.Error);
                    }
                }).Start();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动上一页线程失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        public bool TryEndSlideShow()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                slideShowWindows = PPTApplication.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow != null)
                    {
                        dynamic sswObj = slideShowWindow;
                        view = sswObj.View;
                        if (view != null)
                        {
                            dynamic viewObj = view;
                            viewObj.Exit();
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"结束幻灯片放映失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束幻灯片放映失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public bool TryStartSlideShow()
        {
            try
            {
                if (!IsConnected || CurrentPresentation == null || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication) || !Marshal.IsComObject(CurrentPresentation)) return false;

                CurrentPresentation.SlideShowSettings.Run();
                return true;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"开始幻灯片放映失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始幻灯片放映失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取当前活跃的演示文稿
        /// </summary>
        public object GetCurrentActivePresentation()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            object slide = null;
            object activeWindow = null;
            object presentation = null;
            try
            {
                if (!IsConnected || PPTApplication == null) return null;
                if (!Marshal.IsComObject(PPTApplication)) return null;

                // 检查COM对象是否仍然有效
                try
                {
                    var _ = Marshal.GetIUnknownForObject(PPTApplication);
                    Marshal.Release(_);
                }
                catch (InvalidComObjectException)
                {
                    // COM对象已失效
                    return null;
                }

                if (IsInSlideShow)
                {
                    dynamic pptAppForSSW = PPTApplication;
                    slideShowWindows = pptAppForSSW.SlideShowWindows;
                    if (slideShowWindows != null)
                    {
                        dynamic ssw = slideShowWindows;
                        if (ssw.Count > 0)
                        {
                            slideShowWindow = ssw[1];
                            if (slideShowWindow != null)
                            {
                                dynamic sswObj = slideShowWindow;
                                view = sswObj.View;
                                if (view != null)
                                {
                                    dynamic viewObj = view;
                                    slide = viewObj.Slide;
                                    if (slide != null)
                                    {
                                        dynamic slideObj = slide;
                                        presentation = slideObj.Parent;
                                        return presentation;
                                    }
                                }
                            }
                        }
                    }
                }

                activeWindow = PPTApplication.ActiveWindow;
                if (activeWindow != null)
                {
                    dynamic aw = activeWindow;
                    presentation = aw.Presentation;
                    if (presentation != null)
                    {
                        return presentation;
                    }
                }

                return CurrentPresentation;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                if (hr == 0x80048240)
                {
                    return null;
                }
                LogHelper.WriteLogToFile($"获取当前活跃演示文稿失败: {comEx.Message}", LogHelper.LogType.Warning);
                return CurrentPresentation;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前活跃演示文稿失败: {ex}", LogHelper.LogType.Error);
                return CurrentPresentation;
            }
            finally
            {
                if (presentation != null && !ReferenceEquals(presentation, CurrentPresentation))
                {
                    SafeReleaseComObject(presentation);
                }
                SafeReleaseComObject(slide);
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
                SafeReleaseComObject(activeWindow);
            }
        }

        /// <summary>
        /// 获取当前幻灯片编号
        /// </summary>
        private int GetCurrentSlideIndex(dynamic slideShowWindow)
        {
            object view = null;
            object slide = null;

            try
            {
                if (slideShowWindow == null) return 0;

                // 检查COM对象是否有效
                if (!Marshal.IsComObject(slideShowWindow))
                {
                    return 0;
                }

                try
                {
                    var _ = Marshal.GetIUnknownForObject(slideShowWindow);
                    Marshal.Release(_);
                }
                catch (InvalidComObjectException)
                {
                    return 0;
                }

                dynamic ssw = slideShowWindow;
                view = ssw.View;
                if (view != null)
                {
                    dynamic viewObj = view;
                    slide = viewObj.Slide;
                    if (slide != null)
                    {
                        dynamic slideObj = slide;
                        return slideObj.SlideIndex;
                    }
                }
                return 0;
            }
            catch (COMException comEx)
            {
                // 处理 0x80048240: SlideShowView.Slide : Invalid request. No slide is currently in view.
                var hr = (uint)comEx.HResult;
                if (hr == 0x80048240)
                {
                    return 0;
                }
                throw;
            }
            catch (InvalidComObjectException)
            {
                return 0;
            }
            finally
            {
                SafeReleaseComObject(slide);
                SafeReleaseComObject(view);
            }
        }

        private int GetTotalSlideIndex(dynamic presentation)
        {
            try
            {
                if (presentation == null) return 0;
                dynamic pres = presentation;
                return pres.Slides.Count;
            }
            catch
            {
                return 0;
            }
        }

        public int GetCurrentSlideNumber()
        {
            object activeWindow = null;
            object selection = null;
            object slideRange = null;
            try
            {
                if (!IsConnected || PPTApplication == null) return 0;
                if (!Marshal.IsComObject(PPTApplication)) return 0;

                if (IsInSlideShow && _pptSlideShowWindow != null)
                {
                    try
                    {
                        return GetCurrentSlideIndex(_pptSlideShowWindow);
                    }
                    catch
                    {
                        return 0;
                    }
                }

                activeWindow = PPTApplication.ActiveWindow;
                if (activeWindow != null)
                {
                    dynamic aw = activeWindow;
                    selection = aw.Selection;
                    if (selection != null)
                    {
                        dynamic sel = selection;
                        slideRange = sel.SlideRange;
                        if (slideRange != null)
                        {
                            dynamic sr = slideRange;
                            int slideNumber = sr.SlideNumber;
                            if (slideNumber > 0)
                            {
                                return slideNumber;
                            }
                        }
                    }
                }

                if (CurrentSlide != null && Marshal.IsComObject(CurrentSlide))
                {
                    return CurrentSlide.SlideNumber;
                }

                return 0;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
            finally
            {
                SafeReleaseComObject(slideRange);
                SafeReleaseComObject(selection);
                SafeReleaseComObject(activeWindow);
            }
        }

        public string GetPresentationName()
        {
            try
            {
                if (CurrentPresentation == null || !Marshal.IsComObject(CurrentPresentation)) return "";

                return CurrentPresentation.Name ?? "";
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"获取演示文稿名称失败: {comEx.Message}", LogHelper.LogType.Warning);
                return "";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取演示文稿名称失败: {ex}", LogHelper.LogType.Error);
                return "";
            }
        }

        public string GetPresentationPath()
        {
            try
            {
                if (CurrentPresentation == null || !Marshal.IsComObject(CurrentPresentation)) return "";

                return CurrentPresentation.FullName ?? "";
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"获取演示文稿路径失败: {comEx.Message}", LogHelper.LogType.Warning);
                return "";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取演示文稿路径失败: {ex}", LogHelper.LogType.Error);
                return "";
            }
        }

        public bool TryShowSlideNavigation()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object slideNavigation = null;
            try
            {
                LogHelper.WriteLogToFile($"尝试显示幻灯片导航 - 连接状态: {IsConnected}, 放映状态: {IsInSlideShow}", LogHelper.LogType.Trace);

                if (!IsConnected || !IsInSlideShow || PPTApplication == null)
                {
                    LogHelper.WriteLogToFile("PPT未连接或未在放映状态", LogHelper.LogType.Warning);
                    return false;
                }

                if (!Marshal.IsComObject(PPTApplication))
                {
                    LogHelper.WriteLogToFile("PPT应用程序COM对象无效", LogHelper.LogType.Warning);
                    return false;
                }

                slideShowWindows = PPTApplication.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow == null)
                    {
                        LogHelper.WriteLogToFile("幻灯片放映窗口为空", LogHelper.LogType.Warning);
                        return false;
                    }

                    try
                    {
                        dynamic sswObj = slideShowWindow;
                        slideNavigation = sswObj.SlideNavigation;
                        if (slideNavigation != null)
                        {
                            dynamic sn = slideNavigation;
                            sn.Visible = true;
                            LogHelper.WriteLogToFile("成功显示幻灯片导航（PowerPoint模式）", LogHelper.LogType.Event);
                            return true;
                        }

                        LogHelper.WriteLogToFile("SlideNavigation对象为空，可能是WPS不支持此功能", LogHelper.LogType.Warning);
                        return false;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x80020006)
                        {
                            LogHelper.WriteLogToFile("WPS不支持SlideNavigation功能", LogHelper.LogType.Warning);
                            return false;
                        }
                        throw;
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"显示幻灯片导航COM异常: {comEx.Message} (HRESULT: 0x{hr:X8})", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示幻灯片导航失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(slideNavigation);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }
        #endregion

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
                    LogHelper.WriteLogToFile($"成功记录 WPS 进程 ID: {wpsProcess.Id}", LogHelper.LogType.Trace);

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
            LogHelper.WriteLogToFile("启动 WPS 进程检测定时器", LogHelper.LogType.Trace);
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
                    LogHelper.WriteLogToFile("WPS 进程已正常关闭", LogHelper.LogType.Trace);
                    StopWpsProcessCheckTimer();
                    return;
                }


                // 检查前台WPS窗口是否存在
                bool isForegroundWpsWindowActive = IsForegroundWpsWindowStillActiveOptimized();

                if (isForegroundWpsWindowActive)
                {
                    if (_wpsProcessCheckCount % 5 == 0) // 每10秒记录一次日志
                    {
                        LogHelper.WriteLogToFile($"WPS窗口仍然活跃，继续监控（已检查{_wpsProcessCheckCount}次）", LogHelper.LogType.Trace);
                    }
                    return;
                }

                // 多重验证确保准确性
                if (!PerformMultipleWpsWindowChecks())
                {
                    LogHelper.WriteLogToFile("多重验证显示WPS窗口仍然存在，跳过查杀", LogHelper.LogType.Trace);
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
                    LogHelper.WriteLogToFile("第一重验证：WPS窗口仍然存在", LogHelper.LogType.Trace);
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
                        LogHelper.WriteLogToFile($"第二重验证：发现其他WPS进程{process.Id}有活跃窗口", LogHelper.LogType.Trace);
                        return false;
                    }
                }

                // 第三重验证：检查任务栏中的WPS窗口
                if (HasWpsWindowInTaskbar())
                {
                    LogHelper.WriteLogToFile("第三重验证：任务栏中仍有WPS窗口", LogHelper.LogType.Trace);
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
                    catch { }
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
                    LogHelper.WriteLogToFile("WPS进程已经结束，无需查杀", LogHelper.LogType.Trace);
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
                    LogHelper.WriteLogToFile("已释放pptActWindow对象", LogHelper.LogType.Trace);
                }

                // 第二步：释放 pptActDoc 对象（CurrentPresentation）
                Presentation pptActDoc = CurrentPresentation;
                if (pptActDoc != null)
                {
                    Marshal.ReleaseComObject(pptActDoc);
                    pptActDoc = null;
                    CurrentPresentation = null;
                    LogHelper.WriteLogToFile("已释放pptActDoc对象", LogHelper.LogType.Trace);
                }

                // 第三步：释放 pptApp 对象（PPTApplication）
                if (PPTApplication != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(PPTApplication);
                        PPTApplication = null;
                        LogHelper.WriteLogToFile("已释放pptApp对象", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"释放pptApp对象失败: {ex.Message}", LogHelper.LogType.Trace);
                        PPTApplication = null;
                    }
                }

                // 第四步：强制垃圾回收及等待终结器执行
                LogHelper.WriteLogToFile("执行强制垃圾回收", LogHelper.LogType.Trace);
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
                    try { Marshal.ReleaseComObject(CurrentSlide); } catch { }
                }
                if (CurrentSlides != null && Marshal.IsComObject(CurrentSlides))
                {
                    try { Marshal.ReleaseComObject(CurrentSlides); } catch { }
                }
                if (CurrentPresentation != null && Marshal.IsComObject(CurrentPresentation))
                {
                    try { Marshal.ReleaseComObject(CurrentPresentation); } catch { }
                }
                if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                {
                    try { Marshal.ReleaseComObject(PPTApplication); } catch { }
                }

                CurrentSlide = null;
                CurrentSlides = null;
                CurrentPresentation = null;
                PPTApplication = null;
                SlidesCount = 0;
                StopWpsProcessCheckTimer();

                PPTConnectionChanged?.Invoke(false);

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
            LogHelper.WriteLogToFile("停止 WPS 进程检测定时器", LogHelper.LogType.Trace);
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
            catch { }

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
                                    LogHelper.WriteLogToFile($"检测到WPS进程: {process.ProcessName} (PID: {process.Id})", LogHelper.LogType.Trace);
                                }
                            }
                            catch
                            {
                                // 无法访问进程路径时，基于进程名判断
                                if (exactWpsNames.Contains(pname))
                                {
                                    wpsProcesses.Add(process);
                                    LogHelper.WriteLogToFile($"基于进程名检测到WPS进程: {process.ProcessName} (PID: {process.Id})", LogHelper.LogType.Trace);
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

            LogHelper.WriteLogToFile($"共检测到{wpsProcesses.Count}个WPS进程", LogHelper.LogType.Trace);
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
                        LogHelper.WriteLogToFile($"前台WPS窗口发生变化: {_lastForegroundWpsWindow.Title} -> {currentForegroundWindow.Title}", LogHelper.LogType.Trace);
                    }
                }
                else if (_lastForegroundWpsWindow == null && currentForegroundWindow != null)
                {
                    LogHelper.WriteLogToFile($"检测到新的前台WPS窗口: {currentForegroundWindow.Title}", LogHelper.LogType.Trace);
                }
                else if (_lastForegroundWpsWindow != null && currentForegroundWindow == null)
                {
                    LogHelper.WriteLogToFile($"前台WPS窗口已消失: {_lastForegroundWpsWindow.Title}", LogHelper.LogType.Trace);
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
        public IntPtr GetPptHwnd()
        {
            IntPtr ret = IntPtr.Zero;

            // 方法1: 尝试从 SlideShowWindow 获取
            ret = GetPptHwndFromSlideShowWindow(_pptSlideShowWindow);

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
                        ret = GetPptHwndWin32(fullName, appName);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"获取PPT窗口句柄失败: {ex.Message}", LogHelper.LogType.Trace);
                }
            }

            return ret;
        }

        /// <summary>
        /// 从 SlideShowWindow 对象获取窗口句柄
        /// </summary>
        private IntPtr GetPptHwndFromSlideShowWindow(object pptSlideShowWindowObj)
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
                    LogHelper.WriteLogToFile($"从SlideShowWindow获取窗口句柄成功: {hwnd}", LogHelper.LogType.Trace);
                }
                else
                {
                    // 如果强类型转换失败，尝试使用dynamic
                    try
                    {
                        dynamic ssw = pptSlideShowWindowObj;
                        int hwndVal = ssw.HWND;
                        hwnd = new IntPtr(hwndVal);
                        LogHelper.WriteLogToFile($"从SlideShowWindow获取窗口句柄成功(dynamic): {hwnd}", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"从SlideShowWindow获取窗口句柄失败: {ex.Message}", LogHelper.LogType.Trace);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从SlideShowWindow获取窗口句柄异常: {ex.Message}", LogHelper.LogType.Trace);
            }

            return hwnd;
        }

        /// <summary>
        /// 通过窗口标题匹配获取PPT窗口句柄（备用方法）
        /// </summary>
        private IntPtr GetPptHwndWin32(string presFullName, string appName)
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
                    LogHelper.WriteLogToFile($"通过窗口标题匹配获取窗口句柄成功: {candidates[0]}", LogHelper.LogType.Trace);
                    return candidates[0];
                }
                else if (candidates.Count > 1)
                {
                    LogHelper.WriteLogToFile($"通过窗口标题匹配找到多个候选窗口({candidates.Count}个)，无法确定唯一窗口", LogHelper.LogType.Trace);
                }

                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                // 发生任何不可预知的异常（如Path解析错误等），返回安全值
                LogHelper.WriteLogToFile($"GetPptHwndWin32异常: {ex.Message}", LogHelper.LogType.Trace);
                return IntPtr.Zero;
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                StopWpsProcessCheckTimer();

                _disposed = true;
            }
        }
        #endregion
    }
}


