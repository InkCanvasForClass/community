using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace Ink_Canvas.WordLink
{
    // A real HWND region excludes touch as well as mouse input. HTTRANSPARENT alone
    // cannot reliably forward touch to windows belonging to another process.
    internal sealed class WordWindowInputRegion
    {
        private IntPtr _window;
        private Rect[] _rectangles = Array.Empty<Rect>();
        internal bool IsApplied => _window != IntPtr.Zero;
        internal static bool IsAlive(long window) => window != 0 && IsWindow(new IntPtr(window));

        internal void Update(IntPtr window, IReadOnlyList<Rect> rectangles)
        {
            if (window == IntPtr.Zero) return;
            var valid = rectangles.Where(r => !r.IsEmpty && r.Width > 0 && r.Height > 0
                && double.IsFinite(r.Left) && double.IsFinite(r.Top)
                && double.IsFinite(r.Right) && double.IsFinite(r.Bottom)).ToArray();
            if (_window == window && _rectangles.SequenceEqual(valid)) return;
            IntPtr region = CreateRectRgn(0, 0, 0, 0);
            if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                foreach (Rect rect in valid)
                {
                    IntPtr part = CreateRectRgn(checked((int)Math.Floor(rect.Left)), checked((int)Math.Floor(rect.Top)),
                        checked((int)Math.Ceiling(rect.Right)), checked((int)Math.Ceiling(rect.Bottom)));
                    if (part == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    try
                    {
                        if (CombineRgn(region, region, part, 2 /* RGN_OR */) == 0)
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    finally { DeleteObject(part); }
                }
                if (SetWindowRgn(window, region, true) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                region = IntPtr.Zero; // The system now owns it.
                _window = window; _rectangles = valid;
            }
            finally { if (region != IntPtr.Zero) DeleteObject(region); }
        }

        internal void Restore()
        {
            if (_window == IntPtr.Zero) return;
            if (IsWindow(_window) && SetWindowRgn(_window, IntPtr.Zero, true) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            _window = IntPtr.Zero; _rectangles = Array.Empty<Rect>();
        }

        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern int CombineRgn(IntPtr target, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    }
}
