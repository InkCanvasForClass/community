using System;
using System.Runtime.InteropServices;

namespace InkCanvas.LiquidGlassMagHost
{
    /// <summary>Win32 P/Invoke 与 Magnification API 绑定，独立进程专用。</summary>
    internal static class Native
    {
        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WS_EX_TRANSPARENT = 0x00000020;
        internal const int WS_POPUP = unchecked((int)0x80000000);
        internal const int WS_CHILD = 0x40000000;
        internal const int WS_VISIBLE = 0x10000000;
        internal const int WS_CLIPCHILDREN = 0x02000000;
        internal const int LWA_ALPHA = 0x00000002;
        internal const int SW_SHOWNOACTIVATE = 4;
        internal const int SWP_NOZORDER = 0x0004;
        internal const int SWP_NOACTIVATE = 0x0010;
        internal const int SWP_SHOWWINDOW = 0x0040;
        internal const int SRCCOPY = 0x00CC0020;
        internal const int PW_CLIENTONLY = 0x00000001;
        internal const int PW_RENDERFULLCONTENT = 0x00000002;
        internal const int MW_FILTERMODE_EXCLUDE = 0;
        internal const int BI_RGB = 0;
        internal const int DIB_RGB_COLORS = 0;
        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;
        internal const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MAGTRANSFORM
        {
            public float M00, M01, M02;
            public float M10, M11, M12;
            public float M20, M21, M22;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSG
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public POINT Pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WNDCLASSEX
        {
            public uint CbSize, Style;
            public IntPtr LpfnWndProc;
            public int CbClsExtra, CbWndExtra;
            public IntPtr HInstance, HIcon, HCursor, HbrBackground;
            public string LpszMenuName, LpszClassName;
            public IntPtr HIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public uint BiSize;
            public int BiWidth, BiHeight;
            public ushort BiPlanes, BiBitCount;
            public int BiCompression;
            public uint BiSizeImage;
            public int BiXPelsPerMeter, BiYPelsPerMeter;
            public uint BiClrUsed, BiClrImportant;
        }

        [DllImport("Magnification.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagUninitialize();

        [DllImport("Magnification.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetWindowSource(IntPtr hwnd, ref RECT rect);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM t);

        [DllImport("Magnification.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetWindowFilterList(IntPtr hwnd, int mode, int count, [In] IntPtr[] list);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int ex, string cls, string title, int style,
            int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hwnd, int cmd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, int flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessage(ref MSG msg, IntPtr hwnd, uint min, uint max, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr DispatchMessage(ref MSG msg);

        [DllImport("user32.dll")]
        internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int i);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string n);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr dc, IntPtr o);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr o);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int rop);

        [DllImport("gdi32.dll")]
        internal static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines,
            [Out] byte[] bits, ref BITMAPINFOHEADER info, uint usage);
    }
}