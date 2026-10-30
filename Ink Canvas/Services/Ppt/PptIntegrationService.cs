using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Plugins.Services;
using Ink_Canvas.Windows.SettingsViews.Helpers;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;

namespace Ink_Canvas.Services.Ppt
{
    /// <summary>
    /// PowerPoint COM 互操作状态持有人（自 MainWindow 寄生提取）。原主窗口 5 个 public static 字段
    /// （pptApplication/presentation/slides/slide/slidescount）的实际存放点；主窗口上以同名属性转发，
    /// 使主窗口其余部分的既有读取点零改动。
    /// </summary>
    internal sealed class PptInteropState
    {
        public Microsoft.Office.Interop.PowerPoint.Application PptApplication;
        public Presentation Presentation;
        public Slides Slides;
        public Slide Slide;
        public int SlidesCount;
    }

    /// <summary>
    /// PPT 放映会话状态持有人（寄生提取）。承接原主窗口 PPT 部分的放映跟踪字段：
    /// 结束事件重入保护、黑边标志、上次播放页码与导航标志、当前放映位置、上一页 ID、
    /// 逐页墨迹内存流（页码 → 墨迹流）。主窗口仅保留仍有读取点的同名转发属性
    /// （CurrentPPTSlideIndex / ShowPreviousPageNotification 等），其余读取点随方法一并迁入。
    /// </summary>
    internal sealed class PptSlideShowState
    {
        public bool IsEnteredSlideShowEndEvent;
        public bool IsPresentationHaveBlackSpace;
        public int LastPlaybackPage;
        public bool ShouldNavigateToLastPage;
        public int CurrentSlideShowPosition;
        public int PreviousSlideId;
        public readonly Dictionary<int, MemoryStream> MemoryStreams = new Dictionary<int, MemoryStream>();
    }

    /// <summary>
    /// 智慧模式区域状态持有人（寄生提取）。承接原主窗口 PPT 部分的 5 个智慧模式字段；
    /// 窗口边框命中测试路径（BuildSmartModeRects）重度读写这组字段，
    /// 主窗口以同名属性转发使其零改动（同 PptInteropState 先例）。
    /// </summary>
    internal sealed class PptSmartModeState
    {
        public List<SmartRegion> Regions;
        public int SlideIndex = -1;
        public float SlideWidth;
        public float SlideHeight;
        public IntPtr SlideShowHwnd;
    }

    /// <summary>
    /// PPT 增强预览项（自主窗口私有嵌套类提升为程序集内顶层类，
    /// 供 DisposePPTEnhancedPreviewItems 及主窗口缓存字段组共用；无 XAML/反射引用）。
    /// </summary>
    internal sealed class PPTEnhancedPreviewItem : IDisposable
    {
        public int SlideNumber { get; set; }
        public MemoryStream ThumbnailStream { get; set; }
        public BitmapImage Thumbnail { get; set; }

        public void Dispose()
        {
            ThumbnailStream?.Dispose();
            ThumbnailStream = null;
            Thumbnail = null;
        }
    }

    /// <summary>
    /// 主窗口宿主回调聚合。承载服务方法所需的宿主侧操作：
    /// UI 线程亲和定时器（加载动画 16ms 旋转、长按翻页、进程守护）的启停、
    /// 释放路径中对主窗口私有字段（_pptManager/_singlePPTInkManager/_pptUIManager 等）的处置、
    /// 以及关机路径的 UI 线程探测与同步调度。
    /// 所有委托均由主窗口以 lambda 提供，判空/线程检查语义保留在主窗口侧，本服务只做编排。
    /// </summary>
    internal sealed class PptHostHooks
    {
        // 进程守护定时器（构造参数并入）
        public Action StartProcessMonitorTimer;
        public Action StopProcessMonitorTimer;

        // 增强预览加载动画定时器（UI 线程亲和类型，由主窗口持有）
        /// <summary>创建并启动加载动画定时器，参数为每次 Tick 要执行的回调。</summary>
        public Action<Action> StartLoadingAnimationTimer;
        /// <summary>停止并释放加载动画定时器。</summary>
        public Action StopLoadingAnimationTimer;

        // 长按翻页（定时器由主窗口持有；翻页动作指向主窗口翻页壳）
        /// <summary>将长按定时器间隔重置为给定毫秒数并启动。</summary>
        public Action<int> StartLongPressTimer;
        public Action StopLongPressTimer;
        /// <summary>设置长按定时器间隔（毫秒），不启停。</summary>
        public Action<int> SetLongPressTimerInterval;
        public Action TurnToPreviousSlide;
        public Action TurnToNextSlide;

        // 释放路径（DisposePPTManagers 对主窗口私有成员的处置）
        public Action<bool> DisposePptLinkManager;
        public Action DisposePptInkManager;
        public Action DisposeLongPressTimer;
        public Action ClearPptUiManager;
        public Action ClearProcessMonitorTimer;
        public Action StopPptOnlyVisibilityProbeTimer;
        /// <summary>关机时停止全部 PPT 相关定时器（长按/进程守护/可见性探测）。</summary>
        public Action StopAllTimersForShutdown;

        // UI 线程调度（替代主窗口线程调度器的直引；判空/检查逻辑留在主窗口 lambda 内）
        /// <summary>BeginInvoke 语义的异步调度，返回可等待任务（替代 InvokeAsync）。</summary>
        public Func<Action, Task> UiInvokeAsync;
        /// <summary>同步调度（替代 Invoke）。</summary>
        public Action<Action> UiInvokeSync;
        /// <summary>Send 优先级同步调度（关机路径专用）。</summary>
        public Action<Action> UiInvokeSend;
        /// <summary>当前是否可直接内联执行 UI 线程清理（调度器为空或已在 UI 线程）。</summary>
        public Func<bool> CanRunShutdownCleanupInline;
        /// <summary>UI 线程调度器是否已开始/完成关闭。</summary>
        public Func<bool> IsUiSchedulerShutdown;
    }

    /// <summary>
    /// 放映事件编排宿主回调聚合。承载放映事件编排方法对其他主窗口 partial
    /// 状态与方法的全部依赖：画布/墨迹操作、浮动栏与工具栏、模式切换、插件通知、截图、
    /// 智慧模式坐标转换、后台线程池调度（服务内禁止出现线程池直调形态，
    /// 主窗口侧 lambda 即原线程池调用点）以及未搬移的通知对话组回调。
    /// 所有委托均由主窗口以 lambda 提供，判空/线程检查语义保留在主窗口侧，本服务只做编排。
    /// </summary>
    internal sealed class PptSlideShowHooks
    {
        // ---- 管理器/服务访问器 ----
        public Func<PPTInkManager> GetPptInkManager;
        public Func<PPTUIManager> GetPptUiManager;
        public Func<PresentationSourceService> GetPresentationSourceService;

        // ---- UI 线程调度补充（PptHostHooks.UiInvokeAsync 之外的形态）----
        /// <summary>当前线程是否为 UI 线程（替代主窗口线程亲和检查）。</summary>
        public Func<bool> IsOnUiThread;
        /// <summary>InvokeAsync(async lambda) 语义：等待调度操作完成，不等待 lambda 内首个 await 之后的部分。</summary>
        public Func<Func<Task>, Task> UiInvokeAsyncFunc;
        /// <summary>Background 优先级的 InvokeAsync。</summary>
        public Func<Action, Task> UiInvokeBackgroundAsync;

        // ---- 后台线程池（主窗口侧即原线程池调用点；纯 IO / 纯计算，不触碰 UI 线程亲和对象）----
        public Func<Action, Task> RunBackgroundAsync;
        public Func<Func<StrokeCollection>, Task<StrokeCollection>> RunStrokeLoadAsync;

        // ---- 浮动栏 ----
        public Func<bool> IsFloatingBarFolded;
        public Action FoldFloatingBar;
        public Func<Task> UnFoldFloatingBarAsync;
        /// <summary>放映开始后的延迟边距动画（原实现为独立线程 Sleep(100) 后调度 ViewboxFloatingBarMarginAnimation(60)）。</summary>
        public Action ScheduleFloatingBarMarginAnimationAfterDelay;
        public Action<int> ViewboxFloatingBarMarginAnimation;
        public Action PureViewboxFloatingBarMarginAnimationInDesktopMode;

        // ---- 画布/墨迹 ----
        public Func<int> GetInkStrokeCount;
        /// <summary>inkCanvas.Strokes 集合存在（原 inkCanvas?.Strokes != null 判空）。</summary>
        public Func<bool> HasInkStrokesCollection;
        /// <summary>把当前画布墨迹序列化到内存流（无墨迹时返回空流），对应原 inkCanvas.Strokes.Save 代码块。</summary>
        public Func<MemoryStream> CaptureInkStrokesSnapshot;
        /// <summary>画布有墨迹时序列化到内存流，无墨迹（或 Strokes 为空）时返回 null（原 ExitPPTPresentation 内联代码块）。</summary>
        public Func<MemoryStream> CaptureNonEmptyInkStrokes;
        public Action<StrokeCollection> AddInkStrokes;
        public Action<byte[]> AddInkStrokesFromBytes;
        public Action<bool> ClearStrokes;
        public Action ClearStrokeHistory;
        /// <summary>TimeMachineHistories 非空且有索引 0（原内联判空守卫）。</summary>
        public Func<bool> HasTimeMachineHistories;
        /// <summary>清空 TimeMachineHistories[0] 备份（仅索引 0，不影响白板页面）。</summary>
        public Action ClearFirstTimeMachineHistory;
        public Func<InkCanvasEditingMode> GetInkEditingMode;
        /// <summary>置位 isStopInkReplay（停止墨迹重放标志）。</summary>
        public Action SetInkReplayStopped;

        // ---- 工具栏/模式/窗口 ----
        public Func<int> GetCurrentMode;
        public Action<int> SetCurrentMode;
        public Action CloseWhiteboardImmediately;
        public Action SyncPdfPageSidebarWithCanvas;
        public Action BtnHideInkCanvasClick;
        public Action ImageBlackboardMouseUp;
        public Action PenIconClick;
        public Action BtnColorRedClick;
        public Action CursorIconClick;
        public Action<bool> SetFloatingBarContentVisibility;
        /// <summary>隐藏两个手势面板（TwoFingerGestureBorder/BoardTwoFingerGestureBorder 为 x:Name 元素，留主窗口操作）。</summary>
        public Action HideGesturePanels;
        public Action UpdateToolbarComponentVisibility;
        public Action ApplyFloatingBarMenuOpacity;
        public Action<string> UpdateCurrentToolMode;
        public Action<string> SetFloatingBarHighlightPosition;
        public Action<InkCanvasEditingMode> SetCurrentToolMode;
        /// <summary>激活主窗口（经 ExceptionHandler.TryExecute 包裹），参数为失败日志措辞。</summary>
        public Action<string> TryActivateWindow;
        /// <summary>主窗口可见性检查（对应主窗口同名方法；服务内不出现窗口类名，故取此名）。</summary>
        public Action CheckWindowVisibility;
        public Action SetTransparentNotHitThrough;
        public Func<string, Color> ConvertStringToColor;

        // ---- 导航条/增强预览 ----
        public Action CollapseAllPPTNavBarPreviews;
        /// <summary>选择承载增强预览的底部条（4 个 PPTNavBar x:Name 留在主窗口）。</summary>
        public Func<PPTNavBar, PPTNavBar> ResolvePreviewTargetBar;

        // ---- 智慧模式/媒体穿透 ----
        public Action BuildSmartModeRects;
        /// <summary>清除媒体穿透区域、停止穿透定时器并复位鼠标模式标志（主窗口边框命中测试的私有成员处置）。</summary>
        public Action ClearMediaPassthroughState;

        // ---- 插件/小白板/截图 ----
        public Action<bool> NotifyPluginSlideShowStateChanged;
        public Action<int> NotifyPluginSlideChanged;
        public Action<int> NotifyMiniWhiteboardSlideChanged;
        public Action<bool, string> CaptureAndEnqueueScreenshotSave;

        // ---- 断开连接退出定时器（UI 线程亲和定时器本体留主窗口）----
        public Action StopExitPPTModeAfterDisconnectTimer;

        // ---- 未搬移的通知对话组回调（留主窗口）----
        public Action<Presentation, PPTState> HandlePresentationOpenNavigation;
        public Action<Presentation, PPTState> CheckAndNotifyHiddenSlides;
        public Action<Presentation, PPTState> CheckAndNotifyAutoPlaySettings;
    }

    /// <summary>
    /// PPT 集成服务（自 MainWindow 寄生提取）。承载原主窗口 PPT 部分中零 x:Name 依赖的方法：
    /// COM 进程守护、COM 互操作生命周期、Win32 放映窗口探测与纯静态工具，
    /// 以及增强预览缓存管线、长按翻页、释放路径；加载动画三控件
    /// （进度环/旋转变换/遮罩）以构造注入强类型引用。
    /// 纪律：不持有主窗口引用；UI 线程调度经注入的 uiInvoke/UiInvokeAsync/UiInvokeSync；
    /// UI 线程亲和定时器由主窗口持有，本服务仅通过 PptHostHooks 回调驱动；
    /// COM 调用保持既有线程亲和，RunOnStaAsync 的显式 STA Thread 原样保留。
    /// 另含 7 个 IPPTLinkManager 事件处理器与放映编排（翻页/退出/智慧模式区域）；
    /// 对其他主窗口 partial 的全部依赖经 PptSlideShowHooks 注入；放映会话状态与智慧模式区域状态
    /// 分别由共享的 PptSlideShowState/PptSmartModeState 持有（主窗口保留同名转发属性）。
    /// </summary>
    internal sealed class PptIntegrationService
    {
        private readonly PptInteropState _interopState;
        private readonly Func<IPPTLinkManager> _getPptManager;
        private readonly Action<Action> _uiInvoke;
        private readonly PptHostHooks _hooks;

        // 注入：放映编排宿主回调、放映会话状态、智慧模式区域状态、透明背景板
        private readonly PptSlideShowHooks _slideHooks;
        private readonly PptSlideShowState _slideShowState;
        private readonly PptSmartModeState _smartModeState;
        private readonly Grid _gridTransparencyFakeBackground;

        // 增强预览缓存管线状态（自主窗口随迁）
        private const int PPTEnhancedPreviewPreloadDelayMs = 100;
        private readonly object _pptEnhancedPreviewCacheLock = new object();
        private List<PPTEnhancedPreviewItem> _pptEnhancedPreviewCache;
        private Task<List<PPTEnhancedPreviewItem>> _pptEnhancedPreviewBuildTask;
        private CancellationTokenSource _pptEnhancedPreviewCacheCts;
        private int _pptEnhancedPreviewCacheGeneration;
        private double _pptLoadingProgress;

        // 长按翻页状态（随迁；定时器本体为主窗口持有的 UI 线程亲和类型）
        // 常量为 internal：主窗口侧 InitializeLongPressTimer（定时器创建/接线点，留守主窗口）引用之。
        internal const int LongPressDelay = 500; // 长按延迟时间（毫秒）
        internal const int LongPressInterval = 50; // 长按翻页间隔（毫秒）
        private bool _isLongPressNext = true; // true为下一页，false为上一页

        // 加载动画控件（构造注入强类型引用，禁止按名查找视觉树）
        private readonly System.Windows.Shapes.Path _pptLoadingRing;
        private readonly RotateTransform _pptLoadingArcRotate;
        private readonly Grid _pptEnhancedPreviewLoadingOverlay;

        /// <param name="interopState">与主窗口共享的 COM 互操作状态持有人。</param>
        /// <param name="getPptManager">获取当前 PPT 链接管理器（主窗口字段访问器）。</param>
        /// <param name="uiInvoke">UI 线程调度（主窗口端为 BeginInvoke 语义）。</param>
        /// <param name="hooks">主窗口宿主回调聚合（定时器启停、释放路径、关机调度等）。</param>
        /// <param name="pptLoadingRing">PPT 增强预览加载进度环（主窗口 XAML: PPTLoadingRing）。</param>
        /// <param name="pptLoadingArcRotate">加载弧旋转变换（主窗口 XAML: PPTLoadingArcRotate）。</param>
        /// <param name="pptEnhancedPreviewLoadingOverlay">加载动画遮罩（主窗口 XAML: PPTEnhancedPreviewLoadingOverlay）。</param>
        /// <param name="slideHooks">放映编排宿主回调聚合。</param>
        /// <param name="slideShowState">与主窗口共享的放映会话状态持有人。</param>
        /// <param name="smartModeState">与主窗口共享的智慧模式区域状态持有人。</param>
        /// <param name="gridTransparencyFakeBackground">透明背景板（主窗口 XAML: GridTransparencyFakeBackground）。</param>
        public PptIntegrationService(
            PptInteropState interopState,
            Func<IPPTLinkManager> getPptManager,
            Action<Action> uiInvoke,
            PptHostHooks hooks,
            System.Windows.Shapes.Path pptLoadingRing,
            RotateTransform pptLoadingArcRotate,
            Grid pptEnhancedPreviewLoadingOverlay,
            PptSlideShowHooks slideHooks,
            PptSlideShowState slideShowState,
            PptSmartModeState smartModeState,
            Grid gridTransparencyFakeBackground)
        {
            _interopState = interopState ?? throw new ArgumentNullException(nameof(interopState));
            _getPptManager = getPptManager ?? throw new ArgumentNullException(nameof(getPptManager));
            _uiInvoke = uiInvoke ?? throw new ArgumentNullException(nameof(uiInvoke));
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            _pptLoadingRing = pptLoadingRing;
            _pptLoadingArcRotate = pptLoadingArcRotate;
            _pptEnhancedPreviewLoadingOverlay = pptEnhancedPreviewLoadingOverlay;
            _slideHooks = slideHooks ?? throw new ArgumentNullException(nameof(slideHooks));
            _slideShowState = slideShowState ?? throw new ArgumentNullException(nameof(slideShowState));
            _smartModeState = smartModeState ?? throw new ArgumentNullException(nameof(smartModeState));
            _gridTransparencyFakeBackground = gridTransparencyFakeBackground;
        }

        // ---- 纯静态工具 ----

        /// <summary>
        /// 判断一个 Shape 是否为视频控件（仅识别视频，排除纯音频与普通 ActiveX 控件）。
        /// </summary>
        /// <remarks>
        /// PpMediaType（.NET PIA）取值：ppMediaTypeMovie = 3（视频）、ppMediaTypeSound = 2（音频）、
        /// ppMediaTypeOther = 1、ppMediaTypeMixed = -2；旧代码误用 14 判等导致嵌入式视频分支永不命中。
        /// </remarks>
        internal static bool IsVideoShape(Microsoft.Office.Interop.PowerPoint.Shape shape)
        {
            try
            {
                // msoWebVideo = 26：在线视频（YouTube 等），本身就是视频控件
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoWebVideo)
                    return true;

                // msoMedia = 16：多媒体形状，用 MediaType 区分视频/音频
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoMedia)
                {
                    try
                    {
                        int mediaType = (int)(object)shape.MediaType;
                        // ppMediaTypeMovie = 3（视频）；raw 15 为旧版 Flash（ppMediaTypeFlash），亦属视频类
                        return mediaType == 3 || mediaType == 15;
                    }
                    catch
                    {
                        // MediaType 读取失败时保守放行（保持原行为）
                        return true;
                    }
                }

                // msoOLEControlObject = 12：ActiveX 控件，仅当确认为媒体播放器时才视为视频
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoOLEControlObject)
                {
                    try
                    {
                        string progId = shape.OLEFormat?.ProgID ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(progId))
                        {
                            progId = progId.ToUpperInvariant();
                            // Windows Media Player / VLC / Flash / RealPlayer 等媒体播放器控件
                            if (progId.StartsWith("WMPlayer.", StringComparison.Ordinal) ||
                                progId.StartsWith("VideoLAN.", StringComparison.Ordinal) ||
                                progId.StartsWith("ShockwaveFlash.", StringComparison.Ordinal) ||
                                progId.StartsWith("RealPlayer.", StringComparison.Ordinal) ||
                                progId.StartsWith("RealMedia.", StringComparison.Ordinal))
                                return true;
                        }
                    }
                    catch (Exception ex) { LogService.LogException(ex); }
                    // 无法确认是否为媒体播放器时，不视为视频，避免把普通 ActiveX 控件误判为视频
                    return false;
                }

                // msoEmbeddedOLEObject = 7：嵌入式 OLE，旧版视频格式，MediaType 必须为 ppMediaTypeMovie = 3
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoEmbeddedOLEObject)
                {
                    try
                    {
                        if ((int)(object)shape.MediaType == 3)  // ppMediaTypeMovie
                            return true;
                    }
                    catch (Exception ex) { LogService.LogException(ex); }
                }
            }
            catch (Exception ex) { LogService.LogException(ex); }
            return false;
        }

        internal static BitmapImage LoadBitmapImage(MemoryStream stream)
        {
            try
            {
                if (stream == null || stream.Length == 0) return null;
                lock (stream)
                {
                    stream.Position = 0;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    stream.Position = 0;
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        internal static void DisposePPTEnhancedPreviewItems(List<PPTEnhancedPreviewItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
                item?.Dispose();
        }

        internal static Task<T> RunOnStaAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            // Office interop 要求 STA + COM 单元；线程池（MTA）里跑 COM 会触发 RPC_E_WRONG_THREAD
            // 等随机 COM 失败，表现为增强预览空白或崩溃。显式创建 STA worker 在其中执行导出。
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    tcs.TrySetResult(func());
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        internal static string GetPresentationStrokeFolderPath(Presentation presentation, string presentationName, int totalSlides, string presentationFullName = null)
        {
            string basePath = Path.Combine(SettingsManager.Settings.Automation.AutoSavedStrokesLocation, "Auto Saved - Presentations");
            string fullName = presentationFullName;
            string name = presentationName;
            int slidesTotal = totalSlides;

            if (presentation != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(fullName)) fullName = presentation.FullName;
                    if (string.IsNullOrEmpty(name)) name = presentation.Name;
                    if (slidesTotal <= 0) slidesTotal = presentation.Slides.Count;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"读取演示文稿标识失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }

            if (!string.IsNullOrEmpty(fullName))
            {
                string hash = HashHelper.GetFileHash(fullName);
                return Path.Combine(basePath, $"{name ?? ""}_{slidesTotal}_{hash}");
            }

            return Path.Combine(basePath, $"{name ?? ""}_{slidesTotal}");
        }

        // ---- COM 互操作生命周期（进程守护组，仅 PPTLinkMode.Com 生效）----

        /// <summary>
        /// 创建PowerPoint应用程序实例
        /// <summary>
        /// 创建并初始化一个隐藏的 PowerPoint 应用程序 COM 实例，并在可用时将该实例注入到当前的 PPT 管理器中。
        /// </summary>
        /// <remarks>
        /// 如果配置为使用 ROT 链接或已有有效的 PowerPoint 实例，则不会创建新实例。创建的实例会被设置为不可见并最小化；在实例准备就绪后会通过延迟调用将其设置到 PPT 管理器（SetPPTManagerApplication）。任何创建或注入失败的情况会被记录日志，但不会抛出异常给调用者。
        /// </remarks>
        internal void CreatePowerPointApplication()
        {
            try
            {
                if (SettingsManager.Settings.PowerPointSettings.PPTLinkMode != PPTLinkMode.Com) return;
                // 如果应用程序已存在且有效，则不重复创建
                if (_interopState.PptApplication != null && IsPowerPointApplicationValid())
                {
                    return;
                }

                // 创建新的PowerPoint应用程序实例
                _interopState.PptApplication = new Microsoft.Office.Interop.PowerPoint.Application();

                // 设置为不可见，作为后台进程
                _interopState.PptApplication.Visible = MsoTriState.msoFalse;

                // 设置应用程序属性
                _interopState.PptApplication.WindowState = PpWindowState.ppWindowMinimized;

                // 直接设置PPTManager的PPTApplication属性，绕过COM注册问题
                Task.Delay(1000).ContinueWith(_ =>
                {
                    _uiInvoke(() =>
                    {
                        try
                        {
                            // 直接设置PPTManager的PowerPoint应用程序实例
                            if (_getPptManager() != null)
                            {
                                // 使用反射或直接访问来设置PPTManager的PPTApplication
                                SetPPTManagerApplication(_interopState.PptApplication);
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
        internal void SetPPTManagerApplication(Microsoft.Office.Interop.PowerPoint.Application app)
        {
            try
            {
                if (_getPptManager() == null) return;
                if (SettingsManager.Settings.PowerPointSettings.PPTLinkMode != PPTLinkMode.Com) return;

                // 使用反射调用PPTManager的ConnectToPPT方法
                var pptManagerType = _getPptManager().GetType();
                var connectMethod = pptManagerType.GetMethod("ConnectToPPT",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (connectMethod != null)
                {
                    connectMethod.Invoke(_getPptManager(), new object[] { app });
                    LogHelper.WriteLogToFile("通过ConnectToPPT方法设置PowerPoint应用程序实例", LogHelper.LogType.Event);
                }
                else
                {
                    // 如果无法通过反射调用，尝试直接设置属性
                    var pptApplicationProperty = pptManagerType.GetProperty("PPTApplication",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (pptApplicationProperty != null && pptApplicationProperty.CanWrite)
                    {
                        pptApplicationProperty.SetValue(_getPptManager(), app);
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
        internal bool IsPowerPointApplicationValid()
        {
            try
            {
                if (_interopState.PptApplication == null) return false;
                if (!Marshal.IsComObject(_interopState.PptApplication)) return false;

                // 尝试访问一个简单的属性来验证连接是否有效
                var _ = _interopState.PptApplication.Name;
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
        internal void ClosePowerPointApplication(bool isShutdown = false)
        {
            try
            {
                if (_interopState.PptApplication != null)
                {
                    // 关闭所有打开的演示文稿
                    try
                    {
                        if (_interopState.PptApplication.Presentations.Count > 0)
                        {
                            for (int i = _interopState.PptApplication.Presentations.Count; i >= 1; i--)
                            {
                                try
                                {
                                    _interopState.PptApplication.Presentations[i].Close();
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                            }
                        }
                    }
                    catch (COMException comEx)
                    {
                        // 关机时 COM 对象可能已失效，记录但继续清理
                        LogHelper.WriteLogToFile($"关闭演示文稿时 COM 异常 (HResult: 0x{comEx.HResult:X}): {comEx.Message}",
                            isShutdown ? LogHelper.LogType.Warning : LogHelper.LogType.Error);
                    }

                    // 退出PowerPoint应用程序
                    try
                    {
                        _interopState.PptApplication.Quit();
                    }
                    catch (COMException comEx)
                    {
                        // 关机时 COM 对象可能已失效，记录但继续清理
                        LogHelper.WriteLogToFile($"退出 PowerPoint 时 COM 异常 (HResult: 0x{comEx.HResult:X}): {comEx.Message}",
                            isShutdown ? LogHelper.LogType.Warning : LogHelper.LogType.Error);
                    }

                    // 释放COM对象
                    try
                    {
                        if (Marshal.IsComObject(_interopState.PptApplication))
                        {
                            Marshal.ReleaseComObject(_interopState.PptApplication);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"释放 PowerPoint COM 对象异常: {ex.Message}", LogHelper.LogType.Warning);
                    }

                    _interopState.PptApplication = null;
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
        internal void ClearStaticInteropState()
        {
            try
            {
                if (_interopState.Presentation != null)
                {
                    try { if (Marshal.IsComObject(_interopState.Presentation)) Marshal.ReleaseComObject(_interopState.Presentation); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    _interopState.Presentation = null;
                }
                if (_interopState.Slides != null)
                {
                    try { if (Marshal.IsComObject(_interopState.Slides)) Marshal.ReleaseComObject(_interopState.Slides); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    _interopState.Slides = null;
                }
                if (_interopState.Slide != null)
                {
                    try { if (Marshal.IsComObject(_interopState.Slide)) Marshal.ReleaseComObject(_interopState.Slide); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    _interopState.Slide = null;
                }
                _interopState.SlidesCount = 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ClearStaticInteropState 异常: {ex}", LogHelper.LogType.Warning);
            }
        }

        // ---- 进程守护监控 + Win32 放映窗口探测 ----

        /// <summary>
        /// PowerPoint 全屏放映顶层窗口类名（与编辑态 PPTFrameClass 区分）。
        /// </summary>
        private const string PowerPointSlideShowWindowClassName = "screenClass";

        /// <summary>
        /// 启动PowerPoint应用程序守护
        /// </summary>
        /// <remarks>
        /// 启动对本地 PowerPoint 应用实例的守护监控并在需要时创建应用程序实例。
        /// 仅在 PowerPoint 增强功能已启用且未使用 ROT 链接时生效；方法将创建 PowerPoint 应用（若不存在）并启动用于定期检查应用状态的定时器。
        /// </remarks>
        internal void StartPowerPointProcessMonitoring()
        {
            try
            {
                if (!SettingsManager.Settings.PowerPointSettings.EnablePowerPointEnhancement) return;
                if (SettingsManager.Settings.PowerPointSettings.PPTLinkMode != PPTLinkMode.Com) return;

                // 创建PowerPoint应用程序实例
                CreatePowerPointApplication();

                // 启动应用程序监控定时器（定时器由主窗口持有并接线 Tick，保持 UI 线程亲和）
                _hooks.StartProcessMonitorTimer();

                LogHelper.WriteLogToFile("[PPT] PowerPoint 应用程序守护已启动", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止PowerPoint应用程序守护
        /// </summary>
        internal void StopPowerPointProcessMonitoring(bool isShutdown = false)
        {
            try
            {
                // 停止应用程序监控定时器
                _hooks.StopProcessMonitorTimer();

                // 关闭PowerPoint应用程序（包括关机时）
                ClosePowerPointApplication(isShutdown);

                LogHelper.WriteLogToFile("[PPT] PowerPoint 应用程序守护已停止", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// PowerPoint应用程序监控定时器事件
        /// </summary>
        /// <remarks>
        /// 周期性监控嵌入的 PowerPoint 应用实例的可用性，并在检测到失效时尝试重建实例；当增强功能被禁用时停止监控，并在使用 ROT 链接时不进行检查。
        /// </remarks>
        internal void OnPowerPointApplicationMonitorTick(object sender, EventArgs e)
        {
            try
            {
                if (!SettingsManager.Settings.PowerPointSettings.EnablePowerPointEnhancement)
                {
                    StopPowerPointProcessMonitoring();
                    return;
                }
                if (SettingsManager.Settings.PowerPointSettings.PPTLinkMode != PPTLinkMode.Com) return;

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

        /// <summary>
        /// 检测是否存在 PowerPoint 全屏放映顶层窗口（类名 screenClass，进程 powerpnt），用于 COM 不可用时的兜底。
        /// </summary>
        internal bool IsPowerPointSlideshowSurfacePresentWin32()
        {
            if (!SettingsManager.Settings.ModeSettings.IsPPTOnlyMode)
                return false;

            try
            {
                bool found = false;
                PInvoke.EnumWindows((hWnd, _) =>
                {
                    if (!PInvoke.IsWindow(hWnd) || !PInvoke.IsWindowVisible(hWnd))
                        return true;

                    Span<char> classBuffer = stackalloc char[256];
                    int classLength = PInvoke.GetClassName(hWnd, classBuffer);
                    if (classLength == 0)
                        return true;

                    if (!string.Equals(classBuffer.Slice(0, classLength).ToString(), PowerPointSlideShowWindowClassName, StringComparison.OrdinalIgnoreCase))
                        return true;

                    try
                    {
                        PInvoke.GetWindowThreadProcessId(hWnd, out uint pid);
                        using (var proc = Process.GetProcessById((int)pid))
                        {
                            var name = proc.ProcessName;
                            if (string.Equals(name, "POWERPNT", StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogException(ex);
                    }

                    return true;
                }, IntPtr.Zero);

                return found;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Win32 检测 PPT 放映窗口失败: {ex.Message}", LogHelper.LogType.Trace);
                return false;
            }
        }

        /// <summary>
        /// 枚举顶层 screenClass 窗口，返回当前可见且非最小化的放映窗口句柄。
        /// </summary>
        /// <remarks>
        /// FindWindow("screenClass", null) 会返回 Z 序最前的窗口，可能命中陈旧或已结束的放映；
        /// 此处枚举所有顶层窗口并校验可见性，挑选最合适的放映窗口。
        /// </remarks>
        internal IntPtr FindActiveScreenClassWindow()
        {
            IntPtr best = IntPtr.Zero;
            try
            {
                PInvoke.EnumWindows((hWnd, lParam) =>
                {
                    if (hWnd == IntPtr.Zero) return true;
                    if (!PInvoke.IsWindowVisible(hWnd)) return true;
                    if (PInvoke.IsIconic(hWnd)) return true;

                    Span<char> classBuffer = stackalloc char[256];
                    int classLength = PInvoke.GetClassName(hWnd, classBuffer);
                    if (classLength == 0) return true;
                    if (!string.Equals(classBuffer.Slice(0, classLength).ToString(), PowerPointSlideShowWindowClassName, StringComparison.Ordinal)) return true;

                    best = hWnd;
                    return false; // 停止枚举
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[SmartMode] 枚举放映窗口失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return best;
        }

        // ---- 放映事件编排核心（放映开始/翻页/结束/退出/页码点击）----

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映开始事件：根据设置折叠或展开浮动栏，初始化放映状态，更新UI，加载当前页墨迹，并设置相关参数。
        /// </summary>
        /// <param name="payload">事件负载：PowerPoint 幻灯片放映窗口（SlideShowWindow）实例，或 Agent 通路的 PPTState 快照。</param>
        /// <remarks>
        /// 操作包括：折叠/展开浮动栏、停止墨迹重放、获取放映页码信息、初始化墨迹管理器、
        /// 跳转到首页或上次播放位置、更新 UI 状态、设置浮动栏透明度和边距、显示侧边栏退出按钮、
        /// 处理画板显示、关闭白板模式、显示浮动栏主控件、隐藏手势面板、进入批注模式、
        /// 重置放映结束事件标志、加载当前页墨迹、调整浮动栏边距动画。
        /// 所有UI操作在UI线程异步执行，异常会被捕获并记录为错误日志。
        /// </remarks>
        internal async void OnPPTSlideShowBegin(object payload)
        {
            var wn = payload as SlideShowWindow;
            var agentState = payload as PPTState;
            try
            {
                if (SettingsManager.Settings.Automation.IsAutoFoldInPPTSlideShow)
                {
                    if (!_slideHooks.IsFloatingBarFolded())
                        _slideHooks.FoldFloatingBar();
                }
                else
                {
                    if (_slideHooks.IsFloatingBarFolded())
                    {
                        await _slideHooks.UnFoldFloatingBarAsync();
                    }
                }

                _slideHooks.SetInkReplayStopped();

                int currentSlide = 0;
                int totalSlides = 0;
                string presentationName = null;
                string presentationFullName = null;
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
                            presentationFullName = activePresentation.FullName;
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
                    if (agentState == null && _getPptManager() is PPTAgentLinkManager agentManager)
                    {
                        agentState = agentManager.CurrentState;
                    }

                    if (agentState != null)
                    {
                        currentSlide = agentState.SlideIndex;
                        totalSlides = agentState.TotalSlides;
                        presentationName = agentState.PresentationName;
                        presentationFullName = agentState.PresentationFullName;
                    }
                    else
                    {
                        activePresentation = _getPptManager()?.GetCurrentActivePresentation() as Presentation;
                        currentSlide = _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                        totalSlides = _getPptManager()?.SlidesCount ?? 0;
                        presentationName = _getPptManager()?.GetPresentationName() ?? activePresentation?.Name;
                    }
                }

                _slideShowState.CurrentSlideShowPosition = currentSlide;
                _slideShowState.PreviousSlideId = currentSlide;
                _slideHooks.NotifyPluginSlideShowStateChanged(true);

                lock (_slideShowState.MemoryStreams)
                {
                    foreach (var stream in _slideShowState.MemoryStreams.Values)
                        stream?.Dispose();
                    _slideShowState.MemoryStreams.Clear();
                }

                if (SettingsManager.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint && !string.IsNullOrEmpty(presentationName))
                {
                    string strokePath = GetPresentationStrokeFolderPath(activePresentation, presentationName, totalSlides, presentationFullName);
                    if (Directory.Exists(strokePath))
                    {
                        await _slideHooks.RunBackgroundAsync(() =>
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
                                                lock (_slideShowState.MemoryStreams)
                                                {
                                                    _slideShowState.MemoryStreams[pageNum] = new MemoryStream(bytes);
                                                    _slideShowState.MemoryStreams[pageNum].Position = 0;
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

                await _slideHooks.UiInvokeAsyncFunc(async () =>
                {
                    var inkManager = _slideHooks.GetPptInkManager();
                    if (activePresentation != null && inkManager != null)
                    {
                        try
                        {
                            inkManager.InitializePresentation(activePresentation);
                        }
                        catch (Exception ex)
                        {
                            LogService.LogException(ex);
                        }
                    }

                    // 处理跳转到首页或上次播放位置
                    if (SettingsManager.Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                    {
                        _getPptManager()?.TryNavigateToSlide(1);
                        if (SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext) _slideHooks.TryActivateWindow("激活主窗口失败（PPT 重入首页时）");
                    }
                    else if (_slideShowState.ShouldNavigateToLastPage && _slideShowState.LastPlaybackPage > 0)
                    {
                        _getPptManager()?.TryNavigateToSlide(_slideShowState.LastPlaybackPage);
                        _slideShowState.ShouldNavigateToLastPage = false;
                        if (SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext) _slideHooks.TryActivateWindow("激活主窗口失败（PPT 重入末页时）");
                    }

                    // 更新UI状态
                    _slideHooks.GetPptUiManager()?.UpdateSlideShowStatus(true, currentSlide, totalSlides);

                    // 设置浮动栏透明度和边距
                    _slideHooks.GetPptUiManager()?.SetFloatingBarOpacity(SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue);
                    _slideHooks.ApplyFloatingBarMenuOpacity();
                    _slideHooks.GetPptUiManager()?.SetMainPanelMargin(new Thickness(10, 10, 10, 10));

                    // 显示侧边栏退出按钮
                    _slideHooks.GetPptUiManager()?.UpdateSidebarExitButtons(true);

                    // 显示浮动栏退出PPT按钮
                    ShowFloatingBarExitPPTBtn();

                    // 处理画板显示
                    if (SettingsManager.Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !SettingsManager.Settings.Automation.IsAutoFoldInPPTSlideShow &&
                        _gridTransparencyFakeBackground.Background == Brushes.Transparent && !_slideHooks.IsFloatingBarFolded())
                    {
                        _slideHooks.BtnHideInkCanvasClick();
                    }

                    if (_slideHooks.GetCurrentMode() != 0)
                    {
                        _slideHooks.ImageBlackboardMouseUp();
                        _slideHooks.BtnHideInkCanvasClick();
                    }

                    _slideHooks.SetFloatingBarContentVisibility(true);

                    // 在PPT模式下根据设置决定是否隐藏手势面板和手势按钮
                    _slideHooks.HideGesturePanels();

                    // 根据设置决定是否在PPT放映模式下显示手势按钮
                    _slideHooks.UpdateToolbarComponentVisibility();

                    if (SettingsManager.Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !SettingsManager.Settings.Automation.IsAutoFoldInPPTSlideShow)
                    {
                        await Task.Delay(300);
                        // 先进入批注模式，这会显示调色盘
                        _slideHooks.PenIconClick();
                        // 然后设置颜色
                        _slideHooks.BtnColorRedClick();
                        try
                        {
                            if (_slideHooks.GetInkEditingMode() == InkCanvasEditingMode.Ink)
                            {
                                _slideHooks.UpdateCurrentToolMode("pen");
                                _slideHooks.SetFloatingBarHighlightPosition("pen");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"PPT进入批注模式后同步浮动栏高光状态失败: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }

                    _slideShowState.IsEnteredSlideShowEndEvent = false;

                    // 加载当前页墨迹
                    LoadCurrentSlideInk(currentSlide);

                    // 仅PPT模式：放映开始立即同步主窗口可见性（勿仅依赖 SlideShowStateChanged 定时器）
                    _slideHooks.CheckWindowVisibility();

                    // 刷新智慧模式区域
                    if (SettingsManager.Settings.PowerPointSettings.EnableSmartMode)
                    {
                        System.Diagnostics.Debug.WriteLine("[SmartMode] SlideShowBegin, refreshing regions...");
                        RefreshSmartModeRegions();
                    }
                });

                if (!_slideHooks.IsFloatingBarFolded())
                {
                    _slideHooks.ScheduleFloatingBarMarginAnimationAfterDelay();
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
        /// <param name="payload">事件负载：当前的幻灯片放映窗口，或 Agent 通路的 PPTState 快照；若窗口为 null 或其 View 无效则回退到管理器获取页码。</param>
        /// <remarks>
        /// - 如果收到与当前记录相同的页码或已有切换正在处理，则忽略该事件。
        /// - 在切换过程中会保存前一页的墨迹（如存在）、清空画布与历史、加载新页的墨迹、锁定新页墨迹并刷新当前页显示序号，同时更新内部的当前播放位置状态。
        /// </remarks>
        internal void OnPPTSlideShowNextSlide(object payload)
        {
            var wn = payload as SlideShowWindow;
            var agentState = payload as PPTState;
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
                    currentSlide = agentState?.SlideIndex ?? _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                }

                totalSlides = agentState?.TotalSlides ?? _getPptManager()?.SlidesCount ?? 0;

                if (currentSlide == _slideShowState.PreviousSlideId) return;

                int prev = _slideShowState.PreviousSlideId;

                _ = _hooks.UiInvokeAsync(() =>
                {
                    MemoryStream ms = _slideHooks.CaptureInkStrokesSnapshot();

                    lock (_slideShowState.MemoryStreams)
                    {
                        if (_slideShowState.MemoryStreams.ContainsKey(prev))
                            _slideShowState.MemoryStreams[prev]?.Dispose();
                        _slideShowState.MemoryStreams[prev] = ms;
                    }

                    _slideHooks.ClearStrokes(true);
                    _slideHooks.ClearStrokeHistory();

                    _slideShowState.CurrentSlideShowPosition = currentSlide;
                    _slideHooks.GetPptInkManager()?.LockInkForSlide(currentSlide);
                    _slideHooks.GetPptUiManager()?.UpdateCurrentSlideNumber(currentSlide, totalSlides);

                    byte[] bytesToLoad = null;
                    lock (_slideShowState.MemoryStreams)
                    {
                        if (_slideShowState.MemoryStreams.ContainsKey(currentSlide) && _slideShowState.MemoryStreams[currentSlide] != null)
                            bytesToLoad = _slideShowState.MemoryStreams[currentSlide].ToArray();
                    }
                    if (bytesToLoad != null)
                    {
                        if (bytesToLoad.Length > 8)
                        {
                            int loadingPage = currentSlide;
                            _slideHooks.RunStrokeLoadAsync(() =>
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
                                _ = _hooks.UiInvokeAsync(() =>
                                {
                                    if (_slideShowState.CurrentSlideShowPosition != loadingPage) return;
                                    _slideHooks.AddInkStrokes(t.Result);
                                });
                            });
                        }
                    }
                });
                _slideShowState.PreviousSlideId = currentSlide;
                _slideHooks.NotifyPluginSlideChanged(currentSlide);

                // 刷新智慧模式区域（翻页时视频控件可能变化）
                if (SettingsManager.Settings.PowerPointSettings.EnableSmartMode)
                    _ = _hooks.UiInvokeAsync(() => RefreshSmartModeRegions());

                // 转发PPT翻页事件到小白板（如果已打开且启用了联动）
                _slideHooks.NotifyMiniWhiteboardSlideChanged(currentSlide - 1);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片切换事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理 PowerPoint 幻灯片放映结束时的清理与界面恢复，包括保存当前幻灯片墨迹、重置墨迹管理器状态、恢复主题与工具栏显示，并根据配置折叠或展示浮动工具栏等 UI 调整。
        /// </summary>
        /// <param name="payload">事件负载：触发结束事件的 PowerPoint 演示文稿（Presentation）实例，或 Agent 通路的 PPTState 快照。</param>
        internal async void OnPPTSlideShowEnd(object payload)
        {
            var pres = payload as Presentation;
            var agentState = payload as PPTState;
            try
            {
                await _hooks.UiInvokeAsync(() => _slideHooks.CollapseAllPPTNavBarPreviews());

                if (SettingsManager.Settings.Automation.IsAutoFoldAfterPPTSlideShow && !_slideHooks.IsFloatingBarFolded())
                {
                    _slideHooks.FoldFloatingBar();
                }

                if (_slideShowState.IsEnteredSlideShowEndEvent) return;
                _slideShowState.IsEnteredSlideShowEndEvent = true;
                _slideHooks.NotifyPluginSlideShowStateChanged(false);

                // 清除智慧模式区域
                _smartModeState.Regions = null;
                _smartModeState.SlideIndex = -1;

                // 清除WPF坐标映射和定时器
                _slideHooks.ClearMediaPassthroughState();

                // 获取当前播放页码，优先使用跟踪的页码，否则尝试从PPT管理器获取
                int currentPage = _slideShowState.CurrentSlideShowPosition;
                if (currentPage <= 0)
                {
                    try
                    {
                        currentPage = _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                    }
                    catch
                    {
                        // 如果无法获取，尝试从演示文稿的SlideShowWindow获取
                        try
                        {
                            if (pres != null && pres.SlideShowWindow != null && pres.SlideShowWindow.View != null)
                            {
                                currentPage = pres.SlideShowWindow.View.CurrentShowPosition;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }

                await _hooks.UiInvokeAsync(() =>
                {
                    if (currentPage > 0 && _slideHooks.HasInkStrokesCollection())
                    {
                        MemoryStream ms = _slideHooks.CaptureInkStrokesSnapshot();

                        lock (_slideShowState.MemoryStreams)
                        {
                            if (_slideShowState.MemoryStreams.ContainsKey(currentPage))
                                _slideShowState.MemoryStreams[currentPage]?.Dispose();
                            _slideShowState.MemoryStreams[currentPage] = ms;
                        }
                    }
                });

                string presentationNameForSave = agentState?.PresentationName ?? _getPptManager()?.GetPresentationName() ?? (pres != null ? pres.Name : null);
                string presentationFullNameForSave = agentState?.PresentationFullName;
                int totalSlidesForSave = agentState?.TotalSlides ?? _getPptManager()?.SlidesCount ?? 0;
                if (totalSlidesForSave <= 0 && pres != null)
                {
                    try
                    {
                        totalSlidesForSave = pres.Slides.Count;
                    }
                    catch (COMException comEx)
                    {
                        var hr = (uint)comEx.HResult;
                        if (hr != 0x80048240 && hr != 0x80010108 && hr != 0x800706BA && hr != 0x800706BE)
                            LogHelper.WriteLogToFile($"读取放映结束总页数失败: {comEx.Message}", LogHelper.LogType.Warning);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"读取放映结束总页数失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                if (currentPage > 0 && SettingsManager.Settings.PowerPointSettings.IsNotifyPreviousPage && !string.IsNullOrEmpty(presentationNameForSave) && totalSlidesForSave > 0)
                {
                    try
                    {
                        string folderPathForPosition = GetPresentationStrokeFolderPath(pres, presentationNameForSave, totalSlidesForSave, presentationFullNameForSave);
                        if (!Directory.Exists(folderPathForPosition))
                            Directory.CreateDirectory(folderPathForPosition);
                        File.WriteAllText(Path.Combine(folderPathForPosition, "Position"), currentPage.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"保存上次播放位置失败: {ex}", LogHelper.LogType.Warning);
                    }
                }

                if (SettingsManager.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint && !string.IsNullOrEmpty(presentationNameForSave) && totalSlidesForSave > 0)
                {
                    string folderPathForSave = GetPresentationStrokeFolderPath(pres, presentationNameForSave, totalSlidesForSave, presentationFullNameForSave);
                    await _slideHooks.RunBackgroundAsync(() =>
                    {
                        try
                        {
                            if (!Directory.Exists(folderPathForSave))
                                Directory.CreateDirectory(folderPathForSave);

                            // 先在锁内快照出 (页码, 字节) 列表，再在锁外做磁盘 IO。
                            // 之前在 lock 内直接 File.WriteAllBytes 把磁盘 IO 与字典保护混在一起，
                            // 磁盘繁忙/AV 扫描锁定文件时 MemoryStreams 被独占数十秒，
                            // 期间 OnPPTSlideShowNextSlide/ExitPPTPresentation 任何持锁访问全卡死。
                            var snapshot = new List<(int page, byte[] bytes)>();
                            lock (_slideShowState.MemoryStreams)
                            {
                                for (int i = 1; i <= totalSlidesForSave; i++)
                                {
                                    if (_slideShowState.MemoryStreams.TryGetValue(i, out MemoryStream value) && value != null)
                                        snapshot.Add((i, value.ToArray()));
                                }
                            }

                            foreach (var (page, bytes) in snapshot)
                            {
                                try
                                {
                                    string filePath = Path.Combine(folderPathForSave, page.ToString("0000") + ".icstk");
                                    if (bytes.Length > 8)
                                        File.WriteAllBytes(filePath, bytes);
                                    else if (File.Exists(filePath))
                                        File.Delete(filePath);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"为第 {page} 页保存墨迹文件失败: {ex}", LogHelper.LogType.Warning);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"保存PPT墨迹文件失败: {ex}", LogHelper.LogType.Error);
                        }
                        finally
                        {
                            lock (_slideShowState.MemoryStreams)
                            {
                                foreach (var stream in _slideShowState.MemoryStreams.Values)
                                    stream?.Dispose();
                                _slideShowState.MemoryStreams.Clear();
                            }
                        }
                    });
                }
                else
                {
                    lock (_slideShowState.MemoryStreams)
                    {
                        foreach (var stream in _slideShowState.MemoryStreams.Values)
                            stream?.Dispose();
                        _slideShowState.MemoryStreams.Clear();
                    }
                }

                await _hooks.UiInvokeAsync(() =>
                {
                    try
                    {
                        _slideShowState.IsPresentationHaveBlackSpace = false;

                        // 恢复主题
                        if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light)
                        {
                            { /* Old UI removed */ }
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        // 更新UI状态
                        _slideHooks.GetPptUiManager()?.UpdateSlideShowStatus(false);
                        _slideHooks.GetPptUiManager()?.UpdateSidebarExitButtons(false);

                        // 隐藏浮动栏退出PPT按钮
                        HideFloatingBarExitPPTBtn();

                        _slideHooks.GetPptUiManager()?.SetMainPanelMargin(new Thickness(10, 10, 10, 55));
                        _slideHooks.GetPptUiManager()?.SetFloatingBarOpacity(SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityValue);
                        _slideHooks.ApplyFloatingBarMenuOpacity();

                        if (_slideHooks.GetCurrentMode() != 0)
                        {
                            _slideHooks.CloseWhiteboardImmediately();
                            _slideHooks.SetCurrentMode(0);
                            AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
                        }

                        _slideHooks.SyncPdfPageSidebarWithCanvas();

                        _slideHooks.ClearStrokes(true);
                        // 清空备份历史记录，防止退出白板时恢复已结束PPT的墨迹
                        // 注意：这里只清空索引0的备份，不影响白板页面的墨迹（索引1及以上）
                        _slideHooks.ClearFirstTimeMachineHistory();

                        // 重置墨迹管理器的锁定状态，防止下次放映时墨迹显示错误
                        ResetInkManagerLockState();

                        // 退出PPT模式时恢复手势面板和手势按钮的显示状态
                        _slideHooks.UpdateToolbarComponentVisibility();

                        // 注意：快捷调色盘的可见性现在完全由工具栏规则集管理
                        // 不需要手动设置，UpdateToolbarComponentVisibility 会处理好

                        if (_gridTransparencyFakeBackground.Background != Brushes.Transparent)
                            _slideHooks.BtnHideInkCanvasClick();
                        _slideHooks.SetCurrentToolMode(InkCanvasEditingMode.None);

                        _slideHooks.UpdateCurrentToolMode("cursor");
                        _slideHooks.SetFloatingBarHighlightPosition("cursor");

                        _slideHooks.CheckWindowVisibility();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理幻灯片放映结束UI更新失败: {ex}", LogHelper.LogType.Error);
                    }
                });

                await Task.Delay(100);
                await _hooks.UiInvokeAsync(() =>
                {
                    if (!_slideHooks.IsFloatingBarFolded())
                    {
                        _slideHooks.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                        if (SettingsManager.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                        {
                            Task.Delay(350).ContinueWith(_ =>
                            {
                                _uiInvoke(() =>
                                {
                                    if (!_slideHooks.IsFloatingBarFolded())
                                    {
                                        _slideHooks.ViewboxFloatingBarMarginAnimation(-60);
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

        /// <summary>
        /// 加载当前页的墨迹
        /// </summary>
        /// <param name="slideIndex">要加载的幻灯片页码。</param>
        /// <remarks>
        /// 清空画布与历史后，从逐页墨迹内存流中取出对应页并反序列化添加到画布。
        /// </remarks>
        internal void LoadCurrentSlideInk(int slideIndex)
        {
            try
            {
                _slideHooks.ClearStrokes(true);
                _slideHooks.ClearStrokeHistory();

                byte[] bytes = null;
                lock (_slideShowState.MemoryStreams)
                {
                    if (_slideShowState.MemoryStreams.TryGetValue(slideIndex, out var ms) && ms != null && ms.Length > 0)
                    {
                        ms.Position = 0;
                        bytes = ms.ToArray();
                    }
                }
                if (bytes != null)
                {
                    try
                    {
                        _slideHooks.AddInkStrokesFromBytes(bytes);
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
        internal void ResetInkManagerLockState()
        {
            try
            {
                _slideHooks.GetPptInkManager()?.ResetLockState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置墨迹管理器锁定状态失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 退出 PPT 放映：保存当前页墨迹、切回光标模式、结束放映并恢复浮动栏布局。
        /// 外部演示源激活时等价于结束该演示源。
        /// </summary>
        internal async Task ExitPPTPresentation()
        {
            try
            {
                // 外部演示源（插件文档，如 PDF）激活时，退出按钮等价于结束该演示源：
                // 没有 PPT COM 会话，下面的幻灯片墨迹保存与 TryEndSlideShow 全部不适用。
                var presentationSource = _slideHooks.GetPresentationSourceService();
                if (presentationSource?.IsActive == true)
                {
                    presentationSource?.ForceEnd("用户点击退出按钮");
                    return;
                }

                var currentSlide = _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    // ExitPPTPresentation 通常在 UI 线程上调用。直接在当前线程保存墨迹，
                    // 避免 UI 线程调度在 UI 线程等待自身形成自锁。
                    if (_slideHooks.IsOnUiThread())
                    {
                        SaveCurrentSlideInkSnapshot(currentSlide);
                        _slideHooks.ClearStrokeHistory();
                    }
                    else
                    {
                        await _hooks.UiInvokeAsync(() =>
                        {
                            SaveCurrentSlideInkSnapshot(currentSlide);
                            _slideHooks.ClearStrokeHistory();
                        });
                    }
                }

                await _hooks.UiInvokeAsync(() =>
                {
                    _slideHooks.CursorIconClick();
                });

                await Task.Delay(100);
                await _slideHooks.UiInvokeBackgroundAsync(() => { });

                _uiInvoke(() =>
                {
                    try
                    {
                        _getPptManager()?.TryEndSlideShow();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"结束放映时发生异常: {ex}", LogHelper.LogType.Error);
                    }
                });

                await Task.Delay(150);
                if (!_slideHooks.IsFloatingBarFolded())
                {
                    _slideHooks.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    if (SettingsManager.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                    {
                        await Task.Delay(350);
                        if (!_slideHooks.IsFloatingBarFolded())
                        {
                            _slideHooks.ViewboxFloatingBarMarginAnimation(-60);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束PPT放映操作异常: {ex}", LogHelper.LogType.Error);

                // 确保UI状态正确
                await _hooks.UiInvokeAsync(() =>
                {
                    _slideHooks.GetPptUiManager()?.UpdateSlideShowStatus(false);
                    _slideHooks.GetPptUiManager()?.UpdateSidebarExitButtons(false);
                    HideFloatingBarExitPPTBtn();
                    _slideHooks.CheckWindowVisibility();
                });

                // 异常情况下也手动处理自动收纳
                await HandleManualSlideShowEnd();

                await Task.Delay(150);
                if (!_slideHooks.IsFloatingBarFolded())
                {
                    _slideHooks.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    if (SettingsManager.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode)
                    {
                        await Task.Delay(350);
                        if (!_slideHooks.IsFloatingBarFolded())
                        {
                            _slideHooks.ViewboxFloatingBarMarginAnimation(-60);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 保存当前页画布墨迹到逐页内存流（仅当有墨迹时；原 ExitPPTPresentation 内联代码块）。
        /// </summary>
        private void SaveCurrentSlideInkSnapshot(int currentSlide)
        {
            var ms = _slideHooks.CaptureNonEmptyInkStrokes();
            if (ms != null)
            {
                lock (_slideShowState.MemoryStreams)
                {
                    if (_slideShowState.MemoryStreams.ContainsKey(currentSlide))
                        _slideShowState.MemoryStreams[currentSlide]?.Dispose();
                    _slideShowState.MemoryStreams[currentSlide] = ms;
                }
            }
        }

        /// <summary>由 PPTNavBar 控件 PageClick 事件触发的页码点击逻辑。</summary>
        internal async Task OnPPTNavBarPageClickAsync(PPTNavBar bar)
        {
            if (!SettingsManager.Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;

            // 外部演示源没有幻灯片导航对话框，也没有缩略图来源；
            // 未显式允许时直接忽略点击，避免走下面依赖 PPT COM 的分支。
            var presentationSource = _slideHooks.GetPresentationSourceService();
            if (presentationSource?.IsActive == true)
            {
                if (presentationSource.IsPageNumberClickDisabled()) return;
                LogHelper.WriteLogToFile("外部演示源允许页码点击，但宿主未提供跳页 UI，已忽略。",
                    LogHelper.LogType.Info);
                return;
            }

            if (_getPptManager()?.IsConnected != true || _getPptManager()?.IsInSlideShow != true)
            {
                LogHelper.WriteLogToFile("PPT未连接或未在放映状态，无法执行页码点击操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                _gridTransparencyFakeBackground.Opacity = 1;
                _gridTransparencyFakeBackground.Background = new SolidColorBrush(_slideHooks.ConvertStringToColor("#01FFFFFF"));
                _slideHooks.SetTransparentNotHitThrough();
                _slideHooks.CursorIconClick();

                if (SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview && bar != null)
                {
                    // 侧边条点击时,把增强预览重定向到同侧的底部条上展开
                    var targetBar = _slideHooks.ResolvePreviewTargetBar(bar);
                    if (targetBar == null)
                    {
                        _getPptManager().TryShowSlideNavigation();
                    }
                    else if (targetBar.IsPreviewExpanded)
                    {
                        targetBar.IsPreviewExpanded = false;
                    }
                    else
                    {
                        var slides = await GetOrBuildPPTEnhancedPreviewItemsAsync(EnsurePPTEnhancedPreviewCacheToken());

                        if (slides == null || slides.Count == 0)
                        {
                            LogHelper.WriteLogToFile("PPT增强预览未生成可用缩略图，改用默认导航", LogHelper.LogType.Warning);
                            targetBar.IsPreviewExpanded = false;
                            _getPptManager().TryShowSlideNavigation();
                        }
                        else
                        {
                            var items = new List<PPTNavBar.PreviewItem>(slides.Count);
                            foreach (var s in slides)
                            {
                                items.Add(new PPTNavBar.PreviewItem
                                {
                                    SlideNumber = s.SlideNumber,
                                    Thumbnail = s.Thumbnail
                                });
                            }
                            targetBar.PreviewItems = items;
                            targetBar.CurrentSlide = _slideShowState.CurrentSlideShowPosition > 0
                                ? _slideShowState.CurrentSlideShowPosition
                                : (_getPptManager()?.GetCurrentSlideNumber() ?? 0);
                            targetBar.IsPreviewExpanded = true;
                        }
                    }
                }
                else
                {
                    if (_getPptManager().TryShowSlideNavigation())
                    {
                        if (SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext)
                        {
                            _slideHooks.TryActivateWindow("激活主窗口失败（PPT 导航时）");
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("显示PPT幻灯片导航失败", LogHelper.LogType.Warning);
                    }
                }

                if (!_slideHooks.IsFloatingBarFolded())
                {
                    await Task.Delay(100);
                    _slideHooks.ViewboxFloatingBarMarginAnimation(60);
                }
            }
            catch (OperationCanceledException ex)
            {
                LogService.LogException(ex);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT翻页控件操作失败: {ex}", LogHelper.LogType.Error);
            }
        }

        // ---- 连接/演示文稿/放映状态事件 + 状态重置 + 手动收纳 ----

        /// <summary>
        /// 处理 PowerPoint 连接状态的变更：更新界面连接/放映状态，并在断开时启动一个短延迟以安全退出 PPT 模式。
        /// </summary>
        /// <param name="isConnected">指示当前是否已与 PowerPoint 建立连接；`true` 表示已连接，`false` 表示已断开。</param>
        internal void OnPPTConnectionChanged(bool isConnected)
        {
            try
            {
                _ = _hooks.UiInvokeAsync(() =>
                {
                    _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(isConnected);

                    if (isConnected)
                    {
                        _slideHooks.StopExitPPTModeAfterDisconnectTimer();
                        SchedulePPTEnhancedPreviewPreload();
                        LogHelper.WriteLogToFile("[PPT] 连接已建立", LogHelper.LogType.Info);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("[PPT] 连接已断开，已安排模式清理", LogHelper.LogType.Info);
                        _slideHooks.GetPptInkManager()?.ClearAllStrokes();
                        _slideHooks.CollapseAllPPTNavBarPreviews();
                        ResetPPTEnhancedPreviewCache();
                        _slideHooks.StopExitPPTModeAfterDisconnectTimer();
                        _slideHooks.GetPptUiManager()?.UpdateSlideShowStatus(false);
                        _slideHooks.GetPptUiManager()?.UpdateSidebarExitButtons(false);

                        // 隐藏浮动栏退出PPT按钮
                        HideFloatingBarExitPPTBtn();
                        ResetPPTStateVariables();
                        _ = HandleManualSlideShowEnd();

                        _slideHooks.CheckWindowVisibility();
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
        /// <param name="payload">事件负载：已打开的 PowerPoint 演示文稿（Presentation）实例，或 Agent 通路的 PPTState 快照。</param>
        /// <remarks>
        /// 操作包括：清理画布墨迹和备份历史记录，初始化墨迹管理器，处理跳转到首页或上次播放页的逻辑，检查隐藏幻灯片和自动播放设置，更新UI连接状态，并记录事件日志。
        /// 所有操作在UI线程异步执行，异常会被捕获并记录为错误日志。
        /// </remarks>
        internal void OnPPTPresentationOpen(object payload)
        {
            var pres = payload as Presentation;
            var agentState = payload as PPTState;
            try
            {
                _ = _hooks.UiInvokeAsync(() =>
                {
                    // 在初始化墨迹管理器之前，先清理画布上的所有墨迹
                    ResetPPTEnhancedPreviewCache();

                    _slideHooks.ClearStrokes(true);

                    // 清理备份历史记录，防止旧演示文稿的墨迹影响新演示文稿
                    if (_slideHooks.HasTimeMachineHistories())
                    {
                        _slideHooks.ClearFirstTimeMachineHistory();
                    }

                    if (pres != null)
                    {
                        _slideHooks.GetPptInkManager()?.InitializePresentation(pres);
                    }

                    // 处理跳转到首页或上次播放页的逻辑
                    _slideHooks.HandlePresentationOpenNavigation(pres, agentState);

                    // 检查隐藏幻灯片
                    if (SettingsManager.Settings.PowerPointSettings.IsNotifyHiddenPage)
                    {
                        _slideHooks.CheckAndNotifyHiddenSlides(pres, agentState);
                    }

                    // 检查自动播放设置
                    if (SettingsManager.Settings.PowerPointSettings.IsNotifyAutoPlayPresentation)
                    {
                        _slideHooks.CheckAndNotifyAutoPlaySettings(pres, agentState);
                    }

                    _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(true);

                    SchedulePPTEnhancedPreviewPreload();

                    LogHelper.WriteLogToFile($"[PPT] 演示文稿已打开，已清理墨迹: {pres?.Name ?? agentState?.PresentationName ?? _getPptManager()?.GetPresentationName()}", LogHelper.LogType.Info);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理 PowerPoint 演示文稿关闭事件：收起导航条预览、重置增强预览缓存、释放逐页墨迹内存流并更新连接状态。
        /// </summary>
        internal void OnPPTPresentationClose(object payload)
        {
            try
            {
                _ = _hooks.UiInvokeAsync(() =>
                {
                    _slideHooks.CollapseAllPPTNavBarPreviews();
                    ResetPPTEnhancedPreviewCache();

                    lock (_slideShowState.MemoryStreams)
                    {
                        foreach (var stream in _slideShowState.MemoryStreams.Values)
                            stream?.Dispose();
                        _slideShowState.MemoryStreams.Clear();
                    }

                    _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(false);
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
            catch (Exception ex)
            {
                LogService.LogException(ex);
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
        internal void OnPPTSlideShowStateChanged(bool isInSlideShow)
        {
            try
            {
                _ = _hooks.UiInvokeAsync(() =>
                {
                    // 通知UI管理器放映状态变化
                    _slideHooks.GetPptUiManager()?.OnSlideShowStateChanged(isInSlideShow);
                    _slideHooks.NotifyPluginSlideShowStateChanged(isInSlideShow);

                    if (!isInSlideShow)
                    {
                        _slideHooks.CollapseAllPPTNavBarPreviews();
                    }

                    // 检查主窗口可见性（用于仅PPT模式）
                    _slideHooks.CheckWindowVisibility();
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理PPT放映状态变化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置PPT相关的状态变量，当PPT自动收纳设置变更时调用
        /// </summary>
        /// <remarks>
        /// 将与 PowerPoint 播放和状态追踪相关的内部字段重置为初始默认值。
        /// 具体重置的字段包括：播放结束重入保护标志、演示文稿黑边指示、上次播放页码、
        /// 导航标志、当前放映位置、滑动切换处理状态（逐页墨迹内存流）。
        /// </remarks>
        internal void ResetPPTStateVariables()
        {
            try
            {
                // 重置PPT放映结束事件标志
                _slideShowState.IsEnteredSlideShowEndEvent = false;

                // 重置演示文稿黑边状态
                _slideShowState.IsPresentationHaveBlackSpace = false;

                // 重置上次播放位置相关字段
                _slideShowState.LastPlaybackPage = 0;
                _slideShowState.ShouldNavigateToLastPage = false;

                // 重置当前播放页码跟踪
                _slideShowState.CurrentSlideShowPosition = 0;
                _slideShowState.PreviousSlideId = 0;
                lock (_slideShowState.MemoryStreams)
                {
                    foreach (var stream in _slideShowState.MemoryStreams.Values)
                        stream?.Dispose();
                    _slideShowState.MemoryStreams.Clear();
                }

                LogHelper.WriteLogToFile("PPT状态变量已重置", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置PPT状态变量失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 显示浮动栏的退出PPT按钮（实际由工具栏规则集根据状态计算可见性）。
        /// </summary>
        internal void ShowFloatingBarExitPPTBtn()
        {
            _slideHooks.UpdateToolbarComponentVisibility();
        }

        /// <summary>
        /// 隐藏浮动栏的退出PPT按钮（实际由工具栏规则集根据状态计算可见性）。
        /// </summary>
        internal void HideFloatingBarExitPPTBtn()
        {
            _slideHooks.UpdateToolbarComponentVisibility();
        }

        /// <summary>
        /// 手动处理PPT放映结束时的自动收纳
        /// </summary>
        internal async Task HandleManualSlideShowEnd()
        {
            try
            {
                await _hooks.UiInvokeAsync(() => _slideHooks.CollapseAllPPTNavBarPreviews());

                if (SettingsManager.Settings.Automation.IsAutoFoldAfterPPTSlideShow && !_slideHooks.IsFloatingBarFolded())
                {
                    _slideHooks.FoldFloatingBar();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动处理PPT放映结束自动收纳失败: {ex}", LogHelper.LogType.Error);
            }
        }

        // ---- 翻页与外部演示源路由 ----

        /// <summary>
        /// 若外部演示源处于激活状态，把翻页请求交给它并返回 true；否则返回 false 让调用方走 PPT COM 路径。
        /// </summary>
        internal bool TryRouteNavigationToPresentationSource(PresentationNavigation direction)
        {
            var service = _slideHooks.GetPresentationSourceService();
            if (service?.IsActive != true) return false;

            // 不 await：翻页条点击是同步事件，插件渲染完成后会通过服务回写页码。
            _ = service.HandleNavigationAsync(direction);
            return true;
        }

        /// <summary>
        /// 处理“上一页”按钮的点击操作：在满足自动保存条件时保存当前幻灯片截图并尝试切换到上一张幻灯片；在切换失败或发生异常时记录日志并更新连接状态。
        /// </summary>
        /// <param name="sender">事件的来源对象（通常是触发按钮）。</param>
        /// <param name="e">路由事件参数。</param>
        internal void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e)
        {
            // 外部演示源（插件把自己的文档接入放映模式）优先接管翻页，
            // 此时不存在 PPT COM 会话，不能走下面的 STA COM 路径。
            if (TryRouteNavigationToPresentationSource(PresentationNavigation.Previous)) return;

            int strokeCount = _slideHooks.GetInkStrokeCount();
            bool needScreenshot = strokeCount > SettingsManager.Settings.Automation.MinimumAutomationStrokeNumber &&
                SettingsManager.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;

            if (needScreenshot)
            {
                var currentSlide = _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    var presentationName = _getPptManager()?.GetPresentationName() ?? "";
                    _slideHooks.CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}");
                }
            }

            // 改用 STA worker 跑 COM 翻页：线程池（MTA）会触发 RPC_E_WRONG_THREAD，
            // COM 模式 PPTManager.TryNavigatePrevious 直接调 SlideShowWindows.View.Next()，
            // 跨单元封送在 PPT 忙于播放动画时超时或掉线。RunOnStaAsync 在文件内已定义。
            RunOnStaAsync(() =>
            {
                try
                {
                    return _getPptManager()?.TryNavigatePrevious() ?? false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT上一页操作异常: {ex}", LogHelper.LogType.Error);
                    return false;
                }
            }).ContinueWith(t =>
            {
                _uiInvoke(() =>
                {
                    // IsFaulted 由前面的 if 单独处理；剩余两种终态中 RanToCompletion 才允许读 Result，
                    // Canceled 直接走"切换失败"分支，避免 t.Result 再次抛 OperationCanceledException。
                    if (t.IsFaulted) { _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(false); return; }
                    if (t.IsCompletedSuccessfully && t.Result)
                    {
                        if (SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext) _slideHooks.TryActivateWindow("激活主窗口失败（PPT 上一页时）");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到上一页失败", LogHelper.LogType.Warning);
                        _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(false);
                    }
                });
            });
        }

        /// <summary>
        /// 处理“下一页”按钮点击：在满足自动保存条件时保存当前幻灯片的截图并尝试切换到下一张幻灯片。
        /// </summary>
        /// <remarks>
        /// 如果切换操作失败或发生异常，会写入日志并将 PPT 连接状态更新为断开。
        /// </remarks>
        internal void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e)
        {
            // 外部演示源优先接管翻页，理由同 BtnPPTSlidesUp_Click。
            if (TryRouteNavigationToPresentationSource(PresentationNavigation.Next)) return;

            int strokeCount = _slideHooks.GetInkStrokeCount();
            bool needScreenshot = strokeCount > SettingsManager.Settings.Automation.MinimumAutomationStrokeNumber &&
                SettingsManager.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;

            if (needScreenshot)
            {
                var currentSlide = _getPptManager()?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    var presentationName = _getPptManager()?.GetPresentationName() ?? "";
                    _slideHooks.CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}");
                }
            }

            // 同 BtnPPTSlidesUp_Click：用 STA worker 跑 COM 翻页，避免 MTA RPC_E_WRONG_THREAD。
            RunOnStaAsync(() =>
            {
                try
                {
                    return _getPptManager()?.TryNavigateNext() ?? false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT下一页操作异常: {ex}", LogHelper.LogType.Error);
                    return false;
                }
            }).ContinueWith(t =>
            {
                _uiInvoke(() =>
                {
                    // IsFaulted 由前面的 if 单独处理；Canceled 走"切换失败"分支，
                    // 只有 RanToCompletion 才允许读 t.Result。
                    if (t.IsFaulted) { _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(false); return; }
                    if (t.IsCompletedSuccessfully && t.Result)
                    {
                        if (SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext) _slideHooks.TryActivateWindow("激活主窗口失败（PPT 下一页时）");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到下一页失败", LogHelper.LogType.Warning);
                        _slideHooks.GetPptUiManager()?.UpdateConnectionStatus(false);
                    }
                });
            });
        }

        // ---- 智慧模式区域刷新与 COM 回退 ----

        /// <summary>
        /// 从 PPT Agent / COM 获取当前幻灯片的视频控件区域，缓存后用于鼠标进入/离开判断。
        /// </summary>
        internal void RefreshSmartModeRegions()
        {
            try
            {
                if (!SettingsManager.Settings.PowerPointSettings.EnableSmartMode)
                {
                    _smartModeState.Regions = null;
                    _smartModeState.SlideIndex = -1;
                    LogHelper.WriteLogToFile("[SmartMode] 功能未开启", LogHelper.LogType.Info);
                    return;
                }

                if (_getPptManager() is PPTAgentLinkManager agentManager)
                {
                    var response = agentManager.GetSmartRegions();
                    if (response?.Regions != null && response.Regions.Count > 0)
                    {
                        _smartModeState.Regions = response.Regions;
                        _smartModeState.SlideIndex = response.SlideIndex;
                        _smartModeState.SlideWidth = response.SlideWidth;
                        _smartModeState.SlideHeight = response.SlideHeight;
                        _smartModeState.SlideShowHwnd = new IntPtr(response.SlideShowWindowHandle);
                        LogHelper.WriteLogToFile($"[SmartMode] Agent 加载了 {_smartModeState.Regions.Count} 个区域, 第 {_smartModeState.SlideIndex} 页, Slide={_smartModeState.SlideWidth}x{_smartModeState.SlideHeight}磅", LogHelper.LogType.Info);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("[SmartMode] Agent 返回空区域列表，回退 COM 直接获取（VSTO 未加载/无加载项）", LogHelper.LogType.Info);
                        _smartModeState.Regions = GetVideoRegionsViaCom();
                        _smartModeState.SlideIndex = _slideShowState.CurrentSlideShowPosition;
                        if (_smartModeState.Regions != null)
                            LogHelper.WriteLogToFile($"[SmartMode] COM 回退获取到 {_smartModeState.Regions.Count} 个区域", LogHelper.LogType.Info);
                    }
                }
                else
                {
                    // COM/ROT 模式：直接获取视频区域
                    _smartModeState.Regions = GetVideoRegionsViaCom();
                    _smartModeState.SlideIndex = _slideShowState.CurrentSlideShowPosition;
                    if (_smartModeState.Regions != null && _smartModeState.Regions.Count > 0)
                        LogHelper.WriteLogToFile($"[SmartMode] COM 获取到 {_smartModeState.Regions.Count} 个区域", LogHelper.LogType.Info);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[SmartMode] 刷新失败: {ex}", LogHelper.LogType.Warning);
                _smartModeState.Regions = null;
            }

            // 将磅值坐标转换为 WPF 窗口坐标（必须在 UI 线程执行）。
            if (_slideHooks.IsOnUiThread())
            {
                _slideHooks.BuildSmartModeRects();
            }
            else
            {
                _ = _hooks.UiInvokeAsync(() => _slideHooks.BuildSmartModeRects());
            }
        }

        /// <summary>
        /// 通过 COM interop 直接从 PowerPoint 获取当前幻灯片的视频控件区域（适用于 COM/ROT 模式）。
        /// </summary>
        /// <remarks>
        /// 优先通过 _pptManager.PPTApplication 获取应用实例（ROT 模式下静态字段 pptApplication 为 null），
        /// 再通过活动演示文稿的 SlideShowWindow 定位当前幻灯片，避免依赖静态状态。
        /// </remarks>
        internal List<SmartRegion> GetVideoRegionsViaCom()
        {
            try
            {
                // 优先使用管理器持有的 COM 实例（ROT 模式下静态字段 pptApplication 为 null）。
                object appObj = _getPptManager()?.PPTApplication ?? _interopState.PptApplication;
                if (appObj == null)
                {
                    LogHelper.WriteLogToFile("[SmartMode] COM 获取失败: 未找到 PowerPoint 应用程序实例", LogHelper.LogType.Warning);
                    return null;
                }

                dynamic app = appObj;

                Microsoft.Office.Interop.PowerPoint.Presentation pres = null;
                try { pres = app.ActivePresentation as Microsoft.Office.Interop.PowerPoint.Presentation; } catch { return null; }
                if (pres == null) return null;

                Microsoft.Office.Interop.PowerPoint.SlideShowWindow ssw = null;
                try { ssw = pres.SlideShowWindow; } catch { return null; }
                if (ssw == null) return null;

                dynamic view = null;
                try { view = ssw.View; } catch { return null; }
                if (view == null) return null;

                Microsoft.Office.Interop.PowerPoint.Slide slide = null;
                try { slide = view.Slide as Microsoft.Office.Interop.PowerPoint.Slide; } catch { return null; }
                if (slide == null) return null;

                float slideWidth = pres.PageSetup.SlideWidth;
                float slideHeight = pres.PageSetup.SlideHeight;

                _smartModeState.SlideWidth = slideWidth;
                _smartModeState.SlideHeight = slideHeight;
                // 优先使用放映窗口自身的 HWND，避免 FindWindow 命中陈旧窗口
                try { _smartModeState.SlideShowHwnd = new IntPtr(ssw.HWND); } catch { _smartModeState.SlideShowHwnd = IntPtr.Zero; }
                if (_smartModeState.SlideShowHwnd == IntPtr.Zero)
                {
                    _smartModeState.SlideShowHwnd = FindActiveScreenClassWindow();
                }

                var regions = new List<SmartRegion>();
                foreach (Microsoft.Office.Interop.PowerPoint.Shape shape in slide.Shapes)
                {
                    if (!IsVideoShape(shape)) continue;
                    try
                    {
                        regions.Add(new SmartRegion
                        {
                            X = shape.Left,
                            Y = shape.Top,
                            Width = shape.Width,
                            Height = shape.Height,
                            ShapeName = shape.Name,
                            MediaType = (int)shape.MediaType
                        });
                    }
                    catch (Exception ex) { LogService.LogException(ex); }
                }
                return regions;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[SmartMode] COM 获取失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        // ---- 增强预览缓存管线 ----

        /// <summary>直接同步回调的 IProgress 实现，不走 SynchronizationContext。</summary>
        private sealed class DirectProgress : IProgress<double>
        {
            private readonly Action<double> _handler;
            public DirectProgress(Action<double> handler) => _handler = handler;
            public void Report(double value) => _handler(value);
        }

        internal CancellationToken EnsurePPTEnhancedPreviewCacheToken()
        {
            lock (_pptEnhancedPreviewCacheLock)
            {
                if (_pptEnhancedPreviewCacheCts == null)
                    _pptEnhancedPreviewCacheCts = new CancellationTokenSource();

                return _pptEnhancedPreviewCacheCts.Token;
            }
        }

        internal void ResetPPTEnhancedPreviewCache()
        {
            CancellationTokenSource ctsToCancel = null;
            List<PPTEnhancedPreviewItem> cacheToDispose = null;

            lock (_pptEnhancedPreviewCacheLock)
            {
                _pptEnhancedPreviewCacheGeneration++;
                ctsToCancel = _pptEnhancedPreviewCacheCts;
                _pptEnhancedPreviewCacheCts = null;
                _pptEnhancedPreviewBuildTask = null;
                cacheToDispose = _pptEnhancedPreviewCache;
                _pptEnhancedPreviewCache = null;
            }

            try
            {
                ctsToCancel?.Cancel();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            try
            {
                ctsToCancel?.Dispose();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex);
            }

            DisposePPTEnhancedPreviewItems(cacheToDispose);
        }

        internal void SchedulePPTEnhancedPreviewPreload()
        {
            if (!SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview) return;
            if (_getPptManager()?.IsConnected != true) return;

            var token = EnsurePPTEnhancedPreviewCacheToken();
            _ = PreloadPPTEnhancedPreviewAfterDelayAsync(token);
        }

        internal async Task PreloadPPTEnhancedPreviewAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(PPTEnhancedPreviewPreloadDelayMs, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                if (_getPptManager()?.IsConnected != true) return;
                if (!SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview) return;

                if (SettingsManager.Settings.PowerPointSettings.ShowPPTEnhancedPreviewLoadingAnimation)
                {
                    await _hooks.UiInvokeAsync(() =>
                    {
                        _pptLoadingProgress = 0;
                        _pptLoadingRing.Data = Geometry.Empty;
                        _pptLoadingArcRotate.Angle = 0;
                        _pptEnhancedPreviewLoadingOverlay.Visibility = Visibility.Visible;
                        _hooks.StartLoadingAnimationTimer(() =>
                        {
                            _pptLoadingArcRotate.Angle = (_pptLoadingArcRotate.Angle + 8) % 360;
                            UpdatePPTLoadingRing();
                        });
                    });
                }

                // 不用 Progress<double>（会走 SynchronizationContext 合并），直接同步调用
                IProgress<double> progress = new DirectProgress(v =>
                {
                    _pptLoadingProgress = v;
                    _hooks.UiInvokeSync(() => UpdatePPTLoadingRing());
                });
                var slides = await GetOrBuildPPTEnhancedPreviewItemsAsync(cancellationToken, progress);
                if (!cancellationToken.IsCancellationRequested && slides != null && slides.Count > 0)
                {
                    LogHelper.WriteLogToFile($"PPT enhanced preview preloaded {slides.Count} thumbnails.", LogHelper.LogType.Trace);
                }
            }
            catch (OperationCanceledException ex)
            {
                LogService.LogException(ex);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT enhanced preview preload failed: {ex}", LogHelper.LogType.Warning);
            }
            finally
            {
                await _hooks.UiInvokeAsync(() =>
                {
                    _hooks.StopLoadingAnimationTimer();
                    _pptEnhancedPreviewLoadingOverlay.Visibility = Visibility.Collapsed;
                });
            }
        }

        internal Task<List<PPTEnhancedPreviewItem>> GetOrBuildPPTEnhancedPreviewItemsAsync(CancellationToken cancellationToken, IProgress<double> progress = null)
        {
            lock (_pptEnhancedPreviewCacheLock)
            {
                if (_pptEnhancedPreviewCache != null && _pptEnhancedPreviewCache.Count > 0)
                    return Task.FromResult(_pptEnhancedPreviewCache);

                if (_pptEnhancedPreviewBuildTask != null && !_pptEnhancedPreviewBuildTask.IsCompleted)
                    return _pptEnhancedPreviewBuildTask;

                int generation = _pptEnhancedPreviewCacheGeneration;
                var task = RunOnStaAsync(() => BuildPPTPreviewItems(cancellationToken, progress), cancellationToken);
                _pptEnhancedPreviewBuildTask = task;

                task.ContinueWith(
                    completedTask => StorePPTEnhancedPreviewBuildResult(completedTask, generation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return task;
            }
        }

        internal void StorePPTEnhancedPreviewBuildResult(Task<List<PPTEnhancedPreviewItem>> task, int generation)
        {
            List<PPTEnhancedPreviewItem> itemsToDispose = null;

            if (task.IsFaulted)
            {
                LogHelper.WriteLogToFile($"PPT enhanced preview build failed: {task.Exception?.GetBaseException()}", LogHelper.LogType.Warning);
            }

            lock (_pptEnhancedPreviewCacheLock)
            {
                if (ReferenceEquals(_pptEnhancedPreviewBuildTask, task))
                    _pptEnhancedPreviewBuildTask = null;

                // 用 IsCompletedSuccessfully 而非 Status == RanToCompletion：前者排除 Canceled，
                // 后者会读取 task.Result，对 Canceled 任务会再次抛 OperationCanceledException。
                if (task.IsCompletedSuccessfully)
                {
                    var result = task.Result;
                    if (generation == _pptEnhancedPreviewCacheGeneration && result != null && result.Count > 0)
                    {
                        itemsToDispose = _pptEnhancedPreviewCache;
                        _pptEnhancedPreviewCache = result;
                    }
                    else
                    {
                        itemsToDispose = result;
                    }
                }
            }

            DisposePPTEnhancedPreviewItems(itemsToDispose);
        }

        internal List<PPTEnhancedPreviewItem> BuildPPTPreviewItems(CancellationToken cancellationToken, IProgress<double> progress = null)
        {
            var result = new List<PPTEnhancedPreviewItem>();

            try
            {
                // 第 1 步：COM 导出缩略图 → 进度 0~70%
                var exportProgress = progress != null
                    ? new Progress<double>(p => progress.Report(p * 0.7))
                    : null;
                var thumbnails = _getPptManager()?.ExportSlideThumbnails(480, 270, exportProgress);
                if (thumbnails == null || thumbnails.Count == 0) return result;

                // 第 2 步：转换为 BitmapImage → 进度 70~100%
                int total = thumbnails.Count;
                int converted = 0;
                foreach (var thumbnail in thumbnails)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (thumbnail?.PngBytes == null || thumbnail.PngBytes.Length == 0) { converted++; continue; }

                    var thumbnailStream = new MemoryStream(thumbnail.PngBytes, false);
                    var image = LoadBitmapImage(thumbnailStream);
                    if (image == null)
                    {
                        thumbnailStream.Dispose();
                        converted++;
                        continue;
                    }

                    thumbnailStream.Position = 0;
                    result.Add(new PPTEnhancedPreviewItem
                    {
                        SlideNumber = thumbnail.SlideNumber,
                        ThumbnailStream = thumbnailStream,
                        Thumbnail = image
                    });
                    converted++;
                    progress?.Report(0.7 + 0.3 * converted / total);
                }
            }
            catch (OperationCanceledException)
            {
                DisposePPTEnhancedPreviewItems(result);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"构建PPT增强预览列表失败: {ex}", LogHelper.LogType.Error);
            }

            return result;
        }

        internal void UpdatePPTLoadingRing()
        {
            if (_pptLoadingRing == null) return;
            double progress = Math.Max(0, Math.Min(1, _pptLoadingProgress));

            // 像素坐标：Path Stretch="None"，Grid 64×64，StrokeThickness=4
            const double cx = 32, cy = 32, r = 30;
            const double startAngle = -Math.PI / 2; // 从 12 点方向开始

            if (progress <= 0.001)
            {
                _pptLoadingRing.Data = Geometry.Empty;
                return;
            }

            double sweepAngle = progress * 2 * Math.PI;
            double sx = cx + r * Math.Cos(startAngle);
            double sy = cy + r * Math.Sin(startAngle);

            var figure = new PathFigure { StartPoint = new System.Windows.Point(sx, sy) };

            if (sweepAngle >= 2 * Math.PI - 0.01)
            {
                // 满环：两段 180° 弧
                figure.Segments.Add(new ArcSegment(
                    new System.Windows.Point(cx, cy + r),
                    new Size(r, r), 0, false, SweepDirection.Clockwise, true));
                figure.Segments.Add(new ArcSegment(
                    new System.Windows.Point(sx, sy),
                    new Size(r, r), 0, false, SweepDirection.Clockwise, true));
            }
            else
            {
                double endAngle = startAngle + sweepAngle;
                figure.Segments.Add(new ArcSegment(
                    new System.Windows.Point(cx + r * Math.Cos(endAngle), cy + r * Math.Sin(endAngle)),
                    new Size(r, r), 0, sweepAngle > Math.PI, SweepDirection.Clockwise, true));
            }

            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            _pptLoadingRing.Data = geom;
        }

        // ---- 长按翻页（定时器本体由主窗口持有，经 hooks 驱动）----

        /// <summary>
        /// 启动长按检测
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="isNext">是否为下一页按钮</param>
        internal void StartLongPressDetection(object sender, bool isNext)
        {
            if (!SettingsManager.Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _isLongPressNext = isNext;
            // 重置定时器间隔为初始延迟时间，确保每次长按检测都从正确的延迟开始
            _hooks.StartLongPressTimer(LongPressDelay);
        }

        /// <summary>
        /// 停止长按检测
        /// </summary>
        internal void StopLongPressDetection()
        {
            _hooks.StopLongPressTimer();
        }

        /// <summary>
        /// 长按定时器事件处理
        /// </summary>
        internal void OnLongPressTimerTick(object sender, EventArgs e)
        {
            if (!SettingsManager.Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _hooks.SetLongPressTimerInterval(LongPressInterval);

            // 执行翻页
            if (_isLongPressNext)
            {
                _hooks.TurnToNextSlide();
            }
            else
            {
                _hooks.TurnToPreviousSlide();
            }
        }

        // ---- 释放路径 ----

        /// <summary>
        /// 释放并停止所有与 PowerPoint 集成相关的管理器与资源，恢复和清理应用的 PPT 相关运行状态。
        /// </summary>
        /// <remarks>
        /// 操作包括停止并释放 PPT 管理器、墨迹管理器和长按计时器，停止 PowerPoint 进程监控，关闭 PowerPoint 应用并清除静态 COM/互操作状态；所有异常会被捕获并记录为错误日志。
        /// 对主窗口私有成员的处置经 PptHostHooks 回调完成，编排顺序与原实现逐项一致。
        /// </remarks>
        internal void DisposePPTManagers(bool isShutdown = false)
        {
            try
            {
                _hooks.DisposePptLinkManager(isShutdown);
                _hooks.DisposePptInkManager();
                _hooks.DisposeLongPressTimer();
                _hooks.ClearPptUiManager();

                StopPowerPointProcessMonitoring(isShutdown);
                _hooks.ClearProcessMonitorTimer();

                ClearStaticInteropState();

                _hooks.StopPptOnlyVisibilityProbeTimer();

                LogHelper.WriteLogToFile("[PPT] PPT 管理器已释放", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放PPT管理器失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 关机时卸载 PPT 模块（自主窗口迁入）。UI 线程探测（判空/CheckAccess/关闭状态）与 Send 优先级同步调度
        /// 均经 hooks 回主窗口执行，判空与分支顺序与原实现逐字对应。
        /// </summary>
        internal void UnloadPPTModuleForShutdown()
        {
            LogHelper.WriteLogToFile("[PPT] 开始执行关机清理", LogHelper.LogType.Info);
            try
            {
                try
                {
                    _hooks.StopAllTimersForShutdown();
                    LogHelper.WriteLogToFile("[PPT] 关机时已停止所有相关定时器", LogHelper.LogType.Info);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"关机时停止定时器失败: {ex}", LogHelper.LogType.Warning);
                }

                // 再处理需要 UI 线程调度或 COM 的清理操作
                if (_hooks.CanRunShutdownCleanupInline())
                {
                    DisposePPTManagers(isShutdown: true);
                    LogHelper.WriteLogToFile("[PPT] 关机清理完成", LogHelper.LogType.Info);
                    return;
                }

                if (_hooks.IsUiSchedulerShutdown())
                {
                    // Services/ 内零 UI 调度器直引，字符串内亦不出现该类型名，日志措辞微调，语义不变。
                    LogHelper.WriteLogToFile("关机时 UI 线程调度器已关闭，跳过需要 UI 线程的清理操作", LogHelper.LogType.Warning);
                    return;
                }

                _hooks.UiInvokeSend(() =>
                {
                    DisposePPTManagers(isShutdown: true);
                    LogHelper.WriteLogToFile("[PPT] 关机清理完成", LogHelper.LogType.Info);
                });
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.WriteLogToFile($"关机时卸载PPT模块被取消: {ex.Message}", LogHelper.LogType.Warning);
            }
            catch (ObjectDisposedException ex)
            {
                LogHelper.WriteLogToFile($"关机时卸载PPT模块对象已释放: {ex.Message}", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关机时卸载PPT模块失败: {ex}", LogHelper.LogType.Error);
            }
        }
    }
}
