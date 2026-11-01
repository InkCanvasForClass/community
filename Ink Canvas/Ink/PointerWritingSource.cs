using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Ink_Canvas.Ink
{
    internal readonly record struct PointerWritingContact(uint Id, int X, int Y, double Pressure, bool Released);

    /// <summary>
    /// 读取 WM_POINTER 的笔输入。鼠标与触摸不在这里处理：鼠标继续走 WPF，
    /// 触摸由 <see cref="PalmRawHidSource"/> 或 WPF 触摸处理。
    /// </summary>
    internal sealed class PointerWritingSource : IDisposable
    {
        private const int WmPointerUpdate = 0x0245;
        private const int WmPointerDown = 0x0246;
        private const int WmPointerUp = 0x0247;
        private const int WmPointerCaptureChanged = 0x024C;
        private const uint PointerFlagInContact = 0x00000004;
        private const int PointerTypePen = 3;

        private readonly HwndSource _source;
        private readonly Func<PointerWritingContact, bool> _handler;
        private bool _disposed;

        public PointerWritingSource(HwndSource source, Func<PointerWritingContact, bool> handler)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _source.AddHook(WndProc);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.RemoveHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_disposed) return IntPtr.Zero;
            if (msg != WmPointerDown && msg != WmPointerUpdate && msg != WmPointerUp && msg != WmPointerCaptureChanged)
                return IntPtr.Zero;

            uint pointerId = (uint)(wParam.ToInt64() & 0xFFFF);
            if (pointerId == 0) return IntPtr.Zero;

            if (msg == WmPointerCaptureChanged)
            {
                if (_handler(new PointerWritingContact(pointerId, 0, 0, 0.5, true))) handled = true;
                return IntPtr.Zero;
            }

            if (!GetPointerInfo(pointerId, out PointerInfo info)) return IntPtr.Zero;
            if (info.PointerType != PointerTypePen) return IntPtr.Zero; // 笔专用；鼠标仍由 WPF 处理
            if (!GetPointerPenInfo(pointerId, out PointerPenInfo pen)) return IntPtr.Zero;

            bool released = msg == WmPointerUp || (info.PointerFlags & PointerFlagInContact) == 0;
            double pressure = Math.Clamp(pen.Pressure / 1024.0, 0.01, 1.0);
            if (_handler(new PointerWritingContact(pointerId, info.PixelLocation.X, info.PixelLocation.Y, pressure, released)))
                handled = true;
            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetPointerInfo(uint pointerId, out PointerInfo pointerInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetPointerPenInfo(uint pointerId, out PointerPenInfo penInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerInfo
        {
            public int PointerType;
            public uint PointerId;
            public uint FrameId;
            public uint PointerFlags;
            public IntPtr SourceDevice;
            public IntPtr HwndTarget;
            public Point PixelLocation;
            public Point HimetricLocation;
            public Point PixelLocationRaw;
            public Point HimetricLocationRaw;
            public uint Time;
            public uint HistoryCount;
            public int InputState;
            public uint KeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerPenInfo
        {
            public PointerInfo PointerInfo;
            public uint PenFlags;
            public uint PenMask;
            public uint Pressure;
            public uint Rotation;
            public int TiltX;
            public int TiltY;
        }
    }
}
