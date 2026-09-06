using System;
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
            var target = WindowFromPoint(new NativePoint(x, y));
            if (target == IntPtr.Zero || target == excludedHwnd)
                target = fallbackHwnd;
            if (target == IntPtr.Zero)
                return IntPtr.Zero;

            _activeTargetHwnd = target;
            PostMouseMessage(target, WM_LBUTTONDOWN, x, y, MK_LBUTTON);
            return target;
        }

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
            ScreenToClient(hwnd, ref x, ref y);
            var lParam = (IntPtr)((y << 16) | (ushort)x);
            PostMessage(hwnd, message, (IntPtr)wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hwnd, ref int x, ref int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    }
}
