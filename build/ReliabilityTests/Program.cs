using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink.WinRT;
using Ink_Canvas.Ink;
using InkCanvasPPTAgent.Contracts;
using Windows.Devices.Input;
using Windows.UI.Core;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ICC_Reliability_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Ink_Canvas.App.RootPath = directory + Path.DirectorySeparatorChar;
        Ink_Canvas.MainWindow.Settings.Advanced.IsSaveLogByDate = false;
        LogHelper.SuppressCallerInfo = false;
        try
        {
            if (args.Length > 0)
                Check(OfficeLifecycleGuard.Verify(args[0]) >= 3, "Office monitor lifecycle guard failed");
            GateRegression();
            CoalescedUiRegression();
            InputRecoveryRegression.Run(Check);
            AutoFontLifetimeRegression();
            PalmRegression();
            PalmEngineRegression.Run(Check);
            CoordinateRegression();
            InputRegionAndRenderFenceRegression();
            SmoothingRegression();
            LegacyXmlRegression(directory);
            PipeRegressionAsync().GetAwaiter().GetResult();
            WriterRegressionAsync(directory).GetAwaiter().GetResult();
            StaRegressionAsync().GetAwaiter().GetResult();
            Check(LogHelper.Flush(TimeSpan.FromSeconds(2)), "Log writer did not flush");
            Console.WriteLine($"PASS: {_checks} reliability checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            AtomicFileWriter.Flush(TimeSpan.FromSeconds(2));
            LogHelper.Flush(TimeSpan.FromSeconds(2));
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PointerEventArgs Pointer(uint id) => new()
    {
        CurrentPoint = new() { PointerId = id,
            PointerDevice = new() { PointerDeviceType = PointerDeviceType.Touch } }
    };

    private static void GateRegression()
    {
        var result = PointerGateResult.BlockAndForward;
        int down = 0, up = 0, ended = 0, canceled = 0;
        var gate = new WinRTInkInputGate(_ => result, _ => down++, null, _ => up++,
            null, null, null, null, null, null, null, null, null, _ => ended++, () => canceled++);
        gate.PageFrozen = true;
        gate.OnPointerPressing(null, Pointer(1));
        gate.OnPointerReleasing(null, Pointer(1));
        Check(down == 1 && up == 1, "Frozen page swallowed toolbar input");
        gate.PageFrozen = false;
        gate.CancelActiveStrokes();
        gate.OnPointerPressing(null, Pointer(2));
        gate.OnPointerLost(null, Pointer(2));
        Check(down == 2 && up == 2, "Idle cancellation or pointer loss stuck chrome forwarding");
        gate.CanvasInputEnabled = false;
        gate.OnPointerPressing(null, Pointer(3));
        gate.OnPointerReleasing(null, Pointer(3));
        Check(down == 3, "Canvas disable swallowed toolbar input");
        gate.CanvasInputEnabled = true;
        result = PointerGateResult.AllowInk;
        gate.OnPointerPressing(null, Pointer(4));
        gate.CancelActiveStrokes();
        gate.OnStrokeEnded(null, Pointer(4));
        Check(canceled == 1 && ended == 0, "Active stroke cancellation was lost");
        gate.OnPointerReleasing(null, Pointer(4));
        gate.OnPointerPressing(null, Pointer(5));
        gate.OnStrokeEnded(null, Pointer(5));
        gate.OnPointerReleasing(null, Pointer(5));
        Check(ended == 1, "Cancellation leaked into a new stroke");
        gate.PageFrozen = true;
        var press = Pointer(6);
        gate.OnPointerPressing(null, press);
        Check(press.Handled, "Frozen canvas accepted ink");
    }

    private static void CoalescedUiRegression()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        void Pump(int milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds)
            };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        int calls = 0, latest = 0, captured = -1;
        using var action = new DispatcherCoalescedAction(dispatcher, TimeSpan.FromMilliseconds(15),
            () => { calls++; captured = latest; });
        for (int i = 1; i <= 10000; i++) { latest = i; action.Request(); }
        Pump(80);
        Check(calls == 1 && captured == 10000, "UI burst was not coalesced to the final setting");
        latest = 10001;
        action.Request();
        action.Flush();
        Pump(50);
        Check(calls == 2 && captured == 10001, "Flush lost the final value or a queued callback saved it twice");
        Task.Run(() => { for (int i = 0; i < 10000; i++) action.Request(); }).GetAwaiter().GetResult();
        Pump(70);
        Check(calls == 3, "Background requests did not share the UI scheduler");
        action.Request();
        action.Dispose();
        Pump(50);
        Check(calls == 3, "Disposed UI work executed a stale callback");
        int recoveryCalls = 0;
        using var recover = new DispatcherCoalescedAction(dispatcher, TimeSpan.FromMilliseconds(15), () =>
        {
            if (++recoveryCalls == 1) throw new InvalidOperationException("test callback failure");
        });
        recover.Request();
        Pump(60);
        recover.Request();
        Pump(60);
        Check(recoveryCalls == 2, "Callback failure permanently disabled UI scheduling");
        int progress = 0;
        using var sustained = new DispatcherCoalescedAction(dispatcher, TimeSpan.FromMilliseconds(15), () => progress++);
        var producer = Task.Run(async () =>
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 200) { sustained.Request(); await Task.Delay(2); }
        });
        Pump(300);
        producer.GetAwaiter().GetResult();
        sustained.Flush();
        Check(progress >= 2, "Sustained input postponed every update until the input stopped");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateAutoFontReference(bool unload)
    {
        var text = new System.Windows.Controls.TextBlock { Text = "Toolbar label", Width = 50 };
        AutoFontSizeHelper.SetIsEnabled(text, true);
        if (unload)
        {
            text.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
            text.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        }
        return new WeakReference(text);
    }

    private static void AutoFontLifetimeRegression()
    {
        var unopened = CreateAutoFontReference(false);
        var unloaded = CreateAutoFontReference(true);
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        // Only this isolated regression process forces GC to detect descriptor roots.
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check(!unopened.IsAlive, "Auto font sizing retained a control that was never displayed");
        Check(!unloaded.IsAlive, "Auto font sizing retained a toolbar control after unload");
    }

    private static void PalmRegression()
    {
        var events = new List<WinRTInkPalmEvent>();
        int inkEnded = 0, gestures = 0;
        var gate = new WinRTInkInputGate(_ => PointerGateResult.AllowInk, null, null, null,
            null, null, null, null, null, null, _ => gestures++, null, null,
            _ => inkEnded++, () => { }, events.Add)
        {
            PalmEraserEnabled = true, TwoFingerGestureAllowed = true,
            PalmProfile = new() { Options = new() { AreaThresholdPx2 = 400, LockSize = true, MultiTouch = false } }
        };
        PointerEventArgs Contact(uint id, double width, double height, double x = 0)
        {
            var pointer = Pointer(id);
            pointer.CurrentPoint.Properties.ContactRect = new() { Width = width, Height = height };
            pointer.CurrentPoint.Position = new(x, 10);
            return pointer;
        }
        var palm = Contact(100, 100, 80);
        gate.OnPointerPressing(null, palm);
        Check(palm.Handled && events.Count == 1 && events[0].Phase == WinRTInkPalmPhase.Begin,
            "Large contact was suppressed without starting the eraser");
        Check(events[0].WidthDip == 100 && events[0].HeightDip == 80, "Palm eraser dimensions changed units");
        var secondary = Contact(101, 5, 5);
        gate.OnPointerPressing(null, secondary);
        Check(secondary.Handled && gestures == 0, "A second contact stole the palm as a gesture");
        gate.OnPointerMoving(null, Contact(100, 5, 5, 40));
        Check(events[^1].Phase == WinRTInkPalmPhase.Move && events[^1].Position.X == 40,
            "Palm stopped erasing after the reported contact shrank");
        gate.OnPointerLost(null, palm);
        Check(events[^1].Phase == WinRTInkPalmPhase.End, "Lost palm did not end erasing");
        Check(!gate.OnStrokeEnded(null, palm) && inkEnded == 0, "Palm committed an ink stroke");
        gate.OnPointerReleasing(null, secondary);
        var finger = Contact(102, 5, 5);
        gate.OnPointerPressing(null, finger);
        Check(!finger.Handled, "Palm state leaked into the next finger stroke");
        gate.OnPointerMoving(null, Contact(102, 100, 80));
        Check(events[^1].PointerId == 102 && events[^1].Phase == WinRTInkPalmPhase.Begin,
            "A growing contact was never reevaluated as a palm");
        Check(!gate.OnStrokeEnded(null, finger), "Promoted palm left a dry ink artifact");
        gate.OnPointerReleasing(null, finger);
        Check(events[^1].Phase == WinRTInkPalmPhase.End, "Palm release did not end the transaction");
        gate.OnPointerPressing(null, Contact(103, 100, 80));
        gate.CancelActiveStrokes();
        gate.OnPointerMoving(null, Contact(103, 100, 80));
        Check(events[^1].Phase == WinRTInkPalmPhase.End, "Cancellation restarted palm erasing");
        gate.OnPointerReleasing(null, Contact(103, 100, 80));

        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var delivered = new List<WinRTInkPalmEvent>();
        var bridge = new WinRTInkPalmDispatcher(dispatcher, delivered.Add);
        bridge.Post(new(1, WinRTInkPalmPhase.Begin, default, 20, 20));
        for (int i = 0; i < 1000; i++) bridge.Post(new(1, WinRTInkPalmPhase.Move, new(i, 0), 20, 20));
        bridge.Post(new(1, WinRTInkPalmPhase.End, default, 0, 0));
        var frame = new System.Windows.Threading.DispatcherFrame();
        dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(delivered.Count <= 256 && delivered[^1].Phase == WinRTInkPalmPhase.End,
            "Palm dispatch queue grew without bound or lost release");
        Check(delivered.Any(item => item.PointerId == 0 && item.Phase == WinRTInkPalmPhase.End),
            "Palm dispatch overflow did not reset the old transaction");
    }

    private static async Task PipeRegressionAsync()
    {
        string name = "ICC_Test_" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();
        await client.ConnectAsync(1000);
        await connected;
        using var timeout = new CancellationTokenSource(200);
        var watch = Stopwatch.StartNew();
        try
        {
            await PipeFrame.WriteFrameAsync(client, new string('x', 512 * 1024), timeout.Token);
            throw new Exception("Unresponsive peer unexpectedly accepted full frame");
        }
        catch (OperationCanceledException) { }
        Check(watch.Elapsed < TimeSpan.FromSeconds(2), "Async pipe write deadline failed");
        // Use a fresh pair for the synchronous-read cancellation test.
        string second = name + "_read";
        using var readServer = new NamedPipeServerStream(second, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var readClient = new NamedPipeClientStream(".", second, PipeDirection.InOut, PipeOptions.Asynchronous);
        var readConnection = readServer.WaitForConnectionAsync();
        await readClient.ConnectAsync(1000);
        await readConnection;
        using var deadline = new StreamDeadline(readClient, 200);
        var read = Task.Run(() => { try { return readClient.ReadByte(); } catch (IOException) { return -1; } catch (ObjectDisposedException) { return -1; } });
        Check(await read.WaitAsync(TimeSpan.FromSeconds(2)) == -1, "Synchronous pipe read was not interrupted");
        using var memory = new MemoryStream();
        await PipeFrame.WriteFrameAsync(memory, "{\"test\":true}", CancellationToken.None);
        memory.Position = 0;
        Check(PipeFrame.ReadFrame(memory) == "{\"test\":true}", "Async framing broke synchronous reader compatibility");
    }

    private static async Task WriterRegressionAsync(string directory)
    {
        var path = Path.Combine(directory, "state.json");
        for (int i = 0; i < 100; i++) AtomicFileWriter.WriteLatest(path, i.ToString());
        Check(await AtomicFileWriter.WriteLatestAsync(path, "latest"), "Config write failed");
        Check(File.ReadAllText(path) == "latest", "Coalescing lost the newest settings");
        var writes = Enumerable.Range(0, 30).Select(i => AtomicFileWriter.WriteLatestAsync(Path.Combine(directory, $"{i}.json"), i.ToString()));
        Check((await Task.WhenAll(writes)).All(success => success), "Concurrent config paths lost completion");
        AtomicFileWriter.WriteBytesNow(Path.Combine(directory, "ink.bin"), new byte[] { 1, 2, 3 });
        Check(File.ReadAllBytes(Path.Combine(directory, "ink.bin")).SequenceEqual(new byte[] { 1, 2, 3 }), "Binary atomic write failed");
        using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(!await AtomicFileWriter.WriteLatestAsync(path, "must not replace"), "Locked target incorrectly reported success");
        Check(File.ReadAllText(path) == "latest", "Failed replacement damaged the existing file");
        Check(await AtomicFileWriter.WriteLatestAsync(path, "recovered"), "Writer did not recover after a failed replacement");
        Check(!Directory.GetFiles(directory, "*.tmp").Any(), "Atomic writer leaked temporary files");
    }

    private static async Task StaRegressionAsync()
    {
        Check(await PptStaWorker.RunAsync(() => Thread.CurrentThread.GetApartmentState()) == ApartmentState.STA,
            "Office worker is not STA");
        int first = await PptStaWorker.RunAsync(() => Environment.CurrentManagedThreadId);
        int second = await PptStaWorker.RunAsync(() => Environment.CurrentManagedThreadId);
        Check(first == second, "Office calls created unbounded threads");
        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        var stalled = PptStaWorker.RunAsync(() => { entered.Set(); release.Wait(); return 1; });
        Check(entered.Wait(1000), "Office test did not start");
        try
        {
            using var cancel = new CancellationTokenSource(100);
            try { await PptStaWorker.RunAsync(() => 2, cancel.Token); throw new Exception("Canceled work completed"); }
            catch (OperationCanceledException) { }
            _checks++;
        }
        finally { release.Set(); }
        Check(await stalled == 1, "Office worker failed to recover");
        var pumped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await PptStaWorker.RunAsync(() =>
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); pumped.TrySetResult(true); };
            timer.Start();
            return true;
        });
        Check(await pumped.Task.WaitAsync(TimeSpan.FromSeconds(2)), "Office STA did not pump messages between calls");
    }

    private static void LegacyXmlRegression(string directory)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var stroke = new System.Windows.Ink.Stroke(new System.Windows.Input.StylusPointCollection
            {
                new System.Windows.Input.StylusPoint(1.25, -2.5, 0.25f),
                new System.Windows.Input.StylusPoint(30.5, 40.75, 0.75f)
            });
            stroke.DrawingAttributes.Width = 3.5;
            stroke.DrawingAttributes.Height = 7.25;
            stroke.DrawingAttributes.IsHighlighter = true;
            stroke.DrawingAttributes.Color = System.Windows.Media.Colors.Red;
            var original = new System.Windows.Ink.StrokeCollection { stroke };
            string xml = LegacyInkXmlCodec.Serialize(original, DateTime.Now);
            var path = Path.Combine(directory, "roundtrip.xml");
            AtomicFileWriter.WriteNow(path, xml);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var restored = LegacyInkXmlCodec.Load(path);
            Check(restored.Count == 1 && restored[0].StylusPoints.Count == 2, "XML roundtrip changed stroke count");
            Check(restored[0].StylusPoints[0].X == 1.25 && restored[0].StylusPoints[1].PressureFactor == 0.75f,
                "XML roundtrip changed points or pressure across cultures");
            Check(restored[0].DrawingAttributes.Width == 3.5 && restored[0].DrawingAttributes.Height == 7.25
                && restored[0].DrawingAttributes.IsHighlighter && restored[0].DrawingAttributes.Color == System.Windows.Media.Colors.Red,
                "XML roundtrip changed drawing attributes");
            AtomicFileWriter.WriteNow(path, xml.Replace("Width=3.5", "Width=3,5"));
            Check(LegacyInkXmlCodec.Load(path)[0].DrawingAttributes.Width == 3.5, "Old decimal-comma ink is not compatible");
            AtomicFileWriter.WriteNow(path, "<invalid />");
            try { LegacyInkXmlCodec.Load(path); throw new Exception("Invalid ink was accepted as an empty page"); }
            catch (InvalidDataException) { _checks++; }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    private static void SmoothingRegression()
    {
        using var processor = new HardwareAcceleratedInkProcessor(200, 100);
        var points = new System.Windows.Input.StylusPointCollection();
        for (int i = 0; i < 2000; i++)
            points.Add(new System.Windows.Input.StylusPoint(i / 10.0, 40 + Math.Sin(i / 30.0) * 20, 0.5f));
        var input = new System.Windows.Ink.Stroke(points);
        var result = processor.SmoothStrokeWithGPU(input).GetAwaiter().GetResult();
        Check(result.StylusPoints.Count >= 2, "Background smoothing lost the stroke");
        Check(result.StylusPoints.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)), "Smoothing produced invalid points");
        Check(input.StylusPoints.Count == 2000, "Smoothing mutated its input snapshot");
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    {
        public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public NativePoint Position; public uint Private;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint ex, string cls, string title,
        uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr args);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);

    private static void CoordinateRegression()
    {
        var send = typeof(ChromeInputForwarder).GetMethod("PostMouseMessage", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (int origin in new[] { 140, -600 })
        {
            var window = CreateWindowEx(0, "STATIC", "ICC test (hidden)", 0, origin, origin + 70, 400, 300,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(window != IntPtr.Zero, "Could not create hidden test window");
            try
            {
                foreach (var point in new[] { new NativePoint { X = 37, Y = 91 }, new NativePoint { X = -12, Y = -8 } })
                {
                    var screen = point;
                    Check(ClientToScreen(window, ref screen), "Test coordinate setup failed");
                    send.Invoke(null, new object[] { window, 0x0201u, screen.X, screen.Y, 1 });
                    Check(PeekMessage(out var message, window, 0x0201, 0x0201, 1), "Forwarded down is missing");
                    long packed = message.LParam.ToInt64();
                    Check(unchecked((short)packed) == point.X && unchecked((short)(packed >> 16)) == point.Y,
                        "Forwarded coordinates are not client-relative");
                }
            }
            finally { DestroyWindow(window); }
        }
    }

    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);

    private static void InputRegionAndRenderFenceRegression()
    {
        var window = CreateWindowEx(0, "STATIC", "ICC input region (hidden)", 0, -32000, -32000, 400, 300,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Check(window != IntPtr.Zero, "Input region test window not created");
        try
        {
            var input = new InkWindowInputRegion();
            foreach (double scale in new[] { 1.0, 1.5, 2.0 })
            {
                var canvas = new System.Windows.Rect(0, 0, 200 * scale, 150 * scale);
                var hole = new System.Windows.Rect(40 * scale, 100 * scale, 80 * scale, 40 * scale);
                input.Update(window, canvas, new[] { hole });
                var region = CreateRectRgn(0, 0, 0, 0);
                try
                {
                    Check(GetWindowRgn(window, region) != 0, "Native window region missing");
                    Check(PtInRegion(region, (int)(20 * scale), (int)(20 * scale)), "Canvas no longer accepts input");
                    Check(!PtInRegion(region, (int)(60 * scale), (int)(120 * scale)), "Toolbar still intercepts native pointer input");
                    Check(!PtInRegion(region, (int)(220 * scale), 10), "Input region leaks outside canvas");
                }
                finally { DeleteObject(region); }
            }
            input.Update(window, System.Windows.Rect.Empty, Array.Empty<System.Windows.Rect>());
            var empty = CreateRectRgn(0, 0, 0, 0);
            try
            {
                GetWindowRgn(window, empty);
                Check(!PtInRegion(empty, 20, 20), "Plugin canvas mode still intercepts input");
            }
            finally { DeleteObject(empty); }
        }
        finally { DestroyWindow(window); }

        using var fence = new WpfRenderFrameFence(System.Windows.Threading.Dispatcher.CurrentDispatcher);
        int completed = 0;
        fence.Arm(1, () => completed++);
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(completed == 1, "Wet/dry fence did not complete without rendering");
    }
}
