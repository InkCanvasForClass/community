using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Ink_Canvas.Helpers
{
    internal class ForegroundWindowInfo
    {
        //[DllImport("user32.dll")]
        //private static extern IntPtr GetForegroundWindow();

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        //[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        //private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        //[DllImport("user32.dll")]
        //private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        //[StructLayout(LayoutKind.Sequential)]
        //public struct RECT
        //{
        //    public int Left;
        //    public int Top;
        //    public int Right;
        //    public int Bottom;

        //    public int Width => Right - Left;
        //    public int Height => Bottom - Top;
        //}

        //[StructLayout(LayoutKind.Sequential)]
        //private struct MONITORINFO
        //{
        //    public uint cbSize;
        //    public RECT rcMonitor;
        //    public RECT rcWork;
        //    public uint dwFlags;
        //}

        //[DllImport("user32.dll")]
        //private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        //[DllImport("user32.dll")]
        //private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        //[DllImport("user32.dll")]
        //private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

        public static IntPtr GetForegroundWindowHandle()
        {
            return PInvoke.GetForegroundWindow();
        }

        public static string WindowTitle()
        {
            IntPtr foregroundWindowHandle = PInvoke.GetForegroundWindow();

            const int nChars = 256;
            StringBuilder windowTitle = new StringBuilder(nChars);
            PInvoke.GetWindowText(new HWND(foregroundWindowHandle), new Span<char>(windowTitle.ToString().ToCharArray()));

            return windowTitle.ToString();
        }

        public static string WindowClassName()
        {
            IntPtr foregroundWindowHandle = PInvoke.GetForegroundWindow();

            const int nChars = 256;
            StringBuilder className = new StringBuilder(nChars);
            PInvoke.GetClassName(new HWND(foregroundWindowHandle), new Span<char>(className.ToString().ToCharArray()));

            return className.ToString();
        }

        public static RECT WindowRect()
        {
            IntPtr foregroundWindowHandle = PInvoke.GetForegroundWindow();

            PInvoke.GetWindowRect(new HWND(foregroundWindowHandle), out RECT windowRect);

            return windowRect;
        }

        public static string ProcessName()
        {
            IntPtr foregroundWindowHandle = PInvoke.GetForegroundWindow();
            uint processId;
            PInvoke.GetWindowThreadProcessId(new HWND(foregroundWindowHandle), out processId);

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
            IntPtr foregroundWindowHandle = PInvoke.GetForegroundWindow();
            uint processId;
            PInvoke.GetWindowThreadProcessId(new HWND(foregroundWindowHandle), out processId);

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
                left = screen.Bounds.Left,
                top = screen.Bounds.Top,
                right = screen.Bounds.Right,
                bottom = screen.Bounds.Bottom
            };

            // 获取屏幕句柄
            HMONITOR hMonitor = PInvoke.MonitorFromRect(screenRect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

            // 初始化MONITORINFO结构体
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

            // 获取监视器信息
            PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);

            // 计算任务栏高度：monitorInfo.rcMonitor.bottom减去monitorInfo.rcWork.bottom的值
            int taskbarHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcWork.bottom;
            // 考虑 DPI 缩放
            return taskbarHeight / dpiScaleY;
        }
    }
}