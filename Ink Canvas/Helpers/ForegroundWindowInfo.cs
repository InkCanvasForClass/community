using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

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
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
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

        public static int GetTaskbarHeight(Screen screen, double dpiScaleY)
        {
            // 优先用工作区和屏幕区的差值法，兼容多屏
            int height = 0;
            if (screen.Bounds.Height > screen.WorkingArea.Height)
            {
                // 任务栏在上下
                height = screen.Bounds.Height - screen.WorkingArea.Height;
            }
            else if (screen.Bounds.Width > screen.WorkingArea.Width)
            {
                // 任务栏在左右
                height = screen.Bounds.Width - screen.WorkingArea.Width;
            }
            // 考虑DPI缩放
            return (int)(height / dpiScaleY);
        }
    }
}