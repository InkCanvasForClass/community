using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT联动管理器 - 统一管理PPT和WPS的连接、事件处理和进程管理
    /// </summary>
    public class PPTManager : IDisposable
    {
        #region Events
        public event Action<SlideShowWindow> SlideShowBegin;
        public event Action<SlideShowWindow> SlideShowNextSlide;
        public event Action<Presentation> SlideShowEnd;
        public event Action<Presentation> PresentationOpen;
        public event Action<Presentation> PresentationClose;
        public event Action<bool> PPTConnectionChanged;
        public event Action<bool> SlideShowStateChanged;
        #endregion

        #region Properties
        public Microsoft.Office.Interop.PowerPoint.Application PPTApplication { get; private set; }
        public Presentation CurrentPresentation { get; private set; }
        public Slides CurrentSlides { get; private set; }
        public Slide CurrentSlide { get; private set; }
        public int SlidesCount { get; private set; }
        public bool IsConnected
        {
            get
            {
                if (PPTApplication == null) return false;
                return _cachedIsConnected;
            }
        }
        public bool IsInSlideShow => _cachedIsInSlideShow;
        public bool IsSupportWPS { get; set; } = false;
        public bool SkipAnimationsWhenNavigating { get; set; } = false;

        #endregion

        #region Private Fields
        private Timer _unifiedPptTimer;
        private int _monitorTickCount;
        private volatile bool _unifiedPptTimerRunning;
        private bool _isWpsMonitoringEnabled;

        private Process _wpsProcess;
        private bool _isModuleUnloading = false;
        private bool _hasWpsProcessId;
        private DateTime _wpsProcessRecordTime = DateTime.MinValue;
        private int _wpsProcessCheckCount;
        private WpsWindowInfo _lastForegroundWpsWindow;
        private DateTime _lastWindowCheckTime = DateTime.MinValue;
        private bool _lastSlideShowState;
        private volatile bool _cachedIsConnected;
        private volatile bool _cachedIsInSlideShow;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private static bool IsPptBusyHResult(uint hr) => hr == 0x80010001 || hr == 0x8001010A;
        #endregion

        #region Constructor & Initialization
        public PPTManager()
        {
            InitializeConnectionTimer();
        }

        private void InitializeConnectionTimer()
        {
            _unifiedPptTimer = new Timer(500);
            _unifiedPptTimer.Elapsed += OnUnifiedPptTimerElapsed;
            _unifiedPptTimer.AutoReset = true;
        }

        public void StartMonitoring()
        {
            if (!_disposed)
            {
                _unifiedPptTimer?.Start();
                LogHelper.WriteLogToFile("PPT监控已启动", LogHelper.LogType.Trace);
            }
        }

        public void StopMonitoring()
        {
            _unifiedPptTimer?.Stop();
            StopWpsProcessCheckTimer();
            DisconnectFromPPT();
            LogHelper.WriteLogToFile("PPT监控已停止", LogHelper.LogType.Trace);
        }
        #endregion

        #region Connection Management
        private void OnUnifiedPptTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed || _isModuleUnloading)
                return;

            if (_unifiedPptTimerRunning)
                return;

            _unifiedPptTimerRunning = true;
            try
            {
                var tick = Interlocked.Increment(ref _monitorTickCount);

                CheckAndConnectToPPT();

                if (IsConnected)
                {
                    if (tick % 2 == 0)
                        CheckSlideShowState();

                    if (_isWpsMonitoringEnabled && tick % 4 == 0)
                        CheckWpsProcess();
                }

                if (tick >= int.MaxValue - 1000)
                    Interlocked.Exchange(ref _monitorTickCount, 0);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"统一PPT监控定时器执行失败: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                _unifiedPptTimerRunning = false;
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

                    // 尝试连接到PowerPoint
                    var pptApp = TryConnectToPowerPoint();
                    if (pptApp == null && IsSupportWPS)
                    {
                        // 如果PowerPoint连接失败且支持WPS，尝试连接WPS
                        pptApp = TryConnectToWPS();
                    }

                    if (pptApp != null && !IsConnected)
                    {
                        // 有可用的PPT/WPS应用程序且当前未连接，建立连接
                        ConnectToPPT(pptApp);
                    }
                    else if (pptApp == null && IsConnected)
                    {
                        // 没有可用的PPT/WPS应用程序但当前显示已连接，断开连接
                        DisconnectFromPPT();
                    }
                    else if (pptApp == null && PPTApplication != null)
                    {
                        // PPT/WPS应用程序不可用但PPTApplication对象仍存在，清理无效连接
                        DisconnectFromPPT();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT连接检查异常: {ex}", LogHelper.LogType.Error);
                    if (PPTApplication != null)
                    {
                        DisconnectFromPPT();
                    }
                }
            }
        }

        private bool RefreshIsInSlideShowFromCom()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            try
            {
                if (PPTApplication == null) { _cachedIsInSlideShow = false; return false; }
                if (!Marshal.IsComObject(PPTApplication)) { _cachedIsInSlideShow = false; return false; }

                slideShowWindows = PPTApplication.SlideShowWindows;
                if (slideShowWindows == null) { _cachedIsInSlideShow = false; return false; }

                dynamic ssw = slideShowWindows;
                if (ssw.Count == 0) { _cachedIsInSlideShow = false; return false; }

                slideShowWindow = ssw[1];
                if (slideShowWindow == null) { _cachedIsInSlideShow = false; return false; }

                dynamic sswObj = slideShowWindow;
                view = sswObj.View;
                if (view == null) { _cachedIsInSlideShow = false; return false; }

                _cachedIsInSlideShow = true;
                return true;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                _cachedIsInSlideShow = false;
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706B5))
                {
                    _cachedIsConnected = false;
                    DisconnectFromPPT();
                }
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查PPT放映状态时发生意外错误: {ex}", LogHelper.LogType.Warning);
                _cachedIsInSlideShow = false;
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        private void CheckSlideShowState()
        {
            try
            {
                if (!IsConnected) return;

                var currentSlideShowState = RefreshIsInSlideShowFromCom();
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
                var pptApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");

                if (pptApp != null && Marshal.IsComObject(pptApp))
                {
                    var _ = pptApp.Name;
                    return pptApp;
                }
                return null;
            }
            catch (COMException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Microsoft.Office.Interop.PowerPoint.Application TryConnectToWPS()
        {
            try
            {
                var wpsApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("kwpp.Application");

                if (wpsApp != null && Marshal.IsComObject(wpsApp))
                {
                    var _ = wpsApp.Name;
                    return wpsApp;
                }
                return null;
            }
            catch (COMException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ConnectToPPT(Microsoft.Office.Interop.PowerPoint.Application pptApp)
        {
            try
            {
                PPTApplication = pptApp;
                _cachedIsConnected = true;

                // 在主线程中注册事件，确保COM对象在正确的线程中
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        PPTApplication.PresentationOpen += OnPresentationOpen;
                        PPTApplication.PresentationClose += OnPresentationClose;
                        PPTApplication.SlideShowBegin += OnSlideShowBegin;
                        PPTApplication.SlideShowNextSlide += OnSlideShowNextSlide;
                        PPTApplication.SlideShowEnd += OnSlideShowEnd;

                        LogHelper.WriteLogToFile("PPT事件注册成功", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"PPT事件注册失败: {ex}", LogHelper.LogType.Error);
                        throw; // 重新抛出异常，让外层处理
                    }
                }, DispatcherPriority.Normal, CancellationToken.None, TimeSpan.FromSeconds(2));

                // 获取当前演示文稿信息
                UpdateCurrentPresentationInfo();

                // 触发连接成功事件
                PPTConnectionChanged?.Invoke(true);

                LogHelper.WriteLogToFile("成功连接到PPT应用程序", LogHelper.LogType.Event);

                RefreshIsInSlideShowFromCom();
                if (_cachedIsInSlideShow)
                {
                    OnSlideShowBegin(PPTApplication.SlideShowWindows[1]);
                }
                else if (CurrentPresentation != null)
                {
                    OnPresentationOpen(CurrentPresentation);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"连接PPT应用程序失败: {ex}", LogHelper.LogType.Error);
                _cachedIsConnected = false;
                PPTApplication = null;
            }
        }

        private void DisconnectFromPPT()
        {
            try
            {
                if (PPTApplication != null)
                {
                    // 取消事件注册 - 使用更安全的方式
                    try
                    {
                        // 检查COM对象是否仍然有效
                        if (Marshal.IsComObject(PPTApplication))
                        {
                            // 尝试在主线程中取消事件注册
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try
                                {
                                    PPTApplication.PresentationOpen -= OnPresentationOpen;
                                    PPTApplication.PresentationClose -= OnPresentationClose;
                                    PPTApplication.SlideShowBegin -= OnSlideShowBegin;
                                    PPTApplication.SlideShowNextSlide -= OnSlideShowNextSlide;
                                    PPTApplication.SlideShowEnd -= OnSlideShowEnd;
                                }
                                catch (COMException comEx)
                                {
                                    var hr = (uint)comEx.HResult;
                                    LogHelper.WriteLogToFile($"取消PPT事件注册时COM异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                                }
                                catch (InvalidCastException)
                                {
                                    // COM对象类型转换失败，通常是因为对象已经被释放
                                    LogHelper.WriteLogToFile("PPT COM对象已被释放，跳过事件注册取消", LogHelper.LogType.Trace);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"取消PPT事件注册时发生异常: {ex}", LogHelper.LogType.Warning);
                                }
                            }, DispatcherPriority.Normal, CancellationToken.None, TimeSpan.FromSeconds(1));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"取消PPT事件注册失败: {ex}", LogHelper.LogType.Warning);
                    }

                    SafeReleaseComObject(CurrentSlide, "CurrentSlide");
                    SafeReleaseComObject(CurrentSlides, "CurrentSlides");
                    SafeReleaseComObject(CurrentPresentation, "CurrentPresentation");

                    if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                    {
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
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }
                }

                _cachedIsConnected = false;
                _cachedIsInSlideShow = false;
                PPTApplication = null;
                CurrentPresentation = null;
                CurrentSlides = null;
                CurrentSlide = null;
                SlidesCount = 0;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _isModuleUnloading = true;
                _unifiedPptTimer?.Stop();

                // 触发连接断开事件
                PPTConnectionChanged?.Invoke(false);

                LogHelper.WriteLogToFile("已断开PPT连接，暂时卸载模块以确保COM完全释放", LogHelper.LogType.Event);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Thread.Sleep(2000);

                        if (_disposed)
                        {
                            _isModuleUnloading = false;
                            return;
                        }

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(1000);

                        _isModuleUnloading = false;

                        try
                        {
                            if (!_disposed)
                            {
                                _unifiedPptTimer?.Start();
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            LogHelper.WriteLogToFile("PPT联动模块重载时计时器已释放，跳过重启", LogHelper.LogType.Trace);
                        }

                        LogHelper.WriteLogToFile("PPT联动模块已重新加载", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"重新加载PPT联动模块失败: {ex}", LogHelper.LogType.Error);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void SafeReleaseComObject(object comObject, string objectName)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                LogHelper.WriteLogToFile($"释放COM对象 {objectName} 时COM异常: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放COM对象 {objectName} 时发生异常: {ex}", LogHelper.LogType.Warning);
            }
        }

        private void UpdateCurrentPresentationInfo()
        {
            object activePresentation = null;
            object slideShowWindows = null;
            object slideShowWindow = null;
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
                        activePresentation = PPTApplication.ActivePresentation;
                        if (activePresentation != null)
                        {
                            SafeReleaseComObject(CurrentPresentation, "CurrentPresentation");
                            CurrentPresentation = activePresentation as Presentation;
                            CurrentSlides = CurrentPresentation.Slides;

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
                                slideShowWindows = PPTApplication.SlideShowWindows;
                                if (IsInSlideShow && slideShowWindows != null)
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
                                            CurrentSlide = viewObj.Slide as Slide;
                                            }
                                        }
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
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(activeWindow);
                SafeReleaseComObject(slideShowWindows);
                if (activePresentation != null && !ReferenceEquals(activePresentation, CurrentPresentation))
                {
                    SafeReleaseComObject(activePresentation);
                }
            }
        }
        #endregion

        #region Event Handlers
        private void OnPresentationOpen(Presentation pres)
        {
            try
            {
                UpdateCurrentPresentationInfo();
                PresentationOpen?.Invoke(pres);
                LogHelper.WriteLogToFile($"演示文稿已打开: {pres?.Name}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPresentationClose(Presentation pres)
        {
            try
            {
                PresentationClose?.Invoke(pres);

                DisconnectFromPPT();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿关闭事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowBegin(SlideShowWindow wn)
        {
            _cachedIsInSlideShow = true;
            try
            {
                UpdateCurrentPresentationInfo();
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
                UpdateCurrentPresentationInfo();
                SlideShowNextSlide?.Invoke(wn);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片切换事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowEnd(Presentation pres)
        {
            _cachedIsInSlideShow = false;
            try
            {
                // 记录WPS进程用于后续管理
                if (IsSupportWPS && PPTApplication != null)
                {
                    RecordWpsProcessForManagement();
                }

                SlideShowEnd?.Invoke(pres);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映结束事件失败: {ex}", LogHelper.LogType.Error);
            }
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
                            viewObj.Next();
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005))
                    DisconnectFromPPT();
                LogHelper.WriteLogToFile($"切换到下一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到下一页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public bool TryNavigatePrevious()
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
                            viewObj.Previous();
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005))
                    DisconnectFromPPT();
                LogHelper.WriteLogToFile($"切换到上一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到上一页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
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
        public Presentation GetCurrentActivePresentation()
        {
            try
            {
                if (!IsConnected || PPTApplication == null) return null;
                if (!Marshal.IsComObject(PPTApplication)) return null;

                if (IsInSlideShow && PPTApplication.SlideShowWindows.Count > 0)
                {
                    try
                    {
                        var slideShowWindow = PPTApplication.SlideShowWindows[1];
                        if (slideShowWindow?.View != null)
                        {
                            return (Presentation)slideShowWindow.View.Slide.Parent;
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x80048240 || IsPptBusyHResult(hr))
                            return null;
                        throw;
                    }
                }

                if (PPTApplication.ActiveWindow?.Presentation != null)
                {
                    return PPTApplication.ActiveWindow.Presentation;
                }

                return CurrentPresentation;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005))
                    DisconnectFromPPT();
                if (!IsPptBusyHResult(hr))
                    LogHelper.WriteLogToFile($"获取当前活跃演示文稿失败: {comEx.Message}", LogHelper.LogType.Warning);
                return CurrentPresentation;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前活跃演示文稿失败: {ex}", LogHelper.LogType.Error);
                return CurrentPresentation;
            }
        }

        /// <summary>
        /// 获取当前幻灯片编号
        /// </summary>
        public int GetCurrentSlideNumber()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            object activeWindow = null;
            object selection = null;
            object slideRange = null;
            try
            {
                if (!IsConnected || PPTApplication == null) return 0;
                if (!Marshal.IsComObject(PPTApplication)) return 0;

                if (IsInSlideShow)
                {
                    slideShowWindows = PPTApplication.SlideShowWindows;
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
                                    return viewObj.CurrentShowPosition;
                                }
                            }
                        }
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
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005))
                    DisconnectFromPPT();
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
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
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
                if (!IsPptBusyHResult(hr) && (hr == 0x8001010E || hr == 0x80004005))
                    DisconnectFromPPT();
                if (!IsPptBusyHResult(hr))
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

            _isWpsMonitoringEnabled = true;
            _wpsProcessCheckCount = 0;
        }

        private void CheckWpsProcess()
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
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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
                Microsoft.Office.Interop.PowerPoint.Application pptApp = PPTApplication;
                if (pptApp != null)
                {
                    Marshal.ReleaseComObject(pptApp);
                    pptApp = null;
                    PPTApplication = null;
                    LogHelper.WriteLogToFile("已释放pptApp对象", LogHelper.LogType.Trace);
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
                    try { Marshal.ReleaseComObject(CurrentSlide); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                if (CurrentSlides != null && Marshal.IsComObject(CurrentSlides))
                {
                    try { Marshal.ReleaseComObject(CurrentSlides); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                if (CurrentPresentation != null && Marshal.IsComObject(CurrentPresentation))
                {
                    try { Marshal.ReleaseComObject(CurrentPresentation); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                {
                    try { Marshal.ReleaseComObject(PPTApplication); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }

                CurrentSlide = null;
                CurrentSlides = null;
                CurrentPresentation = null;
                PPTApplication = null;
                SlidesCount = 0;
                StopWpsProcessCheckTimer();

                // 触发连接断开事件
                PPTConnectionChanged?.Invoke(false);

                LogHelper.WriteLogToFile("WPS进程结束后已清理所有COM对象并重启连接检查", LogHelper.LogType.Event);
            }
        }



        private void StopWpsProcessCheckTimer()
        {
            _isWpsMonitoringEnabled = false;

            _wpsProcess = null;
            _hasWpsProcessId = false;
            _wpsProcessRecordTime = DateTime.MinValue;
            _wpsProcessCheckCount = 0;
            _lastForegroundWpsWindow = null;
            _lastWindowCheckTime = DateTime.MinValue;
            LogHelper.WriteLogToFile("停止 WPS 进程检测", LogHelper.LogType.Trace);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

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

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                StopWpsProcessCheckTimer();

                _unifiedPptTimer?.Dispose();

                _disposed = true;
            }
        }
        #endregion
    }
}
