using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class NativePointerInputSource : IDisposable
    {
        private const int WmPointerUpdate = 0x0245;
        private const int WmPointerDown = 0x0246;
        private const int WmPointerUp = 0x0247;
        private const int WmPointerCaptureChanged = 0x024C;
        private const int WmMouseMove = 0x0200;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;
        private const int WmInput = 0x00FF;
        private const uint MouseKeyLeftButton = 0x0001;
        private const uint PointerFlagInContact = 0x00000004;
        private const uint PointerFlagSecondButton = 0x00000020;
        private const uint PointerFlagPrimary = 0x00002000;
        private const uint PointerFlagCanceled = 0x00008000;
        private const uint PenFlagBarrel = 0x00000001;
        private const uint PenFlagInverted = 0x00000002;
        private const uint PenFlagEraser = 0x00000004;
        private const uint PenMaskPressure = 0x00000001;
        private const uint TouchMaskContactArea = 0x00000001;
        private const uint TouchMaskPressure = 0x00000004;
        private const uint MiWpSignature = 0xFF515700;
        private const uint SignatureMask = 0xFFFFFF00;
        private const ulong TouchPromotionBit = 0x00000080;
        private const uint MousePointerId = uint.MaxValue;
        private const uint PromotedPenMousePointerId = uint.MaxValue - 1;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorNoData = 232;
        private const int MaximumHistoryEntries = 4096;

        // ---- RawInput (WM_INPUT) 鼠标高频采样 ----
        private const uint RidInput = 0x10000003;
        private const uint RimTypeMouse = 0;
        private const ushort RiMouseLeftButtonDown = 0x0001;
        private const ushort RiMouseLeftButtonUp = 0x0002;
        private const uint RidDeviceInputSink = 0x00000100; // RIDEV_INPUTSINK
        private const ushort HIDUsagePageGenericDesktop = 0x01;
        private const ushort HIDUsageMouse = 0x02;

        private readonly HwndSource _source;
        private readonly NativePointerInputHandler _handler;
        private readonly Dictionary<uint, NativeInkInputKind> _activePointerKinds = new Dictionary<uint, NativeInkInputKind>();
        private double _dpiScaleX;
        private double _dpiScaleY;
        private bool _mouseInContact;
        private uint _mouseFrameId;
        private bool _disposed;
        // RawInput 鼠标：注册成功时 ink 走 WM_INPUT（设备原生采样率），
        // legacy WM_MOUSEMOVE 只负责"是否阻止 WPF 处理"，避免双墨迹/双擦除。
        private bool _rawMouseActive;
        // 本笔接触期间 WM_INPUT 是否已提供过 Update 样本。为 true 时 legacy
        // WM_MOUSEMOVE 被拦截（样本由 raw 高频提供）；为 false 时回退用 legacy
        // move 喂样本，覆盖「触摸框以注入鼠标上报、不走 RawInput」的设备。
        private bool _rawMouseUpdateDeliveredInContact;
        // 诊断：raw vs legacy 鼠标 sample 计数（供 RealtimeInkDebugLive.json）。
        private long _rawMouseSampleCount;
        private long _legacyMouseSampleCount;
        private int _rawMouseRegisterError;
        public long RawMouseSampleCount => System.Threading.Volatile.Read(ref _rawMouseSampleCount);
        public long LegacyMouseSampleCount => System.Threading.Volatile.Read(ref _legacyMouseSampleCount);
        public bool RawMouseActive => _rawMouseActive;
        public int RawMouseRegisterError => System.Threading.Volatile.Read(ref _rawMouseRegisterError);

        public NativePointerInputSource(
            HwndSource source,
            NativePointerInputHandler handler,
            double dpiScaleX,
            double dpiScaleY)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (dpiScaleX <= 0) throw new ArgumentOutOfRangeException(nameof(dpiScaleX));
            if (dpiScaleY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiScaleY));
            _dpiScaleX = dpiScaleX;
            _dpiScaleY = dpiScaleY;
            _source.AddHook(WndProc);
            TryRegisterRawMouse(source.Handle);
        }

        /// <summary>
        /// 注册 RawInput 鼠标（RIDEV_INPUTSINK，不抑制 legacy 鼠标消息以保留 WPF UI 交互）。
        /// 注册失败静默回退到 WM_MOUSEMOVE 路径。
        /// </summary>
        private void TryRegisterRawMouse(IntPtr hwnd)
        {
            try
            {
                var device = new RawInputDevice
                {
                    UsagePage = HIDUsagePageGenericDesktop,
                    Usage = HIDUsageMouse,
                    Flags = RidDeviceInputSink,
                    Target = hwnd
                };
                var cbSize = (uint)Marshal.SizeOf<RawInputDevice>();
                var result = RegisterRawInputDevices(new[] { device }, 1, cbSize);
                if (result)
                {
                    _rawMouseActive = true;
                    System.Diagnostics.Debug.WriteLine($"[WetInk] RawInput mouse registered (cbSize={cbSize}, hwnd=0x{hwnd.ToInt64():X})");
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    _rawMouseRegisterError = error;
                    System.Diagnostics.Debug.WriteLine($"[WetInk] RegisterRawInputDevices failed: Win32 {error}, cbSize={cbSize}, hwnd=0x{hwnd.ToInt64():X}");
                }
            }
            catch (Exception ex)
            {
                _rawMouseActive = false;
                System.Diagnostics.Debug.WriteLine($"[WetInk] RegisterRawInputDevices threw: {ex.Message}");
            }
        }

        public void UpdateDpi(double dpiScaleX, double dpiScaleY)
        {
            if (dpiScaleX <= 0) throw new ArgumentOutOfRangeException(nameof(dpiScaleX));
            if (dpiScaleY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiScaleY));
            _dpiScaleX = dpiScaleX;
            _dpiScaleY = dpiScaleY;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _source.RemoveHook(WndProc);
        }

        private IntPtr WndProc(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (_disposed)
                return IntPtr.Zero;

            switch (message)
            {
                case WmPointerDown:
                    DispatchPointer(hwnd, wParam, lParam, NativePointerMessageKind.Down, ref handled);
                    break;
                case WmPointerUpdate:
                    DispatchPointer(hwnd, wParam, lParam, NativePointerMessageKind.Update, ref handled);
                    break;
                case WmPointerUp:
                    DispatchPointer(hwnd, wParam, lParam, NativePointerMessageKind.Up, ref handled);
                    break;
                case WmPointerCaptureChanged:
                    DispatchCaptureLost(wParam, ref handled);
                    break;
                case WmLeftButtonDown:
                    // ink 会话生命周期统一由 legacy WM_LBUTTONDOWN/UP 建立/结束（可靠、成对到达），
                    // WM_INPUT 只负责接触期间的高频取位。直接走 DispatchMouse 建会话并按路由结果阻止 WPF。
                    if (_rawMouseActive && !IsPromotedPenOrTouchMouse())
                        _rawMouseUpdateDeliveredInContact = false;
                    DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Down, ref handled);
                    break;
                case WmMouseMove:
                    if (_mouseInContact || (LowWord(wParam) & MouseKeyLeftButton) != 0)
                    {
                        if (_rawMouseActive && !IsPromotedPenOrTouchMouse())
                        {
                            // 绘制期间阻止 WPF 处理 legacy move（防双墨迹/双擦除），
                            // 高频样本由 WM_INPUT Update 提供。若本笔 raw 还没提供过
                            // Update（注入鼠标触摸框不产生 WM_INPUT），回退到 legacy move，
                            // 避免整笔只剩落笔/抬笔两点。
                            if (_rawMouseUpdateDeliveredInContact)
                                handled = true;
                            else
                                DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Update, ref handled);
                            break;
                        }
                        DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Update, ref handled);
                    }
                    break;
                case WmLeftButtonUp:
                    // 抬笔必须经 DispatchMouse 结束会话并提交干墨：WM_INPUT Up 可能缺失/乱序，
                    // 若只复位标志而不结束会话，下一笔 Begin 会 Cancel 上一笔 → 触摸只能写一笔。
                    DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Up, ref handled);
                    break;
                case WmInput:
                    if (_rawMouseActive)
                        DispatchRawMouse(hwnd, lParam, ref handled);
                    break;
            }

            return IntPtr.Zero;
        }

        private void DispatchPointer(
            IntPtr hwnd,
            IntPtr wParam,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            ref bool handled)
        {
            try
            {
                var pointerId = LowWord(wParam);
                if (!GetPointerType(pointerId, out var pointerType))
                    return;

                NativePointerInputBatch batch;
                switch (pointerType)
                {
                    case PointerInputType.Pen:
                        if (!TryReadPenBatch(hwnd, pointerId, lParam, messageKind, out batch))
                            return;
                        break;
                    case PointerInputType.Touch:
                        if (!TryReadTouchBatch(hwnd, pointerId, lParam, messageKind, out batch))
                            return;
                        break;
                    default:
                        return;
                }

                if (messageKind == NativePointerMessageKind.Down)
                    _activePointerKinds[pointerId] = batch.InputKind;
                else if (messageKind == NativePointerMessageKind.Up)
                    _activePointerKinds.Remove(pointerId);
                handled = _handler(batch);
            }
            catch (Exception)
            {
                // Never let pointer-hook failures break the WPF message loop.
                handled = false;
            }
        }

        private void DispatchCaptureLost(IntPtr wParam, ref bool handled)
        {
            var pointerId = LowWord(wParam);
            if (!_activePointerKinds.TryGetValue(pointerId, out var inputKind))
                return;
            _activePointerKinds.Remove(pointerId);

            handled = _handler(new NativePointerInputBatch(
                pointerId,
                inputKind,
                NativePointerMessageKind.CaptureLost,
                Array.Empty<RawInkSample>(),
                false,
                false,
                true));
        }

        /// <summary>
        /// 当前鼠标消息是否实为「笔/触摸提升」的伪鼠标消息（GetMessageExtraInfo 签名位）。
        /// 这类消息继续走 WM_MOUSEMOVE（raw mouse 只服务真实鼠标，避免笔/触摸双处理）。
        /// </summary>
        private static bool IsPromotedPenOrTouchMouse()
        {
            return GetPromotedPointerKind().HasValue;
        }

        /// <summary>
        /// 处理 WM_INPUT（RawInput 鼠标）：从设备原生采样率取按钮状态与光标位置，
        /// 生成与 WM_MOUSEMOVE 相同的 batch 交给 handler。
        /// </summary>
        private void DispatchRawMouse(IntPtr hwnd, IntPtr lParam, ref bool handled)
        {
            try
            {
                uint size = 0;
                if (GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) != 0
                    || size == 0 || size > 1024)
                {
                    return;
                }

                var buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    var read = GetRawInputData(lParam, RidInput, buffer, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
                    if (read != size)
                        return;

                    var raw = Marshal.PtrToStructure<RawInput>(buffer);
                    if (raw.Header.Type != RimTypeMouse)
                        return;

                    // 触摸/笔提升的伪鼠标 raw input 会携带 WI_SIGNATURE（0xFF515700），
                    // 这类输入由 WM_POINTER / promoted legacy 消息处理，不得进入 raw 鼠标路径。
                    // 对 legacy 消息 GetMessageExtraInfo 可靠，但对 WM_INPUT 并不可靠，
                    // 这里直接用 raw 自带的 ExtraInformation 判定。
                    if (((uint)raw.Mouse.ExtraInformation & SignatureMask) == MiWpSignature)
                        return;

                    var buttonFlags = (ushort)(raw.Mouse.Buttons & 0xFFFF);
                    var leftDown = (buttonFlags & RiMouseLeftButtonDown) != 0;
                    var leftUp = (buttonFlags & RiMouseLeftButtonUp) != 0;

                    // 落笔/抬笔的会话生命周期交给 legacy WM_LBUTTONDOWN/UP（可靠、成对到达），
                    // WM_INPUT 只负责接触期间的高频取位：Down/Up 仅翻转接触标志、不建立/结束会话，
                    // 避免「WM_INPUT Up 缺失/乱序」导致会话永不结束、下一笔 Begin 把上一笔干墨取消
                    // （表现为触摸只能写一笔）。
                    if (leftDown)
                    {
                        _mouseInContact = true;
                        _rawMouseUpdateDeliveredInContact = false;
                        handled = false;
                        return;
                    }
                    if (leftUp)
                    {
                        _mouseInContact = false;
                        handled = false;
                        return;
                    }
                    if (!_mouseInContact)
                        return; // 无接触的移动不产生 ink

                    // 绝对坐标：GetCursorPos（屏幕）→ ScreenToClient（客户区）。
                    var point = new NativePoint(0, 0);
                    if (!GetCursorPos(ref point) || !ScreenToClient(hwnd, ref point))
                        return;

                    var flags = NativeInkSampleFlags.InContact | NativeInkSampleFlags.Primary;
                    var sample = new RawInkSample(
                        MousePointerId,
                        NativeInkInputKind.Mouse,
                        point.X / _dpiScaleX,
                        point.Y / _dpiScaleY,
                        0.5f,
                        false,
                        NativePointerTimestampConverter.FromCurrentStopwatch(),
                        ++_mouseFrameId,
                        flags);

                    var consumed = _handler(new NativePointerInputBatch(
                        MousePointerId,
                        NativeInkInputKind.Mouse,
                        NativePointerMessageKind.Update,
                        new[] { sample },
                        false,
                        false,
                        true));

                    _rawMouseUpdateDeliveredInContact = true;
                    System.Threading.Interlocked.Increment(ref _rawMouseSampleCount);
                    handled = consumed;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception)
            {
                // 不因 raw mouse 处理失败破坏 WPF message loop。
                handled = false;
            }
        }

        private void DispatchMouse(
            IntPtr hwnd,
            IntPtr wParam,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            ref bool handled)
        {
            var promotion = GetPromotedPointerKind();
            var promoted = promotion.HasValue;
            var inputKind = promotion == NativeInkInputKind.Pen
                ? NativeInkInputKind.Pen
                : NativeInkInputKind.Mouse;
            var pointerId = promotion == NativeInkInputKind.Pen
                ? PromotedPenMousePointerId
                : MousePointerId;
            if (messageKind == NativePointerMessageKind.Down)
                _mouseInContact = true;
            else if (messageKind == NativePointerMessageKind.Up)
                _mouseInContact = false;

            var point = new NativePoint(SignedLowWord(lParam), SignedHighWord(lParam));
            if (!ClientToScreen(hwnd, ref point))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var clientOrigin = GetClientOrigin(hwnd);
            var flags = messageKind == NativePointerMessageKind.Up
                ? NativeInkSampleFlags.Primary
                : NativeInkSampleFlags.InContact | NativeInkSampleFlags.Primary;
            var sample = new RawInkSample(
                pointerId,
                inputKind,
                (point.X - clientOrigin.X) / _dpiScaleX,
                (point.Y - clientOrigin.Y) / _dpiScaleY,
                0.5f,
                false,
                ResolveTimestampMicroseconds(0, unchecked((uint)GetMessageTime())),
                ++_mouseFrameId,
                flags);
            handled = _handler(new NativePointerInputBatch(
                pointerId,
                inputKind,
                messageKind,
                new[] { sample },
                (LowWord(wParam) & 0x0002) != 0,
                promoted,
                true));
            System.Threading.Interlocked.Increment(ref _legacyMouseSampleCount);
        }

        private bool TryReadPenBatch(
            IntPtr hwnd,
            uint pointerId,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            out NativePointerInputBatch batch)
        {
            try
            {
                batch = ReadPenBatch(hwnd, pointerId, messageKind);
                return true;
            }
            catch (Exception exception)
            {
                var error = exception is Win32Exception win32 ? win32.NativeErrorCode : ErrorNoData;
                batch = CreateFallbackBatch(
                    hwnd,
                    pointerId,
                    NativeInkInputKind.Pen,
                    lParam,
                    messageKind,
                    error);
                return true;
            }
        }

        private bool TryReadTouchBatch(
            IntPtr hwnd,
            uint pointerId,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            out NativePointerInputBatch batch)
        {
            try
            {
                batch = ReadTouchBatch(hwnd, pointerId, messageKind);
                return true;
            }
            catch (Exception exception)
            {
                var error = exception is Win32Exception win32 ? win32.NativeErrorCode : ErrorNoData;
                batch = CreateFallbackBatch(
                    hwnd,
                    pointerId,
                    NativeInkInputKind.Touch,
                    lParam,
                    messageKind,
                    error);
                return true;
            }
        }

        private NativePointerInputBatch CreateFallbackBatch(
            IntPtr hwnd,
            uint pointerId,
            NativeInkInputKind inputKind,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            int error)
        {
            // WM_POINTER* lParam is already client coordinates.
            var flags = messageKind == NativePointerMessageKind.Up
                ? NativeInkSampleFlags.None
                : NativeInkSampleFlags.InContact;
            var sample = new RawInkSample(
                pointerId,
                inputKind,
                SignedLowWord(lParam) / _dpiScaleX,
                SignedHighWord(lParam) / _dpiScaleY,
                0.5f,
                false,
                NativePointerTimestampConverter.FromCurrentStopwatch(),
                0,
                flags);
            return new NativePointerInputBatch(
                pointerId,
                inputKind,
                messageKind,
                new[] { sample },
                false,
                false,
                false,
                error);
        }

        private NativePointerInputBatch ReadPenBatch(
            IntPtr hwnd,
            uint pointerId,
            NativePointerMessageKind messageKind)
        {
            var history = ReadPenHistory(pointerId, out var historyComplete, out var error);
            var clientOrigin = GetClientOrigin(hwnd);
            var samples = new RawInkSample[history.Length];
            var secondaryButtonDown = false;
            for (var i = 0; i < history.Length; i++)
            {
                var item = history[i];
                var pointerInfo = item.PointerInfo;
                var flags = ToSampleFlags(pointerInfo.PointerFlags, item.PenFlags);
                var hasPressure = (item.PenMask & PenMaskPressure) != 0;
                secondaryButtonDown |= (item.PenFlags & PenFlagBarrel) != 0
                                       || (pointerInfo.PointerFlags & PointerFlagSecondButton) != 0;
                samples[i] = new RawInkSample(
                    pointerId,
                    NativeInkInputKind.Pen,
                    (pointerInfo.PixelLocationRaw.X - clientOrigin.X) / _dpiScaleX,
                    (pointerInfo.PixelLocationRaw.Y - clientOrigin.Y) / _dpiScaleY,
                    hasPressure ? ClampPressure(item.Pressure) : 0.5f,
                    hasPressure,
                    ResolveTimestampMicroseconds(pointerInfo.PerformanceCount, pointerInfo.TimeMilliseconds),
                    pointerInfo.FrameId,
                    flags);
            }

            return NativePointerInputBatch.CreateFromOwnedSamples(
                pointerId,
                NativeInkInputKind.Pen,
                messageKind,
                samples,
                secondaryButtonDown,
                false,
                historyComplete,
                error);
        }

        private NativePointerInputBatch ReadTouchBatch(
            IntPtr hwnd,
            uint pointerId,
            NativePointerMessageKind messageKind)
        {
            var history = ReadTouchHistory(pointerId, out var historyComplete, out var error);
            var clientOrigin = GetClientOrigin(hwnd);
            var samples = new RawInkSample[history.Length];
            for (var i = 0; i < history.Length; i++)
            {
                var item = history[i];
                var pointerInfo = item.PointerInfo;
                var hasPressure = (item.TouchMask & TouchMaskPressure) != 0;
                var hasContactArea = (item.TouchMask & TouchMaskContactArea) != 0;
                samples[i] = new RawInkSample(
                    pointerId,
                    NativeInkInputKind.Touch,
                    (pointerInfo.PixelLocationRaw.X - clientOrigin.X) / _dpiScaleX,
                    (pointerInfo.PixelLocationRaw.Y - clientOrigin.Y) / _dpiScaleY,
                    hasPressure ? ClampPressure(item.Pressure) : 0.5f,
                    hasPressure,
                    ResolveTimestampMicroseconds(pointerInfo.PerformanceCount, pointerInfo.TimeMilliseconds),
                    pointerInfo.FrameId,
                    ToSampleFlags(pointerInfo.PointerFlags, 0),
                    hasContactArea ? Math.Abs(item.ContactRaw.Right - item.ContactRaw.Left) : 0,
                    hasContactArea ? Math.Abs(item.ContactRaw.Bottom - item.ContactRaw.Top) : 0);
            }

            return NativePointerInputBatch.CreateFromOwnedSamples(
                pointerId,
                NativeInkInputKind.Touch,
                messageKind,
                samples,
                false,
                false,
                historyComplete,
                error);
        }

        private static NativePointerPenInfo[] ReadPenHistory(
            uint pointerId,
            out bool historyComplete,
            out int error)
        {
            uint count = 0;
            // Query size first. Windows returns FALSE + ERROR_INSUFFICIENT_BUFFER with the count.
            // Never treat a zero-count success as a valid empty stroke payload.
            if (!GetPointerPenInfoHistory(pointerId, ref count, null))
            {
                error = Marshal.GetLastWin32Error();
                // count 超过防御上限时也要按上限读取，而不是放弃整批历史退回单点：
                // 后者恰好在「卡顿→合并历史最多」时丢弃最多采样。
                if (error == ErrorInsufficientBuffer && count > 0)
                {
                    var requested = LimitHistoryEntries(count);
                    var history = new NativePointerPenInfo[requested];
                    var capacity = requested;
                    if (GetPointerPenInfoHistory(pointerId, ref capacity, history))
                    {
                        historyComplete = capacity >= count;
                        error = 0;
                        NativeInkPerfProbe.RecordPointerHistory(
                            (int)count,
                            (int)capacity,
                            historyComplete);
                        return Trim(history, capacity);
                    }
                    error = Marshal.GetLastWin32Error();
                }
            }
            else
            {
                error = 0;
            }

            if (GetPointerPenInfo(pointerId, out var current))
            {
                historyComplete = false;
                error = 0;
                return new[] { current };
            }

            if (error == 0)
                error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Unable to copy pointer pen data for pointer {pointerId}.");
        }

        private static NativePointerTouchInfo[] ReadTouchHistory(
            uint pointerId,
            out bool historyComplete,
            out int error)
        {
            uint count = 0;
            if (!GetPointerTouchInfoHistory(pointerId, ref count, null))
            {
                error = Marshal.GetLastWin32Error();
                // 同 ReadPenHistory：超上限时按上限读取，不放弃整批历史。
                if (error == ErrorInsufficientBuffer && count > 0)
                {
                    var requested = LimitHistoryEntries(count);
                    var history = new NativePointerTouchInfo[requested];
                    var capacity = requested;
                    if (GetPointerTouchInfoHistory(pointerId, ref capacity, history))
                    {
                        historyComplete = capacity >= count;
                        error = 0;
                        NativeInkPerfProbe.RecordPointerHistory(
                            (int)count,
                            (int)capacity,
                            historyComplete);
                        return Trim(history, capacity);
                    }
                    error = Marshal.GetLastWin32Error();
                }
            }
            else
            {
                error = 0;
            }

            if (GetPointerTouchInfo(pointerId, out var current))
            {
                historyComplete = false;
                error = 0;
                return new[] { current };
            }

            if (error == 0)
                error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Unable to copy pointer touch data for pointer {pointerId}.");
        }

        private static uint LimitHistoryEntries(uint availableCount)
        {
            // 读全量合并历史。GetPointerPenInfoHistory/GetPointerTouchInfoHistory 不支持翻页：
            // 缓冲不足时系统只返回「最新 N 条」，更早的条目在下一条消息取走后即永久丢失
            // （见 MSDN remarks：insufficient buffer → most recent entries；retrieving the
            // next message → previous message info may no longer be available）。
            // UI 线程一卡（GC / 大重绘 / PPT 联动），单条 WM_POINTERUPDATE 的合并历史可达数百条，
            // 若在此截断，中间段会被静默丢弃 → ribbon 折线跨段直跳（拐角变直线）。
            // 因此只保留 MaximumHistoryEntries 这一个防御性上限。
            return Math.Min(availableCount, (uint)MaximumHistoryEntries);
        }

        private static NativePointerPenInfo[] Trim(NativePointerPenInfo[] source, uint count)
        {
            if (count >= source.Length)
                return source;
            var result = new NativePointerPenInfo[count];
            Array.Copy(source, result, result.Length);
            return result;
        }

        private static NativePointerTouchInfo[] Trim(NativePointerTouchInfo[] source, uint count)
        {
            if (count >= source.Length)
                return source;
            var result = new NativePointerTouchInfo[count];
            Array.Copy(source, result, result.Length);
            return result;
        }

        private static NativeInkSampleFlags ToSampleFlags(uint pointerFlags, uint penFlags)
        {
            var flags = NativeInkSampleFlags.None;
            if ((pointerFlags & PointerFlagInContact) != 0)
                flags |= NativeInkSampleFlags.InContact;
            if ((pointerFlags & PointerFlagPrimary) != 0)
                flags |= NativeInkSampleFlags.Primary;
            if ((pointerFlags & PointerFlagCanceled) != 0)
                flags |= NativeInkSampleFlags.Canceled;
            if ((penFlags & PenFlagInverted) != 0)
                flags |= NativeInkSampleFlags.Inverted;
            if ((penFlags & PenFlagEraser) != 0)
                flags |= NativeInkSampleFlags.Eraser;
            return flags;
        }

        private static long ResolveTimestampMicroseconds(ulong performanceCount, uint timeMilliseconds)
        {
            if (performanceCount != 0)
            {
                return NativePointerTimestampConverter.FromPerformanceCount(
                    performanceCount,
                    Stopwatch.Frequency);
            }
            if (timeMilliseconds != 0)
            {
                return NativePointerTimestampConverter.FromTickCount(
                    timeMilliseconds,
                    Environment.TickCount64);
            }
            return NativePointerTimestampConverter.FromCurrentStopwatch();
        }

        private static NativePoint GetClientOrigin(IntPtr hwnd)
        {
            var origin = new NativePoint(0, 0);
            if (!ClientToScreen(hwnd, ref origin))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return origin;
        }

        private static float ClampPressure(uint pressure)
        {
            if (pressure >= 1024)
                return 1;
            return pressure / 1024f;
        }

        private static NativeInkInputKind? GetPromotedPointerKind()
        {
            var extraInfo = unchecked((ulong)GetMessageExtraInfo().ToInt64());
            if (((uint)extraInfo & SignatureMask) != MiWpSignature)
                return null;

            return (extraInfo & TouchPromotionBit) != 0
                ? NativeInkInputKind.Touch
                : NativeInkInputKind.Pen;
        }

        private static uint LowWord(IntPtr value) => unchecked((uint)value.ToInt64()) & 0xFFFF;

        private static int SignedLowWord(IntPtr value) => unchecked((short)(value.ToInt64() & 0xFFFF));

        private static int SignedHighWord(IntPtr value) => unchecked((short)((value.ToInt64() >> 16) & 0xFFFF));

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerType(uint pointerId, out PointerInputType pointerType);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerPenInfoHistory(
            uint pointerId,
            ref uint entriesCount,
            [Out] NativePointerPenInfo[] penInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerPenInfo(uint pointerId, out NativePointerPenInfo penInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerTouchInfoHistory(
            uint pointerId,
            ref uint entriesCount,
            [Out] NativePointerTouchInfo[] touchInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPointerTouchInfo(uint pointerId, out NativePointerTouchInfo touchInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        private static extern int GetMessageTime();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices(
            [In, MarshalAs(UnmanagedType.LPArray)] RawInputDevice[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref NativePoint point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint point);

        private enum PointerInputType : uint
        {
            Touch = 2,
            Pen = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePointerInfo
        {
            public PointerInputType PointerType;
            public uint PointerId;
            public uint FrameId;
            public uint PointerFlags;
            public IntPtr SourceDevice;
            public IntPtr HwndTarget;
            public NativePoint PixelLocation;
            public NativePoint HimetricLocation;
            public NativePoint PixelLocationRaw;
            public NativePoint HimetricLocationRaw;
            public uint TimeMilliseconds;
            public uint HistoryCount;
            public int InputData;
            public uint KeyStates;
            public ulong PerformanceCount;
            public uint ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePointerPenInfo
        {
            public NativePointerInfo PointerInfo;
            public uint PenFlags;
            public uint PenMask;
            public uint Pressure;
            public uint Rotation;
            public int TiltX;
            public int TiltY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePointerTouchInfo
        {
            public NativePointerInfo PointerInfo;
            public uint TouchFlags;
            public uint TouchMask;
            public NativeRect Contact;
            public NativeRect ContactRaw;
            public uint Orientation;
            public uint Pressure;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawMouse
        {
            public ushort Flags;
            public uint Buttons;
            public uint RawButtons;
            public int LastX;
            public int LastY;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInput
        {
            public RawInputHeader Header;
            public RawMouse Mouse;
        }
    }
}
