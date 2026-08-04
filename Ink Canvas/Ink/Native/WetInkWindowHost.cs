using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class WetInkWindowHost : IDisposable
    {
        private const string WindowClassName = "InkCanvasForClass.WetInkOverlay.v1";
        private const int WmNcHitTest = 0x0084;
        private const int WmDestroy = 0x0002;
        private const int HtTransparent = -1;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoRedirectionBitmap = 0x00200000;
        private const int GwlpWndProc = -4;
        private const int ErrorClassAlreadyExists = 1410;
        // 不使用 ShowWindow/HideWindow：SWP_HIDEWINDOW 会触发 DWM 合成重排，
        // 湿转干交接时整屏会闪一下。改成始终保持 WS_VISIBLE，无湿墨时移到屏外。
        private const int HiddenPosition = -100000;

        private static readonly object ClassSync = new object();
        private static bool _classRegistered;
        private static WndProcDelegate _classWndProc;

        private readonly WetInkCommandMailbox _mailbox;
        private readonly Action<WetInkRetirementAck> _onRetired;
        private readonly Action<Exception> _onFatalError;
        private readonly Action _onDeviceLost;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _threadReady = new ManualResetEventSlim(false);
        private readonly object _targetSync = new object();
        private readonly Dictionary<long, long> _telemetryLastRealTimestampBySession = new Dictionary<long, long>();

        private IntPtr _ownerHwnd;
        private IntPtr _overlayHwnd;
        private Thread _renderThread;
        private WetInkTargetSnapshot _pendingTarget;
        private bool _targetDirty;
        private bool _shutdownRequested;
        private bool _disposed;
        private bool _overlayShouldBeVisible;
        private Exception _threadStartError;
        private IWetInkBatchRenderer _renderer;

        public WetInkWindowHost(
            IntPtr ownerHwnd,
            WetInkCommandMailbox mailbox,
            Action<WetInkRetirementAck> onRetired,
            Action onDeviceLost = null,
            Action<Exception> onFatalError = null)
        {
            if (ownerHwnd == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(ownerHwnd));
            _ownerHwnd = ownerHwnd;
            _mailbox = mailbox ?? throw new ArgumentNullException(nameof(mailbox));
            _onRetired = onRetired ?? throw new ArgumentNullException(nameof(onRetired));
            _onDeviceLost = onDeviceLost;
            _onFatalError = onFatalError;
        }

        public IntPtr OverlayHandle => _overlayHwnd;

        public bool IsRunning =>
            _renderThread != null
            && _renderThread.IsAlive
            && _overlayHwnd != IntPtr.Zero
            && !_shutdownRequested;

        public void Start(WetInkTargetSnapshot initialTarget)
        {
            EnsureNotDisposed();
            if (initialTarget == null)
                throw new ArgumentNullException(nameof(initialTarget));
            if (_renderThread != null)
                throw new InvalidOperationException("The wet ink window host is already started.");

            EnsureWindowClassRegistered();
            CreateOverlayWindow(initialTarget);
            lock (_targetSync)
            {
                _pendingTarget = initialTarget;
                _targetDirty = true;
            }

            _shutdownRequested = false;
            _threadReady.Reset();
            _renderThread = new Thread(RenderThreadMain)
            {
                IsBackground = true,
                Name = "ICC-WetInk-DComp"
            };
            _renderThread.SetApartmentState(ApartmentState.MTA);
            _renderThread.Start();

            if (!_threadReady.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The wet ink render thread failed to start.");
            if (_threadStartError != null)
                throw new InvalidOperationException(
                    "The wet ink renderer failed to initialize.",
                    _threadStartError);
        }

        public void UpdateTarget(WetInkTargetSnapshot target)
        {
            EnsureNotDisposed();
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            lock (_targetSync)
            {
                _pendingTarget = target;
                _targetDirty = true;
            }

            RepositionOverlay(target);
            SignalWork();
        }

        /// <summary>
        /// Places the overlay over the main window when wet ink is live, otherwise
        /// parks it far off-screen. Avoids ShowWindow/HideWindow so DWM does not
        /// flash during wet→dry handoff.
        /// </summary>
        public void SetOverlayVisible(bool visible)
        {
            _overlayShouldBeVisible = visible;
            if (_disposed || _overlayHwnd == IntPtr.Zero || _shutdownRequested)
                return;

            ApplyOverlayVisibility();
        }

        private void ApplyOverlayVisibility()
        {
            WetInkTargetSnapshot target;
            lock (_targetSync)
            {
                target = _pendingTarget;
            }

            if (target == null)
                return;

            PlaceOverlay(target);
        }

        private void PlaceOverlay(WetInkTargetSnapshot target)
        {
            if (_overlayHwnd == IntPtr.Zero || target == null)
                return;

            var bounds = target.ScreenBounds;
            var shouldShowOnScreen = _overlayShouldBeVisible
                                     && target.IsVisible
                                     && !bounds.IsEmpty;
            var x = shouldShowOnScreen ? bounds.X : HiddenPosition;
            var y = shouldShowOnScreen ? bounds.Y : HiddenPosition;
            var width = Math.Max(1, bounds.Width);
            var height = Math.Max(1, bounds.Height);

            // Always keep the HWND visible (WS_VISIBLE) so Present/DComp stay warm;
            // only the screen position changes.
            PInvoke.SetWindowPos(
                new HWND(_overlayHwnd),
                HWND.Null,
                x,
                y,
                width,
                height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        }

        public void SignalWork()
        {
            if (_disposed || _shutdownRequested)
                return;
            _workEvent.Set();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _shutdownRequested = true;

            try
            {
                _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                    WetInkBoundaryCommandKind.Shutdown,
                    0));
            }
            catch
            {
                // best-effort shutdown signal
            }

            _workEvent.Set();
            if (_renderThread != null && _renderThread.IsAlive)
                _renderThread.Join(TimeSpan.FromSeconds(3));

            DestroyOverlayWindow();
            _workEvent.Dispose();
            _threadReady.Dispose();
        }

        private void RenderThreadMain()
        {
            try
            {
                _renderer = new DirectCompositionInkRenderer();
                WetInkTargetSnapshot target;
                lock (_targetSync)
                {
                    target = _pendingTarget;
                    _targetDirty = false;
                }

                _renderer.BindTarget(_overlayHwnd, target);
                _threadStartError = null;
            }
            catch (Exception ex)
            {
                _threadStartError = ex;
                _threadReady.Set();
                RaiseFatal(ex);
                return;
            }

            _threadReady.Set();

            try
            {
                while (!_shutdownRequested)
                {
                    // Short wait so the thread wakes quickly after SignalWork and
                    // presents the first wet-ink point with minimal pen-down latency.
                    _workEvent.WaitOne(1);
                    PumpOnce();
                }

                PumpOnce();
            }
            catch (Exception ex)
            {
                RaiseFatal(ex);
            }
            finally
            {
                if (_renderer != null)
                {
                    try { _renderer.Dispose(); }
                    catch { /* best-effort */ }
                    _renderer = null;
                }
            }
        }

        private void PumpOnce()
        {
            if (_renderer == null)
                return;

            WetInkTargetSnapshot target = null;
            var targetDirty = false;
            lock (_targetSync)
            {
                if (_targetDirty)
                {
                    target = _pendingTarget;
                    _targetDirty = false;
                    targetDirty = true;
                }
            }

            if (targetDirty && target != null)
            {
                try
                {
                    _renderer.UpdateTarget(target);
                }
                catch (Exception ex)
                {
                    HandleApplyFailure(WetInkApplyResult.Failed(ex));
                    return;
                }
            }

            WetInkMailboxBatch batch;
            try
            {
                batch = _mailbox.Drain();
            }
            catch (Exception ex)
            {
                RaiseFatal(ex);
                return;
            }

            if ((batch == null
                    || (batch.OrderedItems.Count == 0
                        && batch.BoundaryCommands.Count == 0
                        && batch.RenderSnapshots.Count == 0))
                && !targetDirty)
            {
                return;
            }

            WetInkApplyResult result;
            var applyStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                result = _renderer.Apply(batch);
                // 新湿墨实时渲染完成一帧：沿用 NativePointerInputSource 提供的微秒时间戳，
                // 仅按本次提交相对上次已提交快照真正新增的 delta 样本计算 earliest/latest，
                // 避免把整笔首点误当作当前帧的最早样本。
                long earliestSampleAtTicks = 0L;
                long latestSampleAtTicks = 0L;
                if (batch != null && batch.OrderedItems.Count > 0)
                {
                    long earliestMicroseconds = long.MaxValue;
                    long latestMicroseconds = 0;
                    for (var i = 0; i < batch.OrderedItems.Count; i++)
                    {
                        var item = batch.OrderedItems[i];
                        if (item.Kind == WetInkMailboxItemKind.Boundary)
                        {
                            var command = item.BoundaryCommand;
                            if (command.Kind == WetInkBoundaryCommandKind.BeginStroke)
                            {
                                _telemetryLastRealTimestampBySession.Remove(command.SessionId);
                            }
                            else if (command.Kind == WetInkBoundaryCommandKind.CancelStroke
                                || command.Kind == WetInkBoundaryCommandKind.RetireStroke
                                || command.Kind == WetInkBoundaryCommandKind.Reset
                                || command.Kind == WetInkBoundaryCommandKind.Shutdown)
                            {
                                _telemetryLastRealTimestampBySession.Remove(command.SessionId);
                            }
                            continue;
                        }

                        var snapshot = item.RenderSnapshot;
                        var realPoints = snapshot?.RealPoints;
                        if (realPoints == null || realPoints.Count == 0)
                            continue;

                        _telemetryLastRealTimestampBySession.TryGetValue(snapshot.SessionId, out var lastCommittedMicroseconds);
                        long sessionEarliestDelta = long.MaxValue;
                        long sessionLatestDelta = 0;
                        for (var j = 0; j < realPoints.Count; j++)
                        {
                            var timestampMicroseconds = realPoints[j].TimestampMicroseconds;
                            if (timestampMicroseconds <= lastCommittedMicroseconds)
                                continue;
                            if (timestampMicroseconds < sessionEarliestDelta)
                                sessionEarliestDelta = timestampMicroseconds;
                            if (timestampMicroseconds > sessionLatestDelta)
                                sessionLatestDelta = timestampMicroseconds;
                        }

                        var currentLastMicroseconds = realPoints[realPoints.Count - 1].TimestampMicroseconds;
                        _telemetryLastRealTimestampBySession[snapshot.SessionId] = currentLastMicroseconds;

                        if (sessionEarliestDelta == long.MaxValue)
                            continue;
                        if (sessionEarliestDelta < earliestMicroseconds)
                            earliestMicroseconds = sessionEarliestDelta;
                        if (sessionLatestDelta > latestMicroseconds)
                            latestMicroseconds = sessionLatestDelta;
                    }

                    if (earliestMicroseconds != long.MaxValue)
                        earliestSampleAtTicks = earliestMicroseconds * Stopwatch.Frequency / 1_000_000L;
                    if (latestMicroseconds > 0)
                        latestSampleAtTicks = latestMicroseconds * Stopwatch.Frequency / 1_000_000L;
                }
                InkPerformanceMonitor.RecordFrame(new InkFrameSample
                {
                    EarliestSampleAtTicks = earliestSampleAtTicks,
                    LatestSampleAtTicks = latestSampleAtTicks,
                    SubmittedAtTicks = Stopwatch.GetTimestamp()
                });
            }
            catch (Exception ex)
            {
                result = WetInkApplyResult.Failed(ex);
            }

            // 记录 Apply 耗时（毫秒）+ 本批样本数/session 数，供 RealtimeInkDebugLive.json 输出。
            try
            {
                var applyElapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - applyStartTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var sampleCount = batch?.OrderedItems?.Count ?? 0;
                var sessionCount = batch?.RenderSnapshots?.Count ?? 0;
                NativeInkPerfProbe.RecordApply(applyElapsedMs, sampleCount, sessionCount);

                // Render 端按 inputKind 累计 redraw：Snapshot 不携带 InputKind，
                // 兜底按 Mouse 桶（多数场景都是 Mouse/Touch Ink）。inputKind 精确分桶
                // 靠 UI 线程 RecordInputEvent 拿到 batch.InputKind。
                NativeInkPerfProbe.RecordRedraw(2, applyElapsedMs, forceRedraw: false, committed: false);
            }
            catch
            {
                // never throw from the probe path
            }

            // 实时刷新 Live JSON（Debug log 开启时），让用户能看到 new 帧的 nativeWetInk 指标。
            Helpers.RealtimeInkPerformanceMonitor.TickLiveStatus();

            HandleApplyResult(result);
        }

        private void HandleApplyResult(WetInkApplyResult result)
        {
            if (result == null)
                return;

            if (result.Status == WetInkApplyStatus.DeviceLost
                || result.Status == WetInkApplyStatus.Failed)
            {
                HandleApplyFailure(result);
                return;
            }

            if (result.RetirementAcks == null)
                return;

            for (var i = 0; i < result.RetirementAcks.Count; i++)
            {
                var ack = result.RetirementAcks[i];
                try { _onRetired(ack); }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[WetInk] Retirement ack callback failed: {ex}",
                        LogHelper.LogType.Error);
                }
            }
        }

        private void HandleApplyFailure(WetInkApplyResult result)
        {
            if (result.Status == WetInkApplyStatus.DeviceLost)
            {
                try { _onDeviceLost?.Invoke(); }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[WetInk] Device-lost callback failed: {ex}",
                        LogHelper.LogType.Error);
                }
            }

            if (result.Error != null)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Renderer apply failed ({result.Status}): {result.Error}",
                    LogHelper.LogType.Error);
            }
        }

        private void RaiseFatal(Exception ex)
        {
            LogHelper.WriteLogToFile(
                $"[WetInk] Render thread fatal error: {ex}",
                LogHelper.LogType.Error);
            try { _onFatalError?.Invoke(ex); }
            catch
            {
                // never throw from the render thread callback path
            }
        }

        private void CreateOverlayWindow(WetInkTargetSnapshot target)
        {
            var bounds = target.ScreenBounds;
            // Always create WS_VISIBLE so DComp swapchain stays attached; park
            // off-screen until the first wet stroke begins.
            var style = WsPopup | WsVisible;
            var exStyle = WsExNoActivate
                | WsExTransparent
                | WsExToolWindow
                | WsExNoRedirectionBitmap;

            _overlayHwnd = CreateWindowEx(
                exStyle,
                WindowClassName,
                "ICC Wet Ink Overlay",
                style,
                HiddenPosition,
                HiddenPosition,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                _ownerHwnd,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (_overlayHwnd == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed for wet ink overlay.");

            PlaceOverlay(target);
        }

        private void RepositionOverlay(WetInkTargetSnapshot target)
        {
            PlaceOverlay(target);
        }

        private void DestroyOverlayWindow()
        {
            if (_overlayHwnd == IntPtr.Zero)
                return;
            var hwnd = _overlayHwnd;
            _overlayHwnd = IntPtr.Zero;
            DestroyWindow(hwnd);
        }

        private static void EnsureWindowClassRegistered()
        {
            lock (ClassSync)
            {
                if (_classRegistered)
                    return;

                _classWndProc = OverlayWndProc;
                var wndClass = new WndClassEx
                {
                    cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_classWndProc),
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = GetModuleHandle(null),
                    hIcon = IntPtr.Zero,
                    hCursor = IntPtr.Zero,
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = WindowClassName,
                    hIconSm = IntPtr.Zero
                };

                var atom = RegisterClassEx(ref wndClass);
                if (atom == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != ErrorClassAlreadyExists)
                        throw new Win32Exception(error, "RegisterClassEx failed for wet ink overlay.");
                }

                _classRegistered = true;
            }
        }

        private static IntPtr OverlayWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmNcHitTest)
                return new IntPtr(HtTransparent);
            if (msg == WmDestroy)
                return IntPtr.Zero;
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WetInkWindowHost));
        }

        private delegate IntPtr WndProcDelegate(
            IntPtr hwnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
