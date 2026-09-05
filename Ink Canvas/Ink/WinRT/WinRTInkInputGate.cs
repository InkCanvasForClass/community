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
    /// Runs on the ink background thread (CoreInkIndependentInputSource events) and decides,
    /// per pointer, whether the InkPresenter may ink or the input must be suppressed.
    /// Cheap, lock-free: reads volatile snapshots refreshed by the UI thread and only
    /// toggles Handled / cancellation flags. No WPF calls here.
    /// </summary>
    internal sealed class WinRTInkInputGate
    {
        private readonly Func<uint, bool> _allowPointerToInk;
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
        private volatile bool _isGestureInProgress;

        public WinRTInkInputGate(
            Func<uint, bool> allowPointerToInk,
            Action onStrokeEnded,
            Action onStrokeCanceled)
        {
            _allowPointerToInk = allowPointerToInk ?? throw new ArgumentNullException(nameof(allowPointerToInk));
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
                return;
            }

            // UI chrome / foreign windows are handled by the overlay WM_NCHITTEST, which
            // turns the overlay HTTRANSPARENT there so no pointer reaches the presenter.
            // Here we only gate the canvas-wide conditions.
            if (!_canvasInputEnabled || _pageFrozen)
            {
                e.Handled = true;
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
                        return;
                    }
                }
            }

            if (!_allowPointerToInk(pointerId))
            {
                e.Handled = true;
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
            }
        }

        public void OnPointerReleasing(CoreInkIndependentInputSource sender, PointerEventArgs e)
        {
            var pointerId = e.CurrentPoint.PointerId;
            _activeTouchPointers.Remove(pointerId);

            if (_touchGestureInProgress.Remove(pointerId))
            {
                e.Handled = true;
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
    }
}
