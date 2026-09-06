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
        private readonly Action<PointerEventArgs> _onInkPointerPress;
        private readonly Action<PointerEventArgs> _onInkPointerMove;
        private readonly Action<PointerEventArgs> _onInkPointerRelease;
        private readonly Action _onStrokeEnded;
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
        private readonly Dictionary<uint, bool> _touchGestureInProgress = new Dictionary<uint, bool>();
        private readonly HashSet<uint> _activeTouchPointers = new HashSet<uint>();
        private readonly HashSet<uint> _chromeForwardedPointers = new HashSet<uint>();
        private readonly HashSet<uint> _inkingPointers = new HashSet<uint>();
        private uint _mouseForwardingPointerId;
        private long _lastInkMoveForwardTicks;
        private volatile bool _isGestureInProgress;

        public WinRTInkInputGate(
            Func<PointerEventArgs, PointerGateResult> classifyPointer,
            Action<PointerEventArgs> onChromePointerDown,
            Action<PointerEventArgs> onChromePointerMove,
            Action<PointerEventArgs> onChromePointerRelease,
            Action<PointerEventArgs> onInkPointerPress,
            Action<PointerEventArgs> onInkPointerMove,
            Action<PointerEventArgs> onInkPointerRelease,
            Action onStrokeEnded,
            Action onStrokeCanceled)
        {
            _classifyPointer = classifyPointer ?? throw new ArgumentNullException(nameof(classifyPointer));
            _onChromePointerDown = onChromePointerDown;
            _onChromePointerMove = onChromePointerMove;
            _onChromePointerRelease = onChromePointerRelease;
            _onInkPointerPress = onInkPointerPress;
            _onInkPointerMove = onInkPointerMove;
            _onInkPointerRelease = onInkPointerRelease;
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
            // The ink thread observes this on the next moving/releasing event and marks it
            // Handled so the presenter drops the in-progress stroke.
            _cancelAll = true;
        }

        public void OnPointerPressing(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            var device = e.CurrentPoint.PointerDevice.PointerDeviceType;

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

            if (device == PointerDeviceType.Touch)
            {
                _activeTouchPointers.Add(pointerId);

                if (!_multiTouchWriting && _twoFingerGestureAllowed
                    && _activeTouchPointers.Count >= 2)
                {
                    // The second touch finger turns this into a two-finger gesture. Mark both
                    // pointers handled so the presenter cancels the first wet stroke as well.
                    _isGestureInProgress = true;
                    foreach (var activePointerId in _activeTouchPointers)
                        _touchGestureInProgress[activePointerId] = true;
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
                        _touchGestureInProgress[pointerId] = true;
                        e.Handled = true;
                        LogGatePress(device, e, "palm-eraser");
                        return;
                    }
                }
            }

            // Chrome / foreign-window classification: pointer input that reaches the ink
            // HWND bypasses the overlay WM_NCHITTEST pass-through, so the classifier below
            // is the authoritative gate. It also decides whether a blocked press should be
            // re-dispatched to the window under the pointer (BlockAndForward).
            var result = _classifyPointer(e);
            LogGatePress(device, e, result.ToString());
            switch (result)
            {
                case PointerGateResult.AllowInk:
                    _inkingPointers.Add(pointerId);
                    try { _onInkPointerPress?.Invoke(e); }
                    catch { /* tracking is best-effort */ }
                    return;

                case PointerGateResult.BlockAndForward:
                    e.Handled = true;
                    _chromeForwardedPointers.Add(pointerId);
                    if (_mouseForwardingPointerId == 0)
                    {
                        _mouseForwardingPointerId = pointerId;
                        try { _onChromePointerDown?.Invoke(e); }
                        catch { /* forwarding is best-effort */ }
                    }
                    return;

                default:
                    e.Handled = true;
                    return;
            }
        }

        public void OnPointerMoving(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            if (_cancelAll)
            {
                e.Handled = true;
                _cancelAll = false;
                return;
            }
            if (_touchGestureInProgress.ContainsKey(pointerId))
            {
                e.Handled = true;
                return;
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
                // Pause-straighten movement feed. Throttled to ~66 Hz: the UI thread only
                // needs to know "movement happened" to reset its pause timer, and forwarding
                // every pointer update would flood the dispatcher queue.
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                var elapsedMs = (now - _lastInkMoveForwardTicks) * 1000.0
                                / System.Diagnostics.Stopwatch.Frequency;
                if (elapsedMs >= 15)
                {
                    _lastInkMoveForwardTicks = now;
                    try { _onInkPointerMove?.Invoke(e); }
                    catch { /* tracking is best-effort */ }
                }
            }
        }

        public void OnPointerReleasing(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            _activeTouchPointers.Remove(pointerId);

            if (_inkingPointers.Remove(pointerId))
            {
                try { _onInkPointerRelease?.Invoke(e); }
                catch { /* tracking is best-effort */ }
            }

            if (_touchGestureInProgress.Remove(pointerId))
            {
                e.Handled = true;
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
            else if (_cancelAll)
            {
                e.Handled = true;
                _cancelAll = false;
            }

            if (_activeTouchPointers.Count == 0)
            {
                _touchGestureInProgress.Clear();
                _isGestureInProgress = false;
            }
        }

        /// <summary>
        /// Called on the ink thread when the presenter finalizes a stroke. Returns true when
        /// the stroke is a live candidate for custom drying (i.e. we let it through).
        /// </summary>
        public bool OnStrokeEnded(InkStrokeInput sender, PointerEventArgs e)
        {
            if (_cancelAll)
            {
                _cancelAll = false;
                _onStrokeCanceled();
                return false;
            }
            _onStrokeEnded();
            return true;
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
