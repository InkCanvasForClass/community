using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using InkCanvasWordLink;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static int _checks;
    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks++; }
    internal static void Pump(int ms = 5)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    internal static T Wait<T>(Task<T> task)
    {
        long deadline = Environment.TickCount64 + 15000;
        while (!task.IsCompleted && Environment.TickCount64 < deadline) Pump();
        if (!task.IsCompleted) throw new TimeoutException("Word test deadline exceeded");
        return task.GetAwaiter().GetResult();
    }
    internal static void Wait(Task task) => Wait(Complete(task));
    private static async Task<bool> Complete(Task task) { await task; return true; }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--parent") return FakeHost();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string root = Path.Combine(Path.GetTempPath(), "ICC_WordTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            if (args.Contains("--live-only") || args.Contains("--focus-live-only"))
            {
                LiveWordTests.Run(Check, focusOnly: args.Contains("--focus-live-only"));
                Console.WriteLine($"PASS: {_checks} Microsoft Word checks");
                return 0;
            }
            GeometryTests(); StoreTests(root); SaveDestinationTests(root); LocalInkCommitTests(root); CanonicalSnapshotTests(root); MarginInkTests(root); TransientRecoveryTests(root); LiveProjectionTests(root); ProjectionTests(root); ProjectionLoadTests(); ToolHandoffTests(root); NavigationTests(root); ScrollSchedulingTests(root); HostTests();
            TouchStartupTests(root);
            ZoomTests(root);
            PlacementTests(root);
            FocusTests(root);
            if (args.Contains("--live")) LiveWordTests.Run(Check);
            if (args.Contains("--wheel-live")) LiveWordTests.Run(Check, wheelOnly: true);
            Console.WriteLine($"PASS: {_checks} Word link checks" + (args.Contains("--live") || args.Contains("--wheel-live") ? " including Microsoft Word" : ""));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Ink_Canvas.Helpers.AtomicFileWriter.Flush(TimeSpan.FromSeconds(2)); app.Shutdown(); }
    }

    private static void GeometryTests()
    {
        AdjacentPageTests();
        foreach (double scale in new[] { .5, .8, 1.0, 1.25, 1.5, 2.0, 3.0 })
        foreach (double left in new[] { -1800.0, 0.0, 550.0 })
        {
            var anchors = new[] { (70.0, 90.0), (200.0, 300.0), (400.0, 600.0) }
                .Select((p, i) => new WordAnchor(1, i, p.Item1, p.Item2, left + p.Item1 * scale, -200 + p.Item2 * scale)).ToArray();
            var bounds = WordPageGeometry.Solve(anchors, 600, 800, scale);
            Check(bounds != null && Math.Abs(bounds.X - left) < .001 && Math.Abs(bounds.Y + 200) < .001,
                "Page origin drifted across DPI, zoom or negative monitor coordinates");
            Check(Math.Abs(bounds.Width - 600 * scale) < .001 && Math.Abs(bounds.Height - 800 * scale) < .001,
                "Page scale was incorrect");
        }
        Check(WordPageGeometry.Solve(Array.Empty<WordAnchor>(), 600, 800, 1) == null, "Missing anchor accepted");
        Check(WordPageGeometry.Solve(new[] { new WordAnchor(1, 1, double.NaN, 0, 0, 0) }, 600, 800, 1) == null, "NaN anchor accepted");
        var unstable = new[]
        {
            new WordAnchor(1, 1, 297.15, 244.05, 688, 608),
            new WordAnchor(1, 2, 336.8, 88.45, 688, 348),
            new WordAnchor(1, 3, 297.15, 400.25, 688, 868)
        };
        var consensus = WordPageGeometry.Solve(unstable, 595.3, 841.9, 120.0 / 72);
        Check(consensus != null && Math.Abs(consensus.X - 192.75) < 1, "One inconsistent Word layout point prevented a majority-confirmed page origin");
        Check(WordPageGeometry.Solve(unstable.Take(2).ToArray(), 595.3, 841.9, 120.0 / 72) == null,
            "Conflicting points were guessed without a strict majority");
        var invalid = Frame(); invalid.Pages[0].Bounds.Width = double.NaN;
        Check(!invalid.IsValid(), "Untrusted frame accepted non-finite geometry");
        invalid = Frame(); invalid.Pages.Add(invalid.Pages[0]); Check(!invalid.IsValid(), "Duplicate page accepted");
        invalid = Frame(); invalid.DocumentKey = "../../private"; Check(!invalid.IsValid(), "Unsafe storage key accepted");
        var viewport = new WordRect { X = 0, Y = 0, Width = 500, Height = 300 };
        var pages = new[]
        {
            new WordPageFrame { Number = 1, Bounds = new WordRect { X = 0, Y = -99, Width = 500, Height = 100 } },
            new WordPageFrame { Number = 2, Bounds = new WordRect { X = 0, Y = 20, Width = 500, Height = 10000 } }
        };
        Check(WordPageGeometry.SelectCurrentPage(pages, viewport) == 2, "Offscreen page centres overrode the mostly visible current page");
        Check(WordPageGeometry.SelectCurrentPage(pages, viewport, 1) == 2, "Nearest text from another page was mistaken for the centre page");
        Check(WordPageGeometry.SelectCurrentPage(pages, viewport, 9) == 2, "Unknown centre page displaced a visible page");
        var spread = new[]
        {
            new WordPageFrame { Number = 3, Bounds = new WordRect { X = 0, Y = 0, Width = 240, Height = 300 } },
            new WordPageFrame { Number = 4, Bounds = new WordRect { X = 260, Y = 0, Width = 239, Height = 300 } }
        };
        Check(WordPageGeometry.SelectCurrentPage(spread, viewport, 3, 4) == 4, "Facing-page gap ignored navigation target due to rounding differences");
    }

    private static void StoreTests(string root)
    {
        var store = new WordAnnotationStore(Path.Combine(root, "store"));
        string key = new('a', 64), revision = new('b', 64);
        var state = new WordInkDocument(); state.Pages[1] = new StrokeCollection { Stroke(10, 20) }; state.Layout[1] = "layout-1";
        var saved = store.SaveAsync(key, revision, state);
        state.Pages[1][0].StylusPoints[0] = new StylusPoint(900, 900);
        Check(Wait(saved), "Atomic annotation save failed");
        var loaded = Wait(store.LoadAsync(key, revision));
        Check(Math.Abs(loaded.Pages[1][0].StylusPoints[0].X - 10) < .05 && loaded.Layout[1] == "layout-1", "Save did not isolate its snapshot (ISF quantization tolerance)");
        for (int i = 0; i < 15; i++)
        {
            state.Pages[1] = new StrokeCollection { Stroke(i, 20) };
            _ = store.SaveAsync(key, revision, state);
        }
        Check(store.Flush(TimeSpan.FromSeconds(5)), "Coalesced writer did not drain");
        Check(Math.Abs(Wait(store.LoadAsync(key, revision)).Pages[1][0].StylusPoints[0].X - 14) < .05, "Latest snapshot lost to an older save");
        string path = Path.Combine(root, "store", key, revision + ".json");
        File.WriteAllText(path, "{broken");
        bool rejected = false; try { Wait(store.LoadAsync(key, revision)); } catch { rejected = true; }
        Check(rejected && File.ReadAllText(path) == "{broken", "Corrupt annotations were silently overwritten");
    }

    private static void ProjectionTests(string root)
    {
        var canvas = new InkCanvas();
        using var source = new HwndSource(new HwndSourceParameters("Word projection fixture")
        { Width = 1200, Height = 1000, PositionX = 0, PositionY = 0, WindowStyle = unchecked((int)0x80000000) });
        source.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var desktop = Stroke(1, 1); canvas.Strokes.Add(desktop);
        var bar = new Ink_Canvas.Controls.WordLectureBar();
        var transport = new FrameSource { Frame = Frame() };
        long target = 10; bool owned = false;
        using var controller = new WordLectureController(canvas, bar, Path.Combine(root, "projection"), () => true,
            () => false, value => owned = value, action => action(), (_, _) => { }, transport, _ => target, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        Check(owned && canvas.Strokes.Count == 0 && !controller.BlocksInput, "Word did not isolate the desktop canvas");
        Check(canvas.EditingMode == InkCanvasEditingMode.Ink, "Entering Word changed the pen into selection mode");
        Point initial = canvas.PointFromScreen(new Point(120, 130));
        canvas.Strokes.Add(Stroke(initial.X, initial.Y)); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 1 && Math.Abs(canvas.Strokes[0].StylusPoints[0].X - initial.X) < .1, "Canonical roundtrip moved ink");
        Check(canvas.EditingMode == InkCanvasEditingMode.Ink, "Saving ink changed the active pen tool");
        var originalProjection = canvas.Strokes[0];
        int collectionChanges = 0;
        canvas.Strokes.StrokesChanged += (_, _) => collectionChanges++;
        transport.Frame.Pages[0].Bounds = new WordRect { X = 200, Y = 250, Width = 1000, Height = 1400 };
        Wait(controller.PollAsync());
        Point enlarged = canvas.PointFromScreen(new Point(240, 310));
        Check(canvas.EditingMode == InkCanvasEditingMode.Ink, "Scrolling changed the active pen tool");
        Check(ReferenceEquals(originalProjection, canvas.Strokes[0]) && collectionChanges == 0,
            "Scrolling/zoom rebuilt the stroke collection");
        Check(Math.Abs(canvas.Strokes[0].StylusPoints[0].X - enlarged.X) < .1
            && Math.Abs(canvas.Strokes[0].StylusPoints[0].Y - enlarged.Y) < .1,
            $"Scrolling/zoom did not follow the page: {canvas.Strokes[0].StylusPoints[0].X},{canvas.Strokes[0].StylusPoints[0].Y}, expected {enlarged}");
        Check(controller.Undo(false) && canvas.Strokes.Count == 0, "Undo failed after a viewport change");
        Check(controller.Undo(true) && canvas.Strokes.Count == 1 && Math.Abs(canvas.Strokes[0].StylusPoints[0].X - enlarged.X) < .1,
            "Redo restored stale screen coordinates");
        foreach (var mode in new[] { InkCanvasEditingMode.Ink, InkCanvasEditingMode.EraseByPoint,
            InkCanvasEditingMode.EraseByStroke, InkCanvasEditingMode.Select, InkCanvasEditingMode.None })
        {
            canvas.EditingMode = mode;
            transport.Frame.Pages[0].Bounds.Y += 10;
            Wait(controller.PollAsync());
            Check(canvas.EditingMode == mode, $"View refresh replaced tool {mode}");
            Check(canvas.IsHitTestVisible == (mode != InkCanvasEditingMode.None), $"Cursor/edit hit testing was overwritten for {mode}");
        }
        canvas.EditingMode = InkCanvasEditingMode.Ink; Wait(controller.PollAsync());
        Check(canvas.IsHitTestVisible, "Returning from cursor to pen did not restore input");
        var stableProjection = canvas.Strokes[0];
        double stableWidth = stableProjection.DrawingAttributes.Width;
        double stableY = stableProjection.StylusPoints[0].Y;
        double pageY = transport.Frame.Pages[0].Bounds.Y;
        collectionChanges = 0;
        for (int i = 0; i < 40; i++)
        {
            transport.Frame.Pages[0].Bounds.Y = pageY + (i % 2 == 0 ? 40 : 0);
            Wait(controller.PollAsync());
        }
        Check(ReferenceEquals(stableProjection, canvas.Strokes[0]) && collectionChanges == 0,
            "Continuous scrolling allocated replacement strokes or rebuilt the collection");
        Check(Math.Abs(canvas.Strokes[0].StylusPoints[0].Y - stableY) < .001
            && canvas.Strokes[0].DrawingAttributes.Width == stableWidth, "Repeated scrolls accumulated geometry or pen-width drift");
        int callsBefore = transport.Calls;
        transport.BeforeReply = async () =>
        {
            for (int i = 0; i < 1000; i++) controller.NotifyViewChanged();
            await Task.Delay(140);
        };
        transport.Frame.Pages[0].Bounds.Y += 80;
        Wait(controller.PollAsync());
        Check(controller.BlocksInput && Math.Abs(canvas.Strokes[0].StylusPoints[0].Y - stableY) < .001,
            "A slow obsolete scroll result was applied or accepted ink");
        Check(transport.Calls == callsBefore + 1, $"Burst view events queued redundant helper requests: before={callsBefore}, after={transport.Calls}");
        Wait(controller.PollAsync());
        Check(!controller.BlocksInput, "Continuous slow view changes starved every display frame");
        transport.BeforeReply = null; Wait(controller.PollAsync());
        Check(!controller.BlocksInput && canvas.IsHitTestVisible, "Latest scroll result did not restore annotation input");
        transport.Frame = Frame(new string('c', 64)); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 0, "Document switch leaked another document's ink");
        transport.Frame = Frame(); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 1, "Document switch failed to restore its annotations");
        transport.Frame.SessionKey = new string('f', 64); Wait(controller.PollAsync());
        transport.Frame.DocumentKey = new string('e', 64); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 1, "Save As lost annotations from the same live document");
        canvas.Strokes.Clear(); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 0, "Erasing did not update canonical page ink");
        controller.Undo(false);
        Check(canvas.Strokes.Count == 1, "Undo did not restore erased Word annotations");
        transport.Frame.Revision = new string('d', 64); Wait(controller.PollAsync());
        Check(controller.BlocksInput && canvas.Strokes.Count == 0 && bar.Status == "LayoutChanged", "Edited document continued with misaligned ink");
        bar.Continue(); Wait(controller.PollAsync());
        Check(!controller.BlocksInput && canvas.Strokes.Count == 0, "Layout confirmation did not begin a separate annotation revision");
        target = 0; Wait(controller.PollAsync());
        Check(!owned && canvas.Strokes.Count == 1 && ReferenceEquals(canvas.Strokes[0], desktop), "Leaving Word lost the desktop ink identity");
    }

    private static void HostTests()
    {
        using (var budgets = new WordHostClient())
            Check(budgets.RequestTimeout(true, true) == TimeSpan.FromSeconds(8)
                && budgets.RequestTimeout(false, false) == TimeSpan.FromSeconds(8)
                && budgets.RequestTimeout(false, true) == TimeSpan.FromSeconds(3),
                "Cold COM binding or revision scanning has the same short deadline as geometry queries");
        Check(WordHostClient.FailureDelay(1) == 500 && WordHostClient.FailureDelay(2) == 1000
            && WordHostClient.FailureDelay(30) == 4000, "Recovery backoff is slow on the first failure or unbounded");
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "cold-request");
        using (var cold = new WordHostClient(Environment.ProcessPath))
            Check(Wait(cold.ExchangeAsync(new WordRequest { DeferContentCheck = true }, CancellationToken.None))?.Status == "Waiting",
                "A slow first Word COM request repeatedly restarted the helper");
        using (var missing = new WordHostClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")))
        {
            Check(Wait(missing.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "HelperMissing",
                "Missing helper was reported as a generic Word connection failure");
            Check(Wait(missing.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "HelperMissing",
                "Failure backoff hid the missing-helper diagnosis");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "legacy");
        using (var legacy = new WordHostClient(Environment.ProcessPath))
            Check(Wait(legacy.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "HelperMismatch",
                "An old helper binary was not diagnosed by the startup handshake");
        Check(InkCanvasWordLinkHost.OfficeMessageFilter.RetryDelay(0, 2) == 100
            && InkCanvasWordLinkHost.OfficeMessageFilter.RetryDelay(1199, 2) == 100
            && InkCanvasWordLinkHost.OfficeMessageFilter.RetryDelay(1200, 2) == -1
            && InkCanvasWordLinkHost.OfficeMessageFilter.RetryDelay(0, 1) == -1,
            "Office busy retry is unbounded or retries a permanent rejection");
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "valid");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromSeconds(2)))
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting", "Real helper IPC failed");
        using (var warm = new WordHostClient(Environment.ProcessPath))
        {
            Wait(warm.WarmAsync(CancellationToken.None));
            warm.Suspend();
            Check(Wait(warm.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting" && warm.Starts == 1,
                "Leaving Word restarted the warmed helper or raced document release");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "large");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromSeconds(2)))
        {
            for (int i = 0; i < 2; i++)
                Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.DocumentName == new string('文', 900),
                    "Chunked IPC lost Unicode, message boundaries or subsequent replies");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "oversize");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromSeconds(2)))
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Unavailable",
                "Chunked IPC accepted an oversized response");
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "boundary");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromMilliseconds(800)))
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting",
                "A complete response at the pipe buffer boundary was mistaken for a hung Word connection");
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "hang");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromMilliseconds(400)))
        {
            var watch = Stopwatch.StartNew();
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Timeout", "Hung helper was not timed out");
            Check(watch.Elapsed < TimeSpan.FromSeconds(3), "Hung helper blocked the caller");
            Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "valid");
            Pump(550);
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting",
                "A single timeout still imposed five seconds of dead time before recovery");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "slow-start");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromMilliseconds(250)))
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting",
                "Slow helper startup consumed the Word call deadline");
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "busy");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromSeconds(2)))
        {
            var busy = Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None));
            var recovered = Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None));
            Check(busy.Status == "Busy" && recovered.Status == "Waiting" && busy.DocumentName == recovered.DocumentName,
                "A temporarily busy Word caused helper restart/backoff rather than recovery on the same connection");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "hang");
        using (var client = new WordHostClient(Environment.ProcessPath, TimeSpan.FromSeconds(2)))
        {
            var pending = client.ExchangeAsync(new WordRequest(), CancellationToken.None);
            long deadline = Environment.TickCount64 + 2000;
            while (client.LastError != "fake-request-start" && Environment.TickCount64 < deadline) Pump();
            Check(client.LastError == "fake-request-start", "Pending-stop fixture did not enter its request");
            client.Stop();
            Check(Wait(pending) == null, "Explicit stop was reported as a connection error");
            Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", "valid");
            Check(Wait(client.ExchangeAsync(new WordRequest(), CancellationToken.None))?.Status == "Waiting",
                "An old stopped request imposed failure backoff on a newly enabled connection");
        }
        Environment.SetEnvironmentVariable("ICC_WORD_FAKE_MODE", null);
    }
    private static void ProjectionLoadTests()
    {
        var canvas = new InkCanvas();
        using var source = new HwndSource(new HwndSourceParameters("Dense Word ink fixture") { Width = 1200, Height = 1000 });
        source.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var document = new WordInkDocument();
        document.Pages[1] = new StrokeCollection(Enumerable.Range(0, 300).Select(i => new Stroke(new StylusPointCollection(
            Enumerable.Range(0, 80).Select(j => new StylusPoint(50 + j * 2, 50 + i % 250 + j * .1))))));
        var projection = new WordInkProjection(canvas);
        var frame = Frame(); projection.Show(frame, document);
        var references = canvas.Strokes.ToArray();
        double canonicalWidth = document.Pages[1][0].DrawingAttributes.Width;
        double projectedWidth = references[0].DrawingAttributes.Width;
        int changes = 0; canvas.Strokes.StrokesChanged += (_, _) => changes++;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            frame.Pages[0].Bounds.Y = 100 + (i % 2 == 0 ? 80 : 0);
            projection.Show(frame, document);
        }
        Console.WriteLine($"Dense projection: 300 strokes / 24,000 points, 20 scroll updates={watch.Elapsed.TotalMilliseconds:F1}ms");
        Check(references.SequenceEqual(canvas.Strokes) && changes == 0, "Dense scrolling replaced stroke objects or churned the collection");
        Check(document.Pages[1][0].StylusPoints[0].X == 50 && document.Pages[1][0].DrawingAttributes.Width == canonicalWidth,
            "View projection changed canonical ink");
        for (int i = 0; i < 6; i++)
        {
            frame.Pages[0].Bounds.Width = i % 2 == 0 ? 1000 : 500;
            frame.Pages[0].Bounds.Height = i % 2 == 0 ? 1400 : 700;
            projection.Show(frame, document);
        }
        Check(references.SequenceEqual(canvas.Strokes) && changes == 0, "Repeated zooms replaced existing strokes");
        Check(Math.Abs(canvas.Strokes[0].DrawingAttributes.Width - projectedWidth) < .001,
            "Repeated zooms accumulated pen-width drift");
        projection.Reset();
    }
    private static void ToolHandoffTests(string root)
    {
        var canvas = new InkCanvas { EditingMode = InkCanvasEditingMode.None, IsHitTestVisible = false };
        var bar = new Ink_Canvas.Controls.WordLectureBar();
        bool annotation = false;
        long target = 10;
        using var source = new HwndSource(new HwndSourceParameters("Word tool handoff fixture") { Width = 1200, Height = 1000 });
        source.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        using var controller = new WordLectureController(canvas, bar, Path.Combine(root, "handoff"), () => true,
            () => false, _ => { }, action => action(), (_, _) => { }, new FrameSource { Frame = Frame() }, _ => target,
            annotationEnabled: () => annotation, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        Check(canvas.EditingMode == InkCanvasEditingMode.None && !canvas.IsHitTestVisible, "Entering Word consumed cursor input");
        annotation = true; canvas.EditingMode = InkCanvasEditingMode.Ink; Wait(controller.PollAsync());
        target = 0; Wait(controller.PollAsync());
        Check(canvas.EditingMode == InkCanvasEditingMode.Ink && canvas.IsHitTestVisible, "Leaving Word restored a stale cursor state over the pen");
        target = 10; Wait(controller.PollAsync());
        annotation = false; canvas.EditingMode = InkCanvasEditingMode.None; Wait(controller.PollAsync());
        target = 0; Wait(controller.PollAsync());
        Check(canvas.EditingMode == InkCanvasEditingMode.None && !canvas.IsHitTestVisible, "Leaving Word restored stale pen interception over the cursor");
    }
    private static void NavigationTests(string root)
    {
        var canvas = new InkCanvas();
        using var source = new HwndSource(new HwndSourceParameters("Word rapid navigation fixture") { Width = 1200, Height = 1000 });
        source.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var bar = new Ink_Canvas.Controls.WordLectureBar();
        var transport = new FrameSource { Frame = Frame() }; transport.Frame.PageCount = 10;
        using var controller = new WordLectureController(canvas, bar, Path.Combine(root, "navigation"), () => true,
            () => false, _ => { }, action => action(), (_, _) => { }, transport, _ => 10, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        bar.Go(1);
        transport.BeforeReply = async () => { bar.Go(1); bar.Go(1); await Task.Delay(25); };
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetPage == 2 && controller.BlocksInput, "Superseded navigation reply was applied");
        transport.BeforeReply = null;
        transport.Frame.CurrentPage = 4; transport.Frame.Pages[0].Number = 4;
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetPage == 4 && !controller.BlocksInput, "Rapid next clicks were lost or based on a stale current page");
        bar.Go(-1); transport.Frame.CurrentPage = 3; transport.Frame.Pages[0].Number = 3;
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetPage == 3, "Previous navigation did not start from the confirmed current page");
    }
    private static void ScrollSchedulingTests(string root)
    {
        var gate = new WordRefreshGate();
        var fast = new WordRefreshGate();
        fast.Scrolled(0); fast.Complete(4, 4, true);
        Check(fast.Delay(4) == 12, "Cheap view queries remained capped by a 50ms idle gap");
        fast.Scrolled(20); fast.Complete(24, 25, true);
        Check(fast.Delay(50) > 0, "Slow Office calls lost their input/rendering budget");
        gate.Complete(0, 10, false);
        Check(gate.Delay(500) > 0, "Unchanged Office notifications bypassed idle cooldown");
        gate.Scrolled(10); gate.Scrolled(50);
        Check(gate.Delay(100) == 0 && gate.IsScrolling(100), "Continuous input postponed every view query until scrolling stopped");
        gate.Scrolled(180); gate.Complete(200, 25, true);
        gate.Scrolled(240);
        Check(gate.Delay(249) > 0 && gate.Delay(251) == 0, "Live scroll queries did not leave Word time to process input");
        gate.Scrolled(251); gate.Complete(252, 1, true);
        Check(gate.Delay(275) > 0, "One fast reply discarded the recent slow-call budget");
        gate.Complete(600, 200, true);
        Check(gate.Delay(800) > 0, "Slow Word calls immediately started another slow call");
        var slowScroll = new WordRefreshGate();
        slowScroll.Scrolled(1000); slowScroll.Complete(1010, 500, true);
        Check(slowScroll.Delay(1119) == 1 && slowScroll.Delay(1120) == 0,
            "The post-scroll confirmation waited for the three-second motion cooldown");
        slowScroll.Scrolled(1100);
        Check(slowScroll.Delay(1120) > 0 && slowScroll.Delay(1220) == 0,
            "Continuing input did not extend the settle deadline");

        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word scroll scheduling fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var transport = new FrameSource { Frame = Frame() };
        bool paused = false, contact = false;
        uint inputStamp = 1;
        using var controller = new WordLectureController(canvas, new Ink_Canvas.Controls.WordLectureBar(), Path.Combine(root, "scroll-gate"),
            () => true, () => contact, _ => { }, action => action(), (_, _) => { }, transport, _ => 10,
            pausePresentation: value => paused = value, inputStamp: () => inputStamp);
        controller.Configure(true, true); Wait(controller.PollAsync());
        int before = transport.Calls;
        var inputWatch = Stopwatch.StartNew();
        for (int i = 0; i < 12; i++) { controller.NotifyScrolled(); Pump(15); }
        Check(transport.Calls > before && transport.Calls - before <= inputWatch.ElapsedMilliseconds / 16 + 2,
            "Continuous scrolling starved view updates or exceeded the display cadence budget");
        Check(!paused && controller.BlocksInput && !canvas.IsHitTestVisible, "Scrolling hid the ink or accepted misplaced input");
        contact = true;
        long deadline = Environment.TickCount64 + 2000;
        while (controller.BlocksInput && Environment.TickCount64 < deadline) Pump();
        Check(!paused && !controller.BlocksInput && canvas.IsHitTestVisible,
            "Post-scroll sync did not restore editable coordinates");
        contact = false;
        before = transport.Calls;
        for (int i = 0; i < 1000; i++) controller.NotifyViewChanged();
        Pump(50);
        Check(transport.Calls == before, "Office event feedback bypassed the query cooldown");
        Pump(850); // Consume the coalesced view notification above.
        before = transport.Calls;
        Pump(5500);
        Check(transport.Calls == before, "Idle ownership polling repeatedly entered Word automation");
        inputStamp++;
        Pump(850);
        Check(transport.Calls > before, "Input without a native view event did not trigger fallback verification");
        before = transport.Calls;
        inputStamp++;
        controller.NotifyViewChanged(); Pump(100);
        Check(transport.Calls > before, "Native Word input remained behind the idle query cooldown");
        before = transport.Calls;
        controller.NotifyScrolled();
        Pump(100);
        Check(transport.Calls > before, "Idle query suppression prevented a new scroll from waking Word tracking");
    }
    private static int FakeHost()
    {
        int calls = 0;
        string mode = Environment.GetEnvironmentVariable("ICC_WORD_FAKE_MODE");
        if (mode == "slow-start") Thread.Sleep(600);
        while (Console.ReadLine() is string line)
        {
            if (JsonSerializer.Deserialize<WordRequest>(line)?.ReleaseDocument == true)
            { Console.WriteLine(JsonSerializer.Serialize(new WordFrame { Status = "Released" })); Console.Out.Flush(); continue; }
            if (JsonSerializer.Deserialize<WordRequest>(line)?.Handshake == true)
            { Console.WriteLine(JsonSerializer.Serialize(new WordFrame { Status = mode == "legacy" ? "Waiting" : "HostReady" })); Console.Out.Flush(); continue; }
            calls++;
            if (mode == "cold-request" && calls == 1) Thread.Sleep(3400);
            if (mode == "hang") { Console.Error.WriteLine("fake-request-start"); Console.Error.Flush(); Thread.Sleep(10000); }
            if (mode == "boundary")
            {
                string json = JsonSerializer.Serialize(new WordFrame());
                Console.Write(json.PadRight(1023) + "\n");
            }
            else if (mode == "oversize") Console.WriteLine(new string('x', 132000));
            else Console.WriteLine(JsonSerializer.Serialize(new WordFrame { Status = mode == "busy" && calls == 1 ? "Busy" : "Waiting",
                DocumentName = mode == "large" ? new string('文', 900) : mode == "busy" ? Environment.ProcessId.ToString() : "" }));
            Console.Out.Flush();
        }
        return 0;
    }
    internal static WordFrame Frame(string key = null) => new()
    {
        Status = "Ready", DocumentKey = key ?? new string('a', 64), Revision = new string('b', 64), DocumentName = "Fixture.docx",
        WindowHandle = 10, PageCount = 2, CurrentPage = 1,
        Viewport = new WordRect { X = 0, Y = 0, Width = 1200, Height = 1000 },
        Pages = new() { new WordPageFrame { Number = 1, Start = 0, End = 100, WidthPoints = 500, HeightPoints = 700,
            Bounds = new WordRect { X = 100, Y = 100, Width = 500, Height = 700 } } }
    };
    internal static Stroke Stroke(double x, double y) => new(new StylusPointCollection
    { new StylusPoint(x, y, .5f), new StylusPoint(x + 20, y + 20, .7f) });
    private sealed class FrameSource : IWordFrameSource
    {
        internal WordFrame Frame;
        internal Func<Task> BeforeReply;
        internal int Calls;
        internal WordRequest LastRequest;
        public async Task<WordFrame> ExchangeAsync(WordRequest request, CancellationToken token)
        {
            Calls++;
            LastRequest = request;
            var snapshot = JsonSerializer.Deserialize<WordFrame>(JsonSerializer.Serialize(Frame));
            if (BeforeReply != null) await BeforeReply();
            return snapshot;
        }
        public void Stop() { }
        public void Dispose() { }
    }
}
