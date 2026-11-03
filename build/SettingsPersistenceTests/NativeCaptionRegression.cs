using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class NativeCaptionRegression
{
    internal static void Run(Action<bool, string> check)
    {
        SettingsManager.Settings.Startup.EnableWindowChromeRendering = true;
        var window = new TestWindow { Width = 800, Height = 600,
            WindowStyle = WindowStyle.SingleBorderWindow, ResizeMode = ResizeMode.CanResize,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 1 };
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, null);
        try
        {
            window.Show(); window.SetTransparentHitThrough();
            var handle = new WindowInteropHelper(window).Handle;
            bool visible = GetLayeredWindowAttributes(handle, out _, out byte alpha, out uint flags);
            check(visible && alpha == 255 && (flags & 2) != 0,
                $"Opaque WindowChrome alpha: valid={visible}, alpha={alpha}, flags={flags}, opacity={window.Opacity}, chrome={window.IsUsingWindowChromeRendering}");
            window.Opacity = 0;
            check(GetWindowRect(handle, out var rectangle), "Native test window has no bounds");
            int x = rectangle.Right - 20, y = rectangle.Top + 15;
            var hit = SendMessage(handle, 0x84, IntPtr.Zero,
                new IntPtr(unchecked((int)((uint)(ushort)x | ((uint)(ushort)y << 16)))));
            check(hit.ToInt32() == 20, $"Native close caption was intercepted by ink pass-through: {hit}");
            SystemCommands.MaximizeWindow(window); Pump();
            check(window.WindowState == WindowState.Maximized && window.WindowStyle == WindowStyle.SingleBorderWindow,
                "Native maximize removed the caption");
            SystemCommands.RestoreWindow(window); Pump();
            check(window.WindowState == WindowState.Normal, "Native restore did not return to windowed mode");
            window.WindowStyle = WindowStyle.None;
            GetWindowRect(handle, out rectangle);
            x = (rectangle.Left + rectangle.Right) / 2; y = (rectangle.Top + rectangle.Bottom) / 2;
            hit = SendMessage(handle, 0x84, IntPtr.Zero,
                new IntPtr(unchecked((int)((uint)(ushort)x | ((uint)(ushort)y << 16)))));
            check(hit.ToInt32() == -1, $"Desktop borderless input pass-through was lost: {hit}");
        }
        finally { window.Close(); }
    }
    private sealed class TestWindow : PerformanceTransparentWin
    {
        protected override bool ShouldHandleWindowChromeHitTest(Point point) => false;
    }
    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr window, out uint key, out byte alpha, out uint flags);
}
