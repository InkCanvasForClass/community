using System;
using System.ComponentModel;
using System.Collections.Generic;
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
        private const uint MousePointerId = uint.MaxValue;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorNoData = 232;
        private const int MaximumHistoryEntries = 4096;

        private readonly HwndSource _source;
        private readonly NativePointerInputHandler _handler;
        private readonly Dictionary<uint, NativeInkInputKind> _activePointerKinds = new Dictionary<uint, NativeInkInputKind>();
        private double _dpiScaleX;
        private double _dpiScaleY;
        private bool _mouseInContact;
        private uint _mouseFrameId;
        private bool _disposed;

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
                    DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Down, ref handled);
                    break;
                case WmMouseMove:
                    if (_mouseInContact || (LowWord(wParam) & MouseKeyLeftButton) != 0)
                        DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Update, ref handled);
                    break;
                case WmLeftButtonUp:
                    DispatchMouse(hwnd, wParam, lParam, NativePointerMessageKind.Up, ref handled);
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

        private void DispatchMouse(
            IntPtr hwnd,
            IntPtr wParam,
            IntPtr lParam,
            NativePointerMessageKind messageKind,
            ref bool handled)
        {
            var promoted = IsPromotedMouseMessage();
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
                MousePointerId,
                NativeInkInputKind.Mouse,
                (point.X - clientOrigin.X) / _dpiScaleX,
                (point.Y - clientOrigin.Y) / _dpiScaleY,
                0.5f,
                false,
                ResolveTimestampMicroseconds(0, unchecked((uint)GetMessageTime())),
                ++_mouseFrameId,
                flags);
            handled = _handler(new NativePointerInputBatch(
                MousePointerId,
                NativeInkInputKind.Mouse,
                messageKind,
                new[] { sample },
                (LowWord(wParam) & 0x0002) != 0,
                promoted,
                true));
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

            return new NativePointerInputBatch(
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

            return new NativePointerInputBatch(
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
                if (error == ErrorInsufficientBuffer && count > 0 && count <= MaximumHistoryEntries)
                {
                    var history = new NativePointerPenInfo[count];
                    var capacity = count;
                    if (GetPointerPenInfoHistory(pointerId, ref capacity, history))
                    {
                        historyComplete = true;
                        error = 0;
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
                if (error == ErrorInsufficientBuffer && count > 0 && count <= MaximumHistoryEntries)
                {
                    var history = new NativePointerTouchInfo[count];
                    var capacity = count;
                    if (GetPointerTouchInfoHistory(pointerId, ref capacity, history))
                    {
                        historyComplete = true;
                        error = 0;
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

        private static bool IsPromotedMouseMessage()
        {
            var extraInfo = unchecked((ulong)GetMessageExtraInfo().ToInt64());
            return ((uint)extraInfo & SignatureMask) == MiWpSignature;
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
    }
}
