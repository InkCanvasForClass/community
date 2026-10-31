using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;
using Ink_Canvas.Helpers;

internal static class MouseWheelRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var sent = new List<WheelPacket>();
        int batches = 0;
        var target = new WheelTarget(new IntPtr(123), 1, 1);
        using (var forwarder = new MouseWheelForwarder(Dispatcher.CurrentDispatcher, () => batches++, p => { sent.Add(p); return true; }))
        {
            for (int i = 0; i < 1000; i++) forwarder.Enqueue(new WheelPacket(target, 1, 0, -100, -200));
            forwarder.Flush();
            check(sent.Count == 1 && sent[0].Delta == 1000 && batches == 1, "High-resolution wheel bursts lost delta or generated redundant messages");
            forwarder.Enqueue(new WheelPacket(target, -120, 0, -100, -200));
            forwarder.Enqueue(new WheelPacket(target, 120, 0, -100, -200));
            forwarder.Enqueue(new WheelPacket(target, 120, 8, -100, -200));
            forwarder.Enqueue(new WheelPacket(target with { Window = new IntPtr(124) }, 120, 8, -100, -200));
            forwarder.Flush();
            check(sent.Skip(1).Select(p => p.Delta).SequenceEqual(new[] { -120, 120, 120, 120 }) && sent[3].Keys == 8,
                "Wheel batching reordered direction, modifier or target changes");
            sent.Clear();
            for (int i = 0; i < 1000; i++) forwarder.Enqueue(new WheelPacket(target, i % 2 == 0 ? 120 : -120, 0, 0, 0));
            forwarder.Flush();
            check(sent.Count == 1000 && sent.Sum(p => p.Delta) == 0, "Bounded queue dropped fast alternating wheel input");
            sent.Clear();
            for (int i = 0; i < 300; i++) forwarder.Enqueue(new WheelPacket(target, 120, 0, 0, 0));
            forwarder.Flush();
            check(sent.Sum(p => p.Delta) == 36000 && sent.All(p => p.Delta <= short.MaxValue), "Merged delta overflowed native signed wheel data");
            sent.Clear();
            forwarder.Enqueue(new WheelPacket(target, 120, 0, 0, 0)); forwarder.Dispose(); forwarder.Flush();
            check(sent.Count == 0, "Disposed forwarder delivered stale wheel input");
        }
        NativeDelivery(check);
    }

    private static void NativeDelivery(Action<bool, string> check)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--wheel-fixture");
        using var fixture = Process.Start(start)!;
        try
        {
            string ready = fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()!;
            long child = long.Parse(ready);
            using var overlay = new HwndSource(new HwndSourceParameters("Wheel overlay fixture")
            { Width = 200, Height = 200, PositionX = -10000, PositionY = -10000,
                WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000088 });
            int style = GetWindowLong(overlay.Handle, -20);
            IntPtr foreground = GetForegroundWindow();
            var target = MouseWheelForwarder.FindTarget(-9990, -9990, (uint)Environment.ProcessId);
            check(target.Window == new IntPtr(child) && target.Process == fixture.Id, "Native hit test did not skip the ICC overlay or choose the underlying child");
            using var forwarder = new MouseWheelForwarder(Dispatcher.CurrentDispatcher);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) forwarder.Enqueue(new WheelPacket(target, -1, 12, -9990, -9990));
            forwarder.Flush();
            string reply = fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()!;
            var received = JsonSerializer.Deserialize<int[]>(reply)!;
            check(received.SequenceEqual(new[] { -1000, 12, -9990, -9990 }), "Native wheel delivery lost signed delta, modifiers or negative monitor coordinates");
            check(GetWindowLong(overlay.Handle, -20) == style && GetForegroundWindow() == foreground, "Forwarding changed overlay styles or focus");
            Console.WriteLine($"Wheel burst: 1,000 deltas → one native message, round trip={watch.Elapsed.TotalMilliseconds:F1}ms");
            fixture.StandardInput.WriteLine("stall"); fixture.StandardInput.Flush();
            string stalled = fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()!;
            check(stalled == "stalled", "Native busy-window fixture did not start");
            watch.Restart();
            forwarder.Enqueue(new WheelPacket(target, 120, 0, -9990, -9990)); forwarder.Flush();
            check(watch.Elapsed < TimeSpan.FromMilliseconds(200), "An unresponsive target blocked the forwarding thread");
            reply = fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()!;
            check(JsonSerializer.Deserialize<int[]>(reply)![0] == 120, "Busy target lost the queued wheel message");
        }
        finally
        {
            fixture.StandardInput.WriteLine("exit"); fixture.StandardInput.Flush();
            if (!fixture.WaitForExit(2000)) fixture.Kill();
        }
    }

    internal static int RunFixture()
    {
        using var root = new HwndSource(new HwndSourceParameters("Wheel receiver fixture") { Width = 200, Height = 200,
            PositionX = -10000, PositionY = -10000, WindowStyle = unchecked((int)0x90000000), ExtendedWindowStyle = 0x08000080 });
        using var child = new HwndSource(new HwndSourceParameters("Wheel receiver child") { ParentWindow = root.Handle,
            Width = 180, Height = 180, PositionX = 0, PositionY = 0, WindowStyle = 0x50000000 });
        child.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message == 0x020A)
            {
                Console.WriteLine(JsonSerializer.Serialize(new[] { (int)(short)(wParam.ToInt64() >> 16),
                    (int)(ushort)wParam.ToInt64(), (int)(short)lParam.ToInt64(), (int)(short)(lParam.ToInt64() >> 16) }));
                Console.Out.Flush(); handled = true;
            }
            return IntPtr.Zero;
        });
        Console.WriteLine(child.Handle.ToInt64()); Console.Out.Flush();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = Task.Run(() =>
        {
            while (Console.ReadLine() is string command && command == "stall")
                dispatcher.BeginInvoke(new Action(() => { Console.WriteLine("stalled"); Console.Out.Flush(); Thread.Sleep(500); }));
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        });
        Dispatcher.Run();
        return 0;
    }
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
