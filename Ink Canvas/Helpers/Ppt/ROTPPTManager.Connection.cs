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
        #region Connection Management
        private void PPTComService()
        {
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

                            try
                            {
                                ConnectToPPT(bestApp);
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
                                DisconnectFromPPT();
                                continue;
                            }
                        }
                        catch (System.Runtime.InteropServices.InvalidComObjectException)
                        {
                            // COM对象已失效
                            DisconnectFromPPT();
                            continue;
                        }
                        catch (COMException ex) when ((uint)ex.ErrorCode == 0x8001010A)
                        {
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
                                    activeSlideShowWindow = activePresentation.SlideShowWindow;
                                }
                                catch (Exception ex)
                                {
                                    LogCallbackException("轮询中读取 activePresentation.SlideShowWindow 失败", ex);
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
                            }

                            PPTROTConnectionHelper.SafeReleaseComObject(slideShowWindows);
                        }
                        catch (COMException ex) when ((uint)ex.ErrorCode == 0x8001010A)
                        {
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
                                PPTROTConnectionHelper.SafeReleaseComObject(_pptSlideShowWindow);
                                _pptSlideShowWindow = null;
                                _lastPolledSlideNumber = -1;
                                _polling = 1;
                                SlidesCount = 0;

                                _lastSlideShowState = false;
                                _cachedIsInSlideShow = false;
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
                        break;
                    }

                    Thread.Sleep(500);
                }
            }
            catch (InvalidComObjectException ex)
            {
                LogHelper.WriteLogToFile($"PPTComService异常: COM对象已失效 - {ex.Message}", LogHelper.LogType.Error);
                DisconnectFromPPT();
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                LogHelper.WriteLogToFile($"PPTComService异常: COM异常 (HR: 0x{hr:X8}) - {comEx.Message}", LogHelper.LogType.Error);
                DisconnectFromPPT();
            }
            finally
            {
                lock (_monitoringLock)
                {
                    if (ReferenceEquals(_monitoringThread, Thread.CurrentThread))
                    {
                        _monitoringThread = null;
                    }
                }
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
                                    _forcePolling = false;

                                }
                                else
                                {
                                    _bindingEvents = false;
                                    _forcePolling = true;
                                }
                            }
                            catch (Exception)
                            {
                                _bindingEvents = false;
                                _forcePolling = true;
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

                    if (_pptActivePresentation != null)
                    {
                        UpdateCurrentPresentationInfo();
                    }

                    bool wasConnected = _cachedIsConnected;
                    _cachedIsConnected = true;
                    if (!wasConnected)
                    {
                        PPTConnectionChanged?.Invoke(true);
                    }

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
            dynamic slideShowWindows = null;
            dynamic slideShowWindow = null;

            try
            {
                if (!IsConnected || PPTApplication == null)
                    return;

                if (!Marshal.IsComObject(PPTApplication))
                    return;

                dynamic app = PPTApplication;
                slideShowWindows = app.SlideShowWindows;
                if (slideShowWindows == null || slideShowWindows.Count == 0)
                    return;

                slideShowWindow = slideShowWindows[1];
                if (slideShowWindow == null)
                    return;

                OnSlideShowBegin(slideShowWindow);
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }
            finally
            {
                if (slideShowWindow != null && !PPTROTConnectionHelper.AreComObjectsEqual(_pptSlideShowWindow, slideShowWindow))
                {
                    SafeReleaseComObject(slideShowWindow);
                }

                if (slideShowWindows != null)
                {
                    SafeReleaseComObject(slideShowWindows);
                }
            }
        }

        private void ApplyReconnectBackoff()
        {
            _reconnectFailureCount = Math.Min(_reconnectFailureCount + 1, 4);
            int delayMs = 250 * (1 << (_reconnectFailureCount - 1));
            _nextReconnectAttemptUtc = DateTime.UtcNow.AddMilliseconds(delayMs);

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
                        LogService.LogException(ex);
                    }

                    _bindingEvents = false;
                }
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }
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

                // 注意：PresentationClose 只应在真正的关闭（OnPresentationBeforeClose）中抛出。
                // DisconnectFromPPT 会被重绑、热重载、瞬时 COM 失效恢复等内部路径调用，
                // 此时演示文稿仍处于打开状态，抛 PresentationClose 会让上层错误地清空缓存，
                // 而后续重连到同一个演示文稿也不会补发 PresentationOpen。

                SafeReleaseComObject(slideShowWindow, "_pptSlideShowWindow");
                SafeReleaseComObject(activePresentation, "_pptActivePresentation");
                SafeReleaseComObject(currentSlide, "CurrentSlide");
                SafeReleaseComObject(currentSlides, "CurrentSlides");
                SafeReleaseComObject(currentPresentation, "CurrentPresentation");
                SafeFinalReleaseComObject(pptApplication, "PPTApplication");

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

                bool wasConnected = _cachedIsConnected;
                _cachedIsConnected = false;
                _cachedIsInSlideShow = false;
                if (wasConnected && !_suppressDisconnectEvent)
                {
                    PPTConnectionChanged?.Invoke(false);
                }

                LogHelper.WriteLogToFile("已断开PPT连接，并显式释放所有COM对象", LogHelper.LogType.Event);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Thread.Sleep(2000);
                        _isModuleUnloading = false;

                        if (!_shouldStop && !_disposed)
                        {
                            StartMonitoring();
                            LogHelper.WriteLogToFile("ROT PPT联动监控已恢复", LogHelper.LogType.Trace);
                        }
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
            catch (Exception ex)
            {
                LogCallbackException("安全释放 COM 对象（无名称重载）失败", ex);
            }
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
                        return;
                    }
                    catch (COMException comEx) when (IsIgnorableDisconnectComException(comEx))
                    {
                        return;
                    }

                    int refCount = Marshal.ReleaseComObject(comObject);
                }
            }
            catch (InvalidComObjectException)
            {
                // COM对象已失效，这是正常的，无需记录错误
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

        /// <summary>
        /// 终态释放 COM 对象：先尝试 FinalReleaseComObject 一次性归零引用计数，失败时回退为循环 ReleaseComObject。
        /// </summary>
        private void SafeFinalReleaseComObject(object comObject, string objectName)
        {
            if (comObject == null) return;
            try
            {
                if (!Marshal.IsComObject(comObject)) return;

                try
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
                catch
                {
                    try
                    {
                        int refCount = Marshal.ReleaseComObject(comObject);
                        while (refCount > 0)
                        {
                            refCount = Marshal.ReleaseComObject(comObject);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogException(ex);
                    }
                }
            }
            catch (InvalidComObjectException ex)
            {
                LogService.LogException(ex);
            }
            catch (COMException comEx) when (IsIgnorableDisconnectComException(comEx))
            {
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"最终释放COM对象 {objectName} 时发生异常: {ex.Message}", LogHelper.LogType.Warning);
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
                                            LogCallbackException("演示中通过 _pptSlideShowWindow.View.Slide 读取当前页失败", ex);
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
                                    if (hr != 0x8001010E && hr != 0x80004005 && hr != 0x80048240)
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
                    catch (Exception ex)
                    {
                        LogService.LogException(ex);
                    }
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
                try
                {
                    PresentationClose?.Invoke(null);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"触发PresentationClose事件失败: {ex.Message}", LogHelper.LogType.Warning);
                }

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
                        LogService.LogException(ex);
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

                // 兜底：ROT 路径下事件触发时 _pptActivePresentation 可能尚未绑定，
                // 先从 SlideShowWindow.Presentation 取，再退回 PPTApplication.ActivePresentation，
                // 保证后续 SlidesCount 在 UI 读取前一定有效。
                if (_pptActivePresentation == null)
                {
                    try
                    {
                        if (wn != null)
                        {
                            dynamic ssw = wn;
                            dynamic presFromWindow = ssw.Presentation;
                            if (presFromWindow != null)
                            {
                                _pptActivePresentation = presFromWindow;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"OnSlideShowBegin 从 SlideShowWindow 兜底获取演示文稿失败: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    if (_pptActivePresentation == null && PPTApplication != null)
                    {
                        try
                        {
                            dynamic app = PPTApplication;
                            _pptActivePresentation = app.ActivePresentation;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"OnSlideShowBegin 从 PPTApplication 兜底获取演示文稿失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                    }
                }

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
                    _cachedIsInSlideShow = true;
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

                if (_lastSlideShowState)
                {
                    _lastSlideShowState = false;
                    _cachedIsInSlideShow = false;
                    SlideShowStateChanged?.Invoke(false);
                }

                SlideShowEnd?.Invoke(null);
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

                // 在新 STA 线程中执行翻页操作，避免等待动画完成。
                // Office interop 要求 STA：默认 new Thread 是 MTA，触发 0x8001010E (RPC_E_WRONG_THREAD)
                // 会让 DisconnectFromPPT 掉线。下面显式 SetApartmentState。
                var navThread = new Thread(() =>
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
                                catch (Exception ex)
                                {
                                    LogCallbackException("翻到下一页前激活放映窗口 sswObj.Activate() 失败", ex);
                                }
                                try
                                {
                                    object view = sswObj.View;
                                    if (view != null)
                                    {
                                        dynamic viewObj = view;
                                        viewObj.Next();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogCallbackException("调用 viewObj.Next() 翻到下一页失败", ex);
                                }
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
                });
                navThread.IsBackground = true;
                navThread.SetApartmentState(ApartmentState.STA);
                navThread.Start();
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

                // 在新 STA 线程中执行翻页操作（同 TryNavigateNext，必须 STA 避免 RPC_E_WRONG_THREAD）
                var navThread = new Thread(() =>
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
                                catch (Exception ex)
                                {
                                    LogCallbackException("翻到上一页前激活放映窗口 sswObj.Activate() 失败", ex);
                                }
                                try
                                {
                                    object view = sswObj.View;
                                    if (view != null)
                                    {
                                        dynamic viewObj = view;
                                        viewObj.Previous();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogCallbackException("调用 viewObj.Previous() 翻到上一页失败", ex);
                                }
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
                });
                navThread.IsBackground = true;
                navThread.SetApartmentState(ApartmentState.STA);
                navThread.Start();
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

        public List<PPTSlideThumbnail> ExportSlideThumbnails(int width, int height, IProgress<double> progress = null)
        {
            var result = new List<PPTSlideThumbnail>();
            string tempDir = null;
            object slides = null;

            try
            {
                object activePresentation = CurrentPresentation;
                if (activePresentation == null || !Marshal.IsComObject(activePresentation)) return result;

                dynamic presentation = activePresentation;
                slides = presentation.Slides;
                if (slides == null) return result;

                dynamic slideCollection = slides;
                int count = slideCollection.Count;
                if (count <= 0) return result;

                tempDir = Path.Combine(Path.GetTempPath(), "InkCanvas", "PPTPreviews", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                for (int i = 1; i <= count; i++)
                {
                    object slide = null;
                    try
                    {
                        slide = slideCollection[i];
                        dynamic slideObj = slide;
                        var imagePath = Path.Combine(tempDir, $"slide_{i:0000}.png");
                        slideObj.Export(imagePath, "PNG", width, height);
                        result.Add(new PPTSlideThumbnail
                        {
                            SlideNumber = i,
                            PngBytes = File.ReadAllBytes(imagePath)
                        });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[ROT] 生成PPT第{i}页缩略图失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                    finally
                    {
                        SafeReleaseComObject(slide);
                    }

                    progress?.Report((double)i / count);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[ROT] 导出PPT缩略图失败: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                SafeReleaseComObject(slides);
                if (!string.IsNullOrWhiteSpace(tempDir) && Directory.Exists(tempDir))
                {
                    ExceptionHandler.TryExecute(() => Directory.Delete(tempDir, true), "删除 ROT PPT 临时目录失败");
                }
            }

            return result;
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

    }
}
