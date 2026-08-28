using System;
using System.Runtime.InteropServices;

namespace WpfUiCompat.Helpers
{
    /// <summary>
    /// DWM P/Invoke 封装（移植自 iNKORE.UI.WPF.Modern Helpers.Styles，MIT License）。
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINCOMPATTRDATA data);

        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        internal const int DWMWA_MICA_EFFECT = 102;
        internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        internal const int DWMWA_CAPTION_COLOR = 35;
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        internal const int DWMSBT_AUTO = 0;
        internal const int DWMSBT_NONE = 1;
        internal const int DWMSBT_MAINWINDOW = 2;
        internal const int DWMSBT_TRANSIENTWINDOW = 3;
        internal const int DWMSBT_TABBEDWINDOW = 4;

        internal static readonly Version OSVersion = Environment.OSVersion.Version;

        internal static bool IsWindows11OrGreater => OSVersion >= new Version(10, 0, 21996);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINCOMPATTRDATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ACCENT_POLICY
        {
            public uint AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        internal enum ACCENT_STATE
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        internal static int ColorToAbgr(System.Windows.Media.Color value, double alphaScale = 1)
        {
            return value.R << 0 | value.G << 8 | value.B << 16 | (int)(value.A * alphaScale) << 24;
        }
    }

    /// <summary>
    /// 窗口圆角样式（兼容 iNKORE WindowCornerStyle）。
    /// </summary>
    public enum WindowCornerStyle
    {
        DoNotApply = 0,
        Round = 1,
        RoundSmall = 2,
    }
}