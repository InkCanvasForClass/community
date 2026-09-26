using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using Windows.Devices.Input;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Core;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// A pair of physical-pixel touch positions captured on the ink thread. The UI thread uses
    /// the previous/current pair to apply one atomic two-finger gesture increment.
    /// </summary>
    internal readonly struct WinRTInkGestureSnapshot
    {
        public WinRTInkGestureSnapshot(
            uint firstPointerId,
            uint secondPointerId,
            Point previousFirst,
            Point previousSecond,
            Point currentFirst,
            Point currentSecond)
        {
            FirstPointerId = firstPointerId;
            SecondPointerId = secondPointerId;
            PreviousFirst = previousFirst;
            PreviousSecond = previousSecond;
            CurrentFirst = currentFirst;
            CurrentSecond = currentSecond;
        }

        public uint FirstPointerId { get; }
        public uint SecondPointerId { get; }
        public Point PreviousFirst { get; }
        public Point PreviousSecond { get; }
        public Point CurrentFirst { get; }
        public Point CurrentSecond { get; }
    }

    /// <summary>
    /// Result of classifying one pointer press against the UI.
    /// </summary>
    internal enum PointerGateResult
    {
        /// <summary>Let the InkPresenter collect this pointer as ink.</summary>
        AllowInk,

        /// <summary>
        /// Suppress inking and swallow the input (frozen page, two-finger gesture, palm
        /// eraser, or classification failure). The press must not be re-dispatched.
        /// </summary>
        BlockSilently,

        /// <summary>
        /// Suppress inking and forward the pointer to the window under it as synthesized
        /// mouse input. Pointer input delivered to the ink HWND bypasses the overlay
        /// WM_NCHITTEST pass-through, so chrome hits (floating bar, side panels, popup
        /// palettes) never reach their target window on their own — they have to be
        /// re-dispatched explicitly for the controls to stay clickable.
        /// </summary>
        BlockAndForward
    }

    /// <summary>
    /// Runs on the ink background thread (CoreInkIndependentInputSource events) and decides,
    /// per pointer, whether the InkPresenter may ink, the input must be suppressed, or the
    /// input must be re-dispatched to the window under the pointer. Cheap and lock-free apart
    /// from the per-press classifier callback (which marshals to the UI thread). No WPF calls
    /// happen directly here.
    /// </summary>
    internal sealed class WinRTInkInputGate
    {
        private readonly Func<PointerEventArgs, PointerGateResult> _classifyPointer;
        private readonly Action<PointerEventArgs> _onChromePointerDown;
        private readonly Action<PointerEventArgs> _onChromePointerMove;
        private readonly Action<PointerEventArgs> _onChromePointerRelease;
        private readonly Action<uint, Point> _onTouchPointerPress;
        private readonly Action<uint, Point> _onTouchPointerMove;
        private readonly Action<uint, Point> _onTouchPointerRelease;
        private readonly Action<uint, PointerDeviceType, Point> _onInkPointerPress;
        private readonly Action<uint, PointerDeviceType, Point> _onInkPointerMove;
        private readonly Action<uint, PointerDeviceType, Point> _onInkPointerRelease;
        private readonly Action<WinRTInkGestureSnapshot> _onTwoFingerGestureStarted;
        private readonly Action<WinRTInkGestureSnapshot> _onTwoFingerGestureDelta;
        private readonly Action _onTwoFingerGestureCompleted;
        private readonly Action<uint> _onStrokeEnded;
        private readonly Action _onStrokeCanceled;

        // UI-thread refreshed snapshots.
        private volatile bool _canvasInputEnabled = true;
        private volatile bool _pageFrozen;
        private volatile bool _multiTouchWriting;
        private volatile bool _twoFingerGestureAllowed;
        private volatile bool _palmEraserEnabled;
        private long _palmEraserThresholdDipBits;
        private volatile bool _palmEraserActive;
        private volatile bool _cancelAll;

        // Ink-thread only.
        private readonly Dictionary<uint, Point> _touchPoints = new Dictionary<uint, Point>();
        private readonly List<uint> _touchPointerOrder = new List<uint>();
        private readonly HashSet<uint> _activeTouchPointers = new HashSet<uint>();
        private readonly HashSet<uint> _gesturePointers = new HashSet<uint>();
        private readonly HashSet<uint> _canceledStrokePointers = new HashSet<uint>();
        private readonly HashSet<uint> _chromeForwardedPointers = new HashSet<uint>();
        private readonly HashSet<uint> _inkingPointers = new HashSet<uint>();
        private readonly Dictionary<uint, long> _lastInkMoveForwardTicks = new Dictionary<uint, long>();
        private uint _mouseForwardingPointerId;
        private uint _gestureFirstPointerId;
        private uint _gestureSecondPointerId;
        private bool _isGestureInProgress;

        // Gesture feed state: the contact pair at the last delta already handed to the UI
        // thread (the next increment is measured from there, so thinning the feed changes the
        // step size but never the total transform) and the throttle stamp. The UI thread
        // applies every delta synchronously at DispatcherPriority.Normal, which outranks WPF's
        // render work, so an unthrottled per-move feed keeps that queue non-empty for the whole
        // gesture and the canvas visibly moves only once the fingers stop.
        private const double GestureDeltaMinIntervalMs = 15.0;
        private bool _hasGestureBaseline;
        private Point _gestureBaselineFirst;
        private Point _gestureBaselineSecond;
        private long _lastGestureDeltaTicks;
        // Per-gesture counters for the end-of-gesture summary log (forwarded vs. rate-limited).
        private long _gestureStartTicks;
        private int _gestureForwardedDeltas;
        private int _gestureThrottledDeltas;

        public WinRTInkInputGate(
            Func<PointerEventArgs, PointerGateResult> classifyPointer,
            Action<PointerEventArgs> onChromePointerDown,
            Action<PointerEventArgs> onChromePointerMove,
            Action<PointerEventArgs> onChromePointerRelease,
            Action<uint, Point> onTouchPointerPress,
            Action<uint, Point> onTouchPointerMove,
            Action<uint, Point> onTouchPointerRelease,
            Action<uint, PointerDeviceType, Point> onInkPointerPress,
            Action<uint, PointerDeviceType, Point> onInkPointerMove,
            Action<uint, PointerDeviceType, Point> onInkPointerRelease,
            Action<WinRTInkGestureSnapshot> onTwoFingerGestureStarted,
            Action<WinRTInkGestureSnapshot> onTwoFingerGestureDelta,
            Action onTwoFingerGestureCompleted,
            Action<uint> onStrokeEnded,
            Action onStrokeCanceled)
        {
            _classifyPointer = classifyPointer ?? throw new ArgumentNullException(nameof(classifyPointer));
            _onChromePointerDown = onChromePointerDown;
            _onChromePointerMove = onChromePointerMove;
            _onChromePointerRelease = onChromePointerRelease;
            _onTouchPointerPress = onTouchPointerPress;
            _onTouchPointerMove = onTouchPointerMove;
            _onTouchPointerRelease = onTouchPointerRelease;
            _onInkPointerPress = onInkPointerPress;
            _onInkPointerMove = onInkPointerMove;
            _onInkPointerRelease = onInkPointerRelease;
            _onTwoFingerGestureStarted = onTwoFingerGestureStarted;
            _onTwoFingerGestureDelta = onTwoFingerGestureDelta;
            _onTwoFingerGestureCompleted = onTwoFingerGestureCompleted;
            _onStrokeEnded = onStrokeEnded ?? throw new ArgumentNullException(nameof(onStrokeEnded));
            _onStrokeCanceled = onStrokeCanceled ?? throw new ArgumentNullException(nameof(onStrokeCanceled));
        }

        public bool CanvasInputEnabled { set => _canvasInputEnabled = value; }
        public bool PageFrozen { set => _pageFrozen = value; }
        public bool MultiTouchWriting { set => _multiTouchWriting = value; }
        public bool TwoFingerGestureAllowed { set => _twoFingerGestureAllowed = value; }
        public bool PalmEraserEnabled { set => _palmEraserEnabled = value; }
        public double PalmEraserThresholdDip
        {
            set => _palmEraserThresholdDipBits = BitConverter.DoubleToInt64Bits(value);
            get => BitConverter.Int64BitsToDouble(
                Interlocked.Read(ref _palmEraserThresholdDipBits));
        }
        public bool PalmEraserActive { set => _palmEraserActive = value; }
        public bool IsGestureInProgress => _isGestureInProgress;

        public void CancelActiveStrokes()
        {
            // The ink thread observes this on the next moving/releasing/stroke-ended event and
            // marks all currently active strokes handled so the presenter cancels them.
            _cancelAll = true;
            foreach (var pointerId in _inkingPointers)
                _canceledStrokePointers.Add(pointerId);
        }

        public void OnPointerPressing(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            var device = e.CurrentPoint.PointerDevice.PointerDeviceType;
            var position = GetPosition(e);

            // A pointer ID can be reused after a lost/canceled contact. Do not let a stale
            // cancellation marker suppress the new stroke.
            _canceledStrokePointers.Remove(pointerId);

            if (_cancelAll)
            {
                e.Handled = true;
                LogGatePress(device, e, "cancelAll");
                return;
            }

            if (!_canvasInputEnabled || _pageFrozen)
            {
                e.Handled = true;
                LogGatePress(device, e, "canvas-disabled-or-frozen");
                return;
            }

            // Resolve chrome before touching the touch-contact/gesture state. If the first
            // contact lands on a toolbar and the second contact lands on the canvas, the
            // toolbar pointer must remain a forwarded mouse interaction, not become part of
            // the two-finger gesture.
            var result = _classifyPointer(e);
            LogGatePress(device, e, result.ToString());
            if (result == PointerGateResult.BlockAndForward)
            {
                e.Handled = true;
                _chromeForwardedPointers.Add(pointerId);
                if (_mouseForwardingPointerId == 0)
                {
                    _mouseForwardingPointerId = pointerId;
                    try { _onChromePointerDown?.Invoke(e); }
                    catch { /* forwarding is best-effort */ }
                }
                return;
            }

            if (result != PointerGateResult.AllowInk)
            {
                e.Handled = true;
                return;
            }

            if (device == PointerDeviceType.Touch)
            {
                TrackTouchPress(pointerId, position);

                if (!_multiTouchWriting && _twoFingerGestureAllowed
                    && _activeTouchPointers.Count >= 2)
                {
                    BeginTwoFingerGesture();
                    e.Handled = true;
                    LogGatePress(device, e, "two-finger-gesture");
                    return;
                }

                if (_palmEraserEnabled)
                {
                    var contactRect = e.CurrentPoint.Properties.ContactRect;
                    var widthDip = contactRect.Width;
                    var heightDip = contactRect.Height;
                    var metric = !double.IsFinite(heightDip)
                        ? widthDip
                        : (heightDip <= 0 ? widthDip : Math.Sqrt(widthDip * heightDip));
                    if (metric >= PalmEraserThresholdDip)
                    {
                        _gesturePointers.Add(pointerId);
                        e.Handled = true;
                        LogGatePress(device, e, "palm-eraser");
                        return;
                    }
                }

                if (!_multiTouchWriting && _activeTouchPointers.Count >= 2)
                {
                    // With both multi-touch writing and two-finger gestures disabled,
                    // preserve the legacy single-contact behavior.
                    e.Handled = true;
                    return;
                }

                try { _onTouchPointerPress?.Invoke(pointerId, position); }
                catch { /* tracking is best-effort */ }
            }

            _inkingPointers.Add(pointerId);
            try { _onInkPointerPress?.Invoke(pointerId, device, position); }
            catch { /* tracking is best-effort */ }
        }

        public void OnPointerMoving(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            var position = GetPosition(e);

            if (_cancelAll)
            {
                e.Handled = true;
                return;
            }

            if (_activeTouchPointers.Contains(pointerId))
            {
                _touchPoints[pointerId] = position;

                if (_gesturePointers.Contains(pointerId))
                {
                    // A gesture contact belongs to the two-finger path (roaming mode receives
                    // those contacts through the gesture callbacks as well), so skip the
                    // per-move roaming feed and forward the canvas increment rate-limited.
                    e.Handled = true;
                    EmitGestureDelta(force: false);
                    return;
                }

                try { _onTouchPointerMove?.Invoke(pointerId, position); }
                catch { /* tracking is best-effort */ }
            }

            if (_chromeForwardedPointers.Contains(pointerId))
            {
                e.Handled = true;
                if (_mouseForwardingPointerId == pointerId)
                {
                    try { _onChromePointerMove?.Invoke(e); }
                    catch { /* forwarding is best-effort */ }
                }
                return;
            }

            if (_inkingPointers.Contains(pointerId))
            {
                // Pause-straighten movement feed. Each pointer has its own throttle so
                // simultaneous touch contacts cannot starve one another.
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!_lastInkMoveForwardTicks.TryGetValue(pointerId, out var last))
                    last = 0;
                var elapsedMs = (now - last) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (elapsedMs >= 15)
                {
                    _lastInkMoveForwardTicks[pointerId] = now;
                    try
                    {
                        _onInkPointerMove?.Invoke(
                            pointerId,
                            e.CurrentPoint.PointerDevice.PointerDeviceType,
                            position);
                    }
                    catch { /* tracking is best-effort */ }
                }
            }
        }

        public void OnPointerReleasing(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            CompletePointer(e, "pointer-released");
        }

        public void OnPointerLost(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            CompletePointer(e, "pointer-lost");
        }

        public void OnPointerExiting(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            CompletePointer(e, "pointer-exiting");
        }

        /// <summary>
        /// Called on the ink thread when the presenter finalizes a stroke. Returns true when
        /// the stroke is a live candidate for custom drying (i.e. we let it through).
        /// </summary>
        public bool OnStrokeEnded(InkStrokeInput sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            if (_cancelAll || _canceledStrokePointers.Remove(pointerId)
                || _gesturePointers.Contains(pointerId))
            {
                e.Handled = true;
                _onStrokeCanceled();
                return false;
            }

            _onStrokeEnded(pointerId);
            return true;
        }

        public void OnStrokeCanceled(InkStrokeInput sender, PointerEventArgs e)
        {
            _canceledStrokePointers.Remove(e.CurrentPoint.PointerId);
            _onStrokeCanceled();
        }

        private void CompletePointer(PointerEventArgs e, string reason)
        {
            var pointerId = e.CurrentPoint.PointerId;
            var position = GetPosition(e);
            var device = e.CurrentPoint.PointerDevice.PointerDeviceType;

            if (_activeTouchPointers.Contains(pointerId))
            {
                _touchPoints[pointerId] = position;
                // Flush the increment between the last forwarded delta and this lift while both
                // contacts are still known, so the canvas always lands on the fingers' final
                // position. Runs before the release callback below because the roaming bridge
                // drops the contact there (the second flush in EndTwoFingerGesture is a no-op).
                if (_gesturePointers.Contains(pointerId))
                    EmitGestureDelta(force: true);
                try { _onTouchPointerRelease?.Invoke(pointerId, position); }
                catch { /* tracking is best-effort */ }
            }

            if (_gesturePointers.Remove(pointerId))
            {
                _canceledStrokePointers.Add(pointerId);
                e.Handled = true;
                RemoveTouch(pointerId);
                if (_activeTouchPointers.Count == 0)
                    EndTwoFingerGesture();
                LogGatePress(device, e, reason + ":gesture");
                return;
            }

            if (_inkingPointers.Remove(pointerId))
            {
                if (!string.Equals(reason, "pointer-released", StringComparison.Ordinal))
                    _canceledStrokePointers.Add(pointerId);
                try { _onInkPointerRelease?.Invoke(pointerId, device, position); }
                catch { /* tracking is best-effort */ }
                _lastInkMoveForwardTicks.Remove(pointerId);
            }

            if (_chromeForwardedPointers.Remove(pointerId))
            {
                e.Handled = true;
                if (_mouseForwardingPointerId == pointerId)
                {
                    _mouseForwardingPointerId = 0;
                    try { _onChromePointerRelease?.Invoke(e); }
                    catch { /* forwarding is best-effort */ }
                }
            }
            else if (_cancelAll || _canceledStrokePointers.Contains(pointerId))
            {
                e.Handled = true;
            }

            RemoveTouch(pointerId);
            if (_inkingPointers.Count == 0 && _activeTouchPointers.Count == 0)
                _cancelAll = false;
            LogGatePress(device, e, reason);
        }

        private void TrackTouchPress(uint pointerId, Point position)
        {
            _activeTouchPointers.Add(pointerId);
            _touchPoints[pointerId] = position;
            _touchPointerOrder.Remove(pointerId);
            _touchPointerOrder.Add(pointerId);
        }

        private void RemoveTouch(uint pointerId)
        {
            _activeTouchPointers.Remove(pointerId);
            _touchPoints.Remove(pointerId);
            _touchPointerOrder.Remove(pointerId);
        }

        private Point GetTouchPosition(uint pointerId, Point fallback)
            => _touchPoints.TryGetValue(pointerId, out var position) ? position : fallback;

        private void BeginTwoFingerGesture()
        {
            if (_isGestureInProgress)
            {
                foreach (var pointerId in _activeTouchPointers)
                    _gesturePointers.Add(pointerId);
                return;
            }

            if (!TryGetGesturePointerIds(out var firstPointerId, out var secondPointerId))
                return;

            _isGestureInProgress = true;
            _gestureFirstPointerId = firstPointerId;
            _gestureSecondPointerId = secondPointerId;
            foreach (var pointerId in _activeTouchPointers)
            {
                _gesturePointers.Add(pointerId);
                if (_inkingPointers.Remove(pointerId))
                {
                    _canceledStrokePointers.Add(pointerId);
                    _lastInkMoveForwardTicks.Remove(pointerId);
                    try
                    {
                        _onInkPointerRelease?.Invoke(
                            pointerId,
                            PointerDeviceType.Touch,
                            GetTouchPosition(pointerId, new Point()));
                    }
                    catch { /* tracking is best-effort */ }
                }
            }

            var first = GetTouchPosition(firstPointerId, new Point());
            var second = GetTouchPosition(secondPointerId, new Point());
            _hasGestureBaseline = true;
            _gestureBaselineFirst = first;
            _gestureBaselineSecond = second;
            _lastGestureDeltaTicks = 0;
            _gestureStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _gestureForwardedDeltas = 0;
            _gestureThrottledDeltas = 0;
            var snapshot = new WinRTInkGestureSnapshot(
                firstPointerId,
                secondPointerId,
                first,
                second,
                first,
                second);
            try { _onTwoFingerGestureStarted?.Invoke(snapshot); }
            catch { /* gesture forwarding is best-effort */ }
        }

        /// <summary>
        /// Forwards one two-finger canvas increment to the UI thread. The increment runs from
        /// the contact pair at the last forwarded delta to the current pair, so a rate-limited
        /// feed produces bigger steps — never a different total transform. <paramref name="force"/>
        /// bypasses the rate limit for the final flush on lift.
        /// </summary>
        private void EmitGestureDelta(bool force)
        {
            if (!_isGestureInProgress || !_hasGestureBaseline)
                return;

            if (!_touchPoints.TryGetValue(_gestureFirstPointerId, out var currentFirst)
                || !_touchPoints.TryGetValue(_gestureSecondPointerId, out var currentSecond))
                return;

            if (currentFirst == _gestureBaselineFirst && currentSecond == _gestureBaselineSecond)
                return;

            if (!force)
            {
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                if (_lastGestureDeltaTicks != 0)
                {
                    var elapsedMs = (now - _lastGestureDeltaTicks)
                                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (elapsedMs < GestureDeltaMinIntervalMs)
                    {
                        _gestureThrottledDeltas++;
                        return;
                    }
                }
                _lastGestureDeltaTicks = now;
            }

            var snapshot = new WinRTInkGestureSnapshot(
                _gestureFirstPointerId,
                _gestureSecondPointerId,
                _gestureBaselineFirst,
                _gestureBaselineSecond,
                currentFirst,
                currentSecond);

            _gestureBaselineFirst = currentFirst;
            _gestureBaselineSecond = currentSecond;
            _gestureForwardedDeltas++;

            try { _onTwoFingerGestureDelta?.Invoke(snapshot); }
            catch { /* gesture forwarding is best-effort */ }
        }

        private void EndTwoFingerGesture()
        {
            if (!_isGestureInProgress)
                return;

            EmitGestureDelta(force: true);
            var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - _gestureStartTicks)
                            * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LogGestureSummary(_gestureForwardedDeltas, _gestureThrottledDeltas, elapsedMs);
            _isGestureInProgress = false;
            _hasGestureBaseline = false;
            _gesturePointers.Clear();
            _gestureFirstPointerId = 0;
            _gestureSecondPointerId = 0;
            try { _onTwoFingerGestureCompleted?.Invoke(); }
            catch { /* gesture forwarding is best-effort */ }
        }

        /// <summary>
        /// One line per two-finger gesture: how many canvas increments reached the UI thread,
        /// how many the rate limit thinned, and the gesture duration. A gesture that reports
        /// zero forwarded deltas never produced pointer movement at all, which separates an
        /// input-delivery problem from a UI-thread lag problem.
        /// </summary>
        private static void LogGestureSummary(int forwarded, int throttled, double elapsedMs)
        {
            try
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] gesture end: forwarded={forwarded} throttled={throttled} elapsedMs={elapsedMs:0}",
                    LogHelper.LogType.Event);
            }
            catch { /* never throw from the input gate */ }
        }

        private bool TryGetGesturePointerIds(out uint firstPointerId, out uint secondPointerId)
        {
            firstPointerId = 0;
            secondPointerId = 0;
            var found = 0;
            foreach (var pointerId in _touchPointerOrder)
            {
                if (!_activeTouchPointers.Contains(pointerId))
                    continue;
                if (found++ == 0)
                    firstPointerId = pointerId;
                else
                {
                    secondPointerId = pointerId;
                    return true;
                }
            }
            return false;
        }

        private static Point GetPosition(PointerEventArgs e)
        {
            var position = e.CurrentPoint.Position;
            return new Point(position.X, position.Y);
        }

        private static void LogGatePress(
            PointerDeviceType device,
            PointerEventArgs e,
            string decision)
        {
            try
            {
                var p = e.CurrentPoint.Position;
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] gate press device={device} pos=({p.X:0.#},{p.Y:0.#}) -> {decision}",
                    LogHelper.LogType.Event);
            }
            catch { /* never throw from the input gate */ }
        }
    }
}
