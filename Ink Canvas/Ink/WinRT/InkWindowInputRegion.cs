using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// The ink HWND only owns the canvas minus interactive UI. Unlike HTTRANSPARENT,
    /// an HWND region is also honored when Windows selects the touch/pointer target.
    /// All rectangles are overlay-client physical pixels; no global cursor is moved.
    /// </summary>
    internal sealed class InkWindowInputRegion
    {
        private Rect _canvas = Rect.Empty;
        private Rect[] _holes = Array.Empty<Rect>();
        private IntPtr _window;

        internal void Update(IntPtr window, Rect canvas, IReadOnlyList<Rect> holes)
        {
            if (window == IntPtr.Zero) return;
            if (_window == window && _canvas == canvas && _holes.SequenceEqual(holes)) return;
            var region = CreateRectangle(canvas);
            if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                foreach (var hole in holes)
                {
                    var cutout = CreateRectangle(hole);
                    if (cutout == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    try
                    {
                        if (CombineRgn(region, region, cutout, 4 /* RGN_DIFF */) == 0)
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    finally { DeleteObject(cutout); }
                }
                if (SetWindowRgn(window, region, true) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                // Windows owns the region after a successful SetWindowRgn.
                region = IntPtr.Zero;
                _window = window;
                _canvas = canvas;
                _holes = holes.ToArray();
            }
            finally { if (region != IntPtr.Zero) DeleteObject(region); }
        }

        private static IntPtr CreateRectangle(Rect rectangle)
        {
            if (rectangle.IsEmpty) return CreateRectRgn(0, 0, 0, 0);
            return CreateRectRgn(checked((int)Math.Floor(rectangle.Left)), checked((int)Math.Floor(rectangle.Top)),
                checked((int)Math.Ceiling(rectangle.Right)), checked((int)Math.Ceiling(rectangle.Bottom)));
        }

        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
    }
}
