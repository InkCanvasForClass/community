using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// Bridges WPF Stylus/Touch samples into the native ink controller on systems where
    /// the legacy WPF input stack consumes pen/touch before the HWND receives WM_POINTER.
    /// This is only an input source; rendering and dry commit still use the native pipeline.
    /// </summary>
    internal sealed class WpfPointerInputSource : IDisposable
    {
        private const uint StylusPointerNamespace = 0x40000000;
        private const uint TouchPointerNamespace = 0x80000000;

        private readonly UIElement _source;
        private readonly IInputElement _relativeTo;
        private readonly NativePointerInputHandler _handler;
        private readonly Dictionary<uint, long> _lastTimestamps = new Dictionary<uint, long>();
        private readonly Dictionary<uint, uint> _frameIds = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, StylusPoint> _lastStylusPoints = new Dictionary<uint, StylusPoint>();
        // 落笔代际（按 pointerId，含 Stylus/Touch namespace）：每笔递增。捕获丢失的兜底
        // Cancel 必须延迟确认（等正常 Up 先 End 会话），并保证不误杀此后开始的新一笔。
        private readonly Dictionary<uint, long> _downGenerations = new Dictionary<uint, long>();
        private bool _disposed;

        public WpfPointerInputSource(
            UIElement source,
            IInputElement relativeTo,
            NativePointerInputHandler handler)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _relativeTo = relativeTo ?? throw new ArgumentNullException(nameof(relativeTo));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));

            _source.AddHandler(Stylus.StylusDownEvent, new StylusDownEventHandler(OnStylusDown), true);
            _source.AddHandler(Stylus.StylusMoveEvent, new StylusEventHandler(OnStylusMove), true);
            _source.AddHandler(Stylus.StylusUpEvent, new StylusEventHandler(OnStylusUp), true);
            _source.AddHandler(Stylus.LostStylusCaptureEvent, new StylusEventHandler(OnStylusCaptureLost), true);
            _source.AddHandler(UIElement.TouchDownEvent, new EventHandler<TouchEventArgs>(OnTouchDown), true);
            _source.AddHandler(UIElement.TouchMoveEvent, new EventHandler<TouchEventArgs>(OnTouchMove), true);
            _source.AddHandler(UIElement.TouchUpEvent, new EventHandler<TouchEventArgs>(OnTouchUp), true);
            _source.AddHandler(UIElement.LostTouchCaptureEvent, new EventHandler<TouchEventArgs>(OnTouchCaptureLost), true);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _source.RemoveHandler(Stylus.StylusDownEvent, new StylusDownEventHandler(OnStylusDown));
            _source.RemoveHandler(Stylus.StylusMoveEvent, new StylusEventHandler(OnStylusMove));
            _source.RemoveHandler(Stylus.StylusUpEvent, new StylusEventHandler(OnStylusUp));
            _source.RemoveHandler(Stylus.LostStylusCaptureEvent, new StylusEventHandler(OnStylusCaptureLost));
            _source.RemoveHandler(UIElement.TouchDownEvent, new EventHandler<TouchEventArgs>(OnTouchDown));
            _source.RemoveHandler(UIElement.TouchMoveEvent, new EventHandler<TouchEventArgs>(OnTouchMove));
            _source.RemoveHandler(UIElement.TouchUpEvent, new EventHandler<TouchEventArgs>(OnTouchUp));
            _source.RemoveHandler(UIElement.LostTouchCaptureEvent, new EventHandler<TouchEventArgs>(OnTouchCaptureLost));
            _lastTimestamps.Clear();
            _frameIds.Clear();
            _lastStylusPoints.Clear();
            _downGenerations.Clear();
        }

        private void OnStylusDown(object sender, StylusDownEventArgs e)
        {
            if (IsTouchStylus(e.StylusDevice))
                return;
            var pointerId = StylusPointerNamespace | (unchecked((uint)e.StylusDevice.Id) & 0x3FFFFFFF);
            _downGenerations[pointerId] =
                (_downGenerations.TryGetValue(pointerId, out var previous) ? previous : 0) + 1;
            DispatchStylus(e, NativePointerMessageKind.Down);
        }

        private void OnStylusMove(object sender, StylusEventArgs e)
        {
            if (IsTouchStylus(e.StylusDevice))
                return;
            DispatchStylus(e, NativePointerMessageKind.Update);
        }

        private void OnStylusUp(object sender, StylusEventArgs e)
        {
            if (IsTouchStylus(e.StylusDevice))
                return;
            var pointerId = StylusPointerNamespace | (unchecked((uint)e.StylusDevice.Id) & 0x3FFFFFFF);
            DispatchStylus(e, NativePointerMessageKind.Up);
            _downGenerations.Remove(pointerId);
        }

        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            if (_disposed || e?.TouchDevice == null)
                return;
            var pointerId = TouchPointerNamespace | (unchecked((uint)e.TouchDevice.Id) & 0x3FFFFFFF);
            _downGenerations[pointerId] =
                (_downGenerations.TryGetValue(pointerId, out var previous) ? previous : 0) + 1;
            DispatchTouch(e, NativePointerMessageKind.Down);
        }

        private void OnTouchMove(object sender, TouchEventArgs e) =>
            DispatchTouch(e, NativePointerMessageKind.Update);

        private void OnTouchUp(object sender, TouchEventArgs e)
        {
            if (_disposed || e?.TouchDevice == null)
                return;
            var pointerId = TouchPointerNamespace | (unchecked((uint)e.TouchDevice.Id) & 0x3FFFFFFF);
            DispatchTouch(e, NativePointerMessageKind.Up);
            _downGenerations.Remove(pointerId);
        }

        /// <summary>
        /// 捕获丢失的兜底结束。真实红外框/数字化仪的触摸/笔可能不发 Up 而发捕获丢失
        /// （第二指落下、系统手势接管、捕获被抢占、窗口失活等）。若不结束会话，
        /// NativeInkController 会残留 Active 会话，下一笔 Begin 先 Cancel 上一笔
        /// → 只能写一笔 + 湿墨 overlay 卡死。
        ///
        /// 不能在这里同步 Cancel：正常抬起时序是
        ///   PreviewUp(tunneling) → legacy ReleaseAllTouchCaptures
        ///     → Lost*Capture(bubbling)  ← 此时 Up 还没处理！
        ///   → Up(bubbling) → End 会话
        /// 同步 Cancel 会在 Up 之前清掉会话，导致 End 找不到会话、整笔不提交
        /// （表现为「不能落笔」）。因此延迟到 Dispatcher 队列：正常 Up 先 End（并移除代际），
        /// 延迟回调发现代际已消失就跳过；只有 Up 确实不来的异常场景才兜底 Cancel，
        /// 且要求代际未变，避免误杀 Lost*Capture 之后立即开始的新一笔。
        /// </summary>
        private void DeferredCancelOnCaptureLost(uint pointerId, NativeInkInputKind inputKind)
        {
            if (!_downGenerations.TryGetValue(pointerId, out var generation))
                return;

            var capturedGeneration = generation;
            try
            {
                _source.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_disposed)
                        return;
                    if (_downGenerations.TryGetValue(pointerId, out var current)
                        && current == capturedGeneration)
                    {
                        DispatchCanceled(pointerId, inputKind);
                        _downGenerations.Remove(pointerId);
                    }
                }), DispatcherPriority.Input);
            }
            catch
            {
                // Dispatcher shutting down — leave the session to the Up path.
            }
        }

        private void OnTouchCaptureLost(object sender, TouchEventArgs e)
        {
            if (_disposed || e?.TouchDevice == null)
                return;
            var pointerId = TouchPointerNamespace | (unchecked((uint)e.TouchDevice.Id) & 0x3FFFFFFF);
            DeferredCancelOnCaptureLost(pointerId, NativeInkInputKind.Touch);
        }

        private void OnStylusCaptureLost(object sender, StylusEventArgs e)
        {
            if (_disposed || e?.StylusDevice == null)
                return;
            // 触摸型笔的捕获丢失走 LostTouchCapture，这里只处理真实笔。
            if (IsTouchStylus(e.StylusDevice))
                return;
            var pointerId = StylusPointerNamespace | (unchecked((uint)e.StylusDevice.Id) & 0x3FFFFFFF);
            DeferredCancelOnCaptureLost(pointerId, NativeInkInputKind.Pen);
        }

        private void DispatchCanceled(uint pointerId, NativeInkInputKind inputKind)
        {
            try
            {
                _handler(new NativePointerInputBatch(
                    pointerId,
                    inputKind,
                    NativePointerMessageKind.CaptureLost,
                    Array.Empty<RawInkSample>(),
                    false,
                    false,
                    true,
                    isWpfFallback: true));
                Forget(pointerId);
            }
            catch
            {
                // WPF input must continue even if fallback sample conversion fails.
            }
        }

        private void DispatchStylus(StylusEventArgs e, NativePointerMessageKind messageKind)
        {
            if (_disposed || e?.StylusDevice == null)
                return;

            try
            {
                var pointerId = StylusPointerNamespace | (unchecked((uint)e.StylusDevice.Id) & 0x3FFFFFFF);
                var stylusPoints = e.GetStylusPoints(_relativeTo);
                var samples = CreateStylusSamples(pointerId, stylusPoints, messageKind);
                var secondaryButtonDown = IsSecondaryButtonDown(e.StylusDevice);
                var handled = _handler(new NativePointerInputBatch(
                    pointerId,
                    NativeInkInputKind.Pen,
                    messageKind,
                    samples,
                    secondaryButtonDown,
                    false,
                    false,
                    isWpfFallback: true));
                if (handled)
                    e.Handled = true;
                if (messageKind == NativePointerMessageKind.Up)
                    Forget(pointerId);
            }
            catch
            {
                // WPF input must continue even if fallback sample conversion fails.
            }
        }

        private void DispatchTouch(TouchEventArgs e, NativePointerMessageKind messageKind)
        {
            if (_disposed || e?.TouchDevice == null)
                return;

            try
            {
                var pointerId = TouchPointerNamespace | (unchecked((uint)e.TouchDevice.Id) & 0x3FFFFFFF);
                var touchPoint = e.GetTouchPoint(_relativeTo);
                var timestamp = NextTimestamp(pointerId, 1)[0];
                var frameId = NextFrameId(pointerId);
                var flags = messageKind == NativePointerMessageKind.Up
                    ? NativeInkSampleFlags.Primary
                    : NativeInkSampleFlags.InContact | NativeInkSampleFlags.Primary;
                var sample = new RawInkSample(
                    pointerId,
                    NativeInkInputKind.Touch,
                    touchPoint.Position.X,
                    touchPoint.Position.Y,
                    0.5f,
                    false,
                    timestamp,
                    frameId,
                    flags,
                    (float)Math.Max(0, touchPoint.Bounds.Width),
                    (float)Math.Max(0, touchPoint.Bounds.Height));
                var handled = _handler(new NativePointerInputBatch(
                    pointerId,
                    NativeInkInputKind.Touch,
                    messageKind,
                    new[] { sample },
                    false,
                    false,
                    false,
                    isWpfFallback: true));
                if (handled)
                    e.Handled = true;
                if (messageKind == NativePointerMessageKind.Up)
                    Forget(pointerId);
            }
            catch
            {
                // WPF input must continue even if fallback sample conversion fails.
            }
        }

        private RawInkSample[] CreateStylusSamples(
            uint pointerId,
            StylusPointCollection stylusPoints,
            NativePointerMessageKind messageKind)
        {
            var count = Math.Max(1, stylusPoints?.Count ?? 0);
            var timestamps = NextTimestamp(pointerId, count);
            var chronological = new RawInkSample[count];
            if (stylusPoints == null || stylusPoints.Count == 0)
            {
                _lastStylusPoints.TryGetValue(pointerId, out var point);
                chronological[0] = new RawInkSample(
                    pointerId,
                    NativeInkInputKind.Pen,
                    point.X,
                    point.Y,
                    Clamp(point.PressureFactor),
                    true,
                    timestamps[0],
                    NextFrameId(pointerId),
                    ToFlags(messageKind));
            }
            else
            {
                for (var i = 0; i < stylusPoints.Count; i++)
                {
                    var point = stylusPoints[i];
                    chronological[i] = new RawInkSample(
                        pointerId,
                        NativeInkInputKind.Pen,
                        point.X,
                        point.Y,
                        Clamp(point.PressureFactor),
                        true,
                        timestamps[i],
                        NextFrameId(pointerId),
                        ToFlags(messageKind));
                }
                _lastStylusPoints[pointerId] = stylusPoints[stylusPoints.Count - 1];
            }

            // NativePointerInputBatch contract is newest-first.
            Array.Reverse(chronological);
            return chronological;
        }

        private long[] NextTimestamp(uint pointerId, int count)
        {
            var now = NativePointerTimestampConverter.FromCurrentStopwatch();
            _lastTimestamps.TryGetValue(pointerId, out var last);
            var first = Math.Max(last + 1, now - Math.Max(0, count - 1) * 1000L);
            var result = new long[count];
            for (var i = 0; i < count; i++)
                result[i] = first + i * 1000L;
            _lastTimestamps[pointerId] = result[count - 1];
            return result;
        }

        private uint NextFrameId(uint pointerId)
        {
            _frameIds.TryGetValue(pointerId, out var frameId);
            frameId++;
            _frameIds[pointerId] = frameId;
            return frameId;
        }

        private void Forget(uint pointerId)
        {
            _lastTimestamps.Remove(pointerId);
            _frameIds.Remove(pointerId);
            _lastStylusPoints.Remove(pointerId);
        }

        private static NativeInkSampleFlags ToFlags(NativePointerMessageKind messageKind) =>
            messageKind == NativePointerMessageKind.Up
                ? NativeInkSampleFlags.Primary
                : NativeInkSampleFlags.InContact | NativeInkSampleFlags.Primary;

        private static bool IsTouchStylus(StylusDevice stylusDevice) =>
            stylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch;

        private static bool IsSecondaryButtonDown(StylusDevice stylusDevice)
        {
            if (stylusDevice?.StylusButtons == null)
                return false;
            for (var i = 0; i < stylusDevice.StylusButtons.Count; i++)
            {
                if (stylusDevice.StylusButtons[i].StylusButtonState == StylusButtonState.Down)
                    return true;
            }
            return false;
        }

        private static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.5f;
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
