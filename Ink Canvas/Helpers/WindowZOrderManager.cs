using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 窗口Z-Order管理器，用于管理窗口的层级顺序
    /// 在无焦点模式下，确保后打开的窗口能够置顶于先打开的窗口
    /// </summary>
    public static class WindowZOrderManager
    {
        #region Win32 API 声明
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
        #endregion

        // 窗口层级管理
        private static readonly List<WindowInfo> _windowStack = new List<WindowInfo>();
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 窗口信息类
        /// </summary>
        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public Window Window { get; set; }
            public DateTime CreatedTime { get; set; }
            public bool IsTopmost { get; set; }
            public bool IsNoFocusMode { get; set; }
        }

        /// <summary>
        /// 注册窗口到Z-Order管理器
        /// </summary>
        /// <param name="window">要注册的窗口</param>
        /// <param name="isTopmost">是否置顶</param>
        /// <param name="isNoFocusMode">是否无焦点模式</param>
        public static void RegisterWindow(Window window, bool isTopmost = false, bool isNoFocusMode = false)
        {
            if (window == null) return;

            lock (_lockObject)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 移除已存在的记录
                _windowStack.RemoveAll(w => w.Handle == hwnd);

                // 添加新记录
                var windowInfo = new WindowInfo
                {
                    Handle = hwnd,
                    Window = window,
                    CreatedTime = DateTime.Now,
                    IsTopmost = isTopmost,
                    IsNoFocusMode = isNoFocusMode
                };

                _windowStack.Add(windowInfo);

                // 应用Z-Order
                ApplyZOrder();
            }
        }

        /// <summary>
        /// 从Z-Order管理器中移除窗口
        /// </summary>
        /// <param name="window">要移除的窗口</param>
        public static void UnregisterWindow(Window window)
        {
            if (window == null) return;

            lock (_lockObject)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                _windowStack.RemoveAll(w => w.Handle == hwnd);
                ApplyZOrder();
            }
        }

        /// <summary>
        /// 设置窗口为置顶状态
        /// </summary>
        /// <param name="window">要置顶的窗口</param>
        /// <param name="isTopmost">是否置顶</param>
        public static void SetWindowTopmost(Window window, bool isTopmost)
        {
            if (window == null) return;

            lock (_lockObject)
            {
                var windowInfo = _windowStack.FirstOrDefault(w => w.Window == window);
                if (windowInfo != null)
                {
                    windowInfo.IsTopmost = isTopmost;
                    ApplyZOrder();
                }
            }
        }

        /// <summary>
        /// 将窗口移到最顶层
        /// </summary>
        /// <param name="window">要移到最顶层的窗口</param>
        public static void BringToTop(Window window)
        {
            if (window == null) return;

            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 使用更直接的方法：先激活窗口，再置顶
                window.Activate();
                window.Focus();

                // 设置窗口为置顶
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                // 确保窗口样式正确
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                // 再次确保置顶
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BringToTop失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用Z-Order排序
        /// </summary>
        private static void ApplyZOrder()
        {
            // 简化逻辑：直接设置所有窗口为置顶，让Windows系统自然处理层级
            foreach (var windowInfo in _windowStack.ToList())
            {
                if (windowInfo.IsTopmost && IsWindow(windowInfo.Handle) && IsWindowVisible(windowInfo.Handle) && !IsIconic(windowInfo.Handle))
                {
                    // 设置窗口为置顶
                    SetWindowPos(windowInfo.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                    // 确保窗口样式正确
                    int exStyle = GetWindowLong(windowInfo.Handle, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOPMOST) == 0)
                    {
                        SetWindowLong(windowInfo.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    }
                }
            }
        }

        /// <summary>
        /// 检查是否有子窗口在前景
        /// </summary>
        /// <returns>如果有子窗口在前景返回true</returns>
        public static bool HasChildWindowInForeground()
        {
            lock (_lockObject)
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                return _windowStack.Any(w => w.Handle == foregroundWindow);
            }
        }

        /// <summary>
        /// 清理无效的窗口记录
        /// </summary>
        public static void CleanupInvalidWindows()
        {
            lock (_lockObject)
            {
                _windowStack.RemoveAll(w => !IsWindow(w.Handle) || !IsWindowVisible(w.Handle));
            }
        }

        /// <summary>
        /// 获取当前注册的窗口数量
        /// </summary>
        /// <returns>窗口数量</returns>
        public static int GetWindowCount()
        {
            lock (_lockObject)
            {
                return _windowStack.Count;
            }
        }

        /// <summary>
        /// 强制刷新所有窗口的置顶状态
        /// </summary>
        public static void ForceRefreshAllWindows()
        {
            lock (_lockObject)
            {
                foreach (var windowInfo in _windowStack.ToList())
                {
                    if (windowInfo.IsTopmost && IsWindow(windowInfo.Handle))
                    {
                        // 强制设置窗口为置顶
                        SetWindowPos(windowInfo.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                        // 确保窗口样式正确
                        int exStyle = GetWindowLong(windowInfo.Handle, GWL_EXSTYLE);
                        SetWindowLong(windowInfo.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    }
                }
            }
        }
    }
}