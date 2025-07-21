using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 防止窗口进入全屏状态的辅助类
    /// </summary>
    public static partial class AvoidFullScreenHelper
    {
        private static readonly DependencyProperty IsAvoidFullScreenEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsAvoidFullScreenEnabled", 
                typeof(bool), 
                typeof(AvoidFullScreenHelper));

        private static bool _isBoardMode = false;
        public static void SetBoardMode(bool isBoardMode)
        {
            _isBoardMode = isBoardMode;
        }

        public static void StartAvoidFullScreen(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            if (!(bool)window.GetValue(IsAvoidFullScreenEnabledProperty))
            {
                var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                if (hwndSource != null)
                {
                    hwndSource.AddHook(KeepInWorkingAreaHook);
                    window.SetValue(IsAvoidFullScreenEnabledProperty, true);
                }
            }
        }

        public static void StopAvoidFullScreen(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            if ((bool)window.GetValue(IsAvoidFullScreenEnabledProperty))
            {
                var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                if (hwndSource != null)
                {
                    hwndSource.RemoveHook(KeepInWorkingAreaHook);
                    window.ClearValue(IsAvoidFullScreenEnabledProperty);
                }
            }
        }

        public static bool GetIsAvoidFullScreenEnabled(DependencyObject obj) => (bool)obj.GetValue(IsAvoidFullScreenEnabledProperty);
        public static void SetIsAvoidFullScreenEnabled(DependencyObject obj, bool value) => obj.SetValue(IsAvoidFullScreenEnabledProperty, value);

        private static IntPtr KeepInWorkingAreaHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 只拦截主画布窗口的全屏（最大化）操作
            var window = HwndSource.FromHwnd(hwnd)?.RootVisual as Window;
            if (window == null) return IntPtr.Zero;
            // 这里假设主画布窗口类名为MainWindow（如有不同请调整）
            if (window.GetType().Name != "MainWindow") return IntPtr.Zero;

            if (_isBoardMode)
            {
                // 画板模式下允许全屏/最大化，不拦截
                return IntPtr.Zero;
            }
            const int WM_WINDOWPOSCHANGING = 0x0046;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MAXIMIZE = 0xF030;
            if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MAXIMIZE)
            {
                // 拦截最大化命令，强制还原窗口并调整到工作区
                window.WindowState = WindowState.Normal;
                var workingArea = GetWorkingArea(new Rect(window.Left, window.Top, window.Width, window.Height));
                window.Left = workingArea.Left;
                window.Top = workingArea.Top;
                window.Width = workingArea.Width;
                window.Height = workingArea.Height;
                handled = true;
                return IntPtr.Zero;
            }
            if (msg != WM_WINDOWPOSCHANGING)
                return IntPtr.Zero;

            try
            {
                var pos = (WindowPosition)Marshal.PtrToStructure(lParam, typeof(WindowPosition));
                if ((pos.Flags & (WindowPositionFlags.SWP_NOMOVE | WindowPositionFlags.SWP_NOSIZE)) != 0)
                    return IntPtr.Zero;
                // 只处理主画布窗口
                // 计算目标矩形
                var targetRect = new Rect(
                    (pos.Flags & WindowPositionFlags.SWP_NOMOVE) == 0 ? pos.X : window.Left,
                    (pos.Flags & WindowPositionFlags.SWP_NOMOVE) == 0 ? pos.Y : window.Top,
                    (pos.Flags & WindowPositionFlags.SWP_NOSIZE) == 0 ? pos.Width : window.Width,
                    (pos.Flags & WindowPositionFlags.SWP_NOSIZE) == 0 ? pos.Height : window.Height);
                var workingArea = GetWorkingArea(targetRect);
                var adjustedRect = AdjustRectToWorkingArea(targetRect, workingArea);
                pos.X = (int)adjustedRect.Left;
                pos.Y = (int)adjustedRect.Top;
                pos.Width = (int)adjustedRect.Width;
                pos.Height = (int)adjustedRect.Height;
                pos.Flags &= ~(WindowPositionFlags.SWP_NOSIZE | WindowPositionFlags.SWP_NOMOVE | WindowPositionFlags.SWP_NOREDRAW);
                pos.Flags |= WindowPositionFlags.SWP_NOCOPYBITS;
                Marshal.StructureToPtr(pos, lParam, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"窗口位置调整失败: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        private static Rect GetWorkingArea(Rect windowRect)
        {
            // 获取所有显示器
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // 确定窗口主要位于哪个显示器上
            System.Windows.Forms.Screen targetScreen = null;
            double maxIntersection = 0;
            
            foreach (var screen in screens)
            {
                var screenRect = new Rect(
                    screen.WorkingArea.X, 
                    screen.WorkingArea.Y, 
                    screen.WorkingArea.Width, 
                    screen.WorkingArea.Height);
                    
                var intersection = Rect.Intersect(windowRect, screenRect);
                if (intersection.Width * intersection.Height > maxIntersection)
                {
                    maxIntersection = intersection.Width * intersection.Height;
                    targetScreen = screen;
                }
            }
            
            // 如果没找到，使用主显示器
            if (targetScreen == null)
                targetScreen = System.Windows.Forms.Screen.PrimaryScreen;
                
            return new Rect(
                targetScreen.WorkingArea.X,
                targetScreen.WorkingArea.Y,
                targetScreen.WorkingArea.Width,
                targetScreen.WorkingArea.Height);
        }

        private static Rect AdjustRectToWorkingArea(Rect windowRect, Rect workingArea)
        {
            // 调整尺寸以适应工作区域
            if (windowRect.Width > workingArea.Width)
                windowRect.Width = workingArea.Width;
                
            if (windowRect.Height > workingArea.Height)
                windowRect.Height = workingArea.Height;
                
            // 调整位置以确保窗口完全在工作区域内
            if (windowRect.Left < workingArea.Left)
                windowRect.X = workingArea.Left;
            else if (windowRect.Right > workingArea.Right)
                windowRect.X = workingArea.Right - windowRect.Width;
                
            if (windowRect.Top < workingArea.Top)
                windowRect.Y = workingArea.Top;
            else if (windowRect.Bottom > workingArea.Bottom)
                windowRect.Y = workingArea.Bottom - windowRect.Height;
                
            return windowRect;
        }
    }

    // 使用WPF原生类型替代Win32结构
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPosition
    {
        public IntPtr Hwnd;
        public IntPtr HwndInsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public WindowPositionFlags Flags;
    }

    [Flags]
    internal enum WindowPositionFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOREDRAW = 0x0008,
        SWP_NOACTIVATE = 0x0010,
        SWP_FRAMECHANGED = 0x0020,
        SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080,
        SWP_NOCOPYBITS = 0x0100,
        SWP_NOOWNERZORDER = 0x0200,
        SWP_NOSENDCHANGING = 0x0400,
    }
}