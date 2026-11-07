using Ink_Canvas.Helpers;
using Ink_Canvas.Services.Ppt;
using Ink_Canvas.WorkflowAutomation;
using InkCanvasPPTAgent.Contracts;
using iNKORE.UI.WPF.Modern;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Win32;
using Application = System.Windows.Application;
using File = System.IO.File;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        #region Win32 API Declarations
        //[DllImport("user32.dll")]
        //private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        //[DllImport("user32.dll")]
        //private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        //[DllImport("user32.dll")]
        //private static extern uint GetDpiForWindow(IntPtr hWnd);

        //[StructLayout(LayoutKind.Sequential)]
        //private struct RECT
        //{
        //    public int Left, Top, Right, Bottom;
        //}

        //[DllImport("user32.dll")]
        //private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        //[DllImport("user32.dll")]
        //private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        //[DllImport("user32.dll")]
        //private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool IsWindowVisible(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //private static extern bool IsIconic(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //private static extern bool IsZoomed(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //private static extern IntPtr GetForegroundWindow();

        //[DllImport("user32.dll")]
        //private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        //[DllImport("user32.dll")]
        //private static extern bool IsWindow(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //private static extern bool GetWindowRect(IntPtr hWnd, out ForegroundWindowInfo.RECT lpRect);

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        //private const int GWL_STYLE = -16;
        //private const int WS_VISIBLE = 0x10000000;
        //private const int WS_MINIMIZE = 0x20000000;
        //private const uint GW_HWNDNEXT = 2;
        //private const uint GW_HWNDPREV = 3;

        //private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        #endregion

        #region PPT Application Variables
        // 5 个原 public static 字段的实际存放点迁至 Services.Ppt.PptInteropState，
        // 此处以同名属性转发，主窗口其余部分的读取点零改动。
        private static readonly PptInteropState _pptInteropState = new PptInteropState();

        private PptIntegrationService _pptIntegrationService;

        /// <summary>PPT 集成服务（经 PptHostHooks 注入宿主回调）。懒加载以避免字段初始化器捕获 this。</summary>
        private PptIntegrationService PptIntegration => _pptIntegrationService ??= new PptIntegrationService(
            _pptInteropState,
            () => _pptManager,
            action => Dispatcher.BeginInvoke(action),
            new PptHostHooks
            {
                StartProcessMonitorTimer = StartPowerPointProcessMonitorTimer,
                StopProcessMonitorTimer = StopPowerPointProcessMonitorTimer,
                StartLoadingAnimationTimer = StartPPTLoadingAnimationTimer,
                StopLoadingAnimationTimer = StopPPTLoadingAnimationTimer,
                StartLongPressTimer = delayMs =>
                {
                    // 重置定时器间隔为初始延迟时间，确保每次长按检测都从正确的延迟开始
                    _longPressTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
                    _longPressTimer?.Start();
                },
                StopLongPressTimer = () => _longPressTimer?.Stop(),
                SetLongPressTimerInterval = intervalMs => _longPressTimer.Interval = TimeSpan.FromMilliseconds(intervalMs),
                TurnToPreviousSlide = () => BtnPPTSlidesUp_Click(null, null),
                TurnToNextSlide = () => BtnPPTSlidesDown_Click(null, null),
                DisposePptLinkManager = isShutdown =>
                {
                    if (_pptManager != null)
                    {
                        _pptManager.StopMonitoring(isShutdown: isShutdown);
                        _pptManager.Dispose();
                        _pptManager = null;
                    }
                },
                DisposePptInkManager = () =>
                {
                    _singlePPTInkManager?.Dispose();
                    _singlePPTInkManager = null;
                },
                DisposeLongPressTimer = () =>
                {
                    _longPressTimer?.Stop();
                    _longPressTimer = null;
                },
                ClearPptUiManager = () => _pptUIManager = null,
                ClearProcessMonitorTimer = () => _powerPointProcessMonitorTimer = null,
                StopPptOnlyVisibilityProbeTimer = StopPPTOnlyVisibilityProbeTimer,
                StopAllTimersForShutdown = () =>
                {
                    _longPressTimer?.Stop();
                    _powerPointProcessMonitorTimer?.Stop();
                    StopPPTOnlyVisibilityProbeTimer();
                },
                UiInvokeAsync = action => Application.Current.Dispatcher.InvokeAsync(action).Task,
                UiInvokeSync = action => Application.Current.Dispatcher.Invoke(action),
                UiInvokeSend = action => Dispatcher.Invoke(action, DispatcherPriority.Send),
                CanRunShutdownCleanupInline = () => Dispatcher == null || Dispatcher.CheckAccess(),
                IsUiSchedulerShutdown = () => Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished,
            },
            PPTLoadingRing,
            PPTLoadingArcRotate,
            PPTEnhancedPreviewLoadingOverlay,
            // 放映编排宿主回调。lambda 本体即原方法内联代码，判空/线程语义保留在主窗口侧。
            new PptSlideShowHooks
            {
                GetPptInkManager = () => _singlePPTInkManager,
                GetPptUiManager = () => _pptUIManager,
                GetPresentationSourceService = () => _presentationSourceService,
                IsOnUiThread = () => Dispatcher.CheckAccess(),
                UiInvokeAsyncFunc = f => Application.Current.Dispatcher.InvokeAsync(f).Task,
                UiInvokeBackgroundAsync = action => Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task,
                RunBackgroundAsync = action => Task.Run(action),
                RunStrokeLoadAsync = load => Task.Run(load),
                IsFloatingBarFolded = () => isFloatingBarFolded,
                FoldFloatingBar = () => FoldFloatingBar_MouseUp(new object(), null),
                UnFoldFloatingBarAsync = () => UnFoldFloatingBar(new object()),
                ScheduleFloatingBarMarginAnimationAfterDelay = () =>
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(100);
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ViewboxFloatingBarMarginAnimation(60);
                        });
                    }).Start();
                },
                ViewboxFloatingBarMarginAnimation = margin => ViewboxFloatingBarMarginAnimation(margin),
                PureViewboxFloatingBarMarginAnimationInDesktopMode = PureViewboxFloatingBarMarginAnimationInDesktopMode,
                GetInkStrokeCount = () => inkCanvas?.Strokes?.Count ?? 0,
                HasInkStrokesCollection = () => inkCanvas?.Strokes != null,
                CaptureNonEmptyInkStrokes = () =>
                {
                    if (inkCanvas?.Strokes != null && inkCanvas.Strokes.Count > 0)
                    {
                        var ms = new MemoryStream();
                        inkCanvas.Strokes.Save(ms);
                        ms.Position = 0;
                        return ms;
                    }
                    return null;
                },
                CaptureInkStrokesSnapshot = () =>
                {
                    MemoryStream ms;
                    if (inkCanvas.Strokes.Count > 0)
                    {
                        ms = new MemoryStream();
                        inkCanvas.Strokes.Save(ms);
                        ms.Position = 0;
                    }
                    else
                    {
                        ms = new MemoryStream();
                    }
                    return ms;
                },
                AddInkStrokes = strokes => inkCanvas.Strokes.Add(strokes),
                AddInkStrokesFromBytes = bytes => inkCanvas.Strokes.Add(new StrokeCollection(new MemoryStream(bytes))),
                ClearStrokes = isErasedByCode => ClearStrokes(isErasedByCode),
                ClearStrokeHistory = () => timeMachine.ClearStrokeHistory(),
                HasTimeMachineHistories = () => TimeMachineHistories != null && TimeMachineHistories.Length > 0,
                ClearFirstTimeMachineHistory = () => TimeMachineHistories[0] = null,
                GetInkEditingMode = () => inkCanvas.EditingMode,
                SetInkReplayStopped = () => isStopInkReplay = true,
                GetCurrentMode = () => currentMode,
                SetCurrentMode = mode => currentMode = mode,
                CloseWhiteboardImmediately = CloseWhiteboardImmediately,
                SyncPdfPageSidebarWithCanvas = SyncPdfPageSidebarWithCanvas,
                BtnHideInkCanvasClick = () => BtnHideInkCanvas_Click(null, null),
                ImageBlackboardMouseUp = () => ImageBlackboard_MouseUp(null, null),
                PenIconClick = () => PenIcon_Click(null, null),
                BtnColorRedClick = () => BtnColorRed_Click(null, null),
                CursorIconClick = () => CursorIcon_Click(null, null),
                SetFloatingBarContentVisibility = visible => SetFloatingBarContentVisibility(visible),
                HideGesturePanels = () =>
                {
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                },
                UpdateToolbarComponentVisibility = UpdateToolbarComponentVisibility,
                ApplyFloatingBarMenuOpacity = ApplyFloatingBarMenuOpacity,
                UpdateCurrentToolMode = mode => UpdateCurrentToolMode(mode),
                SetFloatingBarHighlightPosition = mode => SetFloatingBarHighlightPosition(mode),
                SetCurrentToolMode = mode => SetCurrentToolMode(mode),
                TryActivateWindow = message => ExceptionHandler.TryExecute(() => this.Activate(), message),
                CheckWindowVisibility = CheckMainWindowVisibility,
                SetTransparentNotHitThrough = SetTransparentNotHitThrough,
                ConvertStringToColor = colorStr => StringToColor(colorStr),
                CollapseAllPPTNavBarPreviews = CollapseAllPPTNavBarPreviews,
                ResolvePreviewTargetBar = bar => ResolvePreviewTargetBar(bar),
                BuildSmartModeRects = BuildSmartModeRects,
                ClearMediaPassthroughState = () =>
                {
                    _mediaPassthroughRects?.Clear();
                    StopMediaPassthroughTimer();
                    _isMediaRegionMouseMode = false;
                },
                NotifyPluginSlideShowStateChanged = isActive => NotifyPluginSlideShowStateChanged(isActive),
                NotifyPluginSlideChanged = slideNumber => NotifyPluginSlideChanged(slideNumber),
                NotifyMiniWhiteboardSlideChanged = slideIndex => _miniWhiteboardWindow?.OnPPTSlideChangedExternal(slideIndex),
                CaptureAndEnqueueScreenshotSave = (isHideNotification, fileName) => CaptureAndEnqueueScreenshotSave(isHideNotification, fileName),
                StopExitPPTModeAfterDisconnectTimer = () =>
                {
                    _exitPPTModeAfterDisconnectTimer?.Stop();
                    _exitPPTModeAfterDisconnectTimer = null;
                },
                HandlePresentationOpenNavigation = (pres, agentState) => HandlePresentationOpenNavigation(pres, agentState),
                CheckAndNotifyHiddenSlides = (pres, agentState) => CheckAndNotifyHiddenSlides(pres, agentState),
                CheckAndNotifyAutoPlaySettings = (pres, agentState) => CheckAndNotifyAutoPlaySettings(pres, agentState),
            },
            _pptSlideShowState,
            _pptSmartModeState,
            GridTransparencyFakeBackground);

        /// <summary>
        /// PowerPoint应用程序实例，用于与PowerPoint进行交互。
        /// </summary>
        public static Microsoft.Office.Interop.PowerPoint.Application pptApplication
        {
            get => _pptInteropState.PptApplication;
            set => _pptInteropState.PptApplication = value;
        }

        /// <summary>
        /// 当前活动的PowerPoint演示文稿。
        /// </summary>
        public static Presentation presentation
        {
            get => _pptInteropState.Presentation;
            set => _pptInteropState.Presentation = value;
        }

        /// <summary>
        /// 当前演示文稿的幻灯片集合。
        /// </summary>
        public static Slides slides
        {
            get => _pptInteropState.Slides;
            set => _pptInteropState.Slides = value;
        }

        /// <summary>
        /// 当前活动的幻灯片。
        /// </summary>
        public static Slide slide
        {
            get => _pptInteropState.Slide;
            set => _pptInteropState.Slide = value;
        }

        /// <summary>
        /// 当前演示文稿的幻灯片总数。
        /// </summary>
        public static int slidescount
        {
            get => _pptInteropState.SlidesCount;
            set => _pptInteropState.SlidesCount = value;
        }
        #endregion

        #region PPT State Management
        // 放映会话状态字段（isEnteredSlideShowEndEvent/_lastPlaybackPage/_shouldNavigateToLastPage/
        // _currentSlideShowPosition/_previousSlideID/_memoryStreams）的实际存放点迁至 Services.Ppt.PptSlideShowState，
        // 智慧模式五字段迁至 PptSmartModeState；此处仅保留仍有主窗口侧读取点的同名转发属性
        // （CurrentPPTSlideIndex、ShowPreviousPageNotification 等，供浮动栏图标与窗口边框命中测试读取），
        // 无剩余读取点的字段不再转发（同 PptInteropState 先例）。
        private readonly PptSlideShowState _pptSlideShowState = new PptSlideShowState();
        private readonly PptSmartModeState _pptSmartModeState = new PptSmartModeState();

        /// <summary>
        /// 演示文稿是否有黑边的指示标志。
        /// </summary>
        private bool isPresentationHaveBlackSpace
        {
            get => _pptSlideShowState.IsPresentationHaveBlackSpace;
            set => _pptSlideShowState.IsPresentationHaveBlackSpace = value;
        }

        // 长按翻页相关字段
        /// <summary>
        /// 用于处理长按翻页功能的定时器。
        /// </summary>
        private DispatcherTimer _longPressTimer;

        // _isLongPressNext 与 LongPressDelay/LongPressInterval 常量已随迁至 PptIntegrationService；
        // 定时器本体（DispatcherTimer，UI 线程亲和）保留在本类，创建/接线见 InitializeLongPressTimer。

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
        private int _lastPlaybackPage
        {
            get => _pptSlideShowState.LastPlaybackPage;
            set => _pptSlideShowState.LastPlaybackPage = value;
        }

        // 当前播放页码跟踪
        /// <summary>
        /// 当前幻灯片放映的位置（页码）。
        /// </summary>
        private int _currentSlideShowPosition
        {
            get => _pptSlideShowState.CurrentSlideShowPosition;
            set => _pptSlideShowState.CurrentSlideShowPosition = value;
        }

        /// <summary>
        /// 当前幻灯片放映位置的公开访问器（0-based）。
        /// 用于小白板等组件获取当前PPT页码。
        /// </summary>
        internal int CurrentPPTSlideIndex => _currentSlideShowPosition > 0 ? _currentSlideShowPosition - 1 : 0;

        /// <summary>
        /// 用于在PowerPoint连接断开后延迟退出PPT模式的定时器。
        /// </summary>
        private DispatcherTimer _exitPPTModeAfterDisconnectTimer;

        /// <summary>
        /// 断开连接后退出PPT模式的延迟时间（毫秒），即连接断开后多长时间才退出PPT模式。
        /// </summary>
        private const int ExitPPTModeAfterDisconnectDelayMs = 1200;

        /// <summary>
        /// 仅PPT模式下周期性探测放映界面（COM 失效时依赖 Win32），间隔不宜过小以免多余开销。
        /// </summary>
        private DispatcherTimer _pptOnlyVisibilityProbeTimer;

        private const int PPTOnlyVisibilityProbeIntervalMs = 800;

        // PowerPointSlideShowWindowClassName 常量已随两个 Win32 探测方法迁入 PptIntegrationService。

        // 智慧模式：视频控件穿透区域
        /// <summary>当前幻灯片的视频控件原始区域列表（磅值），用于鼠标进入/离开判断。</summary>
        private List<SmartRegion> _smartModeRegions
        {
            get => _pptSmartModeState.Regions;
            set => _pptSmartModeState.Regions = value;
        }
        /// <summary>缓存的视频区域对应的幻灯片页码，避免重复查询。</summary>
        private int _smartModeSlideIndex
        {
            get => _pptSmartModeState.SlideIndex;
            set => _pptSmartModeState.SlideIndex = value;
        }
        /// <summary>VSTO/COM 返回的幻灯片尺寸（磅）和放映窗口句柄，用于主应用端坐标转换。</summary>
        private float _smartModeSlideWidth
        {
            get => _pptSmartModeState.SlideWidth;
            set => _pptSmartModeState.SlideWidth = value;
        }
        private float _smartModeSlideHeight
        {
            get => _pptSmartModeState.SlideHeight;
            set => _pptSmartModeState.SlideHeight = value;
        }
        private IntPtr _smartModeSlideShowHwnd
        {
            get => _pptSmartModeState.SlideShowHwnd;
            set => _pptSmartModeState.SlideShowHwnd = value;
        }

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
        public PPTUIManager PPTUIManager => _pptUIManager;
        #endregion

        #region PPT Manager Initialization
        /// <summary>
        /// 初始化并配置用于 PowerPoint 集成的管理器与相关状态。
        /// </summary>
        /// <remarks>
        /// 清理并释放现有的 PPT 管理器与 COM/Interop 状态，创建并配置新的 PPT 管理器（ROT 或 COM 实现，取决于设置）、单一的 PPT 墨迹管理器及其自动保存行为，以及 PPT UI 管理器与其显示/按钮位置选项。方法内部会订阅必要的 PPT 事件并记录初始化过程中的错误或警告。同时初始化长按页翻页定时器以支持长按翻页功能。
        /// </remarks>
        public void InitializePPTManagers()
        {
            try
            {
                LogHelper.WriteLogToFile(
                    $"[PPT] 开始初始化管理器: linkMode={Settings.PowerPointSettings.PPTLinkMode}, supportWps={Settings.PowerPointSettings.IsSupportWPS}",
                    LogHelper.LogType.Info);
                // 初始化长按定时器
                InitializeLongPressTimer();
                WirePPTNavBars();

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

                // 根据设置选择 COM / ROT / Agent 架构
                switch (Settings.PowerPointSettings.PPTLinkMode)
                {
                    case PPTLinkMode.Rot:
                        _pptManager = new ROTPPTManager();
                        break;
                    case PPTLinkMode.Agent:
                        VstoRegistrationHelper.EnsureRegistered();
                        _pptManager = new PPTAgentLinkManager();
                        break;
                    default:
                        _pptManager = new ComPPTLinkManager();
                        break;
                }

                _pptManager.IsSupportWPS = Settings.PowerPointSettings.IsSupportWPS;
                _pptManager.SkipAnimationsWhenNavigating = Settings.PowerPointSettings.SkipAnimationsWhenGoNext;

                // 注册事件
                _pptManager.PPTConnectionChanged += OnPPTConnectionChanged;
                _pptManager.SlideShowBegin += OnPPTSlideShowBegin;
                _pptManager.SlideShowNextSlide += OnPPTSlideShowNextSlide;
                _pptManager.SlideShowEnd += OnPPTSlideShowEnd;
                _pptManager.PresentationOpen += OnPPTPresentationOpen;
                _pptManager.PresentationClose += OnPPTPresentationClose;
                _pptManager.SlideShowStateChanged += OnPPTSlideShowStateChanged;

                _singlePPTInkManager = new PPTInkManager();
                _singlePPTInkManager.IsAutoSaveEnabled = Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                _singlePPTInkManager.AutoSaveLocation = Settings.Automation.AutoSavedStrokesLocation;

                // 初始化UI管理器
                _pptUIManager = new PPTUIManager(this);
                _pptUIManager.SyncSettings();

                LogHelper.WriteLogToFile(
                    $"[PPT] 管理器初始化完成: manager={_pptManager?.GetType().Name}, autoSaveInk={_singlePPTInkManager?.IsAutoSaveEnabled}",
                    LogHelper.LogType.Info);
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
        public void StartPPTMonitoring()
        {
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                _pptManager?.StartMonitoring();
                LogHelper.WriteLogToFile("[PPT] 主窗口已启动 PPT 监控", LogHelper.LogType.Info);
            }
        }

        /// <summary>
        /// 停止 PowerPoint 相关的监控：停止并清除用于延迟退出 PPT 模式的定时器，并停止 PPT 管理器的监控，同时记录事件日志。
        /// </summary>
        public void StopPPTMonitoring()
        {
            try
            {
                _exitPPTModeAfterDisconnectTimer?.Stop();
                _exitPPTModeAfterDisconnectTimer = null;
                ResetPPTEnhancedPreviewCache();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            _pptManager?.StopMonitoring();
            LogHelper.WriteLogToFile("[PPT] 主窗口已停止 PPT 监控", LogHelper.LogType.Info);
        }

        #region PowerPoint Application Management
        /// <summary>
        /// 启动PowerPoint应用程序守护
        /// </summary>
        /// <remarks>
        /// 启动对本地 PowerPoint 应用实例的守护监控并在需要时创建应用程序实例。
        /// 仅在 PowerPoint 增强功能已启用且未使用 ROT 链接时生效；方法将创建 PowerPoint 应用（若不存在）并启动用于定期检查应用状态的定时器。
        /// </remarks>
        public void StartPowerPointProcessMonitoring()
            => PptIntegration.StartPowerPointProcessMonitoring();

        // 守护定时器由 MainWindow 持有（DispatcherTimer 必须留在 UI 线程侧），
        // 以下两个方法经构造注入供 PptIntegrationService 驱动启停，Tick 接线到转发壳。
        private void StartPowerPointProcessMonitorTimer()
        {
            if (_powerPointProcessMonitorTimer == null)
            {
                _powerPointProcessMonitorTimer = new DispatcherTimer();
                _powerPointProcessMonitorTimer.Interval = TimeSpan.FromMilliseconds(ProcessMonitorInterval);
                _powerPointProcessMonitorTimer.Tick += OnPowerPointApplicationMonitorTick;
            }
            _powerPointProcessMonitorTimer.Start();
        }

        private void StopPowerPointProcessMonitorTimer()
        {
            _powerPointProcessMonitorTimer?.Stop();
        }

        /// <summary>
        /// 停止PowerPoint应用程序守护
        /// </summary>
        public void StopPowerPointProcessMonitoring(bool isShutdown = false)
            => PptIntegration.StopPowerPointProcessMonitoring(isShutdown);

        /// <summary>
        /// 关闭PowerPoint应用程序
        /// </summary>
        /// <remarks>
        /// 关闭当前的 PowerPoint 应用程序及其所有打开的演示文稿，释放相关 COM 资源并清理静态互操作状态。</summary>
        /// 会尝试关闭所有打开的演示文稿、退出 PowerPoint 进程、释放 COM 对象引用，并将内部 PowerPoint 互操作状态重置为初始值；操作结果会被记录到日志，发生异常时会记录错误并仍然尝试清理互操作状态。
        /// </remarks>
        private void ClosePowerPointApplication(bool isShutdown = false)
            => PptIntegration.ClosePowerPointApplication(isShutdown);

        /// <summary>
        /// 释放并清理与 PowerPoint COM 互操作相关的引用（演示文稿、Slides、当前幻灯片），并将幻灯片计数重置为 0。
        /// </summary>
        /// <remarks>
        /// 在释放过程中若发生异常会被捕获并以警告级别记录日志，不会抛出异常到调用者。
        /// </remarks>
        private void ClearStaticInteropState()
            => PptIntegration.ClearStaticInteropState();

        /// <summary>
        /// PowerPoint应用程序监控定时器事件
        /// </summary>
        /// <remarks>
        /// 周期性监控嵌入的 PowerPoint 应用实例的可用性，并在检测到失效时尝试重建实例；当增强功能被禁用时停止监控，并在使用 ROT 链接时不进行检查。
        /// </remarks>
        private void OnPowerPointApplicationMonitorTick(object sender, EventArgs e)
            => PptIntegration.OnPowerPointApplicationMonitorTick(sender, e);
        #endregion

        /// <summary>
        /// 释放并停止所有与 PowerPoint 集成相关的管理器与资源，恢复和清理应用的 PPT 相关运行状态。
        /// </summary>
        /// <remarks>
        /// 操作包括停止并释放 PPT 管理器、墨迹管理器和长按计时器，停止 PowerPoint 进程监控，关闭 PowerPoint 应用并清除静态 COM/互操作状态；所有异常会被捕获并记录为错误日志。
        /// 本体迁入 PptIntegrationService，此处为转发壳。
        /// </remarks>
        private void DisposePPTManagers(bool isShutdown = false)
            => PptIntegration.DisposePPTManagers(isShutdown);

        internal void UnloadPPTModuleForShutdown()
            => PptIntegration.UnloadPPTModuleForShutdown();

        /// <summary>
        /// 初始化长按定时器
        /// </summary>
        /// <remarks>定时器为 DispatcherTimer（UI 线程亲和），创建与 Tick 接线保留在本类；Tick 经壳转发至 PptIntegrationService。</remarks>
        private void InitializeLongPressTimer()
        {
            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(PptIntegrationService.LongPressDelay);
            _longPressTimer.Tick += OnLongPressTimerTick;
        }

        /// <summary>
        /// 启动长按检测
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="isNext">是否为下一页按钮</param>
        private void StartLongPressDetection(object sender, bool isNext)
            => PptIntegration.StartLongPressDetection(sender, isNext);

        /// <summary>
        /// 停止长按检测
        /// </summary>
        private void StopLongPressDetection()
            => PptIntegration.StopLongPressDetection();

        /// <summary>
        /// 长按定时器事件处理
        /// </summary>
        private void OnLongPressTimerTick(object sender, EventArgs e)
            => PptIntegration.OnLongPressTimerTick(sender, e);
        #endregion

        #region 仅PPT模式可见性（COM + Win32 兜底）

        /// <summary>
        /// 在启用「仅PPT模式」时启动轻量探测，COM 事件延迟或失效时仍可根据全屏放映窗口显示主窗口。
        /// </summary>
        internal void EnsurePPTOnlyVisibilityProbeTimer()
        {
            try
            {
                if (!Settings.ModeSettings.IsPPTOnlyMode)
                {
                    StopPPTOnlyVisibilityProbeTimer();
                    return;
                }

                if (_pptOnlyVisibilityProbeTimer == null)
                {
                    _pptOnlyVisibilityProbeTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(PPTOnlyVisibilityProbeIntervalMs)
                    };
                    _pptOnlyVisibilityProbeTimer.Tick += (_, __) => CheckMainWindowVisibility();
                }

                if (!_pptOnlyVisibilityProbeTimer.IsEnabled)
                    _pptOnlyVisibilityProbeTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"仅PPT可见性探测计时器启动失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        internal void StopPPTOnlyVisibilityProbeTimer()
        {
            try
            {
                _pptOnlyVisibilityProbeTimer?.Stop();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }
        }

        /// <summary>
        /// 检测是否存在 PowerPoint 全屏放映顶层窗口（类名 screenClass，进程 powerpnt），用于 COM 不可用时的兜底。
        /// </summary>
        internal bool IsPowerPointSlideshowSurfacePresentWin32()
            => PptIntegration.IsPowerPointSlideshowSurfacePresentWin32();

        #endregion

        #region New PPT Event Handlers
        /// <summary>
        /// 处理 PowerPoint 连接状态的变更：更新界面连接/放映状态，并在断开时启动一个短延迟以安全退出 PPT 模式。
        /// </summary>
        /// <param name="isConnected">指示当前是否已与 PowerPoint 建立连接；`true` 表示已连接，`false` 表示已断开。</param>
        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.PPTConnectionChanged 事件订阅接线点）。</remarks>
        private void OnPPTConnectionChanged(bool isConnected)
            => PptIntegration.OnPPTConnectionChanged(isConnected);

        /// <summary>
        /// 处理 PowerPoint 演示文稿打开事件：清理画布墨迹、初始化墨迹管理器、处理导航逻辑、检查隐藏幻灯片和自动播放设置，并更新连接状态。
        /// </summary>
        /// <param name="payload">事件负载：已打开的 PowerPoint 演示文稿（Presentation）实例，或 Agent 通路的 PPTState 快照。</param>
        /// <remarks>
        /// 操作包括：清理画布墨迹和备份历史记录，初始化墨迹管理器，处理跳转到首页或上次播放页的逻辑，检查隐藏幻灯片和自动播放设置，更新UI连接状态，并记录事件日志。
        /// 所有操作在UI线程异步执行，异常会被捕获并记录为错误日志。
        /// 本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.PresentationOpen 事件订阅接线点）。
        /// </remarks>
        private void OnPPTPresentationOpen(object payload)
            => PptIntegration.OnPPTPresentationOpen(payload);

        private string GetPresentationStrokeFolderPath(Presentation presentation, string presentationName, int totalSlides, string presentationFullName = null)
            => PptIntegrationService.GetPresentationStrokeFolderPath(presentation, presentationName, totalSlides, presentationFullName);

        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.PresentationClose 事件订阅接线点）。</remarks>
        private void OnPPTPresentationClose(object payload)
            => PptIntegration.OnPPTPresentationClose(payload);

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映状态变化事件：更新UI管理器的放映状态并检查主窗口可见性。
        /// </summary>
        /// <param name="isInSlideShow">指示当前是否处于幻灯片放映状态；`true` 表示正在放映，`false` 表示已退出放映。</param>
        /// <remarks>
        /// 操作包括：在UI线程异步通知UI管理器放映状态变化，检查并更新主窗口的可见性（用于仅PPT模式）。
        /// 异常会被捕获并记录为错误日志，确保方法执行不会中断。
        /// 本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.SlideShowStateChanged 事件订阅接线点）。
        /// </remarks>
        private void OnPPTSlideShowStateChanged(bool isInSlideShow)
            => PptIntegration.OnPPTSlideShowStateChanged(isInSlideShow);

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映开始事件：根据设置折叠或展开浮动栏，初始化放映状态，更新UI，加载当前页墨迹，并设置相关参数。
        /// </summary>
        /// <param name="payload">事件负载：PowerPoint 幻灯片放映窗口（SlideShowWindow）实例，或 Agent 通路的 PPTState 快照。</param>
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
        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.SlideShowBegin 事件订阅接线点）。</remarks>
        private void OnPPTSlideShowBegin(object payload)
            => PptIntegration.OnPPTSlideShowBegin(payload);

        /// <summary>
        /// 处理幻灯片放映中的切换：在幻灯片变更时保存当前页墨迹、加载目标页墨迹并更新界面状态。
        /// </summary>
        /// <param name="payload">事件负载：当前的幻灯片放映窗口，或 Agent 通路的 PPTState 快照。</param>
        /// <remarks>
        /// - 如果收到与当前记录相同的页码或已有切换正在处理，则忽略该事件。 
        /// - 在切换过程中会保存前一页的墨迹（如存在）、清空画布与历史、加载新页的墨迹、锁定新页墨迹并刷新当前页显示序号，同时更新内部的当前播放位置状态。
        /// </remarks>
        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.SlideShowNextSlide 事件订阅接线点）。</remarks>
        private void OnPPTSlideShowNextSlide(object payload)
            => PptIntegration.OnPPTSlideShowNextSlide(payload);

        #region 智慧模式：视频控件区域刷新与坐标转换

        /// <summary>
        /// 枚举顶层 screenClass 窗口，返回当前可见且非最小化的放映窗口句柄。
        /// </summary>
        /// <remarks>
        /// FindWindow("screenClass", null) 会返回 Z 序最前的窗口，可能命中陈旧或已结束的放映；
        /// 此处枚举所有顶层窗口并校验可见性，挑选最合适的放映窗口。
        /// </remarks>
        private IntPtr FindActiveScreenClassWindow()
            => PptIntegration.FindActiveScreenClassWindow();

        #endregion

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映结束时的清理与界面恢复，包括保存当前幻灯片墨迹、重置墨迹管理器状态、恢复主题与工具栏显示，并根据配置折叠或展示浮动工具栏等 UI 调整。
        /// </summary>
        /// <param name="payload">事件负载：触发结束事件的 PowerPoint 演示文稿（Presentation）实例，或 Agent 通路的 PPTState 快照。</param>
        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳（IPPTLinkManager.SlideShowEnd 事件订阅接线点）。</remarks>
        private void OnPPTSlideShowEnd(object payload)
            => PptIntegration.OnPPTSlideShowEnd(payload);
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
        private void HandlePresentationOpenNavigation(Presentation pres, PPTState agentState = null)
        {
            try
            {
                if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                {
                    _pptManager?.TryNavigateToSlide(1);
                    if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) ExceptionHandler.TryExecute(() => this.Activate(), "激活主窗口失败（PPT 重入首页时）");
                }
                else if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                {
                    ShowPreviousPageNotification(pres, agentState);
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
        private async Task<bool> ShowInlineYesNoDialog(string title, string content)
        {
            var result = await MessageBoxHelper.ShowAsync(this, content, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        private void HideInlineDialog()
        {
            if (InlineDialogRoot != null)
            {
                InlineDialogRoot.Visibility = Visibility.Collapsed;
            }
        }

        private void InlineDialogPrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            HideInlineDialog();
        }

        private void InlineDialogSecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            HideInlineDialog();
        }

        private async void ShowPreviousPageNotification(Presentation pres, PPTState agentState = null)
        {
            try
            {
                var presentationName = agentState?.PresentationName ?? pres?.Name ?? _pptManager?.GetPresentationName();
                var presentationFullName = agentState?.PresentationFullName;
                var totalSlides = agentState?.TotalSlides ?? _pptManager?.SlidesCount ?? 0;
                if (pres != null && totalSlides <= 0)
                    totalSlides = pres.Slides.Count;
                if (string.IsNullOrEmpty(presentationName) || totalSlides <= 0) return;

                var folderPath = GetPresentationStrokeFolderPath(pres, presentationName, totalSlides, presentationFullName);
                var positionFile = Path.Combine(folderPath, "Position");

                if (!File.Exists(positionFile)) return;

                if (int.TryParse(File.ReadAllText(positionFile), out var page) && page > 0)
                {
                    _lastPlaybackPage = page;
                    var result = await ShowInlineYesNoDialog("Ink Canvas For Class CE", string.Format(Properties.PPTStrings.PPT_RememberLastPage_Prompt, page));
                    if (result)
                    {
                        try
                        {
                            if (_pptManager?.TryNavigateToSlide(page) == true)
                            {
                                if (Settings.PowerPointSettings.SkipAnimationsWhenGoNext) ExceptionHandler.TryExecute(() => this.Activate(), "激活主窗口失败（PPT 翻页时）");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"跳转到第{page}页失败: {ex}", LogHelper.LogType.Error);
                        }
                    }
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
        private async void CheckAndNotifyHiddenSlides(Presentation pres, PPTState agentState = null)
        {
            try
            {
                bool hasHiddenSlides = agentState?.HasHiddenSlides == true;
                if (!hasHiddenSlides && pres?.Slides != null)
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
                    var result = await ShowInlineYesNoDialog("Ink Canvas For Class CE", Properties.PPTStrings.PPT_HiddenSlides_Detected);
                    if (result)
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
                            else if (_pptManager is PPTAgentLinkManager agentManager)
                            {
                                agentManager.TryUnhideHiddenSlides();
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
                    }
                    else
                    {
                        IsShowingRestoreHiddenSlidesWindow = false;
                    }
                }
            }
            catch (Exception ex)
            {
                IsShowingRestoreHiddenSlidesWindow = false;
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
        private async void CheckAndNotifyAutoPlaySettings(Presentation pres, PPTState agentState = null)
        {
            try
            {
                if (IsInPPTPresentationMode) return;

                bool hasSlideTimings = agentState?.HasAutoPlayTimings == true;
                if (!hasSlideTimings && pres?.Slides != null)
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
                    var result = await ShowInlineYesNoDialog("Ink Canvas For Class CE", Properties.PPTStrings.PPT_AutoPlay_Detected);
                    if (result)
                    {
                        try
                        {
                            if (pres != null)
                            {
                                pres.SlideShowSettings.AdvanceMode = PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                            }
                            else if (_pptManager is PPTAgentLinkManager agentManager)
                            {
                                agentManager.TryDisableAutoPlayTimings();
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
                    }
                    else
                    {
                        IsShowingAutoplaySlidesWindow = false;
                    }
                }
            }
            catch (Exception ex)
            {
                IsShowingAutoplaySlidesWindow = false;
                LogHelper.WriteLogToFile($"检查自动播放设置失败: {ex}", LogHelper.LogType.Error);
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
        /// 本体迁入 PptIntegrationService，此处为转发壳（public API，签名保持）。
        /// </remarks>
        public void ResetPPTStateVariables()
            => PptIntegration.ResetPPTStateVariables();

        #endregion

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

            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            if (toggle != null)
                Settings.PowerPointSettings.EnablePowerPointEnhancement = toggle.IsOn;

            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                Settings.PowerPointSettings.IsSupportWPS = false;

                if (_pptManager != null)
                {
                    _pptManager.IsSupportWPS = false;
                }
            }

            SaveSettingsToFile();

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

            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            if (toggle != null)
                Settings.PowerPointSettings.IsSupportWPS = toggle.IsOn;

            if (Settings.PowerPointSettings.IsSupportWPS)
            {
                if (!Settings.PowerPointSettings.PowerPointSupport)
                {
                    Settings.PowerPointSettings.PowerPointSupport = true;

                    if (_pptManager == null)
                    {
                        InitializePPTManagers();
                    }
                    StartPPTMonitoring();
                }

                if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
                {
                    Settings.PowerPointSettings.EnablePowerPointEnhancement = false;
                    StopPowerPointProcessMonitoring();
                }
            }

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

            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            if (toggle != null)
                Settings.PowerPointSettings.SkipAnimationsWhenGoNext = toggle.IsOn;

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
        /// <summary>
        /// 处理“上一页”按钮的点击操作：在满足自动保存条件时保存当前幻灯片截图并尝试切换到上一张幻灯片；在切换失败或发生异常时记录日志并更新连接状态。
        /// </summary>
        /// <param name="sender">事件的来源对象（通常是触发按钮）。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>本体迁入 PptIntegrationService，此处为转发壳。</remarks>
        private void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e)
            => PptIntegration.BtnPPTSlidesUp_Click(sender, e);

        /// <summary>
        /// 处理“下一页”按钮点击：在满足自动保存条件时保存当前幻灯片的截图并尝试切换到下一张幻灯片。
        /// </summary>
        /// <remarks>
        /// 如果切换操作失败或发生异常，会写入日志并将 PPT 连接状态更新为断开。
        /// 本体迁入 PptIntegrationService，此处为转发壳。
        /// </remarks>
        private void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e)
            => PptIntegration.BtnPPTSlidesDown_Click(sender, e);

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
        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e) { }
        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e) { }

        /// <summary>由 PPTNavBar 控件 PageClick 事件触发的页码点击逻辑。</summary>
        private Task OnPPTNavBarPageClickAsync(Controls.PPTNavBar bar)
            => PptIntegration.OnPPTNavBarPageClickAsync(bar);

        private void OnPPTNavBarSlideSelected(Controls.PPTNavBar bar, int slideNumber)
        {
            try { _pptManager?.TryNavigateToSlide(slideNumber); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PPT增强预览跳转异常: {ex}", LogHelper.LogType.Error); }
            finally { if (bar != null) bar.IsPreviewExpanded = false; }
        }

        /// <summary>
        /// 选择承载增强预览的底部条:
        /// - 来自侧边条的点击重定向到同侧底部条;
        /// - 若同侧底部条不可用,退化到任意可用的底部条;
        /// - 来自底部条的点击保持原行为。
        /// </summary>
        private Controls.PPTNavBar ResolvePreviewTargetBar(Controls.PPTNavBar source)
        {
            if (source == null) return null;
            switch (source.Direction)
            {
                case Controls.PPTNavBar.NavDirection.LeftSide:
                    return PickVisibleBar(LeftBottomPanelForPPTNavigation, RightBottomPanelForPPTNavigation) ?? source;
                case Controls.PPTNavBar.NavDirection.RightSide:
                    return PickVisibleBar(RightBottomPanelForPPTNavigation, LeftBottomPanelForPPTNavigation) ?? source;
                default:
                    return source;
            }
        }

        private static Controls.PPTNavBar PickVisibleBar(params Controls.PPTNavBar[] candidates)
        {
            foreach (var c in candidates)
            {
                if (c != null && c.Visibility == Visibility.Visible) return c;
            }
            return null;
        }

        // _pptLoadingProgress 与 DirectProgress 已随迁至 PptIntegrationService；
        // 加载动画定时器（DispatcherTimer，UI 线程亲和）保留在本类，经构造注入供服务启停（第六条 2/5）。
        private DispatcherTimer _pptLoadingTimer;

        private void StartPPTLoadingAnimationTimer(Action tick)
        {
            _pptLoadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _pptLoadingTimer.Tick += (_, _) => tick();
            _pptLoadingTimer.Start();
        }

        private void StopPPTLoadingAnimationTimer()
        {
            _pptLoadingTimer?.Stop();
            _pptLoadingTimer = null;
        }

        private void CollapseAllPPTNavBarPreviews()
        {
            var bars = new[]
            {
                LeftBottomPanelForPPTNavigation,
                RightBottomPanelForPPTNavigation,
                LeftSidePanelForPPTNavigation,
                RightSidePanelForPPTNavigation,
            };
            foreach (var bar in bars)
            {
                if (bar == null) continue;
                ExceptionHandler.TryExecute(
                    () =>
                    {
                        bar.IsPreviewExpanded = false;
                        bar.PreviewItems = null;
                    },
                    "重置 PPT 导航栏预览状态失败",
                    continueOnError: true);
            }
        }

        /// <summary>
        /// 外部演示源服务（插件把自己的文档接入放映模式）。
        /// 由 App 启动时通过 <see cref="AttachPresentationSourceService"/> 注入。
        /// </summary>
        private Plugins.Services.PresentationSourceService _presentationSourceService;

        /// <summary>供 App 在创建服务实例后注入，使翻页条能路由到外部演示源。</summary>
        internal void AttachPresentationSourceService(Plugins.Services.PresentationSourceService service)
        {
            _presentationSourceService = service;
        }

        /// <summary>当前是否由外部演示源（而非真实 PowerPoint）占用放映模式。</summary>
        internal bool IsExternalPresentationActive => _presentationSourceService?.IsActive == true;

        /// <summary>外部演示源的总页数；未激活时为 0。供 PPTUIManager 判断翻页条可见性。</summary>
        internal int ExternalPresentationPageCount => _presentationSourceService?.PageCount ?? 0;

        /// <summary>在 MainWindow 加载完成后调用,把 4 个 PPTNavBar 的事件接到本类。</summary>
        private bool _pptNavBarsWired;

        private void WirePPTNavBars()
        {
            // InitializePPTManagers 可能被多次调用（切换 COM/ROT、设置变更等）。
            // PPTNavBar 事件若在同一控件上重复订阅，会导致翻页、长按、预览展开等逻辑成倍触发。
            if (_pptNavBarsWired) return;

            var bars = new[]
            {
                LeftBottomPanelForPPTNavigation,
                RightBottomPanelForPPTNavigation,
                LeftSidePanelForPPTNavigation,
                RightSidePanelForPPTNavigation,
            };
            foreach (var bar in bars)
            {
                if (bar == null) continue;
                bar.PreviousClick += (s, e) => BtnPPTSlidesUp_Click(null, null);
                bar.NextClick += (s, e) => BtnPPTSlidesDown_Click(null, null);
                bar.PreviousPressedDown += (s, e) =>
                {
                    if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
                        StartLongPressDetection(s, false);
                };
                bar.NextPressedDown += (s, e) =>
                {
                    if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
                        StartLongPressDetection(s, true);
                };
                bar.PressEnded += (s, e) => StopLongPressDetection();
                var captured = bar;
                bar.PageClick += async (s, e) => await OnPPTNavBarPageClickAsync(captured);
                bar.SlideSelected += (s, slideNumber) => OnPPTNavBarSlideSelected(captured, slideNumber);
                bar.PreviewExpandedChanged += (s, expanded) => OnPPTNavBarPreviewExpandedChanged(captured, expanded);
            }

            _pptNavBarsWired = true;
        }

        private bool _suppressPreviewExpandedSync;

        private void OnPPTNavBarPreviewExpandedChanged(Controls.PPTNavBar bar, bool expanded)
        {
            if (_suppressPreviewExpandedSync) return;
            try
            {
                _suppressPreviewExpandedSync = true;

                if (expanded)
                {
                    // 仅允许同时一侧展开
                    foreach (var other in new[]
                    {
                        LeftBottomPanelForPPTNavigation,
                        RightBottomPanelForPPTNavigation,
                        LeftSidePanelForPPTNavigation,
                        RightSidePanelForPPTNavigation,
                    })
                    {
                        if (other == null || ReferenceEquals(other, bar)) continue;
                        if (other.IsPreviewExpanded) other.IsPreviewExpanded = false;
                    }
                }

                // 底部条展开时,隐藏同侧的中部侧边条避免遮挡;收起后还原可见性
                ApplyBottomBarSideOcclusion();
            }
            finally
            {
                _suppressPreviewExpandedSync = false;
            }
        }

        private void ApplyBottomBarSideOcclusion()
        {
            var leftBottomExpanded = LeftBottomPanelForPPTNavigation != null && LeftBottomPanelForPPTNavigation.IsPreviewExpanded;
            var rightBottomExpanded = RightBottomPanelForPPTNavigation != null && RightBottomPanelForPPTNavigation.IsPreviewExpanded;

            // 同侧的侧边条在底部条展开时隐藏
            if (LeftSidePanelForPPTNavigation != null)
            {
                if (leftBottomExpanded)
                {
                    LeftSidePanelForPPTNavigation.Tag = LeftSidePanelForPPTNavigation.Visibility;
                    LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                }
                else if (LeftSidePanelForPPTNavigation.Tag is Visibility cached)
                {
                    LeftSidePanelForPPTNavigation.Visibility = cached;
                    LeftSidePanelForPPTNavigation.ClearValue(TagProperty);
                }
            }
            if (RightSidePanelForPPTNavigation != null)
            {
                if (rightBottomExpanded)
                {
                    RightSidePanelForPPTNavigation.Tag = RightSidePanelForPPTNavigation.Visibility;
                    RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                }
                else if (RightSidePanelForPPTNavigation.Tag is Visibility cached)
                {
                    RightSidePanelForPPTNavigation.Visibility = cached;
                    RightSidePanelForPPTNavigation.ClearValue(TagProperty);
                }
            }
        }

        private void ResetPPTEnhancedPreviewCache()
            => PptIntegration.ResetPPTEnhancedPreviewCache();

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
            // TryStartSlideShow 在 COM/ROT 模式下会调用 PPT COM 对象，必须在 STA 线程跑，
            // 否则会偶发 0x8001010E (RPC_E_WRONG_THREAD) 导致联动掉线。
            // 默认 new Thread 是 MTA，需显式 SetApartmentState。
            var t = new Thread(() =>
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
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public Task ExitPPTPresentation()
            => PptIntegration.ExitPPTPresentation();

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
            // 旧 XAML 入口已废弃，事件由 PPTNavBar 控件转发；保留方法签名以兼容潜在外部引用。
        }
        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            StopLongPressDetection();
        }
        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StopLongPressDetection();
            BtnPPTSlidesUp_Click(null, null);
        }


        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 旧 XAML 入口已废弃，事件由 PPTNavBar 控件转发；保留方法签名以兼容潜在外部引用。
        }
        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            StopLongPressDetection();
        }
        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StopLongPressDetection();
            BtnPPTSlidesDown_Click(null, null);
        }

        /// <summary>
        /// 处理PPT结束控制按钮的鼠标释放事件
        /// </summary>
        /// <param name="sender">事件的来源对象</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法在用户释放PPT结束控制按钮时调用BtnPPTSlideShowEnd_Click方法，实现结束幻灯片放映的功能
        /// </remarks>
        internal async void ImagePPTControlEnd_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await ExitPPTPresentation();
        }
    }
}
