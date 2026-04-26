using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Ink_Canvas.Helpers
{
    internal class ForegroundWindowInfo
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

        public static IntPtr GetForegroundWindowHandle()
        {
            return GetForegroundWindow();
        }

        public static string WindowTitle()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            const int nChars = 256;
            StringBuilder windowTitle = new StringBuilder(nChars);
            GetWindowText(foregroundWindowHandle, windowTitle, nChars);

            return windowTitle.ToString();
        }

        public static string WindowClassName()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            const int nChars = 256;
            StringBuilder className = new StringBuilder(nChars);
            GetClassName(foregroundWindowHandle, className, nChars);

            return className.ToString();
        }

        public static RECT WindowRect()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            RECT windowRect;
            GetWindowRect(foregroundWindowHandle, out windowRect);

            return windowRect;
        }

        public static string ProcessName()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();
            uint processId;
            GetWindowThreadProcessId(foregroundWindowHandle, out processId);

            try
            {
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (ArgumentException)
            {
                // Process with the given ID not found
                return "Unknown";
            }
        }

        public static string ProcessPath()
        {
            IntPtr foregroundWindowHandle = GetForegroundWindow();
            uint processId;
            GetWindowThreadProcessId(foregroundWindowHandle, out processId);

            try
            {
                Process process = Process.GetProcessById((int)processId);
                return process.MainModule.FileName;
            }
            catch
            {
                // Process with the given ID not found
                return "Unknown";
            }
        }

        public static double GetTaskbarHeight(Screen screen, double dpiScaleY)
        {
            // 创建RECT结构体表示屏幕边界
            RECT screenRect = new RECT
            {
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Right = screen.Bounds.Right,
                Bottom = screen.Bounds.Bottom
            };

            // 获取屏幕句柄
            const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr hMonitor = MonitorFromRect(ref screenRect, MONITOR_DEFAULTTONEAREST);

            // 初始化MONITORINFO结构体
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

            // 获取监视器信息
            GetMonitorInfo(hMonitor, ref monitorInfo);

            // 计算任务栏高度：monitorInfo.rcMonitor.bottom减去monitorInfo.rcWork.bottom的值
            int taskbarHeight = monitorInfo.rcMonitor.Bottom - monitorInfo.rcWork.Bottom;
            // 考虑 DPI 缩放
            return taskbarHeight / dpiScaleY;
        }
    }
}