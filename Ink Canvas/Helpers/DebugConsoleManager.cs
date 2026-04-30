using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Ink_Canvas.Helpers
{
    public static class DebugConsoleManager
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleTitle(string lpConsoleTitle);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;

        private static bool _allocated;

        public static bool IsVisible { get; private set; }

        public static void Show()
        {
            try
            {
                if (!_allocated)
                {
                    if (GetConsoleWindow() == IntPtr.Zero)
                    {
                        if (!AllocConsole()) return;
                    }
                    _allocated = true;

                    Console.OutputEncoding = Encoding.UTF8;
                    SetConsoleTitle("InkCanvasForClass - Debug Console");

                    // 移除关闭菜单，避免用户点 X 时直接结束进程
                    var hWnd = GetConsoleWindow();
                    if (hWnd != IntPtr.Zero)
                    {
                        var hMenu = GetSystemMenu(hWnd, false);
                        if (hMenu != IntPtr.Zero) DeleteMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
                    }
                }
                else
                {
                    var hWnd = GetConsoleWindow();
                    if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_SHOW);
                }
                IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DebugConsoleManager] Show failed: {ex.Message}");
            }
        }

        public static void Hide()
        {
            try
            {
                var hWnd = GetConsoleWindow();
                if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_HIDE);
                IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DebugConsoleManager] Hide failed: {ex.Message}");
            }
        }

        public static void WriteLine(string line)
        {
            if (!IsVisible) return;
            try { Console.WriteLine(line); }
            catch { }
        }
    }
}