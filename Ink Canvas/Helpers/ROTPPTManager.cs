using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 基于 ROT 的 PPT 联动管理器实现。
    /// </summary>
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
        /// <summary>
        /// 通过 ROT 获取到的 PowerPoint.Application 实例。
        /// </summary>
        public object PPTApplication => _pptApplication;

        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_pptApplication == null) return false;
                    if (!Marshal.IsComObject(_pptApplication)) return false;

                    // 访问 Name 属性验证 COM 是否仍然有效
                    dynamic app = _pptApplication;
                    var _ = app.Name;
                    return true;
                }
                catch (COMException comEx)
                {
                    var hr = (uint)comEx.HResult;
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
                    if (_pptApplication == null || !Marshal.IsComObject(_pptApplication)) return false;

                    dynamic app = _pptApplication;
                    slideShowWindows = app.SlideShowWindows;
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
                    LogHelper.WriteLogToFile($"[ROT] 检查 PPT 放映状态失败: {comEx.Message} (HR: 0x{hr:X8})", LogHelper.LogType.Warning);
                    return false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[ROT] 检查 PPT 放映状态时发生意外错误: {ex}", LogHelper.LogType.Warning);
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

        /// <summary>
        /// 当前演示文稿的总页数（每次按需计算，不缓存 COM 对象）。
        /// </summary>
        public int SlidesCount
        {
            get
            {
                object pres = null;
                object slides = null;
                try
                {
                    pres = GetCurrentActivePresentation();
                    if (pres == null) return 0;

                    dynamic dp = pres;
                    slides = dp.Slides;
                    if (slides == null) return 0;

                    dynamic ds = slides;
                    return (int)ds.Count;
                }
                catch
                {
                    return 0;
                }
                finally
                {
                    SafeReleaseComObject(slides);
                    SafeReleaseComObject(pres);
                }
            }
        }
        #endregion

        #region Private Fields
        // 唯一持久化的 COM 对象字段：PPT 应用程序实例
        private object _pptApplication;

        private Timer _unifiedRotTimer;
        private int _rotTickCount;

        private bool _isModuleUnloading;
        private bool _lastSlideShowState;
        private readonly object _lockObject = new object();
        private bool _disposed;
        #endregion

        #region Lifecycle
        public ROTPPTManager()
        {
            InitializeTimers();
        }

        private void InitializeTimers()
        {
            _unifiedRotTimer = new Timer(500);
            _unifiedRotTimer.Elapsed += OnUnifiedRotTimerElapsed;
            _unifiedRotTimer.AutoReset = true;
        }

        private void OnUnifiedRotTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var tick = Interlocked.Increment(ref _rotTickCount);

            OnConnectionCheckTimerElapsed(sender, e);

            if (tick % 2 == 0)
                OnSlideShowStateCheckTimerElapsed(sender, e);
        }

        public void StartMonitoring()
        {
            if (_disposed) return;

            _unifiedRotTimer?.Start();
        }

        public void StopMonitoring()
        {
            _unifiedRotTimer?.Stop();
            DisconnectFromPPT();
        }

        public void ReloadConnection()
        {
            if (_disposed) return;
            LogHelper.WriteLogToFile("[ROT] 执行热重载：强制断开并重新连接", LogHelper.LogType.Event);
            DisconnectFromPPT();
        }
        #endregion

        #region Connection Management
        private void OnConnectionCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_disposed || _isModuleUnloading)
                    return;
                CheckAndConnectToPPTViaRot();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] PPT 连接检查失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowStateCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_disposed || _isModuleUnloading || !IsConnected)
                    return;
                CheckSlideShowState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] PPT 放映状态检查失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void CheckAndConnectToPPTViaRot()
        {
            if (_disposed || _isModuleUnloading) return;

            if (_pptApplication != null && !IsConnected)
            {
                DisconnectFromPPT();
                return;
            }

            lock (_lockObject)
            {
                try
                {
                    if (_isModuleUnloading) return;

                    // 使用 ROT 获取当前最佳 PPT 实例
                    var bestApp = PPTROTConnectionHelper.TryConnectViaROT(IsSupportWPS);

                    if (bestApp != null && _pptApplication == null)
                    {
                        // 从未连接 -> 连接
                        ConnectToPPT(bestApp);
                    }
                    else if (bestApp == null && _pptApplication != null)
                    {
                        // 原来有，现在没有 -> 断开
                        DisconnectFromPPT();
                    }
                    else if (bestApp != null && _pptApplication != null)
                    {
                        // 已连接，检查是否切换到了另一份 PPT
                        if (!PPTROTConnectionHelper.AreComObjectsEqual(_pptApplication, bestApp))
                        {
                            DisconnectFromPPT();
                            ConnectToPPT(bestApp);
                        }
                        else
                        {
                            PPTROTConnectionHelper.SafeReleaseComObject(bestApp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[ROT] PPT 连接检查异常: {ex}", LogHelper.LogType.Error);
                    if (_pptApplication != null)
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
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 检查 PPT 放映状态异常: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ConnectToPPT(object appObj)
        {
            try
            {
                _pptApplication = appObj;

                // 在 UI 线程上注册事件（使用 Interop 类型做内部绑定，不向外泄露）
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        var app = _pptApplication as Microsoft.Office.Interop.PowerPoint.Application;
                        if (app == null) return;

                        app.PresentationOpen += OnPresentationOpenInternal;
                        app.PresentationClose += OnPresentationCloseInternal;
                        app.SlideShowBegin += OnSlideShowBeginInternal;
                        app.SlideShowNextSlide += OnSlideShowNextSlideInternal;
                        app.SlideShowEnd += OnSlideShowEndInternal;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[ROT] PPT 事件注册失败: {ex}", LogHelper.LogType.Error);
                        throw;
                    }
                }, DispatcherPriority.Normal);

                PPTConnectionChanged?.Invoke(true);
                LogHelper.WriteLogToFile("[ROT] 成功连接到 PPT 应用程序", LogHelper.LogType.Event);

                // 如果已在放映中，根据当前状态触发一次开始事件
                if (IsInSlideShow)
                {
                    try
                    {
                        var app = _pptApplication as Microsoft.Office.Interop.PowerPoint.Application;
                        if (app != null && app.SlideShowWindows.Count > 0)
                        {
                            OnSlideShowBeginInternal(app.SlideShowWindows[1]);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 连接 PPT 应用程序失败: {ex}", LogHelper.LogType.Error);
                _pptApplication = null;
            }
        }

        private void DisconnectFromPPT()
        {
            try
            {
                _isModuleUnloading = true;
                _unifiedRotTimer?.Stop();

                PPTConnectionChanged?.Invoke(false);
                LogHelper.WriteLogToFile("[ROT] 准备断开 PPT 连接，先卸载监控模块", LogHelper.LogType.Event);

                if (_pptApplication != null)
                {
                    try
                    {
                        if (Marshal.IsComObject(_pptApplication))
                        {
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try
                                {
                                    var app = _pptApplication as Microsoft.Office.Interop.PowerPoint.Application;
                                    if (app != null)
                                    {
                                        app.PresentationOpen -= OnPresentationOpenInternal;
                                        app.PresentationClose -= OnPresentationCloseInternal;
                                        app.SlideShowBegin -= OnSlideShowBeginInternal;
                                        app.SlideShowNextSlide -= OnSlideShowNextSlideInternal;
                                        app.SlideShowEnd -= OnSlideShowEndInternal;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"[ROT] 取消 PPT 事件注册异常: {ex}", LogHelper.LogType.Warning);
                                }
                            }, DispatcherPriority.Normal);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[ROT] 取消 PPT 事件注册失败: {ex}", LogHelper.LogType.Warning);
                    }

                    if (Marshal.IsComObject(_pptApplication))
                    {
                        try
                        {
                            Marshal.FinalReleaseComObject(_pptApplication);
                        }
                        catch
                        {
                            try
                            {
                                int refCount = Marshal.ReleaseComObject(_pptApplication);
                                while (refCount > 0)
                                {
                                    refCount = Marshal.ReleaseComObject(_pptApplication);
                                }
                            }
                            catch { }
                        }
                    }
                }

                _pptApplication = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                LogHelper.WriteLogToFile("[ROT] 已断开 PPT 连接并尝试释放所有 COM 对象", LogHelper.LogType.Event);

                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(300);

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        System.Threading.Thread.Sleep(200);

                        _isModuleUnloading = false;
                        if (!_disposed)
                            _unifiedRotTimer?.Start();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[ROT] 重新进入监控状态失败: {ex}", LogHelper.LogType.Error);
                        _isModuleUnloading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 断开 PPT 连接失败: {ex}", LogHelper.LogType.Error);
                _isModuleUnloading = false;
            }
        }
        #endregion

        #region PPT Event Handlers (internal, strong-typed)
        private void OnPresentationOpenInternal(Presentation pres)
        {
            try
            {
                PresentationOpen?.Invoke(pres);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPresentationCloseInternal(Presentation pres)
        {
            try
            {
                PresentationClose?.Invoke(pres);
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                {
                    // COM 对象已失效，静默忽略
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnSlideShowBeginInternal(SlideShowWindow wn)
        {
            try
            {
                SlideShowBegin?.Invoke(wn);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 处理放映开始事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowNextSlideInternal(SlideShowWindow wn)
        {
            try
            {
                SlideShowNextSlide?.Invoke(wn);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 处理放映翻页事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnSlideShowEndInternal(Presentation pres)
        {
            try
            {
                SlideShowEnd?.Invoke(pres);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 处理放映结束事件失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region IPPTLinkManager Methods
        public bool TryStartSlideShow()
        {
            object pres = null;
            try
            {
                if (!IsConnected || _pptApplication == null) return false;
                if (!Marshal.IsComObject(_pptApplication)) return false;

                dynamic app = _pptApplication;
                try
                {
                    pres = app.ActivePresentation;
                }
                catch
                {
                    pres = null;
                }

                if (pres == null || !Marshal.IsComObject(pres)) return false;

                dynamic dp = pres;
                dp.SlideShowSettings.Run();
                return true;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"[ROT] 开始幻灯片放映失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 开始幻灯片放映失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(pres);
            }
        }

        public bool TryEndSlideShow()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            try
            {
                if (!IsConnected || _pptApplication == null) return false;
                if (!Marshal.IsComObject(_pptApplication)) return false;

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    int count = 0;
                    try { count = ssw.Count; } catch { count = 0; }

                    for (int i = 1; i <= count; i++)
                    {
                        try
                        {
                            slideShowWindow = ssw[i];
                            if (slideShowWindow != null)
                            {
                                dynamic sswObj = slideShowWindow;
                                view = sswObj.View;
                                if (view != null)
                                {
                                    dynamic viewObj = view;
                                    viewObj.Exit();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"[ROT] 结束第 {i} 个放映窗口失败: {ex}", LogHelper.LogType.Warning);
                        }
                        finally
                        {
                            SafeReleaseComObject(view);
                            SafeReleaseComObject(slideShowWindow);
                            view = null;
                            slideShowWindow = null;
                        }
                    }
                }

                return true;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"[ROT] 结束幻灯片放映失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 结束幻灯片放映失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public bool TryNavigateToSlide(int slideNumber)
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            try
            {
                if (!IsConnected || !IsInSlideShow || _pptApplication == null) return false;
                if (!Marshal.IsComObject(_pptApplication)) return false;

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow != null)
                    {
                        dynamic sswObj = slideShowWindow;
                        sswObj.Activate();
                        view = sswObj.View;
                        if (view != null)
                        {
                            dynamic viewObj = view;
                            viewObj.GotoSlide(slideNumber);
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
                LogHelper.WriteLogToFile($"[ROT] 跳转到第 {slideNumber} 页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 跳转到第 {slideNumber} 页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
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
                if (!IsConnected || !IsInSlideShow || _pptApplication == null) return false;
                if (!Marshal.IsComObject(_pptApplication)) return false;

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow != null)
                    {
                        dynamic sswObj = slideShowWindow;
                        sswObj.Activate();
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
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"[ROT] 切换到下一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 切换到下一页失败: {ex}", LogHelper.LogType.Error);
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
                if (!IsConnected || !IsInSlideShow || _pptApplication == null) return false;
                if (!Marshal.IsComObject(_pptApplication)) return false;

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow != null)
                    {
                        dynamic sswObj = slideShowWindow;
                        sswObj.Activate();
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
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"[ROT] 切换到上一页失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 切换到上一页失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public int GetCurrentSlideNumber()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            try
            {
                if (!IsConnected || _pptApplication == null) return 0;
                if (!Marshal.IsComObject(_pptApplication)) return 0;

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
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
                            return (int)viewObj.CurrentShowPosition;
                        }
                    }
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
                LogHelper.WriteLogToFile($"[ROT] 获取当前页码失败: {comEx.Message}", LogHelper.LogType.Error);
                return 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 获取当前页码失败: {ex}", LogHelper.LogType.Error);
                return 0;
            }
            finally
            {
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public string GetPresentationName()
        {
            object pres = null;
            try
            {
                pres = GetCurrentActivePresentation();
                if (pres == null) return "";

                dynamic dp = pres;
                return (string)dp.Name;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 获取演示文稿名称失败: {ex}", LogHelper.LogType.Error);
                return "";
            }
            finally
            {
                SafeReleaseComObject(pres);
            }
        }

        public bool TryShowSlideNavigation()
        {
            object slideShowWindows = null;
            object slideShowWindow = null;
            object slideNavigation = null;
            try
            {
                LogHelper.WriteLogToFile($"[ROT] 尝试显示幻灯片导航 - 连接状态: {IsConnected}, 放映状态: {IsInSlideShow}", LogHelper.LogType.Trace);

                if (!IsConnected || !IsInSlideShow || _pptApplication == null)
                {
                    LogHelper.WriteLogToFile("[ROT] PPT 未连接或未在放映状态", LogHelper.LogType.Warning);
                    return false;
                }

                if (!Marshal.IsComObject(_pptApplication))
                {
                    LogHelper.WriteLogToFile("[ROT] PPT 应用程序 COM 对象无效", LogHelper.LogType.Warning);
                    return false;
                }

                dynamic app = _pptApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows != null)
                {
                    dynamic ssw = slideShowWindows;
                    slideShowWindow = ssw[1];
                    if (slideShowWindow == null)
                    {
                        LogHelper.WriteLogToFile("[ROT] 幻灯片放映窗口为空", LogHelper.LogType.Warning);
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
                            LogHelper.WriteLogToFile("[ROT] 成功显示幻灯片导航", LogHelper.LogType.Event);
                            return true;
                        }

                        LogHelper.WriteLogToFile("[ROT] SlideNavigation 对象为空，可能当前环境不支持", LogHelper.LogType.Warning);
                        return false;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr == 0x80020006)
                        {
                            LogHelper.WriteLogToFile("[ROT] 当前 PPT 实例不支持 SlideNavigation 功能", LogHelper.LogType.Warning);
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
                LogHelper.WriteLogToFile($"[ROT] 显示幻灯片导航失败: {comEx.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 显示幻灯片导航失败: {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                SafeReleaseComObject(slideNavigation);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
            }
        }

        public object GetCurrentActivePresentation()
        {
            object pres = null;
            object slideShowWindows = null;
            object slideShowWindow = null;
            object view = null;
            object slide = null;
            try
            {
                if (!IsConnected || _pptApplication == null) return null;
                if (!Marshal.IsComObject(_pptApplication)) return null;

                dynamic app = _pptApplication;

                // 优先使用放映窗口里的当前演示文稿
                if (IsInSlideShow)
                {
                    slideShowWindows = app.SlideShowWindows;
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
                                        dynamic ds = slide;
                                        pres = ds.Parent;
                                        return pres;
                                    }
                                }
                            }
                        }
                    }
                }

                // 其次尝试 ActiveWindow.Presentation
                try
                {
                    var aw = app.ActiveWindow;
                    if (aw != null)
                    {
                        dynamic daw = aw;
                        pres = daw.Presentation;
                        if (pres != null) return pres;
                    }
                }
                catch
                {
                }

                return null;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005)
                {
                    DisconnectFromPPT();
                }
                LogHelper.WriteLogToFile($"[ROT] 获取当前演示文稿失败: {comEx.Message}", LogHelper.LogType.Error);
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 获取当前演示文稿失败: {ex}", LogHelper.LogType.Error);
                return null;
            }
            finally
            {
                SafeReleaseComObject(slide);
                SafeReleaseComObject(view);
                SafeReleaseComObject(slideShowWindow);
                SafeReleaseComObject(slideShowWindows);
                // 注意：pres 作为返回值时不在这里释放，由调用方负责（如有需要）
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// 安全释放 COM 对象。
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
            catch
            {
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                StopMonitoring();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] Dispose 异常: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
}