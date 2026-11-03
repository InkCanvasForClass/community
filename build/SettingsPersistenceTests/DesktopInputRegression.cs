using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class DesktopInputRegression
{
    internal static void Contract(Action<bool, string> check)
    {
        foreach (bool preference in new[] { false, true })
        {
            SettingsManager.Settings.Startup.EnableWindowChromeRendering = preference;
            var window = new PerformanceTransparentWin { Width = 400, Height = 200,
                ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                check(window.AllowsTransparency && !window.IsUsingWindowChromeRendering,
                    "A saved preference reactivated the unsafe compositor");
                check(window.WindowStyle == WindowStyle.None && window.Background == Brushes.Transparent,
                    "The overlay no longer uses a transparent borderless surface");
                var handle = new WindowInteropHelper(window).Handle;
                for (int i = 0; i < 1000; i++) {
                    window.SetTransparentHitThrough(); window.SetTransparentNotHitThrough();
                }
                check(new WindowInteropHelper(window).Handle == handle, "Tool changes recreated the HWND");
                bool attributes = GetLayeredWindowAttributes(handle, out _, out _, out uint flags);
                check(!attributes || (flags & 2) == 0, "Tool changes replaced per-pixel alpha with constant HWND alpha");
            }
            finally { window.Close(); }
        }
    }

    internal static void Surface(Action<bool, string> check)
    {
        foreach (bool preference in new[] { false, true })
        {
            SettingsManager.Settings.Startup.EnableWindowChromeRendering = preference;
            var canvas = new Canvas { Width = 400, Height = 200 };
            canvas.Children.Add(new Border { Width = 80, Height = 40, Background = Brushes.White });
            var window = new PerformanceTransparentWin { Width = 400, Height = 200,
                ShowActivated = false, ShowInTaskbar = false, Content = canvas };
            try
            {
                window.Show(); window.UpdateLayout(); Pump(100);
                check(window.AllowsTransparency && !window.IsUsingWindowChromeRendering,
                    "A saved preference reactivated constant-alpha rendering");
                check(window.WindowStyle == WindowStyle.None, "Transparent overlay acquired an incompatible caption");
                var handle = new WindowInteropHelper(window).Handle;
                window.SetTransparentHitThrough(); window.SetTransparentNotHitThrough();
                check(new WindowInteropHelper(window).Handle == handle, "Mode changes recreated the HWND");
                canvas.Measure(new Size(400, 200)); canvas.Arrange(new Rect(0, 0, 400, 200)); canvas.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(400, 200, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(canvas); var pixels = new byte[400 * 200 * 4]; bitmap.CopyPixels(pixels, 1600, 0);
                check(pixels[(100 * 400 + 200) * 4 + 3] == 0, "Empty overlay pixels lost their alpha-zero surface");
                check(pixels[(20 * 400 + 20) * 4 + 3] == 255, "Toolbar pixels became invisible");
                bool attributes = GetLayeredWindowAttributes(handle, out _, out _, out uint flags);
                check(!attributes || (flags & 2) == 0, "Overlay replaced per-pixel alpha with constant HWND alpha");
            }
            finally { window.Close(); }
        }
    }

    // Exercise OS input routing, not just WM_NCHITTEST's return value. The
    // underlying window deliberately belongs to another process/thread.
    internal static void Run(Action<bool, string> check)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(DesktopInputRegression).Assembly.Location);
        start.ArgumentList.Add("--desktop-click-fixture");
        using var fixture = Process.Start(start);
        PerformanceTransparentWin overlay = null;
        bool hasPointer = GetCursorPos(out var originalPointer);
        try
        {
            if (!hasPointer) throw new InvalidOperationException("Interactive desktop unavailable: GetCursorPos was denied");
            var ready = fixture.StandardOutput.ReadLineAsync();
            Wait(ready);
            check(long.TryParse(ready.Result, out var rawHandle), "External click fixture failed to start");
            var external = new IntPtr(rawHandle);
            SettingsManager.Settings.Startup.EnableWindowChromeRendering = true;
            var canvas = new Canvas();
            int toolbarClicks = 0, overlayClicks = 0;
            var toolbar = new Button { Width = 100, Height = 45, Content = "Toolbar", Background = Brushes.White };
            Canvas.SetLeft(toolbar, 20); Canvas.SetTop(toolbar, 20);
            toolbar.Click += (_, _) => toolbarClicks++;
            canvas.Children.Add(toolbar);
            canvas.MouseDown += (_, _) => overlayClicks++;
            overlay = new PerformanceTransparentWin { Left = 240, Top = 180, Width = 600, Height = 320,
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, Content = canvas };
            overlay.Show(); Pump(250); overlay.SetTransparentHitThrough();
            check(overlay.AllowsTransparency && !overlay.IsUsingWindowChromeRendering,
                "Persisted WindowChrome preference reactivated the unsafe renderer");
            var desktopPoint = overlay.PointToScreen(new Point(300, 150));
            var toolbarPoint = toolbar.PointToScreen(new Point(40, 20));
            Click(desktopPoint); var clicked = fixture.StandardOutput.ReadLineAsync(); Wait(clicked);
            check(clicked.Result == "clicked", "Actual desktop click did not reach the external process");
            Click(toolbarPoint); Pump(100);
            check(toolbarClicks == 1, "Pass-through disabled the ICCCE toolbar");
            // Drawing needs a real input surface; returning to mouse mode must
            // remove it without removing the toolbar or recreating the HWND.
            var handle = new WindowInteropHelper(overlay).Handle;
            for (int i = 0; i < 5; i++)
            {
                canvas.Background = Brushes.White;
                overlay.SetTransparentNotHitThrough(); Pump(80);
                int previous = overlayClicks;
                Click(desktopPoint); Pump(50);
                check(overlayClicks == previous + 1, "Drawing mode lost its input");
                canvas.Background = null;
                overlay.SetTransparentHitThrough(); Pump(80);
                Click(desktopPoint); clicked = fixture.StandardOutput.ReadLineAsync(); Wait(clicked);
                check(clicked.Result == "clicked", "Returning to mouse mode left a blocking overlay");
            }
            check(new WindowInteropHelper(overlay).Handle == handle, "Tool changes recreated the window");
            // A visible retained ink visual must survive the transparent path.
            var stroke = new System.Windows.Shapes.Line { X1 = 140, X2 = 220, Y1 = 80, Y2 = 100,
                Stroke = Brushes.Red, StrokeThickness = 4, IsHitTestVisible = false };
            canvas.Children.Add(stroke); overlay.UpdateLayout(); Pump(80);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(600, 320, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas); var pixels = new byte[600 * 320 * 4]; bitmap.CopyPixels(pixels, 2400, 0);
            check(Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[4*i+2] > 200 && pixels[4*i+1] < 50 && pixels[4*i+3] > 200),
                "Visible ink disappeared from the transparent overlay");
            Click(desktopPoint); clicked = fixture.StandardOutput.ReadLineAsync(); Wait(clicked);
            check(clicked.Result == "clicked", "Visible ink blocked the remaining desktop surface");
        }
        finally
        {
            overlay?.Close();
            if (hasPointer) SetCursorPos(originalPointer.X, originalPointer.Y);
            if (!fixture.HasExited) {
                fixture.StandardInput.WriteLine("exit"); fixture.StandardInput.Flush();
                if (!fixture.WaitForExit(3000)) fixture.Kill();
            }
        }
    }

    internal static int RunFixture()
    {
        var app = new Application();
        var window = new Window { Left = 240, Top = 180, Width = 600, Height = 320,
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
        var target = new Button { Content = "External process click target", Background = Brushes.White };
        target.Click += (_, _) => { Console.WriteLine("clicked"); Console.Out.Flush(); };
        window.Content = target;
        window.Loaded += (_, _) => {
            Console.WriteLine(new WindowInteropHelper(window).Handle.ToInt64()); Console.Out.Flush();
            _ = Task.Run(() => { Console.ReadLine(); window.Dispatcher.BeginInvoke(new Action(window.Close)); });
        };
        app.Run(window); return 0;
    }
    private static void Wait(Task task)
    {
        var deadline = Stopwatch.StartNew();
        while (!task.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5)) Pump(20);
        if (!task.IsCompleted) throw new TimeoutException("Cross-process desktop click fixture timed out");
        task.GetAwaiter().GetResult();
    }
    private static void Click(Point point)
    {
        if (!SetCursorPos((int)point.X, (int)point.Y)) throw new InvalidOperationException("No interactive desktop for OS click fixture");
        GetCursorPos(out var actual);
        if (actual.X != (int)point.X || actual.Y != (int)point.Y) throw new InvalidOperationException($"Pointer did not reach fixture: {actual.X},{actual.Y}");
        mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr window, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}

