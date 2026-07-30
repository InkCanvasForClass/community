using Ink_Canvas.Helpers;
using Ink_Canvas.Ink.Native;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private WetInkCommandMailbox _nativeWetInkMailbox;
        private NativeInkSessionManager _nativeInkSessions;
        private NativeInkController _nativeInkController;
        private NativePointerInputSource _nativePointerInputSource;
        private WpfPointerInputSource _wpfPointerInputSource;
        private NativePointerUpdatePump _nativePointerUpdatePump;
        private WetInkWindowHost _wetInkWindowHost;
        private WpfRenderFrameFence _wpfRenderFrameFence;

        private readonly Dictionary<uint, NativeInkRouteDecision> _nativeCapturedRoutes =
            new Dictionary<uint, NativeInkRouteDecision>();
        private readonly HashSet<uint> _nativeActiveTouchPointers = new HashSet<uint>();
        private readonly Dictionary<uint, NativeInkInputKind> _nativeHardwarePointers =
            new Dictionary<uint, NativeInkInputKind>();
        private readonly Dictionary<uint, NativeInkInputKind> _wpfFallbackPointers =
            new Dictionary<uint, NativeInkInputKind>();
        private long _lastNativePenInputTimestamp;
        private long _lastNativeTouchInputTimestamp;

        private bool _nativeWetInkStarted;
        private bool _nativeWetInkDisabled;
        private bool _nativeWetInkDeviceFailureNotified;
        private long _nativeCoordinateGeneration = 1;
        private EventHandler _nativeWetInkLocationChangedHandler;
        private DependencyPropertyChangedEventHandler _nativeWetInkIsVisibleChangedHandler;
        private readonly Dictionary<long, DispatcherTimer> _nativePauseStraightenTimers =
            new Dictionary<long, DispatcherTimer>();

        private void TryStartNativeWetInkPipeline()
        {
            if (Settings?.Canvas?.UseLegacyWetInk == true)
            {
                LogHelper.WriteLogToFile(
                    "[WetInk] Legacy WPF wet-ink input system is enabled; native pipeline skipped.",
                    LogHelper.LogType.Event);
                return;
            }

            if (_nativeWetInkStarted || _nativeWetInkDisabled)
                return;

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                var hwndSource = HwndSource.FromHwnd(hwnd);
                if (hwndSource == null)
                    return;

                var dpi = GetNativeDpiScales();
                _nativeWetInkMailbox = new WetInkCommandMailbox();
                _nativeInkSessions = new NativeInkSessionManager();
                _nativeInkController = new NativeInkController(_nativeInkSessions, _nativeWetInkMailbox);
                _nativePointerUpdatePump = new NativePointerUpdatePump(
                    _nativeInkController,
                    () => _wetInkWindowHost?.SignalWork());
                _wpfRenderFrameFence = new WpfRenderFrameFence(Dispatcher);

                _wetInkWindowHost = new WetInkWindowHost(
                    hwnd,
                    _nativeWetInkMailbox,
                    OnNativeWetInkRetired,
                    OnNativeWetInkDeviceLost,
                    OnNativeWetInkFatalError);
                _wetInkWindowHost.Start(BuildWetInkTargetSnapshot());

                _nativePointerInputSource = new NativePointerInputSource(
                    hwndSource,
                    OnNativePointerInput,
                    dpi.X,
                    dpi.Y);
                // Legacy WPF stylus stack can consume pen/touch before WM_POINTER reaches
                // the HWND (common on touch films). Bridge those samples into the same
                // native controller; no legacy wet renderer is re-enabled.
                _wpfPointerInputSource = new WpfPointerInputSource(
                    this,
                    inkCanvas,
                    OnNativePointerInput);

                WireNativeWetInkGeometryListeners();
                EnsureNativePenPhysicalEditingMode();
                _nativeWetInkStarted = true;
                // The overlay window is created visible; hide it until there is
                // actually wet ink to render so it never blocks input in cursor mode.
                RefreshOverlayVisibility();
                LogHelper.WriteLogToFile(
                    "[WetInk] Native WM_POINTER + DirectComposition wet-ink pipeline started.",
                    LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Failed to start native wet-ink pipeline: {ex}",
                    LogHelper.LogType.Error);
                DisableNativeWetInkAfterFailure(ex, notify: true);
            }
        }

        /// <summary>
        /// 原生湿墨迹管线是否可用（已启动且未被禁用）。
        /// SetCurrentToolMode / EnsureNativePenPhysicalEditingMode 据此决定是否将 Ink→None 映射；
        /// 管线不可用时应保持 WPF 内置 Ink 收集，避免"UI 显示笔但无法绘制"。
        /// </summary>
        private bool IsNativeWetInkPipelineAvailable => _nativeWetInkStarted && !_nativeWetInkDisabled;

        private void ShutdownNativeWetInkPipeline()
        {
            UnwireNativeWetInkGeometryListeners();

            try { _wpfRenderFrameFence?.CancelAll(); }
            catch { /* best-effort */ }

            try { _nativeInkController?.CancelAll(); }
            catch { /* best-effort */ }

            try { _wetInkWindowHost?.SignalWork(); }
            catch { /* best-effort */ }

            try { _wpfPointerInputSource?.Dispose(); }
            catch { /* best-effort */ }
            _wpfPointerInputSource = null;

            try { _nativePointerUpdatePump?.FlushAll(); }
            catch { /* best-effort */ }
            try { _nativePointerUpdatePump?.Dispose(); }
            catch { /* best-effort */ }
            _nativePointerUpdatePump = null;

            try { _nativePointerInputSource?.Dispose(); }
            catch { /* best-effort */ }
            _nativePointerInputSource = null;

            try { _wetInkWindowHost?.Dispose(); }
            catch { /* best-effort */ }
            _wetInkWindowHost = null;

            try { _wpfRenderFrameFence?.Dispose(); }
            catch { /* best-effort */ }
            _wpfRenderFrameFence = null;

            _nativeInkController = null;
            _nativeInkSessions = null;
            _nativeWetInkMailbox = null;
            _nativeCapturedRoutes.Clear();
            _nativeActiveTouchPointers.Clear();
            _nativeHardwarePointers.Clear();
            _wpfFallbackPointers.Clear();
            _nativeWetInkStarted = false;
        }

        private void WireNativeWetInkGeometryListeners()
        {
            if (_nativeWetInkLocationChangedHandler == null)
            {
                _nativeWetInkLocationChangedHandler = (_, __) => UpdateNativeWetInkTarget();
                LocationChanged += _nativeWetInkLocationChangedHandler;
            }

            if (_nativeWetInkIsVisibleChangedHandler == null)
            {
                _nativeWetInkIsVisibleChangedHandler = (_, __) => UpdateNativeWetInkTarget();
                IsVisibleChanged += _nativeWetInkIsVisibleChangedHandler;
            }

            StateChanged -= NativeWetInk_StateChanged;
            StateChanged += NativeWetInk_StateChanged;
        }

        private void UnwireNativeWetInkGeometryListeners()
        {
            if (_nativeWetInkLocationChangedHandler != null)
            {
                LocationChanged -= _nativeWetInkLocationChangedHandler;
                _nativeWetInkLocationChangedHandler = null;
            }

            if (_nativeWetInkIsVisibleChangedHandler != null)
            {
                IsVisibleChanged -= _nativeWetInkIsVisibleChangedHandler;
                _nativeWetInkIsVisibleChangedHandler = null;
            }

            StateChanged -= NativeWetInk_StateChanged;
        }

        private void NativeWetInk_StateChanged(object sender, EventArgs e)
        {
            UpdateNativeWetInkTarget();
        }

        private void UpdateNativeWetInkDpi()
        {
            if (!_nativeWetInkStarted || _nativePointerInputSource == null)
                return;

            var dpi = GetNativeDpiScales();
            try
            {
                _nativePointerInputSource.UpdateDpi(dpi.X, dpi.Y);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] UpdateDpi failed: {ex.Message}",
                    LogHelper.LogType.Warning);
            }

            _nativeCoordinateGeneration++;
            UpdateNativeWetInkTarget();
        }

        private void UpdateNativeWetInkTarget()
        {
            if (!_nativeWetInkStarted || _wetInkWindowHost == null || _nativeWetInkDisabled)
                return;

            try
            {
                _wetInkWindowHost.UpdateTarget(BuildWetInkTargetSnapshot());
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] UpdateTarget failed: {ex}",
                    LogHelper.LogType.Error);
            }
        }

        private void CancelAllNativeWetInkSessions(string reason = null)
        {
            if (_nativeInkController == null)
                return;

            try
            {
                _wpfRenderFrameFence?.CancelAll();
                _nativePointerUpdatePump?.DiscardAll();
                _nativePointerUpdatePump?.FlushAll();
                _nativeInkController.CancelAll();
                StopAllPauseStraightenTimers();
                RefreshOverlayVisibility();
                _wetInkWindowHost?.SignalWork();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] CancelAll failed ({reason}): {ex.Message}",
                    LogHelper.LogType.Warning);
            }

            _nativeCapturedRoutes.Clear();
            _nativeActiveTouchPointers.Clear();
        }

        private void SetOverlayVisible(bool visible)
        {
            try { _wetInkWindowHost?.SetOverlayVisible(visible); }
            catch { /* best-effort */ }
        }

        private void RefreshOverlayVisibility()
        {
            if (_nativeInkController == null)
            {
                SetOverlayVisible(false);
                return;
            }

            var visible = _nativeInkController.HasLiveWetVisual();
            SetOverlayVisible(visible);
        }

        #region Pause straightening (mid-stroke)

        private void ResetPauseStraightenTimerForPointer(uint pointerId)
        {
            if (!Settings.Canvas.PauseStraightenLine
                || _nativeInkController == null
                || !_nativeInkController.TryGetSession(pointerId, out var session)
                || session.State != NativeInkSessionState.Active)
            {
                return;
            }

            ResetPauseStraightenTimer(session.SessionId);
        }

        private void StopPauseStraightenTimerForPointer(uint pointerId)
        {
            if (_nativeInkController != null
                && _nativeInkController.TryGetSession(pointerId, out var session))
            {
                StopPauseStraightenTimer(session.SessionId);
            }
        }

        private void ResetPauseStraightenTimer(long sessionId)
        {
            if (!Settings.Canvas.PauseStraightenLine)
                return;

            if (!_nativePauseStraightenTimers.TryGetValue(sessionId, out var timer))
            {
                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(Settings.Canvas.PauseStraightenDelay)
                };
                timer.Tick += (_, __) => OnPauseStraightenTimerElapsed(sessionId);
                _nativePauseStraightenTimers[sessionId] = timer;
            }

            timer.Stop();
            timer.Start();
        }

        private void OnPauseStraightenTimerElapsed(long sessionId)
        {
            StopPauseStraightenTimer(sessionId);
            try
            {
                if (_nativeInkController != null)
                {
                    FlushNativePointerUpdateForSession(sessionId);
                    if (_nativeInkController.TryStraightenSession(sessionId))
                    {
                        _wetInkWindowHost?.SignalWork();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Pause straighten failed for session {sessionId}: {ex.Message}",
                    LogHelper.LogType.Warning);
            }
        }

        private void StopPauseStraightenTimer(long sessionId)
        {
            if (_nativePauseStraightenTimers.TryGetValue(sessionId, out var timer))
            {
                timer.Stop();
                _nativePauseStraightenTimers.Remove(sessionId);
            }
        }

        private void StopAllPauseStraightenTimers()
        {
            foreach (var timer in _nativePauseStraightenTimers.Values)
                timer.Stop();
            _nativePauseStraightenTimers.Clear();
        }

        #endregion

        private bool ShouldAcceptPointerBatch(NativePointerInputBatch batch)
        {
            if (batch == null)
                return false;
            if (batch.InputKind == NativeInkInputKind.Mouse)
                return true;

            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            ref var lastNativeTimestamp = ref batch.InputKind == NativeInkInputKind.Pen
                ? ref _lastNativePenInputTimestamp
                : ref _lastNativeTouchInputTimestamp;

            if (!batch.IsWpfFallback)
            {
                // If WPF delivered Down first, keep that source for the whole stroke;
                // switching sources mid-contact would create a duplicate / broken stroke.
                if (ContainsInputKind(_wpfFallbackPointers, batch.InputKind))
                    return false;

                lastNativeTimestamp = now;
                _nativeHardwarePointers[batch.PointerId] = batch.InputKind;
                if (batch.MessageKind == NativePointerMessageKind.Up
                    || batch.MessageKind == NativePointerMessageKind.CaptureLost)
                {
                    _nativeHardwarePointers.Remove(batch.PointerId);
                }
                return true;
            }

            // WPF legacy input is a fallback only. If the HWND is actively receiving or
            // recently received the same physical input kind, this routed event is its mirror.
            if (ContainsInputKind(_nativeHardwarePointers, batch.InputKind))
                return false;
            var elapsedMilliseconds = lastNativeTimestamp == 0
                ? double.MaxValue
                : (now - lastNativeTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds <= 100)
                return false;

            _wpfFallbackPointers[batch.PointerId] = batch.InputKind;
            if (batch.MessageKind == NativePointerMessageKind.Up
                || batch.MessageKind == NativePointerMessageKind.CaptureLost)
            {
                _wpfFallbackPointers.Remove(batch.PointerId);
            }
            return true;
        }

        private static bool ContainsInputKind(
            Dictionary<uint, NativeInkInputKind> pointers,
            NativeInkInputKind inputKind)
        {
            foreach (var pair in pointers)
            {
                if (pair.Value == inputKind)
                    return true;
            }
            return false;
        }

        private bool OnNativePointerInput(NativePointerInputBatch batch)
        {
            try
            {
                if (!ShouldAcceptPointerBatch(batch))
                    return false;

                switch (batch.MessageKind)
                {
                    case NativePointerMessageKind.Down:
                        return HandleNativePointerDown(batch);
                    case NativePointerMessageKind.Update:
                        return HandleNativePointerUpdate(batch);
                    case NativePointerMessageKind.Up:
                        return HandleNativePointerUp(batch);
                    case NativePointerMessageKind.CaptureLost:
                        return HandleNativePointerCaptureLost(batch);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Pointer handler failed ({batch.MessageKind}): {ex}",
                    LogHelper.LogType.Error);
                try { _nativePointerUpdatePump?.DiscardPointer(batch.PointerId); }
                catch { /* best-effort */ }
                try { _nativePointerUpdatePump?.FlushPointer(batch.PointerId); }
                catch { /* best-effort */ }
                try { _nativeInkController.Cancel(batch.PointerId); }
                catch { /* best-effort */ }
                _nativeCapturedRoutes.Remove(batch.PointerId);
                if (batch.InputKind == NativeInkInputKind.Touch)
                    _nativeActiveTouchPointers.Remove(batch.PointerId);
                _wetInkWindowHost?.SignalWork();
                return false;
            }
        }

        private bool HandleNativePointerDown(NativePointerInputBatch batch)
        {
            if (batch.InputKind == NativeInkInputKind.Touch)
                _nativeActiveTouchPointers.Add(batch.PointerId);

            var facts = CreatePointerFacts(batch);
            var context = BuildRouteContext(facts);
            var decision = NativeInkInputRouter.DecideDown(facts, context);
            _nativeCapturedRoutes[batch.PointerId] = decision;

            LogHelper.WriteLogToFile(
                $"[WetInk] Down id={batch.PointerId} kind={batch.InputKind} tool={context.Tool} " +
                $"hit={context.HitZone} route={decision.Route} consume={decision.ConsumeNativeMessage} " +
                $"xy=({facts.XDip:F1},{facts.YDip:F1}) samples={batch.SamplesNewestFirst.Count} " +
                $"promoted={batch.IsPromotedMouse} mode={_currentToolMode}",
                LogHelper.LogType.Event);

            if (decision.Route == NativeInputRoute.BlockedFrozen)
            {
                TryBlockFrozenPageMutation("书写");
                return decision.ConsumeNativeMessage;
            }

            if (decision.Route != NativeInputRoute.Ink || decision.SuppressPointEmission)
                return decision.ConsumeNativeMessage;

            var style = CaptureStrokeStyleSnapshot();
            var processorSettings = CaptureProcessorSettings(style);
            var startedAt = batch.SamplesNewestFirst.Count > 0
                ? batch.SamplesNewestFirst[batch.SamplesNewestFirst.Count - 1].TimestampMicroseconds
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;

            var canvasSamples = ToInkCanvasSamples(batch.SamplesNewestFirst);
            var session = _nativeInkController.Begin(
                batch.PointerId,
                batch.InputKind,
                style,
                processorSettings,
                startedAt,
                canvasSamples);

            if (session != null)
            {
                _stylusDownTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                RefreshOverlayVisibility();
                _wetInkWindowHost?.SignalWork();
                LogHelper.WriteLogToFile(
                    $"[WetInk] Begin session={session.SessionId} pointsInHistory={batch.SamplesNewestFirst.Count} " +
                    $"color=0x{style.ColorArgb:X8} w={style.Width:F2}",
                    LogHelper.LogType.Event);
            }

            return decision.ConsumeNativeMessage;
        }

        private bool HandleNativePointerUpdate(NativePointerInputBatch batch)
        {
            if (!_nativeCapturedRoutes.TryGetValue(batch.PointerId, out var captured))
                return false;

            var facts = CreatePointerFacts(batch);
            var context = BuildCapturedRouteContext(captured, batch.InputKind);
            var decision = NativeInkInputRouter.DecideCaptured(facts, context, captured);

            if (decision.Route == NativeInputRoute.BlockedFrozen
                || decision.Route == NativeInputRoute.CanvasGesture
                || HasCanceledFlag(facts))
            {
                _nativePointerUpdatePump?.DiscardPointer(batch.PointerId);
                if (_nativeInkController.Cancel(batch.PointerId))
                {
                    RefreshOverlayVisibility();
                    _wetInkWindowHost?.SignalWork();
                }
                StopPauseStraightenTimerForPointer(batch.PointerId);
                _nativeCapturedRoutes.Remove(batch.PointerId);
                if (batch.InputKind == NativeInkInputKind.Touch)
                    _nativeActiveTouchPointers.Remove(batch.PointerId);
                if (decision.Route == NativeInputRoute.BlockedFrozen)
                    TryBlockFrozenPageMutation("书写");
                return decision.ConsumeNativeMessage || captured.ConsumeNativeMessage;
            }

            if (decision.Route == NativeInputRoute.Ink && !decision.SuppressPointEmission)
            {
                var predictionEnabled = Settings?.Canvas?.EnableNativeInkPrediction == true;
                if (_nativeInkController.TryGetSessionInfo(batch.PointerId, out var sessionId, out var state)
                    && state == NativeInkSessionState.Active)
                {
                    _nativePointerUpdatePump?.Enqueue(
                        batch.PointerId,
                        sessionId,
                        ToInkCanvasSampleArray(batch.SamplesNewestFirstArray),
                        predictionEnabled);
                    ResetPauseStraightenTimerForPointer(batch.PointerId);
                }
            }

            return decision.ConsumeNativeMessage || captured.ConsumeNativeMessage;
        }

        private bool HandleNativePointerUp(NativePointerInputBatch batch)
        {
            _nativeCapturedRoutes.TryGetValue(batch.PointerId, out var captured);
            _nativeCapturedRoutes.Remove(batch.PointerId);
            if (batch.InputKind == NativeInkInputKind.Touch)
                _nativeActiveTouchPointers.Remove(batch.PointerId);

            if (captured.Route != NativeInputRoute.Ink)
                return captured.Route != default && captured.ConsumeNativeMessage;

            // 抬笔：停止该笔的停顿拉直计时器。
            StopPauseStraightenTimerForPointer(batch.PointerId);

            var facts = CreatePointerFacts(batch);
            if (HasCanceledFlag(facts))
            {
                _nativePointerUpdatePump?.DiscardPointer(batch.PointerId);
                _nativePointerUpdatePump?.FlushPointer(batch.PointerId);
                if (_nativeInkController.Cancel(batch.PointerId))
                    _wetInkWindowHost?.SignalWork();
                return true;
            }

            var endedAt = batch.SamplesNewestFirst.Count > 0
                ? batch.SamplesNewestFirst[0].TimestampMicroseconds
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;

            _nativePointerUpdatePump?.FlushPointer(batch.PointerId);

            // 开启实时书写预测时，抬笔把当前预测笔尾烘焙进真实干墨（进入保存/撤销）。
            var bakePrediction = Settings?.Canvas?.EnableNativeInkPrediction == true;
            var payload = _nativeInkController.End(
                batch.PointerId,
                endedAt,
                ToInkCanvasSamples(batch.SamplesNewestFirst),
                bakePredictionIntoRealInk: bakePrediction);
            RefreshOverlayVisibility();
            _wetInkWindowHost?.SignalWork();

            if (payload == null)
                return true;

            CommitNativeStrokePayload(payload);
            return true;
        }

        private bool HandleNativePointerCaptureLost(NativePointerInputBatch batch)
        {
            _nativeCapturedRoutes.Remove(batch.PointerId);
            if (batch.InputKind == NativeInkInputKind.Touch)
                _nativeActiveTouchPointers.Remove(batch.PointerId);

            _nativePointerUpdatePump?.DiscardPointer(batch.PointerId);
            _nativePointerUpdatePump?.FlushPointer(batch.PointerId);

            if (_nativeInkController.Cancel(batch.PointerId))
            {
                RefreshOverlayVisibility();
                _wetInkWindowHost?.SignalWork();
                StopPauseStraightenTimerForPointer(batch.PointerId);
                return true;
            }

            StopPauseStraightenTimerForPointer(batch.PointerId);
            return false;
        }

        private void CommitNativeStrokePayload(NativeStrokeCommitPayload payload)
        {
            if (payload == null)
                return;

            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("书写");
                _nativePointerUpdatePump?.DiscardPointer(payload.PointerId);
                _nativePointerUpdatePump?.FlushPointer(payload.PointerId);
                _nativeInkController.Cancel(payload.PointerId);
                RefreshOverlayVisibility();
                _wetInkWindowHost?.SignalWork();
                return;
            }

            Stroke stroke;
            try
            {
                stroke = WpfStrokeCommitter.CreateStroke(payload);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] CreateStroke failed: {ex}",
                    LogHelper.LogType.Error);
                _nativePointerUpdatePump?.DiscardPointer(payload.PointerId);
                _nativePointerUpdatePump?.FlushPointer(payload.PointerId);
                _nativeInkController.Cancel(payload.PointerId);
                _wetInkWindowHost?.SignalWork();
                return;
            }

            try
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Commit session={payload.SessionId} points={payload.Points.Count} " +
                    $"first=({payload.Points[0].X:F1},{payload.Points[0].Y:F1}) " +
                    $"last=({payload.Points[payload.Points.Count - 1].X:F1},{payload.Points[payload.Points.Count - 1].Y:F1})",
                    LogHelper.LogType.Event);

                // Dry ink is the single source of truth. Add first so StrokesChanged /
                // TimeMachine / dirty-page hooks fire before post-processing.
                inkCanvas.Strokes.Add(stroke);
                _nativeInkController.MarkDryCommitted(payload.SessionId);
                // 不要在此处移走湿墨 overlay：WPF 尚未把干墨绘制到下一帧，若此时移走
                // 湿墨会出现“干墨未画、湿墨已消失”的空档，导致烘干闪变（整条墨迹闪一下）。
                // 保持湿墨 overlay 在屏上，直到多帧 WPF 渲染栅栏确认干墨已合成，再退休湿墨
                // （OnNativeWetInkRetired → RefreshOverlayVisibility → 移到屏外）。
                ProcessCommittedStroke(stroke);

                // Keep wet ink until several WPF composition frames paint the dry stroke.
                // The first Rendering callback often still precedes dry-stroke DWM compose.
                inkCanvas.InvalidateVisual();
                var sessionId = payload.SessionId;
                _wpfRenderFrameFence.Arm(sessionId, () =>
                {
                    try
                    {
                        if (_nativeInkController == null)
                            return;
                        _nativeInkController.MarkWpfFrameRendered(sessionId);
                        _wetInkWindowHost?.SignalWork();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile(
                            $"[WetInk] Fence callback failed: {ex}",
                            LogHelper.LogType.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Dry commit failed: {ex}",
                    LogHelper.LogType.Error);
                try
                {
                    if (inkCanvas.Strokes.Contains(stroke))
                        inkCanvas.Strokes.Remove(stroke);
                }
                catch { /* best-effort */ }

                try
                {
                    _wpfRenderFrameFence?.Cancel(payload.SessionId);
                    _nativePointerUpdatePump?.DiscardPointer(payload.PointerId);
                    _nativePointerUpdatePump?.FlushPointer(payload.PointerId);
                    _nativeInkController?.Cancel(payload.PointerId);
                    _wetInkWindowHost?.SignalWork();
                }
                catch { /* best-effort */ }
            }
        }

        private void OnNativeWetInkRetired(WetInkRetirementAck ack)
        {
            void Apply()
            {
                try
                {
                    _nativeInkController?.TryMarkWetVisualRetired(ack.SessionId, ack.Version);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[WetInk] TryMarkWetVisualRetired failed: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
                // A retired session may be the last live wet visual; hide the
                // overlay so it stops covering the main window's own content.
                RefreshOverlayVisibility();
            }

            if (Dispatcher.CheckAccess())
                Apply();
            else
                Dispatcher.BeginInvoke(Apply, DispatcherPriority.Send);
        }

        private void OnNativeWetInkDeviceLost()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DisableNativeWetInkAfterFailure(
                    new InvalidOperationException("Direct3D / DirectComposition device lost."),
                    notify: true);
            }), DispatcherPriority.Send);
        }

        private void OnNativeWetInkFatalError(Exception ex)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DisableNativeWetInkAfterFailure(ex, notify: true);
            }), DispatcherPriority.Send);
        }

        private void DisableNativeWetInkAfterFailure(Exception ex, bool notify)
        {
            _nativeWetInkDisabled = true;
            try
            {
                CancelAllNativeWetInkSessions("device-failure");
            }
            catch { /* best-effort */ }

            // 管线失败后，若当前逻辑模式是笔且物理模式被设成了 None（原生笔），
            // 回退到 WPF 内置 Ink 收集，避免"UI 显示笔但无法绘制"。
            try
            {
                if (inkCanvas != null
                    && inkCanvas.EditingMode == InkCanvasEditingMode.None
                    && ResolveLogicalInkTool() == LogicalInkTool.Pen)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
            }
            catch { /* best-effort */ }

            if (notify && !_nativeWetInkDeviceFailureNotified)
            {
                _nativeWetInkDeviceFailureNotified = true;
                try
                {
                    ShowNotification(Properties.CanvasStrings.Canvas_WetInkRendererFailed);
                }
                catch { /* never throw from failure path */ }
            }

            LogHelper.WriteLogToFile(
                $"[WetInk] Freehand disabled after pipeline failure: {ex}",
                LogHelper.LogType.Error);
        }

        private NativePointerFacts CreatePointerFacts(NativePointerInputBatch batch)
        {
            var sample = batch.SamplesNewestFirst.Count > 0
                ? batch.SamplesNewestFirst[0]
                : default;

            // Hit-testing is done in window client DIP space (same as the HWND hook).
            var windowPoint = ToWindowClientPoint(sample.X, sample.Y);

            var dpi = GetNativeDpiScales();
            var contactWidthDip = sample.ContactWidthPixels > 0
                ? sample.ContactWidthPixels / dpi.X
                : 0;
            var contactHeightDip = sample.ContactHeightPixels > 0
                ? sample.ContactHeightPixels / dpi.Y
                : 0;

            return new NativePointerFacts(
                batch.PointerId,
                batch.InputKind,
                sample.Flags,
                batch.SecondaryBarrelButtonDown,
                batch.IsPromotedMouse,
                windowPoint.X,
                windowPoint.Y,
                contactWidthDip,
                contactHeightDip);
        }

        /// <summary>
        /// Native samples are window-client DIPs. Wet overlay and dry InkCanvas both use
        /// the same client origin in ICC's full-screen layout; when the canvas is offset
        /// (e.g. future layout changes), convert into inkCanvas local space.
        /// </summary>
        private IReadOnlyList<RawInkSample> ToInkCanvasSamples(IReadOnlyList<RawInkSample> windowSamples)
        {
            if (windowSamples == null || windowSamples.Count == 0)
                return windowSamples ?? Array.Empty<RawInkSample>();

            var array = new RawInkSample[windowSamples.Count];
            for (var i = 0; i < windowSamples.Count; i++)
                array[i] = windowSamples[i];
            return ToInkCanvasSampleArray(array);
        }

        private RawInkSample[] ToInkCanvasSampleArray(RawInkSample[] windowSamples)
        {
            if (windowSamples == null || windowSamples.Length == 0 || inkCanvas == null)
                return windowSamples ?? Array.Empty<RawInkSample>();

            Point inkOriginInWindow;
            try
            {
                inkOriginInWindow = inkCanvas.TransformToAncestor(this)
                    .Transform(new Point(0, 0));
            }
            catch
            {
                var fallback = new RawInkSample[windowSamples.Length];
                Array.Copy(windowSamples, fallback, fallback.Length);
                return fallback;
            }

            var converted = new RawInkSample[windowSamples.Length];
            if (Math.Abs(inkOriginInWindow.X) < 0.01 && Math.Abs(inkOriginInWindow.Y) < 0.01)
            {
                Array.Copy(windowSamples, converted, converted.Length);
                return converted;
            }

            for (var i = 0; i < windowSamples.Length; i++)
            {
                var s = windowSamples[i];
                converted[i] = new RawInkSample(
                    s.PointerId,
                    s.InputKind,
                    s.X - inkOriginInWindow.X,
                    s.Y - inkOriginInWindow.Y,
                    s.Pressure,
                    s.HasPressure,
                    s.TimestampMicroseconds,
                    s.FrameId,
                    s.Flags,
                    s.ContactWidthPixels,
                    s.ContactHeightPixels);
            }

            return converted;
        }

        private Point ToWindowClientPoint(double xDip, double yDip)
        {
            // Samples are already window-client DIPs from NativePointerInputSource.
            return new Point(xDip, yDip);
        }

        private NativeInkRouteContext BuildRouteContext(NativePointerFacts pointer)
        {
            var hitZone = ResolveHitZone(pointer.XDip, pointer.YDip);
            var tool = ResolveLogicalInkTool();
            var multiTouchWriting = currentMode == 0
                ? Settings.Gesture.IsEnableMultiTouchMode || isInMultiTouchMode
                : Settings.Gesture.IsEnableMultiTouchModeBoard || isInMultiTouchMode;
            var twoFingerAllowed = ResolveTwoFingerGestureAllowed();
            var activeTouchCount = Math.Max(dec.Count, _nativeActiveTouchPointers.Count);
            var palm = BuildPalmRoutePolicy();

            return new NativeInkRouteContext(
                hitZone,
                tool,
                canvasInputEnabled: IsEnabled && IsVisible && inkCanvas != null,
                pageFrozen: IsCurrentPageFrozen,
                videoPresenter: _isVideoPresenterSpecialMode,
                multiTouchWriting: multiTouchWriting,
                twoFingerGestureAllowed: twoFingerAllowed,
                activeTouchCount: activeTouchCount,
                palm: palm);
        }

        private NativeInkRouteContext BuildCapturedRouteContext(
            NativeInkRouteDecision captured,
            NativeInkInputKind inputKind)
        {
            var tool = ResolveLogicalInkTool();
            var multiTouchWriting = currentMode == 0
                ? Settings.Gesture.IsEnableMultiTouchMode || isInMultiTouchMode
                : Settings.Gesture.IsEnableMultiTouchModeBoard || isInMultiTouchMode;
            var twoFingerAllowed = ResolveTwoFingerGestureAllowed();
            var activeTouchCount = Math.Max(dec.Count, _nativeActiveTouchPointers.Count);
            var palm = inputKind == NativeInkInputKind.Touch ? BuildPalmRoutePolicy() : default;

            return new NativeInkRouteContext(
                hitZone: captured.Route == NativeInputRoute.Ink
                    ? CanvasHitZone.CanvasSurface
                    : CanvasHitZone.Outside,
                tool,
                canvasInputEnabled: IsEnabled && IsVisible && inkCanvas != null,
                pageFrozen: IsCurrentPageFrozen,
                videoPresenter: _isVideoPresenterSpecialMode,
                multiTouchWriting: multiTouchWriting,
                twoFingerGestureAllowed: twoFingerAllowed,
                activeTouchCount: activeTouchCount,
                palm: palm);
        }

        private void FlushNativePointerUpdateForSession(long sessionId)
        {
            if (_nativePointerUpdatePump == null || _nativeInkController == null)
                return;

            foreach (var route in _nativeCapturedRoutes)
            {
                if (_nativeInkController.TryGetSessionInfo(route.Key, out var activeSessionId, out var state)
                    && activeSessionId == sessionId
                    && state == NativeInkSessionState.Active)
                {
                    _nativePointerUpdatePump.FlushPointer(route.Key);
                    return;
                }
            }
        }

        private LogicalInkTool ResolveLogicalInkTool()
        {
            if (IsBoardRoamingMode)
                return LogicalInkTool.BoardRoam;
            if (drawingShapeMode != 0
                || string.Equals(_currentToolMode, "shape", StringComparison.OrdinalIgnoreCase))
                return LogicalInkTool.Shape;

            switch (_currentToolMode)
            {
                case "pen":
                case "color":
                    return LogicalInkTool.Pen;
                case "eraser":
                    return LogicalInkTool.PointEraser;
                case "eraserByStrokes":
                    return LogicalInkTool.StrokeEraser;
                case "select":
                    return LogicalInkTool.Select;
                case "roaming":
                    return LogicalInkTool.BoardRoam;
                case "cursor":
                default:
                    return LogicalInkTool.Cursor;
            }
        }

        private bool ResolveTwoFingerGestureAllowed()
        {
            if (IsInPPTPresentationMode)
                return Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode
                       && Settings.Gesture.IsEnableTwoFingerGesture;
            return Settings.Gesture.IsEnableTwoFingerGesture;
        }

        private PalmRoutePolicy BuildPalmRoutePolicy()
        {
            var canvas = Settings.Canvas;
            var advanced = Settings.Advanced;
            var isNib = Settings.Startup.IsEnableNibMode;

            double sensitivityMultiplier;
            switch (canvas.PalmEraserSensitivity)
            {
                case 0:
                    sensitivityMultiplier = 3.0;
                    break;
                case 1:
                    sensitivityMultiplier = 2.5;
                    break;
                default:
                    sensitivityMultiplier = 2.0;
                    break;
            }

            return new PalmRoutePolicy(
                enabled: canvas.EnablePalmEraser,
                isActive: isPalmEraserActive,
                isQuadIr: advanced.IsQuadIR,
                isSpecialScreen: advanced.IsSpecialScreen,
                boundsWidthDip: BoundsWidth,
                thresholdFactor: isNib
                    ? advanced.NibModeBoundsWidthThresholdValue
                    : advanced.FingerModeBoundsWidthThresholdValue,
                sensitivityMultiplier: sensitivityMultiplier,
                eraserSizeFactor: isNib
                    ? advanced.NibModeBoundsWidthEraserSize
                    : advanced.FingerModeBoundsWidthEraserSize,
                touchMultiplier: advanced.TouchMultiplier);
        }

        private CanvasHitZone ResolveHitZone(double xDip, double yDip)
        {
            if (inkCanvas == null)
                return CanvasHitZone.Outside;

            try
            {
                var windowPoint = new Point(xDip, yDip);
                if (windowPoint.X < 0 || windowPoint.Y < 0
                    || windowPoint.X > ActualWidth || windowPoint.Y > ActualHeight)
                {
                    return CanvasHitZone.Outside;
                }

                // Conservative model: for Pen freehand, default to the writing
                // surface and only override when a known UI element is explicitly hit.
                var hit = InputHitTest(windowPoint) as DependencyObject;

                // Eraser overlay sits above the canvas and owns its input.
                if (hit != null
                    && (IsUnderNamed(hit, "EraserOverlayCanvas")
                        || IsUnderElement(hit, EraserOverlayCanvas)))
                {
                    return CanvasHitZone.EraserOverlay;
                }

                // The selection cover only blocks input while it is actually visible.
                if (hit != null)
                {
                    var selectionCover = FindName("GridInkCanvasSelectionCover") as FrameworkElement;
                    if (selectionCover != null
                        && selectionCover.Visibility == Visibility.Visible
                        && (IsUnderNamed(hit, "GridInkCanvasSelectionCover")
                            || IsUnderElement(hit, selectionCover)))
                    {
                        return CanvasHitZone.SelectionOverlay;
                    }
                }

                // Floating bar / board chrome / replay controls are explicit UI.
                if (hit != null && IsUiChromeHit(hit))
                    return CanvasHitZone.UiChrome;

                // Everything else over the window is the writing surface for Pen.
                return CanvasHitZone.CanvasSurface;
            }
            catch
            {
                return CanvasHitZone.CanvasSurface;
            }
        }

        private bool IsUiChromeHit(DependencyObject hit)
        {
            if (hit == null)
                return false;

            if (IsUnderNamed(hit, "ViewboxFloatingBar")
                || IsUnderNamed(hit, "ViewboxBlackboardLeftSide")
                || IsUnderNamed(hit, "ViewboxBlackboardCenterSide")
                || IsUnderNamed(hit, "ViewboxBlackboardRightSide")
                || IsUnderNamed(hit, "BlackboardLeftSide")
                || IsUnderNamed(hit, "BlackboardCenterSide")
                || IsUnderNamed(hit, "BlackboardRightSide")
                || IsUnderNamed(hit, "BorderInkReplayToolBox")
                || IsUnderNamed(hit, "IdleMiniBar")
                || IsUnderNamed(hit, "EdgeExpandHint")
                || IsUnderNamed(hit, "PPTControlsGrid")
                || IsUnderNamed(hit, "GridPPTControlLeft")
                || IsUnderNamed(hit, "GridPPTControlRight")
                || IsUnderNamed(hit, "LeftBottomPanelForPPTNavigation")
                || IsUnderNamed(hit, "RightBottomPanelForPPTNavigation")
                || IsUnderNamed(hit, "LeftSidePanelForPPTNavigation")
                || IsUnderNamed(hit, "RightSidePanelForPPTNavigation")
                || IsUnderNamed(hit, "PPTQuickPanelContainer"))
            {
                return true;
            }

            var current = hit;
            while (current != null)
            {
                if (current is Button
                    || current is System.Windows.Controls.Primitives.Thumb
                    || current is Slider
                    || current is System.Windows.Controls.Primitives.ToggleButton
                    || current is System.Windows.Controls.Primitives.ScrollBar)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }

            return false;
        }

        private InkStrokeStyleSnapshot CaptureStrokeStyleSnapshot()
        {
            var attrs = inkCanvas.DefaultDrawingAttributes;
            var color = attrs.Color;
            uint colorArgb = ((uint)color.A << 24)
                             | ((uint)color.R << 16)
                             | ((uint)color.G << 8)
                             | color.B;

            var useVelocity = ShouldUseRealtimeVelocityBrushTip()
                              && penType != 1
                              && drawingShapeMode == 0
                              && !isPalmEraserActive;
            var tipShape = attrs.StylusTip == StylusTip.Rectangle
                           || attrs.IsHighlighter
                           || penType == 1
                ? InkStylusTipShape.Rectangle
                : InkStylusTipShape.Ellipse;

            // 笔锋相关样式必须按 PressureFactor 渲染。不要 OR attrs.IgnorePressure：
            // 无压感设备上系统可能把 DefaultDrawingAttributes.IgnorePressure 置 true，
            // 会让点集/速率/实时笔锋全部无效。
            return new InkStrokeStyleSnapshot(
                colorArgb,
                Math.Max(0.1, attrs.Width),
                Math.Max(0.1, attrs.Height),
                ignorePressure: Settings.Canvas.DisablePressure,
                isHighlighter: attrs.IsHighlighter || penType == 1,
                useVelocityBrushTip: useVelocity,
                velocityBrushTipMix: (float)Settings.Canvas.VelocityBrushTipMix,
                minimumDistanceScale: (float)Settings.Canvas.RealtimeBrushTipMinDistanceScale,
                coordinateGeneration: _nativeCoordinateGeneration,
                pageGeneration: CurrentWhiteboardIndex,
                stylusTipShape: tipShape,
                renderMode: penType == 2 ? InkRenderMode.Laser : InkRenderMode.Standard);
        }

        private InkSampleProcessorSettings CaptureProcessorSettings(InkStrokeStyleSnapshot style)
        {
            return new InkSampleProcessorSettings
            {
                DisablePressure = Settings.Canvas.DisablePressure,
                EnablePressureForTouch = Settings.Canvas.EnablePressureTouchMode,
                UseVelocityBrushTip = style.UseVelocityBrushTip,
                VelocityBrushTipMix = style.VelocityBrushTipMix,
                MinimumDistanceScale = style.MinimumDistanceScale,
                BaseWidth = style.Width,
                InkStyle = Settings.Canvas.InkStyle
            };
        }

        private WetInkTargetSnapshot BuildWetInkTargetSnapshot()
        {
            var dpi = GetNativeDpiScales();
            var widthDip = Math.Max(1.0, ActualWidth);
            var heightDip = Math.Max(1.0, ActualHeight);
            Point topLeft;
            try
            {
                topLeft = PointToScreen(new Point(0, 0));
            }
            catch
            {
                topLeft = new Point(Left, Top);
            }

            var screenBounds = new WetInkPixelRect(
                (int)Math.Round(topLeft.X),
                (int)Math.Round(topLeft.Y),
                Math.Max(1, (int)Math.Round(widthDip * dpi.X)),
                Math.Max(1, (int)Math.Round(heightDip * dpi.Y)));

            var visible = IsVisible
                          && WindowState != WindowState.Minimized
                          && Opacity > 0.01
                          && ActualWidth > 0
                          && ActualHeight > 0;

            return new WetInkTargetSnapshot(
                screenBounds,
                (float)(96.0 * dpi.X),
                (float)(96.0 * dpi.Y),
                visible,
                BuildExclusionRects(dpi, topLeft));
        }

        private IReadOnlyList<WetInkPixelRect> BuildExclusionRects(
            (double X, double Y) dpi,
            Point windowTopLeftScreen)
        {
            var list = new List<WetInkPixelRect>(8);
            TryAddElementExclusion(list, FindName("ViewboxFloatingBar") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("ViewboxBlackboardLeftSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("ViewboxBlackboardCenterSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("ViewboxBlackboardRightSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("BlackboardLeftSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("BlackboardCenterSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("BlackboardRightSide") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("LeftBottomPanelForPPTNavigation") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("RightBottomPanelForPPTNavigation") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("LeftSidePanelForPPTNavigation") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, FindName("RightSidePanelForPPTNavigation") as FrameworkElement, dpi, windowTopLeftScreen);
            TryAddElementExclusion(list, EraserOverlayCanvas, dpi, windowTopLeftScreen);
            return list;
        }

        private static void TryAddElementExclusion(
            List<WetInkPixelRect> list,
            FrameworkElement element,
            (double X, double Y) dpi,
            Point windowTopLeftScreen)
        {
            if (element == null
                || element.Visibility != Visibility.Visible
                || element.ActualWidth <= 0
                || element.ActualHeight <= 0)
            {
                return;
            }

            try
            {
                var topLeft = element.PointToScreen(new Point(0, 0));
                var width = Math.Max(1, (int)Math.Round(element.ActualWidth * dpi.X));
                var height = Math.Max(1, (int)Math.Round(element.ActualHeight * dpi.Y));
                list.Add(new WetInkPixelRect(
                    (int)Math.Round(topLeft.X),
                    (int)Math.Round(topLeft.Y),
                    width,
                    height));
            }
            catch
            {
                // Element may not be connected to a presentation source yet.
            }
        }

        private (double X, double Y) GetNativeDpiScales()
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var m = source.CompositionTarget.TransformToDevice;
                    return (m.M11 > 0 ? m.M11 : 1.0, m.M22 > 0 ? m.M22 : 1.0);
                }
            }
            catch { /* fall through */ }

            var scale = GetDpiScale();
            return (scale > 0 ? scale : 1.0, scale > 0 ? scale : 1.0);
        }

        /// <summary>
        /// Logical Pen freehand uses the native wet-ink pipeline, so the physical
        /// InkCanvas editing mode must stay None to block WPF automatic stroke capture.
        /// Erase/Select keep their WPF modes.
        /// </summary>
        private void EnsureNativePenPhysicalEditingMode()
        {
            if (inkCanvas == null)
                return;

            var tool = ResolveLogicalInkTool();
            if (tool != LogicalInkTool.Pen)
                return;

            // 仅当原生管线可用时才将 Ink→None；管线不可用时保持 WPF 内置 Ink 收集
            if (!IsNativeWetInkPipelineAvailable)
                return;

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
        }

        private static bool HasCanceledFlag(NativePointerFacts facts)
        {
            return (facts.Flags & NativeInkSampleFlags.Canceled) != 0;
        }

        private static bool IsPointOverElement(FrameworkElement element, Point windowPoint)
        {
            if (element == null || element.Visibility != Visibility.Visible)
                return false;

            try
            {
                var window = Window.GetWindow(element);
                if (window == null)
                    return false;
                var topLeftInWindow = element.TranslatePoint(new Point(0, 0), window);
                var rect = new Rect(
                    topLeftInWindow,
                    new Size(element.ActualWidth, element.ActualHeight));
                return rect.Contains(windowPoint);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInteractiveCanvasChild(DependencyObject hit)
        {
            // True only for user-placed interactive children (images, media, PDF),
            // NOT the InkCanvas's own template parts (InkPresenter / AdornerDecorator /
            // AdornerLayer / Adorner), which are ContentControls but belong to the canvas.
            var current = hit;
            while (current != null)
            {
                if (current is Image
                    || current is MediaElement)
                {
                    return true;
                }

                if (current is FrameworkElement fe)
                {
                    var typeName = current.GetType().Name;
                    if (string.Equals(typeName, "InkPresenter", StringComparison.Ordinal)
                        || typeName.IndexOf("Adorner", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }

                    if (fe.IsHitTestVisible
                        && current is ContentControl
                        && !string.IsNullOrEmpty(typeName)
                        && typeName.IndexOf("Adorner", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return true;
                    }

                    if (typeName.IndexOf("Pdf", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Media", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }

            return false;
        }

        private static bool IsUnderElement(DependencyObject hit, DependencyObject ancestor)
        {
            if (hit == null || ancestor == null)
                return false;
            var current = hit;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return false;
        }

        private static bool IsUnderNamed(DependencyObject hit, string name)
        {
            var current = hit;
            while (current != null)
            {
                if (current is FrameworkElement fe
                    && string.Equals(fe.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return false;
        }

        private static DependencyObject FindVisualParentByTypeName(DependencyObject child, string typeName)
        {
            var current = child;
            while (current != null)
            {
                if (current.GetType().Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return current;
                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return null;
        }

        private static DependencyObject FindVisualParent(
            DependencyObject child,
            Func<DependencyObject, bool> predicate)
        {
            var current = VisualTreeHelper.GetParent(child)
                          ?? (child as FrameworkElement)?.Parent;
            while (current != null)
            {
                if (predicate(current))
                    return current;
                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return null;
        }
    }
}
