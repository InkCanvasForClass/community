using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern int SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private const uint ABM_GETTASKBARPOS = 0x00000005;
        private const uint ABM_GETSTATE = 0x00000004;
        private const int SPI_GETWORKAREA = 0x0030;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const int ABS_AUTOHIDE = 0x0000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        /// <summary>
        /// 获取Windows任务栏的高度（仅计算任务栏，不包括其他应用的停靠栏）
        /// </summary>
        /// <param name="screen">当前屏幕</param>
        /// <param name="dpiScaleY">DPI缩放Y值</param>
        /// <returns>任务栏高度</returns>
        public static double GetTaskbarHeight(System.Windows.Forms.Screen screen, double dpiScaleY)
        {
            try
            {
                // 创建APPBARDATA结构
                var abd = new APPBARDATA();
                abd.cbSize = Marshal.SizeOf(abd);

                // 获取任务栏状态
                IntPtr state = SHAppBarMessage(ABM_GETSTATE, ref abd);
                bool isAutoHide = (state.ToInt32() & ABS_AUTOHIDE) == ABS_AUTOHIDE;

                // 如果任务栏是自动隐藏的,返回0
                if (isAutoHide)
                {
                    LogHelper.WriteLogToFile("任务栏处于自动隐藏状态", LogHelper.LogType.Info);
                    return 0;
                }

                // 获取任务栏信息
                IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
                if (result != IntPtr.Zero)
                {
                    // 获取当前屏幕的工作区
                    RECT workArea = new RECT();
                    SystemParametersInfo(SPI_GETWORKAREA, 0, Marshal.AllocHGlobal(Marshal.SizeOf(workArea)), 0);

                    // 根据任务栏位置计算高度
                    int taskbarHeight = 0;

                    // 任务栏的uEdge: 0=左, 1=上, 2=右, 3=下
                    switch (abd.uEdge)
                    {
                        case 1: // 上
                            taskbarHeight = abd.rc.Height;
                            break;
                        case 3: // 下
                            taskbarHeight = abd.rc.Height;
                            break;
                        case 0: // 左
                        case 2: // 右
                            // 水平任务栏不影响高度
                            taskbarHeight = 0;
                            break;
                    }

                    // 考虑DPI缩放
                    return taskbarHeight / dpiScaleY;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取任务栏高度出错: {ex.Message}");
                LogHelper.WriteLogToFile($"获取任务栏高度出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 如果获取失败，回退到通用方法
            return (screen.Bounds.Height - screen.WorkingArea.Height) / dpiScaleY;
        }

        public static string WindowTitle() {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            const int nChars = 256;
            StringBuilder windowTitle = new StringBuilder(nChars);
            GetWindowText(foregroundWindowHandle, windowTitle, nChars);

            return windowTitle.ToString();
        }

        public static string WindowClassName() {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            const int nChars = 256;
            StringBuilder className = new StringBuilder(nChars);
            GetClassName(foregroundWindowHandle, className, nChars);

            return className.ToString();
        }

        public static RECT WindowRect() {
            IntPtr foregroundWindowHandle = GetForegroundWindow();

            RECT windowRect;
            GetWindowRect(foregroundWindowHandle, out windowRect);

            return windowRect;
        }

        public static string ProcessName() {
            IntPtr foregroundWindowHandle = GetForegroundWindow();
            uint processId;
            GetWindowThreadProcessId(foregroundWindowHandle, out processId);

            try {
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            } catch (ArgumentException) {
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
            catch {
                // Process with the given ID not found
                return "Unknown";
            }
        }
    }
}