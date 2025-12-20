using Microsoft.Office.Interop.PowerPoint;
using Microsoft.Office.Core;
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
                try
                {
                    if (PPTApplication == null || !Marshal.IsComObject(PPTApplication)) return false;

                    // 检查是否有放映窗口
                    var slideShowWindows = PPTApplication.SlideShowWindows;
                    if (slideShowWindows == null || slideShowWindows.Count == 0) return false;

                    // 验证放映窗口是否真正有效
                    try
                    {
                        var slideShowWindow = slideShowWindows[1];
                        if (slideShowWindow == null) return false;

                        // 尝试访问放映窗口的属性来验证其有效性
                        var _ = slideShowWindow.View;
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
                        return false;
                    }
                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    if (hr == 0x8001010E || hr == 0x80004005)
                    {
                        // COM对象已失效，触发断开连接
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
            }
        }
        public bool IsSupportWPS { get; set; } = false;
        #endregion

        #region Private Fields
        private Timer _connectionCheckTimer;
        private Timer _slideShowStateCheckTimer;
        private Timer _wpsProcessCheckTimer;
        private Process _wpsProcess;
        private bool _hasWpsProcessId;
        private DateTime _wpsProcessRecordTime = DateTime.MinValue;
        private int _wpsProcessCheckCount;
        private WpsWindowInfo _lastForegroundWpsWindow;
        private DateTime _lastWindowCheckTime = DateTime.MinValue;
        private bool _lastSlideShowState;
        private readonly object _lockObject = new object();
        private bool _disposed;
        #endregion

        #region Constructor & Initialization
        public PPTManager()
        {
            InitializeConnectionTimer();
        }

        private void InitializeConnectionTimer()
        {
            _connectionCheckTimer = new Timer(500);
            _connectionCheckTimer.Elapsed += OnConnectionCheckTimerElapsed;
            _connectionCheckTimer.AutoReset = true;

            _slideShowStateCheckTimer = new Timer(1000);
            _slideShowStateCheckTimer.Elapsed += OnSlideShowStateCheckTimerElapsed;
            _slideShowStateCheckTimer.AutoReset = true;
        }

        public void StartMonitoring()
        {
            if (!_disposed)
            {
                _connectionCheckTimer?.Start();
                _slideShowStateCheckTimer?.Start();
                LogHelper.WriteLogToFile("PPT监控已启动", LogHelper.LogType.Trace);
            }
        }

        public void StopMonitoring()
        {
            _connectionCheckTimer?.Stop();
            _slideShowStateCheckTimer?.Stop();
            DisconnectFromPPT();
            LogHelper.WriteLogToFile("PPT监控已停止", LogHelper.LogType.Trace);
        }
        #endregion

        #region Connection Management
        private void OnConnectionCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                CheckAndConnectToPPT();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT连接检查失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowStateCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                CheckSlideShowState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT放映状态检查失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void CheckAndConnectToPPT()
        {
            lock (_lockObject)
            {
                try
                {
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
                var pptApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");

                // 验证COM对象是否有效
                if (pptApp != null && Marshal.IsComObject(pptApp))
                {
                    // 尝试访问一个简单的属性来验证连接
                    var _ = pptApp.Name;
                    return pptApp;
                }
                return null;
            }
            catch (COMException ex)
            {
                var hr = (uint)ex.HResult;
                return null;
            }
            catch (InvalidCastException)
            {
                // COM对象类型转换失败
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

                // 验证COM对象是否有效
                if (wpsApp != null && Marshal.IsComObject(wpsApp))
                {
                    // 尝试访问一个简单的属性来验证连接
                    var _ = wpsApp.Name;
                    return wpsApp;
                }
                return null;
            }
            catch (COMException ex)
            {
                var hr = (uint)ex.HResult;
                // 忽略常见的WPS连接错误：
                // 0x800401E3: 操作无法使用
                // 0x80004005: 未指定错误
                // 0x800706B5: RPC服务器不可用
                // 0x8001010E: 应用程序调用一个已为另一线程整理的接口
                // 0x800401F3: 无效的类字符串（WPS未安装或COM组件未注册）
                if (hr != 0x800401E3 && hr != 0x80004005 && hr != 0x800706B5 && hr != 0x8001010E && hr != 0x800401F3)
                {
                    LogHelper.WriteLogToFile($"连接WPS失败: {ex}", LogHelper.LogType.Warning);
                }
                return null;
            }
            catch (InvalidCastException)
            {
                // COM对象类型转换失败
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"连接WPS时发生意外错误: {ex}", LogHelper.LogType.Warning);
                return null;
            }
        }

        private void ConnectToPPT(Microsoft.Office.Interop.PowerPoint.Application pptApp)
        {
            try
            {
                PPTApplication = pptApp;

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

                // 停止连接检查定时器
                _connectionCheckTimer?.Stop();

                // 触发连接成功事件
                PPTConnectionChanged?.Invoke(true);

                LogHelper.WriteLogToFile("成功连接到PPT应用程序", LogHelper.LogType.Event);

                // 如果已经在放映状态，立即触发放映开始事件
                if (IsInSlideShow)
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

                    // 安全释放COM对象
                    SafeReleaseComObject(CurrentSlide, "CurrentSlide");
                    SafeReleaseComObject(CurrentSlides, "CurrentSlides");
                    SafeReleaseComObject(CurrentPresentation, "CurrentPresentation");
                    SafeReleaseComObject(PPTApplication, "PPTApplication");

                    // 清理引用
                    PPTApplication = null;
                    CurrentPresentation = null;
                    CurrentSlides = null;
                    CurrentSlide = null;
                    SlidesCount = 0;

                    // 重新启动连接检查定时器
                    _connectionCheckTimer?.Start();

                    // 触发连接断开事件
                    PPTConnectionChanged?.Invoke(false);

                    LogHelper.WriteLogToFile("已断开PPT连接", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"断开PPT连接失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 安全释放COM对象
        /// </summary>
        private void SafeReleaseComObject(object comObject, string objectName)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    int refCount = Marshal.ReleaseComObject(comObject);
                    LogHelper.WriteLogToFile($"已释放COM对象 {objectName}，引用计数: {refCount}", LogHelper.LogType.Trace);
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
            try
            {
                if (PPTApplication != null && Marshal.IsComObject(PPTApplication))
                {
                    // 检查是否有活动的演示文稿
                    try
                    {
                        var activePresentation = PPTApplication.ActivePresentation;
                        if (activePresentation != null)
                        {
                            CurrentPresentation = activePresentation;
                            CurrentSlides = CurrentPresentation.Slides;

                            // 验证页数读取是否成功
                            try
                            {
                                var slideCount = CurrentSlides.Count;
                                if (slideCount > 0)
                                {
                                    SlidesCount = slideCount;
                                }
                                else
                                {
                                    // 页数为0，可能是空演示文稿或读取失败
                                    SlidesCount = 0;
                                    LogHelper.WriteLogToFile("PPT演示文稿页数为0，可能为空演示文稿", LogHelper.LogType.Warning);
                                }
                            }
                            catch (COMException comEx)
                            {
                                // 页数读取失败
                                var hr = (uint)comEx.HResult;
                                SlidesCount = 0;
                                LogHelper.WriteLogToFile($"读取PPT页数失败: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                            }

                            // 获取当前幻灯片
                            try
                            {
                                if (IsInSlideShow && PPTApplication.SlideShowWindows.Count > 0)
                                {
                                    CurrentSlide = PPTApplication.SlideShowWindows[1].View.Slide;
                                }
                                else if (PPTApplication.ActiveWindow?.Selection?.SlideRange?.SlideNumber > 0)
                                {
                                    CurrentSlide = CurrentSlides[PPTApplication.ActiveWindow.Selection.SlideRange.SlideNumber];
                                }
                                else if (SlidesCount > 0)
                                {
                                    // 如果获取失败，使用第一张幻灯片
                                    CurrentSlide = CurrentSlides[1];
                                }
                            }
                            catch (COMException comEx)
                            {
                                // COM异常，尝试使用第一张幻灯片
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
                            // 没有活动演示文稿，清理状态
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
                            // 常见的COM错误，可能是没有活动演示文稿
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
                    // PPT应用程序无效，清理状态
                    CurrentPresentation = null;
                    CurrentSlides = null;
                    CurrentSlide = null;
                    SlidesCount = 0;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新演示文稿信息失败: {ex}", LogHelper.LogType.Error);
                // 发生异常时清理状态
                CurrentPresentation = null;
                CurrentSlides = null;
                CurrentSlide = null;
                SlidesCount = 0;
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

                // 重新启动连接检查
                _connectionCheckTimer?.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿关闭事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowBegin(SlideShowWindow wn)
        {
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
            try
            {
                if (!IsConnected || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                if (IsInSlideShow && PPTApplication.SlideShowWindows.Count >= 1)
                {
                    var slideShowWindow = PPTApplication.SlideShowWindows[1];
                    if (slideShowWindow?.View != null)
                    {
                        slideShowWindow.View.GotoSlide(slideNumber);
                        return true;
                    }
                }
                else if (CurrentPresentation != null)
                {
                    if (CurrentPresentation.Windows?.Count >= 1)
                    {
                        CurrentPresentation.Windows[1].View.GotoSlide(slideNumber);
                        return true;
                    }
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
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
        }

        public bool TryNavigateNext(bool skipAnimations = false)
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                var slideShowWindow = PPTApplication.SlideShowWindows[1];
                if (slideShowWindow?.View != null)
                {
                    slideShowWindow.Activate();

                    var view = slideShowWindow.View;
                    var currentPosition = 0;
                    var totalSlides = 0;

                    try
                    {
                        currentPosition = view.CurrentShowPosition;
                        totalSlides = slideShowWindow.Presentation?.Slides?.Count ?? 0;
                    }
                    catch
                    {
                    }

                    if (skipAnimations && currentPosition > 0 && totalSlides > 0 && currentPosition < totalSlides)
                    {
                        try
                        {
                            view.GotoSlide(currentPosition + 1, MsoTriState.msoFalse);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"跳过转场动画跳转到下一页失败: {ex}", LogHelper.LogType.Warning);
                            try
                            {
                                view.Next();
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            view.Next();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"调用下一页失败: {ex}", LogHelper.LogType.Warning);
                            return false;
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
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"切换到下一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到下一页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        public bool TryNavigatePrevious()
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                var slideShowWindow = PPTApplication.SlideShowWindows[1];
                if (slideShowWindow?.View != null)
                {
                    slideShowWindow.Activate();
                    slideShowWindow.View.Previous();
                    return true;
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"切换到上一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到上一页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        public bool TryEndSlideShow()
        {
            try
            {
                if (!IsConnected || !IsInSlideShow || PPTApplication == null) return false;
                if (!Marshal.IsComObject(PPTApplication)) return false;

                var slideShowWindow = PPTApplication.SlideShowWindows[1];
                if (slideShowWindow?.View != null)
                {
                    slideShowWindow.View.Exit();
                    return true;
                }
                return false;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
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
        /// 获取当前活跃的演示文稿（用于多窗口墨迹分离）
        /// </summary>
        public Presentation GetCurrentActivePresentation()
        {
            try
            {
                if (!IsConnected || PPTApplication == null) return null;
                if (!Marshal.IsComObject(PPTApplication)) return null;

                // 如果在放映模式，获取放映窗口的演示文稿
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
                        if (hr == 0x80048240) // Integer out of range
                        {
                            // 放映窗口已不存在，返回null
                            return null;
                        }
                        throw; // 重新抛出其他COM异常
                    }
                }

                // 如果不在放映模式，获取活动窗口的演示文稿
                if (PPTApplication.ActiveWindow?.Presentation != null)
                {
                    return PPTApplication.ActiveWindow.Presentation;
                }

                // 如果没有活动窗口，返回当前演示文稿
                return CurrentPresentation;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
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
            try
            {
                if (!IsConnected || PPTApplication == null) return 0;
                if (!Marshal.IsComObject(PPTApplication)) return 0;

                // 如果在放映模式，获取放映窗口的当前幻灯片编号
                if (IsInSlideShow && PPTApplication.SlideShowWindows.Count > 0)
                {
                    var slideShowWindow = PPTApplication.SlideShowWindows[1];
                    if (slideShowWindow?.View != null)
                    {
                        return slideShowWindow.View.CurrentShowPosition;
                    }
                }

                // 如果不在放映模式，获取活动窗口的当前幻灯片编号
                if (PPTApplication.ActiveWindow?.Selection?.SlideRange?.SlideNumber > 0)
                {
                    return PPTApplication.ActiveWindow.Selection.SlideRange.SlideNumber;
                }

                // 如果CurrentSlide存在，尝试获取其编号
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
                    // COM对象已失效，触发断开连接
                    DisconnectFromPPT();
                }
                return 0;
            }
            catch (Exception)
            {
                return 0;
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

                var slideShowWindow = PPTApplication.SlideShowWindows[1];
                if (slideShowWindow == null)
                {
                    LogHelper.WriteLogToFile("幻灯片放映窗口为空", LogHelper.LogType.Warning);
                    return false;
                }

                // 检查是否为WPS，WPS可能不支持SlideNavigation
                try
                {
                    if (slideShowWindow.SlideNavigation != null)
                    {
                        slideShowWindow.SlideNavigation.Visible = true;
                        LogHelper.WriteLogToFile("成功显示幻灯片导航（PowerPoint模式）", LogHelper.LogType.Event);
                        return true;
                    }

                    LogHelper.WriteLogToFile("SlideNavigation对象为空，可能是WPS不支持此功能", LogHelper.LogType.Warning);
                    return false;
                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
                    // 0x80020006: 未知名称 - WPS可能不支持SlideNavigation
                    if (hr == 0x80020006)
                    {
                        LogHelper.WriteLogToFile("WPS不支持SlideNavigation功能", LogHelper.LogType.Warning);
                        return false;
                    }
                    throw; // 重新抛出其他COM异常
                }
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    // COM对象已失效，触发断开连接
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

                // 重新启动连接检查定时器，以便能够检测新的WPS实例
                _connectionCheckTimer?.Start();

                // 触发连接断开事件
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

        #region Dispose
        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                StopWpsProcessCheckTimer();

                _connectionCheckTimer?.Dispose();
                _slideShowStateCheckTimer?.Dispose();
                _wpsProcessCheckTimer?.Dispose();

                _disposed = true;
            }
        }
        #endregion
    }
}
