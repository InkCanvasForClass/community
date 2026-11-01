using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Re-dispatches a pointer press that the input gate blocked, delivering it to the
    /// window actually under the pointer as synthesized mouse input.
    ///
    /// Pointer input routed to the ink HWND does not honor the overlay's WM_NCHITTEST
    /// pass-through, so chrome hits — floating bar, side panels, popup palettes — never
    /// reach their target window on their own. The forwarder resolves the destination
    /// HWND with WindowFromPoint at press time (excluding the ink overlay itself) and
    /// replays down/move/up as PostMessage'd mouse messages to that same window, so
    /// drag interactions that rely on WPF mouse capture keep working.
    /// </summary>
    internal sealed class ChromeInputForwarder
    {
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const int MK_LBUTTON = 0x0001;

        // Sticky down-target: moves and the up go to the window the press landed on,
        // not to whatever is under the pointer mid-drag (WPF mouse capture semantics).
        private IntPtr _activeTargetHwnd;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Replays a blocked press as a left-button down. The destination is the window
        /// under the pointer, falling back to <paramref name="fallbackHwnd"/> when the
        /// point resolves to nothing or to <paramref name="excludedHwnd"/> (the ink
        /// overlay must never receive the forwarded input back). Returns the target.
        /// </summary>
        public IntPtr ForwardDown(double screenX, double screenY, IntPtr fallbackHwnd, IntPtr excludedHwnd)
        {
            var x = (int)Math.Round(screenX);
            var y = (int)Math.Round(screenY);
            var target = FindTarget(x, y, fallbackHwnd, excludedHwnd);
            if (target == IntPtr.Zero)
                return IntPtr.Zero;

            PostMouseMessage(target, WM_LBUTTONDOWN, x, y, MK_LBUTTON);
            _activeTargetHwnd = target;
            return target;
        }

        private static IntPtr FindTarget(int x, int y, IntPtr fallbackHwnd, IntPtr excludedHwnd)
        {
            var target = WindowFromPoint(new NativePoint(x, y));
            if (target != IntPtr.Zero && target != excludedHwnd) return target;

            // Skip the overlay, preserving z-order, so a palette owned by the main
            // window receives its click instead of unconditionally falling back to main.
            var window = GetWindow(excludedHwnd, 2 /* GW_HWNDNEXT */);
            for (int i = 0; window != IntPtr.Zero && i < 512; i++, window = GetWindow(window, 2))
            {
                NativeRect rect;
                if (window == excludedHwnd || !IsWindowVisible(window) || !IsWindowEnabled(window)
                    || !GetWindowRect(window, out rect)) continue;
                int cloaked;
                if (DwmGetWindowAttribute(window, 14 /* DWMWA_CLOAKED */, out cloaked, sizeof(int)) == 0
                    && cloaked != 0) continue;
                if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom)
                    return window;
            }
            return fallbackHwnd;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        /// <summary>Replays a move for an already-forwarded pointer (button held).</summary>
        public void ForwardMove(double screenX, double screenY)
        {
            var target = _activeTargetHwnd;
            if (target == IntPtr.Zero)
                return;
            PostMouseMessage(target, WM_MOUSEMOVE, (int)Math.Round(screenX), (int)Math.Round(screenY), MK_LBUTTON);
        }

        /// <summary>Replays the button-up for an already-forwarded pointer and clears the target.</summary>
        public void ForwardUp(double screenX, double screenY)
        {
            var target = _activeTargetHwnd;
            _activeTargetHwnd = IntPtr.Zero;
            if (target == IntPtr.Zero)
                return;
            PostMouseMessage(target, WM_LBUTTONUP, (int)Math.Round(screenX), (int)Math.Round(screenY), 0);
        }

        private static void PostMouseMessage(IntPtr hwnd, uint message, int x, int y, int wParam)
        {
            // WM_MOUSE* coordinates are client-area-relative of the receiving window.
            // ScreenToClient takes one contiguous POINT, not separate X/Y references.
            // Passing ref int allowed native code to overwrite memory beyond X and left
            // Y unconverted, so forwarded touch clicks could miss the toolbar.
            var point = new NativePoint(x, y);
            if (!ScreenToClient(hwnd, ref point))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var lParam = (IntPtr)unchecked((point.Y << 16) | (ushort)point.X);
            if (!PostMessage(hwnd, message, (IntPtr)wParam, lParam))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ScreenToClient(IntPtr hwnd, ref NativePoint point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    }
}
