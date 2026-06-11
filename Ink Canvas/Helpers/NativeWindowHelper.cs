using System;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 共享的 Win32 P/Invoke 声明，供窗口置顶管理系统使用。
    /// 避免在多个文件中重复声明相同的 P/Invoke。
    /// </summary>
    internal static class NativeWindowHelper
    {
        #region 常量

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOOWNERZORDER = 0x0200;

        #endregion

        #region P/Invoke

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        public delegate bool EnumThreadWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWindowsProc lpfn, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        #region 便捷方法

        /// <summary>
        /// 将窗口设为 TOPMOST（同时设置 SetWindowPos + WS_EX_TOPMOST 样式）
        /// </summary>
        public static void SetTopmost(IntPtr handle)
        {
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) == 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
            }
        }

        /// <summary>
        /// 将窗口取消 TOPMOST（同时设置 HWND_NOTOPMOST + 清除 WS_EX_TOPMOST 样式）
        /// </summary>
        public static void SetNotTopmost(IntPtr handle)
        {
            SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) != 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
            }
        }

        /// <summary>
        /// 检查窗口句柄是否有效（存在 + 可见 + 非最小化）
        /// </summary>
        public static bool IsWindowReady(IntPtr handle)
        {
            return handle != IntPtr.Zero && IsWindow(handle) && IsWindowVisible(handle) && !IsIconic(handle);
        }

        #endregion
    }
}
