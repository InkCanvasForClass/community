using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Ink_Canvas.Helpers;

internal static class FloatingWindowNativeRegression
{
    private const int FloatingStyle = 0x06080000;
    private const int ToolNoActivate = 0x08000080;

    internal static void Run(Action<bool, string> check)
    {
        using var interceptor = new FloatingWindowInterceptor();
        foreach (var ruleToDisable in interceptor.GetAllRules().Keys) interceptor.SetInterceptRule(ruleToDisable, false);
        var rule = interceptor.GetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard5Floating);
        rule.IsEnabled = true;
        rule.ProcessName = Process.GetCurrentProcess().ProcessName;
        rule.ClassNamePattern = "HwndWrapper[";
        rule.HasWindowStyle = false; // Hidden fixtures intentionally omit WS_VISIBLE.
        var match = typeof(FloatingWindowInterceptor).GetMethod("MatchesRulePrecise", BindingFlags.Instance | BindingFlags.NonPublic);
        bool Matches(HwndSource source) => (bool)match.Invoke(interceptor, new object[] { source.Handle, rule });

        using var popup = Make("", FloatingStyle, ToolNoActivate);
        GetWindowRect(popup.Handle, out var rect);
        var className = new System.Text.StringBuilder(256); GetClassName(popup.Handle, className, 256);
        check(Matches(popup), $"Actual HWND/DPI bridge rejected the known popup dimensions: class={className}, "
            + $"style={GetWindowLong(popup.Handle, -16):X}, ex={GetWindowLong(popup.Handle, -20):X}, "
            + $"size={rect.Right - rect.Left}x{rect.Bottom - rect.Top}, dpi={GetDpiForWindow(popup.Handle)}");
        SetWindowText(popup.Handle, "希沃白板授课");
        check(!Matches(popup), "Revalidation accepted a popup handle repurposed as a teaching window");
        using var captioned = Make("", FloatingStyle | 0x00C00000, ToolNoActivate);
        check(!Matches(captioned), "Actual captioned HWND matched the floating rule");
        using var appWindow = Make("", FloatingStyle, ToolNoActivate | 0x00040000);
        check(!Matches(appWindow), "Actual application/taskbar HWND matched the floating rule");
        using var child = Make("", FloatingStyle | 0x40000000, 0, popup.Handle);
        check(!Matches(child), "Actual child HWND matched the floating rule");
        using var modalFrame = Make("", FloatingStyle, ToolNoActivate | 1);
        check(!Matches(modalFrame), "Untitled modal-frame upload dialog matched the floating rule");
        using var ownerWindow = Make("授课窗口", FloatingStyle, ToolNoActivate);
        using var uploadDialog = Make("", FloatingStyle, ToolNoActivate, ownerWindow.Handle);
        check(Matches(uploadDialog), "Ownership alone should not reject an otherwise known floating bar");
        EnableWindow(ownerWindow.Handle, false);
        check(!Matches(uploadDialog), "Untitled upload dialog with an actual disabled owner matched the floating rule");
        using var nestedDialog = Make("", FloatingStyle, ToolNoActivate, uploadDialog.Handle);
        check(!Matches(nestedDialog), "Nested upload dialog bypassed the disabled-owner protection");
        EnableWindow(ownerWindow.Handle, true);
        check(Matches(uploadDialog), "Completed modal operation left the floating bar permanently excluded");

        // Our own cloaked fixture process deliberately stops pumping its window
        // thread. Hide/restore must return without waiting for that thread to recover.
        var start = new ProcessStartInfo(Environment.ProcessPath)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(FloatingWindowNativeRegression).Assembly.Location);
        start.ArgumentList.Add("--interceptor-fixture");
        using var fixture = Process.Start(start);
        try
        {
            var ready = fixture.StandardOutput.ReadLineAsync();
            check(ready.Wait(TimeSpan.FromSeconds(10)), "Native fixture did not initialize");
            string handleText = ready.Result;
            if (!long.TryParse(handleText, out long nativeHandle))
                throw new InvalidOperationException($"Native fixture failed: exit={fixture.ExitCode}; {fixture.StandardError.ReadToEnd()}");
            var handle = new IntPtr(nativeHandle);
            rule.HasWindowStyle = true;
            var hide = typeof(FloatingWindowInterceptor).GetMethod("InterceptWindow", BindingFlags.Instance | BindingFlags.NonPublic);
            var request = Task.Run(() => hide.Invoke(interceptor, new object[] { handle, rule }));
            check(request.Wait(TimeSpan.FromSeconds(2)), "Hiding a non-responsive target blocked the interceptor");
            check(interceptor.GetInterceptedWindowsCount() == 1, "Native hide request was not tracked for restoration");
            var restore = Task.Run(() => interceptor.RestoreWindow(handle));
            check(restore.Wait(TimeSpan.FromSeconds(2)) && restore.Result, "Restoring a non-responsive target blocked or failed");
            check(interceptor.GetInterceptedWindowsCount() == 0, "Restored fixture retained its interception state");
            var secondRequest = Task.Run(() => hide.Invoke(interceptor, new object[] { handle, rule }));
            check(secondRequest.Wait(TimeSpan.FromSeconds(2)) && interceptor.GetInterceptedWindowsCount() == 1,
                "Second native hide request failed");
            fixture.StandardInput.WriteLine("promote"); fixture.StandardInput.Flush();
            var promoted = fixture.StandardOutput.ReadLineAsync();
            check(promoted.Wait(TimeSpan.FromSeconds(2)) && promoted.Result == "promoted", "Fixture did not change to teaching mode");
            var cleanup = typeof(FloatingWindowInterceptor).GetMethod("CleanupInvalidWindows", BindingFlags.Instance | BindingFlags.NonPublic);
            var recovery = Task.Run(() => cleanup.Invoke(interceptor, null));
            check(recovery.Wait(TimeSpan.FromSeconds(2)) && interceptor.GetInterceptedWindowsCount() == 0,
                "A tracked floating HWND repurposed as a teaching window was not restored");
            fixture.StandardInput.WriteLine("float"); fixture.StandardInput.Flush();
            var floated = fixture.StandardOutput.ReadLineAsync();
            check(floated.Wait(TimeSpan.FromSeconds(2)) && floated.Result == "floating", "Fixture did not return to floating mode");
            var finalRequest = Task.Run(() => hide.Invoke(interceptor, new object[] { handle, rule }));
            check(finalRequest.Wait(TimeSpan.FromSeconds(2)) && interceptor.GetInterceptedWindowsCount() == 1,
                "Final native hide request failed");
            bool updating = Ink_Canvas.App.IsUpdateInstalling;
            try
            {
                Ink_Canvas.App.IsUpdateInstalling = true;
                var stop = Task.Run(interceptor.Stop);
                check(stop.Wait(TimeSpan.FromSeconds(2)) && interceptor.GetInterceptedWindowsCount() == 0,
                    "Update shutdown abandoned its hidden HWNDs or blocked on the target thread");
            }
            finally { Ink_Canvas.App.IsUpdateInstalling = updating; }
        }
        finally
        {
            if (!fixture.HasExited)
            {
                fixture.StandardInput.WriteLine("exit"); fixture.StandardInput.Flush();
                if (!fixture.WaitForExit(5000)) fixture.Kill(true);
            }
        }
    }

    internal static int RunFixture()
    {
        // Invisible to the user, but WS_VISIBLE remains set for the interception test.
        using var window = Make("", FloatingStyle, ToolNoActivate, x: -5000);
        int cloak = 1;
        if (DwmSetWindowAttribute(window.Handle, 13, ref cloak, 4) != 0) return 2;
        ShowWindow(window.Handle, 4); // Own fixture thread only; production uses ShowWindowAsync.
        Console.WriteLine(window.Handle.ToInt64()); Console.Out.Flush();
        if (Console.ReadLine() == "promote")
        {
            SetWindowText(window.Handle, "希沃白板授课");
            Console.WriteLine("promoted"); Console.Out.Flush();
            if (Console.ReadLine() == "float")
            {
                SetWindowText(window.Handle, "");
                Console.WriteLine("floating"); Console.Out.Flush();
                Console.ReadLine();
            }
        }
        return 0;
    }

    private static HwndSource Make(string title, int style, int extended, IntPtr parent = default, int x = -20000)
    {
        var source = new HwndSource(new HwndSourceParameters(title)
        {
            Width = 550, Height = 200, PositionX = x, PositionY = 0,
            WindowStyle = style, ExtendedWindowStyle = extended, ParentWindow = parent
        });
        // CreateWindow adds WS_CAPTION to an overlapped HWND; EasiNote removes that
        // frame after creation. Reproduce its actual observed frameless style.
        SetWindowLong(source.Handle, -16, unchecked((uint)style));
        SetWindowLong(source.Handle, -20, unchecked((uint)extended));
        SetWindowPos(source.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0037);
        return source;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(IntPtr hwnd, string text);
    [DllImport("user32.dll")] private static extern bool EnableWindow(IntPtr hwnd, bool enable);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern uint GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern uint SetWindowLong(IntPtr hwnd, int index, uint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attribute, ref int value, uint size);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
}
