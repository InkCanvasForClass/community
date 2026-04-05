using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Win32 API Declarations
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
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out ForegroundWindowInfo.RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_MINIMIZE = 0x20000000;
        private const uint GW_HWNDNEXT = 2;
        private const uint GW_HWNDPREV = 3;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        #endregion

        #region PPT Application Variables
        /// <summary>
        /// PowerPoint应用程序实例，用于与PowerPoint进行交互。
        /// </summary>
        public static Microsoft.Office.Interop.PowerPoint.Application pptApplication;

        /// <summary>
        /// 当前活动的PowerPoint演示文稿。
        /// </summary>
        public static Presentation presentation;

        /// <summary>
        /// 当前演示文稿的幻灯片集合。
        /// </summary>
        public static Slides slides;

        /// <summary>
        /// 当前活动的幻灯片。
        /// </summary>
        public static Slide slide;

        /// <summary>
        /// 当前演示文稿的幻灯片总数。
        /// </summary>
        public static int slidescount;
        #endregion

        #region PPT State Management
        /// <summary>
        /// 幻灯片放映结束事件重入保护标志，防止重复处理放映结束事件。
        /// </summary>
        private bool isEnteredSlideShowEndEvent;

        /// <summary>
        /// 演示文稿是否有黑边的指示标志。
        /// </summary>
        private bool isPresentationHaveBlackSpace;

        // 长按翻页相关字段
        /// <summary>
        /// 用于处理长按翻页功能的定时器。
        /// </summary>
        private DispatcherTimer _longPressTimer;

        /// <summary>
        /// 长按翻页方向标志，true表示下一页，false表示上一页。
        /// </summary>
        private bool _isLongPressNext = true; // true为下一页，false为上一页

        /// <summary>
        /// 长按延迟时间（毫秒），即用户需要按住按钮多长时间才开始连续翻页。
        /// </summary>
        private const int LongPressDelay = 500; // 长按延迟时间（毫秒）

        /// <summary>
        /// 长按翻页间隔（毫秒），即连续翻页的时间间隔。
        /// </summary>
        private const int LongPressInterval = 50; // 长按翻页间隔（毫秒）

        // PowerPoint应用程序守护相关字段
        /// <summary>
        /// 用于监控PowerPoint应用程序状态的定时器。
        /// </summary>
        private DispatcherTimer _powerPointProcessMonitorTimer;

        /// <summary>
        /// 应用程序监控间隔（毫秒），即每隔多长时间检查一次PowerPoint应用程序状态。
        /// </summary>
        private const int ProcessMonitorInterval = 1000; // 应用程序监控间隔（毫秒）

        // 上次播放位置相关字段
        /// <summary>
        /// 上次播放的幻灯片页码。
        /// </summary>
        private int _lastPlaybackPage = 0;

        /// <summary>
        /// 是否应该导航到上次播放页码的标志。
        /// </summary>
        private bool _shouldNavigateToLastPage = false;

        // 当前播放页码跟踪
        /// <summary>
        /// 当前幻灯片放映的位置（页码）。
        /// </summary>
        private int _currentSlideShowPosition = 0;

        private Dictionary<int, MemoryStream> _memoryStreams = new Dictionary<int, MemoryStream>();
        private int _previousSlideID = 0;

        /// <summary>
        /// 用于在PowerPoint连接断开后延迟退出PPT模式的定时器。
        /// </summary>
        private DispatcherTimer _exitPPTModeAfterDisconnectTimer;

        /// <summary>
        /// 断开连接后退出PPT模式的延迟时间（毫秒），即连接断开后多长时间才退出PPT模式。
        /// </summary>
        private const int ExitPPTModeAfterDisconnectDelayMs = 1200;
        #endregion

        #region PPT Managers
        /// <summary>
        /// PPT链接管理器，用于管理与PowerPoint的连接和事件处理。
        /// </summary>
        private IPPTLinkManager _pptManager;

        /// <summary>
        /// PPT墨迹管理器，用于管理PowerPoint幻灯片上的墨迹。
        /// </summary>
        private PPTInkManager _singlePPTInkManager;

        /// <summary>
        /// PPT UI管理器，用于管理与PowerPoint相关的用户界面元素。
        /// </summary>
        private PPTUIManager _pptUIManager;

        /// <summary>
        /// 获取PPT管理器实例
        /// </summary>
        /// <remarks>
        /// 提供对内部PPT链接管理器的公共访问，用于外部代码与PowerPoint进行交互。
        /// </remarks>
        public IPPTLinkManager PPTManager => _pptManager;
        #endregion

        #region PPT Manager Initialization
        /// <summary>
        /// 初始化并配置用于 PowerPoint 集成的管理器与相关状态。
        /// </summary>
        /// <remarks>
        /// 清理并释放现有的 PPT 管理器与 COM/Interop 状态，创建并配置新的 PPT 管理器（ROT 或 COM 实现，取决于设置）、单一的 PPT 墨迹管理器及其自动保存行为，以及 PPT UI 管理器与其显示/按钮位置选项。方法内部会订阅必要的 PPT 事件并记录初始化过程中的错误或警告。同时初始化长按页翻页定时器以支持长按翻页功能。
        /// </remarks>
        private void InitializePPTManagers()
        {
            try
            {
                // 初始化长按定时器
                InitializeLongPressTimer();

                // 完全清理旧模式
                try
                {
                    _pptManager?.StopMonitoring();
                    _pptManager?.Dispose();
                    _pptManager = null;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理旧 PPT 管理器异常: {ex}", LogHelper.LogType.Warning);
                }

                try
                {
                    StopPowerPointProcessMonitoring();
                    _powerPointProcessMonitorTimer = null;
                    ClosePowerPointApplication();
                    ClearStaticInteropState();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理 Interop 状态异常: {ex}", LogHelper.LogType.Warning);
                }

                // 根据设置选择 COM / ROT 架构
                if (Settings.PowerPointSettings.UseRotPptLink)
                {
                    _pptManager = new ROTPPTManager();
                }
                else
                {
                    _pptManager = new ComPPTLinkManager();
                }

                _pptManager.IsSupportWPS = Settings.PowerPointSettings.IsSupportWPS;
                _pptManager.SkipAnimationsWhenNavigating = Settings.PowerPointSettings.SkipAnimationsWhenGoNext;

                // 注册事件
                _pptManager.PPTConnectionChanged += OnPPTConnectionChanged;
                _pptManager.SlideShowBegin += o => OnPPTSlideShowBegin(o as SlideShowWindow);
                _pptManager.SlideShowNextSlide += o => OnPPTSlideShowNextSlide(o as SlideShowWindow);
                _pptManager.SlideShowEnd += o => OnPPTSlideShowEnd(o as Presentation);
                _pptManager.PresentationOpen += o => OnPPTPresentationOpen(o as Presentation);
                _pptManager.PresentationClose += o => OnPPTPresentationClose(o as Presentation);
                _pptManager.SlideShowStateChanged += OnPPTSlideShowStateChanged;

                _singlePPTInkManager = new PPTInkManager();
                _singlePPTInkManager.IsAutoSaveEnabled = Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                _singlePPTInkManager.AutoSaveLocation = Settings.Automation.AutoSavedStrokesLocation;

                // 初始化UI管理器
                _pptUIManager = new PPTUIManager(this);
                _pptUIManager.ShowPPTButton = Settings.PowerPointSettings.ShowPPTButton;
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.PPTBButtonsOption = Settings.PowerPointSettings.PPTBButtonsOption;
                _pptUIManager.PPTLSButtonPosition = Settings.PowerPointSettings.PPTLSButtonPosition;
                _pptUIManager.PPTRSButtonPosition = Settings.PowerPointSettings.PPTRSButtonPosition;
                _pptUIManager.PPTLBButtonPosition = Settings.PowerPointSettings.PPTLBButtonPosition;
                _pptUIManager.PPTRBButtonPosition = Settings.PowerPointSettings.PPTRBButtonPosition;
                _pptUIManager.EnablePPTButtonPageClickable = Settings.PowerPointSettings.EnablePPTButtonPageClickable;
                _pptUIManager.EnablePPTButtonLongPressPageTurn = Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn;

                LogHelper.WriteLogToFile("PPT管理器初始化完成", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT管理器初始化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动PPT监控：当PowerPoint支持功能启用时，启动PPT管理器的监控功能。
        /// </summary>
        /// <remarks>
        /// 只有当Settings.PowerPointSettings.PowerPointSupport为true时才会启动监控，并记录启动事件日志。
        /// </remarks>
        private void StartPPTMonitoring()
        {
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                _pptManager?.StartMonitoring();
                LogHelper.WriteLogToFile("PPT监控已启动", LogHelper.LogType.Event);
            }
        }

        /// <summary>
        /// 停止 PowerPoint 相关的监控：停止并清除用于延迟退出 PPT 模式的定时器，并停止 PPT 管理器的监控，同时记录事件日志。
        /// </summary>
        private void StopPPTMonitoring()
        {
            try
            {
                _exitPPTModeAfterDisconnectTimer?.Stop();
                _exitPPTModeAfterDisconnectTimer = null;
            }
            catch
            {
            }

            _pptManager?.StopMonitoring();
            LogHelper.WriteLogToFile("PPT监控已停止", LogHelper.LogType.Event);
        }

        #region PowerPoint Application Management
        /// <summary>
        /// 启动PowerPoint应用程序守护
        /// <summary>
        /// 启动对本地 PowerPoint 应用实例的守护监控并在需要时创建应用程序实例。
        /// </summary>
        /// <remarks>
        /// 仅在 PowerPoint 增强功能已启用且未使用 ROT 链接时生效；方法将创建 PowerPoint 应用（若不存在）并启动用于定期检查应用状态的定时器。
        /// </remarks>
        private void StartPowerPointProcessMonitoring()
        {
            try
            {
                if (!Settings.PowerPointSettings.EnablePowerPointEnhancement) return;
                if (Settings.PowerPointSettings.UseRotPptLink) return;

                // 创建PowerPoint应用程序实例
                CreatePowerPointApplication();

                // 启动应用程序监控定时器
                if (_powerPointProcessMonitorTimer == null)
                {
                    _powerPointProcessMonitorTimer = new DispatcherTimer();
                    _powerPointProcessMonitorTimer.Interval = TimeSpan.FromMilliseconds(ProcessMonitorInterval);
                    _powerPointProcessMonitorTimer.Tick += OnPowerPointApplicationMonitorTick;
                }
                _powerPointProcessMonitorTimer.Start();

                LogHelper.WriteLogToFile("PowerPoint应用程序守护已启动", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止PowerPoint应用程序守护
        /// </summary>
        private void StopPowerPointProcessMonitoring()
        {
            try
            {
                // 停止应用程序监控定时器
                _powerPointProcessMonitorTimer?.Stop();

                // 关闭PowerPoint应用程序
                ClosePowerPointApplication();

                LogHelper.WriteLogToFile("PowerPoint应用程序守护已停止", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 创建PowerPoint应用程序实例
        /// <summary>
        /// 创建并初始化一个隐藏的 PowerPoint 应用程序 COM 实例，并在可用时将该实例注入到当前的 PPT 管理器中。
        /// </summary>
        /// <remarks>
        /// 如果配置为使用 ROT 链接或已有有效的 PowerPoint 实例，则不会创建新实例。创建的实例会被设置为不可见并最小化；在实例准备就绪后会通过延迟调用将其设置到 PPT 管理器（SetPPTManagerApplication）。任何创建或注入失败的情况会被记录日志，但不会抛出异常给调用者。
        /// </remarks>
        private void CreatePowerPointApplication()
        {
            try
            {
                if (Settings.PowerPointSettings.UseRotPptLink) return;
                // 如果应用程序已存在且有效，则不重复创建
                if (pptApplication != null && IsPowerPointApplicationValid())
                {
                    return;
                }

                // 创建新的PowerPoint应用程序实例
                pptApplication = new Microsoft.Office.Interop.PowerPoint.Application();

                // 设置为不可见，作为后台进程
                pptApplication.Visible = MsoTriState.msoFalse;

                // 设置应用程序属性
                pptApplication.WindowState = PpWindowState.ppWindowMinimized;

                // 直接设置PPTManager的PPTApplication属性，绕过COM注册问题
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // 直接设置PPTManager的PowerPoint应用程序实例
                            if (_pptManager != null)
                            {
                                // 使用反射或直接访问来设置PPTManager的PPTApplication
                                SetPPTManagerApplication(pptApplication);
                                LogHelper.WriteLogToFile("已直接设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Event);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"设置PPTManager的PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
                        }
                    });
                });

                LogHelper.WriteLogToFile("PowerPoint应用程序实例已创建", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 设置PPTManager的PowerPoint应用程序实例
        /// </summary>
        /// <remarks>
        /// 将给定的 PowerPoint 应用实例注入到当前的 PPT 管理器中，若管理器为 null 或启用 ROT 链接则不做任何操作。
        /// 尝试使用非公开的 `ConnectToPPT` 方法进行绑定，若不可用则回退到写入公共 `PPTApplication` 属性；操作结果和异常通过日志记录。
        /// </remarks>
        /// <param name="app">要注入的 PowerPoint 应用实例（Microsoft.Office.Interop.PowerPoint.Application）。</param>
        private void SetPPTManagerApplication(Microsoft.Office.Interop.PowerPoint.Application app)
        {
            try
            {
                if (_pptManager == null) return;
                if (Settings.PowerPointSettings.UseRotPptLink) return;

                // 使用反射调用PPTManager的ConnectToPPT方法
                var pptManagerType = _pptManager.GetType();
                var connectMethod = pptManagerType.GetMethod("ConnectToPPT",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (connectMethod != null)
                {
                    connectMethod.Invoke(_pptManager, new object[] { app });
                    LogHelper.WriteLogToFile("通过ConnectToPPT方法设置PowerPoint应用程序实例", LogHelper.LogType.Event);
                }
                else
                {
                    // 如果无法通过反射调用，尝试直接设置属性
                    var pptApplicationProperty = pptManagerType.GetProperty("PPTApplication",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (pptApplicationProperty != null && pptApplicationProperty.CanWrite)
                    {
                        pptApplicationProperty.SetValue(_pptManager, app);
                        LogHelper.WriteLogToFile("通过属性设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("无法设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置PPTManager的PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查PowerPoint应用程序是否有效
        /// </summary>
        private bool IsPowerPointApplicationValid()
        {
            try
            {
                if (pptApplication == null) return false;
                if (!Marshal.IsComObject(pptApplication)) return false;

                // 尝试访问一个简单的属性来验证连接是否有效
                var _ = pptApplication.Name;
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

        /// <summary>
        /// 关闭PowerPoint应用程序
        /// </summary>
        /// <remarks>
        /// 关闭当前的 PowerPoint 应用程序及其所有打开的演示文稿，释放相关 COM 资源并清理静态互操作状态。</summary>
        /// 会尝试关闭所有打开的演示文稿、退出 PowerPoint 进程、释放 COM 对象引用，并将内部 PowerPoint 互操作状态重置为初始值；操作结果会被记录到日志，发生异常时会记录错误并仍然尝试清理互操作状态。
        /// </remarks>
        private void ClosePowerPointApplication()
        {
            try
            {
                if (pptApplication != null)
                {
                    // 关闭所有打开的演示文稿
                    if (pptApplication.Presentations.Count > 0)
                    {
                        for (int i = pptApplication.Presentations.Count; i >= 1; i--)
                        {
                            try
                            {
                                pptApplication.Presentations[i].Close();
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }

                    // 退出PowerPoint应用程序
                    pptApplication.Quit();

                    // 释放COM对象
                    Marshal.ReleaseComObject(pptApplication);
                    pptApplication = null;
                }

                ClearStaticInteropState();
                LogHelper.WriteLogToFile("PowerPoint应用程序已关闭", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭PowerPoint应用程序失败: {ex}", LogHelper.LogType.Error);
                ClearStaticInteropState();
            }
        }

        /// <summary>
        /// 释放并清理与 PowerPoint COM 互操作相关的引用（演示文稿、Slides、当前幻灯片），并将幻灯片计数重置为 0。
        /// </summary>
        /// <remarks>
        /// 在释放过程中若发生异常会被捕获并以警告级别记录日志，不会抛出异常到调用者。
        /// </remarks>
        private void ClearStaticInteropState()
        {
            try
            {
                if (presentation != null)
                {
                    try { if (Marshal.IsComObject(presentation)) Marshal.ReleaseComObject(presentation); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    presentation = null;
                }
                if (slides != null)
                {
                    try { if (Marshal.IsComObject(slides)) Marshal.ReleaseComObject(slides); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    slides = null;
                }
                if (slide != null)
                {
                    try { if (Marshal.IsComObject(slide)) Marshal.ReleaseComObject(slide); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    slide = null;
                }
                slidescount = 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ClearStaticInteropState 异常: {ex}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// PowerPoint应用程序监控定时器事件
        /// </summary>
        /// <remarks>
        /// 周期性监控嵌入的 PowerPoint 应用实例的可用性，并在检测到失效时尝试重建实例；当增强功能被禁用时停止监控，并在使用 ROT 链接时不进行检查。
        /// </remarks>
        private void OnPowerPointApplicationMonitorTick(object sender, EventArgs e)
        {
            try
            {
                if (!Settings.PowerPointSettings.EnablePowerPointEnhancement)
                {
                    StopPowerPointProcessMonitoring();
                    return;
                }
                if (Settings.PowerPointSettings.UseRotPptLink) return;

                // 检查应用程序是否还在运行
                if (!IsPowerPointApplicationValid())
                {
                    LogHelper.WriteLogToFile("检测到PowerPoint应用程序已失效，重新创建", LogHelper.LogType.Event);
                    CreatePowerPointApplication();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PowerPoint应用程序监控异常: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        /// <summary>
        /// 释放并停止所有与 PowerPoint 集成相关的管理器与资源，恢复和清理应用的 PPT 相关运行状态。
        /// </summary>
        /// <remarks>
        /// 操作包括停止并释放 PPT 管理器、墨迹管理器和长按计时器，停止 PowerPoint 进程监控，关闭 PowerPoint 应用并清除静态 COM/互操作状态；所有异常会被捕获并记录为错误日志。
        /// </remarks>
        private void DisposePPTManagers()
        {
            try
            {
                _pptManager?.StopMonitoring();
                _pptManager?.Dispose();
                _singlePPTInkManager?.Dispose();
                _longPressTimer?.Stop();
                _longPressTimer = null;
                _pptManager = null;
                _singlePPTInkManager = null;
                _pptUIManager = null;

                // 清理PowerPoint进程守护
                StopPowerPointProcessMonitoring();
                _powerPointProcessMonitorTimer = null;
                ClosePowerPointApplication();
                ClearStaticInteropState();

                LogHelper.WriteLogToFile("PPT管理器已释放", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放PPT管理器失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化长按定时器
        /// </summary>
        private void InitializeLongPressTimer()
        {
            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressDelay);
            _longPressTimer.Tick += OnLongPressTimerTick;
        }

        /// <summary>
        /// 启动长按检测
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="isNext">是否为下一页按钮</param>
        private void StartLongPressDetection(object sender, bool isNext)
        {
            if (!Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _isLongPressNext = isNext;
            // 重置定时器间隔为初始延迟时间，确保每次长按检测都从正确的延迟开始
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressDelay);
            _longPressTimer?.Start();
        }

        /// <summary>
        /// 停止长按检测
        /// </summary>
        private void StopLongPressDetection()
        {
            _longPressTimer?.Stop();
        }

        /// <summary>
        /// 长按定时器事件处理
        /// </summary>
        private void OnLongPressTimerTick(object sender, EventArgs e)
        {
            if (!Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressInterval);

            // 执行翻页
            if (_isLongPressNext)
            {
                BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
            }
            else
            {
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
            }
        }
        #endregion

        #region New PPT Event Handlers
        /// <summary>
        /// 处理 PowerPoint 连接状态的变更：更新界面连接/放映状态，并在断开时启动一个短延迟以安全退出 PPT 模式。
        /// </summary>
        /// <param name="isConnected">指示当前是否已与 PowerPoint 建立连接；`true` 表示已连接，`false` 表示已断开。</param>
        private void OnPPTConnectionChanged(bool isConnected)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _pptUIManager?.UpdateConnectionStatus(isConnected);

                    if (isConnected)
                    {
                        _exitPPTModeAfterDisconnectTimer?.Stop();
                        _exitPPTModeAfterDisconnectTimer = null;
                        LogHelper.WriteLogToFile("PPT连接已建立", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("PPT连接已断开", LogHelper.LogType.Event);
                        _singlePPTInkManager?.ClearAllStrokes();
                        _exitPPTModeAfterDisconnectTimer?.Stop();
                        _exitPPTModeAfterDisconnectTimer = null;
                        _pptUIManager?.UpdateSlideShowStatus(false);
                        _pptUIManager?.UpdateSidebarExitButtons(false);
                        ResetPPTStateVariables();
                        _ = HandleManualSlideShowEnd();
                        if (Settings.PowerPointSettings.UseRotPptLink)
                            _pptManager?.ReloadConnection();
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理PPT连接状态变化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理 PowerPoint 演示文稿打开事件：清理画布墨迹、初始化墨迹管理器、处理导航逻辑、检查隐藏幻灯片和自动播放设置，并更新连接状态。
        /// </summary>
        /// <param name="pres">已打开的 PowerPoint 演示文稿（Presentation）实例。</param>
        /// <remarks>
        /// 操作包括：清理画布墨迹和备份历史记录，初始化墨迹管理器，处理跳转到首页或上次播放页的逻辑，检查隐藏幻灯片和自动播放设置，更新UI连接状态，并记录事件日志。
        /// 所有操作在UI线程异步执行，异常会被捕获并记录为错误日志。
        /// </remarks>
        private void OnPPTPresentationOpen(Presentation pres)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 在初始化墨迹管理器之前，先清理画布上的所有墨迹
                    ClearStrokes(true);

                    // 清理备份历史记录，防止旧演示文稿的墨迹影响新演示文稿
                    if (TimeMachineHistories != null && TimeMachineHistories.Length > 0)
                    {
                        TimeMachineHistories[0] = null;
                    }

                    _singlePPTInkManager?.InitializePresentation(pres);

                    // 处理跳转到首页或上次播放页的逻辑
                    HandlePresentationOpenNavigation(pres);

                    // 检查隐藏幻灯片
                    if (Settings.PowerPointSettings.IsNotifyHiddenPage)
                    {
                        CheckAndNotifyHiddenSlides(pres);
                    }

                    // 检查自动播放设置
                    if (Settings.PowerPointSettings.IsNotifyAutoPlayPresentation)
                    {
                        CheckAndNotifyAutoPlaySettings(pres);
                    }

                    _pptUIManager?.UpdateConnectionStatus(true);

                    LogHelper.WriteLogToFile($"已打开新演示文稿: {pres.Name}，墨迹状态已清理", LogHelper.LogType.Event);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private string GetPresentationStrokeFolderPath(Presentation presentation, string presentationName, int totalSlides)
        {
            string basePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "Auto Saved - Presentations");
            if (presentation != null && !string.IsNullOrEmpty(presentation.FullName))
            {
                string hash = HashHelper.GetFileHash(presentation.FullName);
                return Path.Combine(basePath, $"{presentation.Name}_{presentation.Slides.Count}_{hash}");
            }
            return Path.Combine(basePath, $"{presentationName ?? ""}_{totalSlides}");
        }

        private void OnPPTPresentationClose(Presentation pres)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    lock (_memoryStreams)
                    {
                        foreach (var stream in _memoryStreams.Values)
                            stream?.Dispose();
                        _memoryStreams.Clear();
                    }

                    _pptUIManager?.UpdateConnectionStatus(false);
                });
            }
            catch (COMException comEx)
            {
                // COM对象已失效，这是正常情况，完全静默处理
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                {
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映状态变化事件：更新UI管理器的放映状态并检查主窗口可见性。
        /// </summary>
        /// <param name="isInSlideShow">指示当前是否处于幻灯片放映状态；`true` 表示正在放映，`false` 表示已退出放映。</param>
        /// <remarks>
        /// 操作包括：在UI线程异步通知UI管理器放映状态变化，检查并更新主窗口的可见性（用于仅PPT模式）。
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void OnPPTSlideShowStateChanged(bool isInSlideShow)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 通知UI管理器放映状态变化
                    _pptUIManager?.OnSlideShowStateChanged(isInSlideShow);

                    if (!isInSlideShow)
                    {
                    }

                    // 检查主窗口可见性（用于仅PPT模式）
                    CheckMainWindowVisibility();
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理PPT放映状态变化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映开始事件：根据设置折叠或展开浮动栏，初始化放映状态，更新UI，加载当前页墨迹，并设置相关参数。
        /// </summary>
        /// <param name="wn">PowerPoint 幻灯片放映窗口（SlideShowWindow）实例，包含当前放映状态和视图信息。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 根据设置自动折叠或展开浮动栏
        /// 2. 停止墨迹重放
        /// 3. 获取当前活动演示文稿、当前幻灯片和总幻灯片数
        /// 4. 初始化墨迹管理器
        /// 5. 处理跳转到首页或上次播放位置的逻辑
        /// 6. 更新UI状态，包括放映状态、当前幻灯片编号
        /// 7. 设置浮动栏透明度和边距
        /// 8. 显示侧边栏退出按钮
        /// 9. 处理画板显示
        /// 10. 关闭白板模式（如果当前在白板模式）
        /// 11. 显示浮动栏主控件
        /// 12. 根据设置隐藏或显示手势面板和按钮
        /// 13. 如果设置了在新放映时显示画布，则进入批注模式并显示调色盘
        /// 14. 重置幻灯片放映结束事件标志
        /// 15. 加载当前页墨迹
        /// 16. 调整浮动栏边距动画
        /// 
        /// 所有UI操作在UI线程异步执行，异常会被捕获并记录为错误日志。
        /// </remarks>
        private async void OnPPTSlideShowBegin(SlideShowWindow wn)
        {
            try
            {
                if (Settings.Automation.IsAutoFoldInPPTSlideShow)
                {
                    if (!isFloatingBarFolded)
                        FoldFloatingBar_MouseUp(new object(), null);
                }
                else
                {
                    if (isFloatingBarFolded)
                    {
                        await UnFoldFloatingBar(new object());
                    }
                }

                isStopInkReplay = true;

                int currentSlide = 0;
                int totalSlides = 0;
                string presentationName = null;
                Presentation activePresentation = null;

                if (wn != null)
                {
                    try
                    {
                        if (wn.View != null && wn.Presentation != null)
                        {
                            activePresentation = wn.Presentation;
                            currentSlide = wn.View.CurrentShowPosition;
                            totalSlides = activePresentation.Slides.Count;
                            presentationName = activePresentation.Name;
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        activePresentation = null;
                        currentSlide = 0;
                        totalSlides = 0;
                        presentationName = null;
                    }
                    catch (Exception)
                    {
                        activePresentation = null;
                        currentSlide = 0;
                        totalSlides = 0;
                        presentationName = null;
                    }
                }

                if (activePresentation == null)
                {
                    activePresentation = _pptManager?.GetCurrentActivePresentation() as Presentation;
                    currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    totalSlides = _pptManager?.SlidesCount ?? 0;
                    presentationName = _pptManager?.GetPresentationName() ?? activePresentation?.Name;
                }

                _currentSlideShowPosition = currentSlide;
                _previousSlideID = currentSlide;

                lock (_memoryStreams)
                {
                    foreach (var stream in _memoryStreams.Values)
                        stream?.Dispose();
                    _memoryStreams.Clear();
                }

                if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint && !string.IsNullOrEmpty(presentationName))
                {
                    string strokePath = GetPresentationStrokeFolderPath(activePresentation, presentationName, totalSlides);
                    if (Directory.Exists(strokePath))
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                var files = new DirectoryInfo(strokePath).GetFiles("*.icstk");
                                foreach (var file in files)
                                {
                                    int pageNum = 0;
                                    try
                                    {
                                        string name = Path.GetFileNameWithoutExtension(file.Name);
                                        if (int.TryParse(name, out pageNum) && pageNum > 0)
                                        {
                                            byte[] bytes = File.ReadAllBytes(file.FullName);
                                            if (bytes.Length > 8)
                                            {
                                                lock (_memoryStreams)
                                                {
                                                    _memoryStreams[pageNum] = new MemoryStream(bytes);
                                                    _memoryStreams[pageNum].Position = 0;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.WriteLogToFile($"加载第 {pageNum} 页墨迹文件失败: {ex}", LogHelper.LogType.Warning);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"加载PPT墨迹文件失败: {ex}", LogHelper.LogType.Error);
                            }
                        });
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (activePresentation != null && _singlePPTInkManager != null)
                    {
                        try
                        {
                            _singlePPTInkManager.InitializePresentation(activePresentation);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    // 处理跳转到首页或上次播放位置
                    if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                    {
                        _pptManager?.TryNavigateToSlide(1);
                        if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) { try { this.Activate(); } catch { } }
                    }
                    else if (_shouldNavigateToLastPage && _lastPlaybackPage > 0)
                    {
                        _pptManager?.TryNavigateToSlide(_lastPlaybackPage);
                        _shouldNavigateToLastPage = false; // 重置标志位
                        if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) { try { this.Activate(); } catch { } }
                    }

                    // 更新UI状态
                    _pptUIManager?.UpdateSlideShowStatus(true, currentSlide, totalSlides);

                    // 设置浮动栏透明度和边距
                    _pptUIManager?.SetFloatingBarOpacity(Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue);
                    _pptUIManager?.SetMainPanelMargin(new Thickness(10, 10, 10, 10));

                    // 显示侧边栏退出按钮
                    _pptUIManager?.UpdateSidebarExitButtons(true);

                    // 处理画板显示
                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow &&
                        GridTransparencyFakeBackground.Background == Brushes.Transparent && !isFloatingBarFolded)
                    {
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    if (currentMode != 0)
                    {
                        ImageBlackboard_MouseUp(null, null);
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    BorderFloatingBarMainControls.Visibility = Visibility.Visible;

                    // 在PPT模式下根据设置决定是否隐藏手势面板和手势按钮
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);

                    // 根据设置决定是否在PPT放映模式下显示手势按钮
                    if (Settings.PowerPointSettings.ShowGestureButtonInSlideShow)
                    {
                        // 如果启用了PPT放映模式显示手势按钮，则显示手势按钮
                        if (Settings.Gesture.IsEnableTwoFingerGesture)
                        {
                            CheckEnableTwoFingerGestureBtnVisibility(true);
                        }
                    }
                    else
                    {
                        // 如果禁用了PPT放映模式显示手势按钮，则隐藏手势按钮
                        EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                    }

                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow)
                    {
                        await Task.Delay(300);
                        // 先进入批注模式，这会显示调色盘
                        PenIcon_Click(null, null);
                        // 然后设置颜色
                        BtnColorRed_Click(null, null);
                        try
                        {
                            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                            {
                                UpdateCurrentToolMode("pen");
                                SetFloatingBarHighlightPosition("pen");
                                if (Settings.Appearance.IsShowQuickColorPalette && QuickColorPalettePanel != null && QuickColorPaletteSingleRowPanel != null)
                                {
                                    // 根据显示模式选择显示哪个面板
                                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                                    {
                                        // 单行显示模式
                                        QuickColorPalettePanel.Visibility = Visibility.Collapsed;
                                        QuickColorPaletteSingleRowPanel.Visibility = Visibility.Visible;
                                    }
                                    else
                                    {
                                        // 双行显示模式
                                        QuickColorPalettePanel.Visibility = Visibility.Visible;
                                        QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"PPT进入批注模式后同步浮动栏高光状态失败: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }

                    isEnteredSlideShowEndEvent = false;

                    // 加载当前页墨迹
                    LoadCurrentSlideInk(currentSlide);
                });

                if (!isFloatingBarFolded)
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(100);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ViewboxFloatingBarMarginAnimation(60);
                        });
                    }).Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映开始事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理幻灯片放映中的切换：在幻灯片变更时保存当前页墨迹、加载目标页墨迹并更新界面状态。
        /// </summary>
        /// <param name="wn">当前的幻灯片放映窗口；若为 null 或其 View/Presentation 无效则方法不执行。</param>
        /// <remarks>
        /// - 如果收到与当前记录相同的页码或已有切换正在处理，则忽略该事件。 
        /// - 在切换过程中会保存前一页的墨迹（如存在）、清空画布与历史、加载新页的墨迹、锁定新页墨迹并刷新当前页显示序号，同时更新内部的当前播放位置状态。
        /// </remarks>
        private void OnPPTSlideShowNextSlide(SlideShowWindow wn)
        {
            try
            {
                int currentSlide = 0;
                int totalSlides = 0;

                if (wn != null)
                {
                    try
                    {
                        if (wn.View != null)
                        {
                            currentSlide = wn.View.CurrentShowPosition;
                        }
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        LogHelper.WriteLogToFile(
                            $"通过 SlideShowWindow.View 获取当前页失败: {comEx.Message} (HR: 0x{hr:X8})，将回退到 PPT 管理器获取",
                            LogHelper.LogType.Warning);
                        currentSlide = 0;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile(
                            $"通过 SlideShowWindow.View 获取当前页时发生异常，将回退到 PPT 管理器获取: {ex}",
                            LogHelper.LogType.Warning);
                        currentSlide = 0;
                    }
                }

                if (currentSlide <= 0)
                {
                    currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                }

                totalSlides = _pptManager?.SlidesCount ?? 0;

                if (currentSlide == _previousSlideID) return;

                int prev = _previousSlideID;

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var ms = new MemoryStream();
                    inkCanvas.Strokes.Save(ms);
                    ms.Position = 0;
                    lock (_memoryStreams)
                    {
                        if (_memoryStreams.ContainsKey(prev))
                            _memoryStreams[prev]?.Dispose();
                        _memoryStreams[prev] = ms;
                    }

                    ClearStrokes(true);
                    timeMachine.ClearStrokeHistory();

                    _currentSlideShowPosition = currentSlide;
                    _singlePPTInkManager?.LockInkForSlide(currentSlide);
                    _pptUIManager?.UpdateCurrentSlideNumber(currentSlide, totalSlides);

                    byte[] bytesToLoad = null;
                    lock (_memoryStreams)
                    {
                        if (_memoryStreams.ContainsKey(currentSlide) && _memoryStreams[currentSlide] != null)
                            bytesToLoad = _memoryStreams[currentSlide].ToArray();
                    }
                    if (bytesToLoad != null)
                    {
                        int loadingPage = currentSlide;
                        Task.Run(() =>
                        {
                            try
                            {
                                return new StrokeCollection(new MemoryStream(bytesToLoad));
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"从内存流加载第 {loadingPage} 页墨迹失败: {ex}", LogHelper.LogType.Warning);
                                return null;
                            }
                        }).ContinueWith(t =>
                        {
                            if (t.IsFaulted || t.Result == null) return;
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (_currentSlideShowPosition != loadingPage) return;
                                inkCanvas.Strokes.Add(t.Result);
                            });
                        });
                    }
                });
                _previousSlideID = currentSlide;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片切换事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映结束时的清理与界面恢复，包括保存当前幻灯片墨迹、重置墨迹管理器状态、恢复主题与工具栏显示，并根据配置折叠或展示浮动工具栏等 UI 调整。
        /// </summary>
        /// <param name="pres">触发结束事件的 PowerPoint 演示文稿（Presentation）实例，用于保存墨迹并尝试读取放映时的当前页码。</param>
        private async void OnPPTSlideShowEnd(Presentation pres)
        {
            try
            {
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow && !isFloatingBarFolded)
                {
                    FoldFloatingBar_MouseUp(new object(), null);
                }

                if (isEnteredSlideShowEndEvent) return;
                isEnteredSlideShowEndEvent = true;

                // 获取当前播放页码，优先使用跟踪的页码，否则尝试从PPT管理器获取
                int currentPage = _currentSlideShowPosition;
                if (currentPage <= 0)
                {
                    try
                    {
                        currentPage = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    }
                    catch
                    {
                        // 如果无法获取，尝试从演示文稿的SlideShowWindow获取
                        try
                        {
                            if (pres.SlideShowWindow != null && pres.SlideShowWindow.View != null)
                            {
                                currentPage = pres.SlideShowWindow.View.CurrentShowPosition;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (currentPage > 0 && inkCanvas?.Strokes != null && inkCanvas.Strokes.Count > 0)
                    {
                        var ms = new MemoryStream();
                        inkCanvas.Strokes.Save(ms);
                        ms.Position = 0;
                        lock (_memoryStreams)
                        {
                            if (_memoryStreams.ContainsKey(currentPage))
                                _memoryStreams[currentPage]?.Dispose();
                            _memoryStreams[currentPage] = ms;
                        }
                    }
                });

                string presentationNameForSave = _pptManager?.GetPresentationName() ?? (pres != null ? pres.Name : null);
                int totalSlidesForSave = _pptManager?.SlidesCount ?? (pres != null ? pres.Slides.Count : 0);

                if (currentPage > 0 && Settings.PowerPointSettings.IsNotifyPreviousPage && !string.IsNullOrEmpty(presentationNameForSave) && totalSlidesForSave > 0)
                {
                    try
                    {
                        string folderPathForPosition = GetPresentationStrokeFolderPath(pres, presentationNameForSave, totalSlidesForSave);
                        if (!Directory.Exists(folderPathForPosition))
                            Directory.CreateDirectory(folderPathForPosition);
                        File.WriteAllText(Path.Combine(folderPathForPosition, "Position"), currentPage.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"保存上次播放位置失败: {ex}", LogHelper.LogType.Warning);
                    }
                }

                if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint && !string.IsNullOrEmpty(presentationNameForSave) && totalSlidesForSave > 0)
                {
                    string folderPathForSave = GetPresentationStrokeFolderPath(pres, presentationNameForSave, totalSlidesForSave);
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (!Directory.Exists(folderPathForSave))
                                Directory.CreateDirectory(folderPathForSave);

                            lock (_memoryStreams)
                            {
                                for (int i = 1; i <= totalSlidesForSave; i++)
                                {
                                    if (_memoryStreams.TryGetValue(i, out MemoryStream value) && value != null)
                                    {
                                        try
                                        {
                                            byte[] allBytes = value.ToArray();
                                            string filePath = Path.Combine(folderPathForSave, i.ToString("0000") + ".icstk");
                                            if (allBytes.Length > 8)
                                                File.WriteAllBytes(filePath, allBytes);
                                            else if (File.Exists(filePath))
                                                File.Delete(filePath);
                                        }
                                        catch (Exception ex)
                                        {
                                            LogHelper.WriteLogToFile($"为第 {i} 页保存墨迹文件失败: {ex}", LogHelper.LogType.Warning);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"保存PPT墨迹文件失败: {ex}", LogHelper.LogType.Error);
                        }
                        finally
                        {
                            lock (_memoryStreams)
                            {
                                foreach (var stream in _memoryStreams.Values)
                                    stream?.Dispose();
                                _memoryStreams.Clear();
                            }
                        }
                    });
                }
                else
                {
                    lock (_memoryStreams)
                    {
                        foreach (var stream in _memoryStreams.Values)
                            stream?.Dispose();
                        _memoryStreams.Clear();
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        isPresentationHaveBlackSpace = false;

                        // 恢复主题
                        if (BtnSwitchTheme.Content.ToString() == "深色")
                        {
                            BtnExit.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        // 更新UI状态
                        _pptUIManager?.UpdateSlideShowStatus(false);
                        _pptUIManager?.UpdateSidebarExitButtons(false);
                        _pptUIManager?.SetMainPanelMargin(new Thickness(10, 10, 10, 55));
                        _pptUIManager?.SetFloatingBarOpacity(Settings.Appearance.ViewboxFloatingBarOpacityValue);

                        if (currentMode != 0)
                        {
                            CloseWhiteboardImmediately();
                            currentMode = 0;
                        }

                        ClearStrokes(true);
                        // 清空备份历史记录，防止退出白板时恢复已结束PPT的墨迹
                        // 注意：这里只清空索引0的备份，不影响白板页面的墨迹（索引1及以上）
                        TimeMachineHistories[0] = null;

                        // 重置墨迹管理器的锁定状态，防止下次放映时墨迹显示错误
                        ResetInkManagerLockState();

                        // 退出PPT模式时恢复手势面板和手势按钮的显示状态
                        if (Settings.Gesture.IsEnableTwoFingerGesture && ToggleSwitchEnableMultiTouchMode.IsOn)
                        {
                            // 根据手势设置决定是否显示手势面板和手势按钮
                            CheckEnableTwoFingerGestureBtnVisibility(true);
                        }
                        else
                        {
                            // 如果手势功能未启用，确保手势按钮保持隐藏
                            EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                        }

                        // 退出PPT模式时隐藏快捷调色盘
                        if (QuickColorPalettePanel != null)
                        {
                            QuickColorPalettePanel.Visibility = Visibility.Collapsed;
                        }
                        if (QuickColorPaletteSingleRowPanel != null)
                        {
                            QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
                        }

                        if (GridTransparencyFakeBackground.Background != Brushes.Transparent)
                            BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                        SetCurrentToolMode(InkCanvasEditingMode.None);

                        UpdateCurrentToolMode("cursor");
                        SetFloatingBarHighlightPosition("cursor");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理幻灯片放映结束UI更新失败: {ex}", LogHelper.LogType.Error);
                    }
                });

                await Task.Delay(100);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!isFloatingBarFolded)
                    {
                        PureViewboxFloatingBarMarginAnimationInDesktopMode();
                        if (Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                        {
                            Task.Delay(350).ContinueWith(_ =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (!isFloatingBarFolded)
                                    {
                                        ViewboxFloatingBarMarginAnimation(-60);
                                    }
                                });
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映结束事件失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 处理演示文稿打开时的导航逻辑：根据设置决定跳转到首页或显示上次播放页通知。
        /// </summary>
        /// <param name="pres">当前打开的 PowerPoint 演示文稿（Presentation）实例。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 如果设置了总是跳转到首页，则尝试导航到第1页
        /// 2. 否则，如果设置了显示上次播放页通知，则显示上次播放页通知
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void HandlePresentationOpenNavigation(Presentation pres)
        {
            try
            {
                if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                {
                    _pptManager?.TryNavigateToSlide(1);
                    if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) { try { this.Activate(); } catch { } }
                }
                else if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                {
                    ShowPreviousPageNotification(pres);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿导航失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 显示上次播放页通知：检查演示文稿的上次播放位置并显示跳转提示。
        /// </summary>
        /// <param name="pres">当前打开的 PowerPoint 演示文稿（Presentation）实例。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查演示文稿是否为null
        /// 2. 获取演示文稿路径并计算文件哈希值
        /// 3. 构建保存位置文件夹路径和位置文件路径
        /// 4. 检查位置文件是否存在
        /// 5. 尝试解析位置文件中的页码
        /// 6. 如果解析成功且页码大于0，则保存上次播放页码并显示跳转提示窗口
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void ShowPreviousPageNotification(Presentation pres)
        {
            try
            {
                if (pres == null) return;

                var folderPath = GetPresentationStrokeFolderPath(pres, pres.Name, pres.Slides.Count);
                var positionFile = Path.Combine(folderPath, "Position");

                if (!File.Exists(positionFile)) return;

                if (int.TryParse(File.ReadAllText(positionFile), out var page) && page > 0)
                {
                    _lastPlaybackPage = page;
                    new YesOrNoNotificationWindow($"上次播放到了第 {page} 页, 是否立即跳转", () =>
                    {
                        try
                        {
                            var pptApp = _pptManager?.PPTApplication as Microsoft.Office.Interop.PowerPoint.Application;
                            if (pptApp != null)
                            {
                                if (pptApp.SlideShowWindows.Count >= 1)
                                {
                                    pres.SlideShowWindow.View.GotoSlide(page);
                                }
                                else
                                {
                                    pres.Windows[1].View.GotoSlide(page);
                                }
                                if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) { try { this.Activate(); } catch { } }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"跳转到第{page}页失败: {ex}", LogHelper.LogType.Error);
                        }
                    }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示上次播放页通知失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查并通知隐藏幻灯片：扫描演示文稿中的所有幻灯片，检测隐藏幻灯片并显示取消隐藏的提示。
        /// </summary>
        /// <param name="pres">要检查的 PowerPoint 演示文稿（Presentation）实例。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查演示文稿及其幻灯片集合是否为null
        /// 2. 遍历所有幻灯片，检测是否存在隐藏的幻灯片
        /// 3. 如果存在隐藏幻灯片且未显示过恢复隐藏幻灯片窗口，则显示确认窗口
        /// 4. 如果用户确认，则取消所有幻灯片的隐藏状态
        /// 5. 无论用户选择如何，都会重置IsShowingRestoreHiddenSlidesWindow标志
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void CheckAndNotifyHiddenSlides(Presentation pres)
        {
            try
            {
                bool hasHiddenSlides = false;
                if (pres?.Slides != null)
                {
                    foreach (Slide slide in pres.Slides)
                    {
                        if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                        {
                            hasHiddenSlides = true;
                            break;
                        }
                    }
                }

                if (hasHiddenSlides && !IsShowingRestoreHiddenSlidesWindow)
                {
                    IsShowingRestoreHiddenSlidesWindow = true;
                    new YesOrNoNotificationWindow("检测到此演示文档中包含隐藏的幻灯片，是否取消隐藏？",
                        () =>
                        {
                            try
                            {
                                if (pres?.Slides != null)
                                {
                                    foreach (Slide slide in pres.Slides)
                                    {
                                        if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                                            slide.SlideShowTransition.Hidden = MsoTriState.msoFalse;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"取消隐藏幻灯片失败: {ex}", LogHelper.LogType.Error);
                            }
                            finally
                            {
                                IsShowingRestoreHiddenSlidesWindow = false;
                            }
                        },
                        () => { IsShowingRestoreHiddenSlidesWindow = false; },
                        () => { IsShowingRestoreHiddenSlidesWindow = false; }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查隐藏幻灯片失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查并通知自动播放设置：扫描演示文稿中的所有幻灯片，检测自动播放或排练计时设置并显示取消提示。
        /// </summary>
        /// <param name="pres">要检查的 PowerPoint 演示文稿（Presentation）实例。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查是否正在显示PPT放映结束按钮，如果是则直接返回
        /// 2. 检查演示文稿及其幻灯片集合是否为null
        /// 3. 遍历所有幻灯片，检测是否存在自动播放或排练计时设置
        /// 4. 如果存在自动播放设置且未显示过自动播放提示窗口，则显示确认窗口
        /// 5. 如果用户确认，则将演示文稿的放映设置设置为手动播放模式
        /// 6. 无论用户选择如何，都会重置IsShowingAutoplaySlidesWindow标志
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void CheckAndNotifyAutoPlaySettings(Presentation pres)
        {
            try
            {
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) return;

                bool hasSlideTimings = false;
                if (pres?.Slides != null)
                {
                    foreach (Slide slide in pres.Slides)
                    {
                        if (slide.SlideShowTransition.AdvanceOnTime == MsoTriState.msoTrue &&
                            slide.SlideShowTransition.AdvanceTime > 0)
                        {
                            hasSlideTimings = true;
                            break;
                        }
                    }
                }

                if (hasSlideTimings && !IsShowingAutoplaySlidesWindow)
                {
                    IsShowingAutoplaySlidesWindow = true;
                    new YesOrNoNotificationWindow("检测到此演示文档中自动播放或排练计时已经启用，可能导致幻灯片自动翻页，是否取消？",
                        () =>
                        {
                            try
                            {
                                if (pres != null)
                                {
                                    pres.SlideShowSettings.AdvanceMode = PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"设置手动播放模式失败: {ex}", LogHelper.LogType.Error);
                            }
                            finally
                            {
                                IsShowingAutoplaySlidesWindow = false;
                            }
                        },
                        () => { IsShowingAutoplaySlidesWindow = false; },
                        () => { IsShowingAutoplaySlidesWindow = false; }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查自动播放设置失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载当前幻灯片的墨迹：清空画布和历史记录，然后加载指定幻灯片的墨迹。
        /// </summary>
        /// <param name="slideIndex">要加载墨迹的幻灯片索引。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 清空画布上的所有墨迹
        /// 2. 清空时间机器的墨迹历史记录
        /// 3. 从墨迹管理器加载指定幻灯片的墨迹
        /// 4. 如果加载到墨迹且墨迹集合不为空，则将墨迹添加到画布
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// </remarks>
        private void LoadCurrentSlideInk(int slideIndex)
        {
            try
            {
                ClearStrokes(true);
                timeMachine.ClearStrokeHistory();

                byte[] bytes = null;
                lock (_memoryStreams)
                {
                    if (_memoryStreams.TryGetValue(slideIndex, out var ms) && ms != null && ms.Length > 0)
                    {
                        ms.Position = 0;
                        bytes = ms.ToArray();
                    }
                }
                if (bytes != null)
                {
                    try
                    {
                        inkCanvas.Strokes.Add(new StrokeCollection(new MemoryStream(bytes)));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"从内存流加载第 {slideIndex} 页墨迹失败: {ex}", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载当前页墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置墨迹管理器的锁定状态，防止墨迹显示错误
        /// </summary>
        private void ResetInkManagerLockState()
        {
            try
            {
                _singlePPTInkManager?.ResetLockState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置墨迹管理器锁定状态失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置PPT相关的状态变量，当PPT自动收纳设置变更时调用
        /// </summary>
        /// <remarks>
        /// 将与 PowerPoint 播放和状态追踪相关的内部字段重置为初始默认值。
        /// 具体重置的字段包括：
        /// 1. 播放结束重入保护标志（isEnteredSlideShowEndEvent）
        /// 2. 演示文稿黑边指示（isPresentationHaveBlackSpace）
        /// 3. 上次播放页码（_lastPlaybackPage）
        /// 4. 导航标志（_shouldNavigateToLastPage）
        /// 5. 当前放映位置（_currentSlideShowPosition）
        /// 6. 滑动切换处理状态（_isProcessingSlideSwitch）
        /// 
        /// 该方法在执行过程中会：
        /// - 使用线程安全的方式重置滑动切换处理状态
        /// - 成功时记录追踪日志
        /// - 发生异常时记录错误日志并继续执行
        /// </remarks>
        public void ResetPPTStateVariables()
        {
            try
            {
                // 重置PPT放映结束事件标志
                isEnteredSlideShowEndEvent = false;

                // 重置演示文稿黑边状态
                isPresentationHaveBlackSpace = false;

                // 重置上次播放位置相关字段
                _lastPlaybackPage = 0;
                _shouldNavigateToLastPage = false;

                // 重置当前播放页码跟踪
                _currentSlideShowPosition = 0;
                _previousSlideID = 0;
                lock (_memoryStreams)
                {
                    foreach (var stream in _memoryStreams.Values)
                        stream?.Dispose();
                    _memoryStreams.Clear();
                }

                LogHelper.WriteLogToFile("PPT状态变量已重置", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置PPT状态变量失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        /// <summary>
        /// 发起一次手动的 PowerPoint 连接检查并在短延迟后报告结果。
        /// </summary>
        /// <remarks>
        /// 如果尚未初始化 PPT 管理器则先进行初始化，然后重载连接并启动监控；
        /// 延迟约 800 毫秒后在 UI 线程上检查连接状态：若已连接仅记录事件日志，若未连接则弹出提示并记录警告；
        /// 若过程中抛出异常则记录错误日志、将 UI 连接状态置为断开并提示用户未找到幻灯片。
        /// </remarks>
        private void BtnCheckPPT_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用新的PPT管理器进行连接检查
                if (_pptManager == null)
                {
                    InitializePPTManagers();
                }

                _pptManager?.ReloadConnection();
                _pptManager?.StartMonitoring();

                Task.Delay(800).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_pptManager?.IsConnected == true)
                        {
                            LogHelper.WriteLogToFile("手动PPT连接检查成功", LogHelper.LogType.Event);
                        }
                        else
                        {
                            MessageBox.Show("未找到幻灯片");
                            LogHelper.WriteLogToFile("手动PPT连接检查失败", LogHelper.LogType.Warning);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动检查PPT应用程序失败: {ex}", LogHelper.LogType.Error);
                _pptUIManager?.UpdateConnectionStatus(false);
                MessageBox.Show("未找到幻灯片");
            }
        }

        /// <summary>
        /// 处理PowerPoint增强功能开关的切换事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当PowerPoint增强功能被启用时：
        /// 1. 禁用WPS支持
        /// 2. 更新PPT管理器的WPS支持设置
        /// 3. 启动PowerPoint进程守护
        /// 当PowerPoint增强功能被禁用时：
        /// 1. 停止PowerPoint进程守护
        /// 无论开关状态如何变化，都会保存设置到文件
        /// </remarks>
        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.EnablePowerPointEnhancement = ToggleSwitchPowerPointEnhancement.IsOn;

            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                Settings.PowerPointSettings.IsSupportWPS = false;
                ToggleSwitchSupportWPS.IsOn = false;

                // 更新PPT管理器的WPS支持设置
                if (_pptManager != null)
                {
                    _pptManager.IsSupportWPS = false;
                }
            }

            SaveSettingsToFile();

            // 启动或停止PowerPoint进程守护
            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                StartPowerPointProcessMonitoring();
            }
            else
            {
                StopPowerPointProcessMonitoring();
            }
        }

        /// <summary>
        /// 处理WPS支持开关的切换事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 当WPS支持被启用时：
        /// 1. 如果PowerPoint支持未启用，则启用PowerPoint支持
        /// 2. 启动PPT监控
        /// 3. 如果PowerPoint增强功能已启用，则禁用它并停止PowerPoint进程守护
        /// 无论开关状态如何变化，都会：
        /// 1. 更新PPT管理器的WPS支持设置
        /// 2. 保存设置到文件
        /// </remarks>
        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsSupportWPS = ToggleSwitchSupportWPS.IsOn;

            if (Settings.PowerPointSettings.IsSupportWPS)
            {
                if (!Settings.PowerPointSettings.PowerPointSupport)
                {
                    Settings.PowerPointSettings.PowerPointSupport = true;
                    ToggleSwitchSupportPowerPoint.IsOn = true;

                    // 启动PPT监控
                    if (_pptManager == null)
                    {
                        InitializePPTManagers();
                    }
                    StartPPTMonitoring();
                }

                if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
                {
                    Settings.PowerPointSettings.EnablePowerPointEnhancement = false;
                    ToggleSwitchPowerPointEnhancement.IsOn = false;
                    StopPowerPointProcessMonitoring();
                }
            }

            // 更新PPT管理器的WPS支持设置与翻页跳过动画设置
            if (_pptManager != null)
            {
                _pptManager.IsSupportWPS = Settings.PowerPointSettings.IsSupportWPS;
                _pptManager.SkipAnimationsWhenNavigating = Settings.PowerPointSettings.SkipAnimationsWhenGoNext;
            }

            SaveSettingsToFile();
        }

        private void ToggleSwitchSkipAnimationsWhenGoNext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.SkipAnimationsWhenGoNext = ToggleSwitchSkipAnimationsWhenGoNext.IsOn;

            if (_pptManager != null)
            {
                _pptManager.SkipAnimationsWhenNavigating = Settings.PowerPointSettings.SkipAnimationsWhenGoNext;
            }

            SaveSettingsToFile();
        }

        /// <summary>
        /// 获取当前是否启用了WPS支持
        /// </summary>
        /// <value>如果启用了WPS支持，则为true；否则为false</value>
        private static bool isWPSSupportOn => Settings.PowerPointSettings.IsSupportWPS;

        /// <summary>
        /// 指示是否正在显示恢复隐藏幻灯片的窗口
        /// </summary>
        public static bool IsShowingRestoreHiddenSlidesWindow;

        /// <summary>
        /// 指示是否正在显示自动播放提示窗口
        /// </summary>
        private static bool IsShowingAutoplaySlidesWindow;

        /// <summary>
        /// 处理“上一页”按钮的点击操作：在满足自动保存条件时保存当前幻灯片截图并尝试切换到上一张幻灯片；在切换失败或发生异常时记录日志并更新连接状态。
        /// </summary>
        /// <param name="sender">事件的来源对象（通常是触发按钮）。</param>
        /// <param name="e">路由事件参数。</param>
        private void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e)
        {
            int strokeCount = inkCanvas?.Strokes?.Count ?? 0;
            bool needScreenshot = strokeCount > Settings.Automation.MinimumAutomationStrokeNumber &&
                Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;

            if (needScreenshot)
            {
                var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    var presentationName = _pptManager?.GetPresentationName() ?? "";
                    CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}");
                }
            }

            Task.Run(() =>
            {
                try
                {
                    return _pptManager?.TryNavigatePrevious() ?? false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT上一页操作异常: {ex}", LogHelper.LogType.Error);
                    return false;
                }
            }).ContinueWith(t =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { _pptUIManager?.UpdateConnectionStatus(false); return; }
                    if (t.Result)
                    {
                        if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) try { this.Activate(); } catch { }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到上一页失败", LogHelper.LogType.Warning);
                        _pptUIManager?.UpdateConnectionStatus(false);
                    }
                }));
            });
        }

        /// <summary>
        /// 处理“下一页”按钮点击：在满足自动保存条件时保存当前幻灯片的截图并尝试切换到下一张幻灯片。
        /// </summary>
        /// <remarks>
        /// 如果切换操作失败或发生异常，会写入日志并将 PPT 连接状态更新为断开。
        /// </remarks>
        private void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e)
        {
            int strokeCount = inkCanvas?.Strokes?.Count ?? 0;
            bool needScreenshot = strokeCount > Settings.Automation.MinimumAutomationStrokeNumber &&
                Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;

            if (needScreenshot)
            {
                var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    var presentationName = _pptManager?.GetPresentationName() ?? "";
                    CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}");
                }
            }

            Task.Run(() =>
            {
                try
                {
                    return _pptManager?.TryNavigateNext() ?? false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT下一页操作异常: {ex}", LogHelper.LogType.Error);
                    return false;
                }
            }).ContinueWith(t =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { _pptUIManager?.UpdateConnectionStatus(false); return; }
                    if (t.Result)
                    {
                        if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) try { this.Activate(); } catch { }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到下一页失败", LogHelper.LogType.Warning);
                        _pptUIManager?.UpdateConnectionStatus(false);
                    }
                }));
            });
        }

        /// <summary>
        /// 处理PPT导航按钮的鼠标按下事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户按下PPT导航按钮时执行以下操作：
        /// 1. 记录按下的按钮对象
        /// 2. 检查是否启用了PPT按钮页码点击功能
        /// 3. 根据按下的按钮设置相应的反馈边框透明度
        /// </remarks>
        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0.15;
            }
        }

        /// <summary>
        /// 处理PPT导航按钮的鼠标离开事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 该方法在用户鼠标离开PPT导航按钮时执行以下操作：
        /// 1. 重置按下的按钮对象为null
        /// 2. 根据离开的按钮设置相应的反馈边框透明度为0（隐藏反馈效果）
        /// </remarks>
        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }
        }

        /// <summary>
        /// 处理PPT导航按钮的鼠标释放事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户释放PPT导航按钮时执行以下操作：
        /// 1. 检查释放的按钮是否与按下的按钮一致
        /// 2. 隐藏按钮的反馈效果
        /// 3. 检查是否启用了PPT按钮页码点击功能
        /// 4. 检查PPT是否已连接且在放映状态
        /// 5. 设置背景透明度和颜色
        /// 6. 切换到光标模式
        /// 7. 尝试显示PPT幻灯片导航
        /// 8. 如果浮动栏未折叠，则调整其位置
        /// 9. 捕获并记录可能的异常
        /// </remarks>
        private async void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }

            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;

            // 使用新的PPT管理器检查连接状态
            if (_pptManager?.IsConnected != true || _pptManager?.IsInSlideShow != true)
            {
                LogHelper.WriteLogToFile("PPT未连接或未在放映状态，无法执行页码点击操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                CursorIcon_Click(null, null);

                // 使用新的PPT管理器显示导航
                if (_pptManager.TryShowSlideNavigation())
                {
                    LogHelper.WriteLogToFile("成功显示PPT幻灯片导航", LogHelper.LogType.Trace);
                    // 若启用了“翻页时跳过PPT动画”，显示导航后把焦点拉回本窗口
                    if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext)
                    {
                        try { this.Activate(); } catch { }
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("显示PPT幻灯片导航失败", LogHelper.LogType.Warning);
                }

                // 控制居中
                if (!isFloatingBarFolded)
                {
                    await Task.Delay(100);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT翻页控件操作失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理“开始幻灯片放映”按钮的点击事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 该方法在用户点击“开始幻灯片放映”按钮时执行以下操作：
        /// 1. 在新线程中尝试启动PPT幻灯片放映
        /// 2. 如果启动失败，记录警告日志
        /// 3. 捕获并记录可能的异常
        /// </remarks>
        private void BtnPPTSlideShow_Click(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                try
                {
                    if (_pptManager?.TryStartSlideShow() != true)
                    {
                        LogHelper.WriteLogToFile("启动幻灯片放映失败", LogHelper.LogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"启动幻灯片放映异常: {ex}", LogHelper.LogType.Error);
                }
            }).Start();
        }

        private async void BtnPPTSlideShowEnd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (inkCanvas?.Strokes != null && inkCanvas.Strokes.Count > 0)
                        {
                            var ms = new MemoryStream();
                            inkCanvas.Strokes.Save(ms);
                            ms.Position = 0;
                            if (_memoryStreams.ContainsKey(currentSlide))
                                _memoryStreams[currentSlide]?.Dispose();
                            _memoryStreams[currentSlide] = ms;
                        }
                        timeMachine.ClearStrokeHistory();
                    });
                }

                // 结束放映
                if (_pptManager?.TryEndSlideShow() == true)
                {
                    // 如果成功结束放映，等待OnPPTSlideShowEnd事件处理收纳状态恢复
                }
                else
                {
                    LogHelper.WriteLogToFile("结束幻灯片放映失败", LogHelper.LogType.Warning);

                    // 手动更新UI状态，防止事件未触发
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _pptUIManager?.UpdateSlideShowStatus(false);
                        _pptUIManager?.UpdateSidebarExitButtons(false);
                        LogHelper.WriteLogToFile("手动更新放映结束UI状态", LogHelper.LogType.Trace);
                    });

                    // 手动处理自动收纳，因为OnPPTSlideShowEnd事件可能未触发
                    await HandleManualSlideShowEnd();
                }

                HideSubPanels("cursor");
                SetCurrentToolMode(InkCanvasEditingMode.None);

                await Task.Delay(150);
                if (!isFloatingBarFolded)
                {
                    PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    if (Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                    {
                        await Task.Delay(350);
                        if (!isFloatingBarFolded)
                        {
                            ViewboxFloatingBarMarginAnimation(-60);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束PPT放映操作异常: {ex}", LogHelper.LogType.Error);

                // 确保UI状态正确
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _pptUIManager?.UpdateSlideShowStatus(false);
                    _pptUIManager?.UpdateSidebarExitButtons(false);
                });

                // 异常情况下也手动处理自动收纳
                await HandleManualSlideShowEnd();

                await Task.Delay(150);
                if (!isFloatingBarFolded)
                {
                    PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    if (Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                    {
                        await Task.Delay(350);
                        if (!isFloatingBarFolded)
                        {
                            ViewboxFloatingBarMarginAnimation(-60);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 手动处理PPT放映结束时的自动收纳
        /// </summary>
        private async Task HandleManualSlideShowEnd()
        {
            try
            {
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow && !isFloatingBarFolded)
                {
                    FoldFloatingBar_MouseUp(new object(), null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动处理PPT放映结束自动收纳失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理PPT上一页控制按钮的鼠标按下事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户按下PPT上一页控制按钮时执行以下操作：
        /// 1. 记录按下的按钮对象
        /// 2. 根据按下的按钮设置相应的反馈边框透明度
        /// 3. 如果启用了PPT按钮长按翻页功能，则启动长按检测
        /// </remarks>
        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }

            // 启动长按检测
            if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
            {
                StartLongPressDetection(sender, false);
            }
        }
        /// <summary>
        /// 处理PPT上一页控制按钮的鼠标离开事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 该方法在用户鼠标离开PPT上一页控制按钮时执行以下操作：
        /// 1. 重置按下的按钮对象为null
        /// 2. 根据离开的按钮设置相应的反馈边框透明度为0（隐藏反馈效果）
        /// 3. 停止长按检测
        /// </remarks>
        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();
        }
        /// <summary>
        /// 处理PPT上一页控制按钮的鼠标释放事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户释放PPT上一页控制按钮时执行以下操作：
        /// 1. 检查释放的按钮是否与按下的按钮一致
        /// 2. 根据释放的按钮设置相应的反馈边框透明度为0（隐藏反馈效果）
        /// 3. 停止长按检测
        /// 4. 调用上一页按钮的点击事件处理方法，实现切换到上一页的功能
        /// </remarks>
        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();

            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
        }


        /// <summary>
        /// 处理PPT下一页控制按钮的鼠标按下事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户按下PPT下一页控制按钮时执行以下操作：
        /// 1. 记录按下的按钮对象
        /// 2. 根据按下的按钮设置相应的反馈边框透明度
        /// 3. 如果启用了PPT按钮长按翻页功能，则启动长按检测
        /// </remarks>
        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0.15;
            }

            // 启动长按检测
            if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
            {
                StartLongPressDetection(sender, true);
            }
        }
        /// <summary>
        /// 处理PPT下一页控制按钮的鼠标离开事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标事件参数</param>
        /// <remarks>
        /// 该方法在用户鼠标离开PPT下一页控制按钮时执行以下操作：
        /// 1. 重置按下的按钮对象为null
        /// 2. 根据离开的按钮设置相应的反馈边框透明度为0（隐藏反馈效果）
        /// 3. 停止长按检测
        /// </remarks>
        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();
        }
        /// <summary>
        /// 处理PPT下一页控制按钮的鼠标释放事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户释放PPT下一页控制按钮时执行以下操作：
        /// 1. 检查释放的按钮是否与按下的按钮一致
        /// 2. 根据释放的按钮设置相应的反馈边框透明度为0（隐藏反馈效果）
        /// 3. 停止长按检测
        /// 4. 调用下一页按钮的点击事件处理方法，实现切换到下一页的功能
        /// </remarks>
        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();

            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
        }

        /// <summary>
        /// 处理PPT结束控制按钮的鼠标释放事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户释放PPT结束控制按钮时调用BtnPPTSlideShowEnd_Click方法，实现结束幻灯片放映的功能
        /// </remarks>
        private void ImagePPTControlEnd_MouseUp(object sender, MouseButtonEventArgs e)
        {
            BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }
    }
}
