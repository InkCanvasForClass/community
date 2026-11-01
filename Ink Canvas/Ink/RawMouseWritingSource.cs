using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Ink_Canvas.Ink
{
    internal readonly record struct RawMouseContact(int X, int Y, bool Down, bool Up);

    /// <summary>
    /// 物理鼠标的原始输入（WM_INPUT + raw HID）。落笔/抬笔用 WM_LBUTTONDOWN/UP 的可靠成对消息，
    /// 接触期间的高频位置来自 WM_INPUT；两者都在这里处理，不交给 WPF InkCanvas 画。
    /// </summary>
    internal sealed class RawMouseWritingSource : IDisposable
    {
        private const int WmInput = 0x00FF;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;
        private const int WmCancelMode = 0x001F;
        private const int WmCaptureChanged = 0x0215;
        private const uint RidInput = 0x10000003;
        private const uint RimTypeMouse = 0;
        private const uint RidDeviceInputSink = 0x00000100;
        private const uint RidDeviceRemove = 0x00000001;
        private const ushort HidUsagePageGenericDesktop = 0x01;
        private const ushort HidUsageMouse = 0x02;
        private const ushort RiMouseLeftButtonDown = 0x0001;
        private const ushort RiMouseLeftButtonUp = 0x0002;
        private const uint MiWpSignature = 0xFF515700;
        private const uint SignatureMask = 0xFFFFFF00;

        private readonly HwndSource _source;
        private readonly Func<RawMouseContact, bool> _handler;
        private bool _inContact;
        private bool _disposed;

        public RawMouseWritingSource(HwndSource source, Func<RawMouseContact, bool> handler)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _source.AddHook(WndProc);
            TryRegister(source.Handle);
        }

        public bool IsActive { get; private set; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.RemoveHook(WndProc);
            try
            {
                var device = new RawInputDevice
                {
                    UsagePage = HidUsagePageGenericDesktop,
                    Usage = HidUsageMouse,
                    Flags = RidDeviceRemove,
                    Target = IntPtr.Zero
                };
                RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>());
            }
            catch { }
        }

        private void TryRegister(IntPtr hwnd)
        {
            try
            {
                var device = new RawInputDevice
                {
                    UsagePage = HidUsagePageGenericDesktop,
                    Usage = HidUsageMouse,
                    Flags = RidDeviceInputSink,
                    Target = hwnd
                };
                IsActive = RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>());
            }
            catch
            {
                IsActive = false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_disposed) return IntPtr.Zero;
            switch (msg)
            {
                case WmLeftButtonDown:
                    // Touch is promoted to mouse messages as well. It must not start a mouse
                    // stroke or take capture; the raw HID touch path owns that contact.
                    if (IsPromotedTouch()) break;
                    if (TryGetScreenPoint(out int downX, out int downY)
                        && _handler(new RawMouseContact(downX, downY, true, false)))
                    {
                        _inContact = true;
                        SetCapture(hwnd);
                        handled = true; // WPF/InkCanvas 不再画这一笔
                    }
                    break;

                case WmLeftButtonUp:
                    if (IsPromotedTouch()) break;
                    if (_inContact)
                    {
                        if (TryGetScreenPoint(out int upX, out int upY))
                            _handler(new RawMouseContact(upX, upY, false, true));
                        _inContact = false;
                        ReleaseCapture();
                        handled = true;
                    }
                    break;

                case WmCaptureChanged:
                case WmCancelMode:
                    if (_inContact)
                    {
                        _handler(new RawMouseContact(0, 0, false, true));
                        _inContact = false;
                        ReleaseCapture();
                        handled = true;
                    }
                    break;

                case WmInput:
                    if (!_inContact) return IntPtr.Zero;
                    if (!TryReadRawMouse(lParam, out bool leftDown, out bool leftUp)) return IntPtr.Zero;
                    if (leftDown || leftUp) return IntPtr.Zero; // 生命周期由上面的按钮消息负责
                    if (TryGetScreenPoint(out int moveX, out int moveY))
                    {
                        // 高频移动样本；即使 WPF 也收到 WM_MOUSEMOVE，没有按下状态也不会起笔。
                        _handler(new RawMouseContact(moveX, moveY, false, false));
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private static bool IsPromotedTouch()
        {
            long extra = GetMessageExtraInfo().ToInt64();
            return ((ulong)extra & SignatureMask) == MiWpSignature;
        }

        private static bool TryGetScreenPoint(out int x, out int y)
        {
            x = y = 0;
            if (!GetCursorPos(out var point)) return false;
            x = point.X;
            y = point.Y;
            return true;
        }

        private static bool TryReadRawMouse(IntPtr lParam, out bool leftDown, out bool leftUp)
        {
            leftDown = false;
            leftUp = false;
            uint size = 0;
            if (GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) != 0
                || size == 0 || size > 1024)
                return false;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint read = GetRawInputData(lParam, RidInput, buffer, ref size,
                    (uint)Marshal.SizeOf<RawInputHeader>());
                if (read != size) return false;

                var raw = Marshal.PtrToStructure<RawInput>(buffer);
                if (raw.Header.Type != RimTypeMouse) return false;
                if (((uint)raw.Mouse.ExtraInformation & SignatureMask) == MiWpSignature) return false;

                ushort buttons = (ushort)(raw.Mouse.Buttons & 0xFFFF);
                leftDown = (buttons & RiMouseLeftButtonDown) != 0;
                leftUp = (buttons & RiMouseLeftButtonUp) != 0;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [In, MarshalAs(UnmanagedType.LPArray)] RawInputDevice[] devices,
            uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetCapture(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ReleaseCapture();

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
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
