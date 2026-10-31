using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using InkCanvasWordLink;
using InkCanvasWordLinkHost;
using Ink_Canvas.WordLink;

internal static class LiveWordTests
{
    internal static void Run(Action<bool, string> check, bool wheelOnly = false, bool focusOnly = false)
    {
        var type = Type.GetTypeFromProgID("Word.Application") ?? throw new InvalidOperationException("Microsoft Word is not registered");
        var existing = Process.GetProcessesByName("WINWORD").Select(p => { int id = p.Id; p.Dispose(); return id; }).ToHashSet();
        dynamic app = null, documents = null, document = null, window = null, range = null;
        IntPtr hwnd = IntPtr.Zero;
        bool ownsProcess = false;
        try
        {
            app = Activator.CreateInstance(type);
            var created = Process.GetProcessesByName("WINWORD").Where(p => !existing.Contains(p.Id)).ToArray();
            ownsProcess = created.Length == 1;
            foreach (var candidate in created) candidate.Dispose();
            check(ownsProcess, "Fixture did not receive an isolated Word process; existing documents will not be touched");
            app.AutomationSecurity = 3;
            app.DisplayAlerts = 0;
            documents = app.Documents;
            document = documents.Add();
            range = document.Content;
            range.Text = string.Join("\r", Enumerable.Range(1, 160).Select(i => $"ICC Word lecture fixture line {i}: coordinates and page tracking."));
            window = document.ActiveWindow;
            window.WindowState = 0;
            window.Left = 20; window.Top = 20; window.Width = 800; window.Height = 600;
            hwnd = new IntPtr((int)window.Hwnd);
            GetWindowThreadProcessId(hwnd, out uint pid);
            check(!existing.Contains((int)pid), "Fixture document belonged to an existing Word process");
            dynamic view = window.View;
            view.Type = 3;
            dynamic zoom = view.Zoom; zoom.Percentage = 100;
            Marshal.ReleaseComObject(zoom); Marshal.ReleaseComObject(view);
            app.Visible = true;
            app.ScreenRefresh();
            check(WordWindowSelector.IsWordWindow(hwnd) && !WordWindowSelector.IsWordWindow(IntPtr.Zero), "Word window validation accepted a stale handle or rejected the real document");
            int cloak = 1; DwmSetWindowAttribute(hwnd, 13, ref cloak, 4);
            document.Repaginate();
            using var observer = new WordObserver();
            WordFrame frame = null;
            for (int i = 0; i < 4; i++) { frame = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64() }); if (frame.Status == "Ready") break; Thread.Sleep(250); }
            Console.WriteLine($"Live Word: {frame.Status}, pages={frame.PageCount}, visible={frame.Pages.Count}");
            if (frame.Status != "Ready") Console.WriteLine(observer.LastDiagnostic);
            check(frame.Status == "Ready" && frame.IsValid() && frame.PageCount >= 2, "Live Word page discovery failed");
            if (focusOnly)
            {
                int focusVisible = 0; DwmSetWindowAttribute(hwnd, 13, ref focusVisible, 4);
                FocusModeTests((object)app, (object)window, observer, check);
                return;
            }
            string revision = frame.Revision;
            WordFrame ReadZoom(WordRequest request)
            {
                long until = Environment.TickCount64 + 3000;
                WordFrame result;
                do
                {
                    result = observer.Read(request);
                    if (result.Status is not ("Busy" or "Locating")) return result;
                    Program.Pump(40);
                } while (Environment.TickCount64 < until);
                return result;
            }
            var zoomed = ReadZoom(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                TargetZoomPercentage = 120, VerifyCachedView = true, DeferContentCheck = true });
            check(zoomed.Status == "Ready" && zoomed.ZoomPercentage == 120 && zoomed.Revision == revision
                && zoomed.Pages.Any(p => frame.Pages.Any(old => old.Number == p.Number
                    && Math.Abs(p.Bounds.Width / old.Bounds.Width - 1.2) < .01)),
                $"Word zoom did not update geometry while retaining revision: status={zoomed.Status}, zoom={zoomed.ZoomPercentage}, same revision={zoomed.Revision == revision}");
            var wrongDocumentZoom = ReadZoom(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = new string('c', 64),
                TargetZoomPercentage = 150, DeferContentCheck = true });
            check(wrongDocumentZoom.Status == "Ready" && wrongDocumentZoom.ZoomPercentage == 120,
                "A stale document identity changed another document's zoom");
            var restoredZoom = ReadZoom(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                TargetZoomPercentage = 100, DeferContentCheck = true });
            check(restoredZoom.Status == "Ready" && restoredZoom.ZoomPercentage == 100 && restoredZoom.Revision == revision,
                "Word zoom could not return to the original scale");
            Console.WriteLine("Real Word zoom: 100% → 120% → 100%; geometry, revision and stale-document guard verified");
            var next = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey, TargetPage = 2 });
            check(next.Status == "Ready" && next.CurrentPage == 2 && next.Pages.Any(p => p.Number == 2), "Live Word navigation did not report page 2");
            if (wheelOnly)
            {
                int wheelVisible = 0; DwmSetWindowAttribute(hwnd, 13, ref wheelVisible, 4);
                app.ScreenRefresh(); Program.Pump(100);
                WheelDeliveryTests((object)document, (object)window, hwnd, check);
                return;
            }
            int boundaryReads = observer.PageBoundaryReads;
            var timings = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var watch = Stopwatch.StartNew();
                var steady = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64() });
                timings.Add(watch.Elapsed.TotalMilliseconds);
                check(steady.Status == "Ready" && steady.Revision == revision, "Cached view query lost the current page");
            }
            check(observer.PageBoundaryReads == boundaryReads, "Steady view queries repeated expensive page boundary discovery");
            double beforeScroll = next.Pages.Single(p => p.Number == 2).Bounds.Y;
            int viewEvents = 0;
            using var watcher = new WordViewChangeWatcher(() => viewEvents++);
            watcher.Observe(hwnd.ToInt64());
            window.SmallScroll(Down: 2);
            Program.Pump(80);
            var scrollWatch = Stopwatch.StartNew();
            var scrolled = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64() });
            scrollWatch.Stop();
            check(scrolled.Status == "Ready" && scrolled.Pages.Any(p => p.Number == 2 && p.Bounds.Y < beforeScroll - 1),
                "Small scroll did not update cached page coordinates immediately");
            check(viewEvents > 0, "Real Word scrolling did not wake the native view listener");
            int previewReads = observer.PreviewReads, contentReads = observer.ContentReads, samples = observer.DiscoverySamples;
            window.SmallScroll(Down: 1); Program.Pump(40);
            var previewWatch = Stopwatch.StartNew();
            var preview = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                ScrollPreview = true, DeferContentCheck = true });
            previewWatch.Stop();
            check(preview.Status == "Ready" && observer.PreviewReads == previewReads + 1
                && observer.ContentReads == contentReads && observer.DiscoverySamples == samples,
                "Warm scroll preview repeated full document discovery or content scanning");
            var verified = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), DeferContentCheck = true });
            check(verified.Status == "Ready" && preview.Pages.All(p => verified.Pages.Any(v => v.Number == p.Number
                && Math.Abs(v.Bounds.X - p.Bounds.X) < 2 && Math.Abs(v.Bounds.Y - p.Bounds.Y) < 2)),
                "Lightweight scroll geometry disagreed with a full verified view");
            samples = observer.DiscoverySamples; contentReads = observer.ContentReads;
            var settled = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                VerifyCachedView = true, DeferContentCheck = true });
            check(settled.Status == "Ready" && observer.DiscoverySamples == samples && observer.ContentReads == contentReads
                && settled.Pages.All(p => verified.Pages.Any(v => v.Number == p.Number && Math.Abs(v.Bounds.Y - p.Bounds.Y) < 2)),
                "Settling repeated a full page search or failed to verify cached anchors");
            Console.WriteLine($"Word lightweight scroll preview={previewWatch.Elapsed.TotalMilliseconds:F1}ms; no content/discovery reads");
            var previewTimes = new List<double>(); var fullTimes = new List<double>();
            for (int i = 0; i < 8; i++)
            {
                var measured = Stopwatch.StartNew();
                var fast = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                    ScrollPreview = true, DeferContentCheck = true });
                previewTimes.Add(measured.Elapsed.TotalMilliseconds); measured.Restart();
                var full = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), DeferContentCheck = true });
                fullTimes.Add(measured.Elapsed.TotalMilliseconds);
                check(fast.Status == "Ready" && full.Status == "Ready", "Repeated warm preview lost page geometry");
            }
            Console.WriteLine($"Warm geometry median: lightweight={previewTimes.Order().ElementAt(4):F1}ms, full={fullTimes.Order().ElementAt(4):F1}ms");
            check(observer.NativeAttachments == 1, "Repeated geometry queries recreated the Word native automation binding");
            observer.ResetConnection();
            var reconnected = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64() });
            check(reconnected.Status == "Ready" && reconnected.DocumentKey == frame.DocumentKey
                && observer.NativeAttachments == 2, "Released Word binding could not reconnect to the same document");
            watcher.Dispose(); int previousEvents = viewEvents;
            window.SmallScroll(Down: 1); Program.Pump(40);
            check(viewEvents == previousEvents, "Disposed view listener continued delivering callbacks");
            Console.WriteLine($"Word query ms: median={timings.Order().ElementAt(2):F1}, scroll={scrollWatch.Elapsed.TotalMilliseconds:F1}; page boundary reads={observer.PageBoundaryReads}");
            view = window.View; zoom = view.Zoom; zoom.Percentage = 125;
            Marshal.ReleaseComObject(zoom); Marshal.ReleaseComObject(view);
            Thread.Sleep(250);
            var enlarged = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64() });
            check(enlarged.Status == "Ready" && enlarged.Revision == revision, "Zoom changed the document revision or lost page positioning");
            // Cross-page movement must update current page immediately, even when
            // an anchor from the preceding page survives inside the viewport.
            view = window.View; zoom = view.Zoom; zoom.Percentage = 60;
            Marshal.ReleaseComObject(zoom); Marshal.ReleaseComObject(view);
            var elapsed = Stopwatch.StartNew();
            foreach (int page in new[] { 3, 1, 4, 2 })
            {
                var moved = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                    TargetPage = page, DeferContentCheck = true });
                if (moved.CurrentPage != page) Console.WriteLine($"Page mismatch: wanted={page}, viewport={System.Text.Json.JsonSerializer.Serialize(moved.Viewport)}, pages={System.Text.Json.JsonSerializer.Serialize(moved.Pages)}");
                check(moved.Status == "Ready" && moved.CurrentPage == page, $"Rapid real Word navigation reported {moved.CurrentPage} instead of {page}");
            }
            Console.WriteLine($"Real Word: four rapid page transitions={elapsed.Elapsed.TotalMilliseconds:F1}ms");
            view = window.View; zoom = view.Zoom; zoom.Percentage = 100;
            Marshal.ReleaseComObject(zoom); Marshal.ReleaseComObject(view);
            var boundaryStart = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                TargetPage = 2, DeferContentCheck = true });
            window.SmallScroll(Up: 8); Program.Pump(80);
            var boundary = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), DeferContentCheck = true });
            check(boundary.Status == "Ready" && boundary.Pages.Any(p => p.Number == 1) && boundary.Pages.Any(p => p.Number == 2),
                "Real Word page boundary did not expose both page 1 and page 2");
            var boundaryPreview = observer.Read(new WordRequest { WindowHandle = hwnd.ToInt64(), ExpectedDocumentKey = frame.DocumentKey,
                VerifyCachedView = true, DeferContentCheck = true });
            check(boundaryPreview.Status == "Ready" && boundaryPreview.Pages.Any(p => p.Number == 1)
                && boundaryPreview.Pages.Any(p => p.Number == 2), "Settling at the real Word boundary dropped a visible page");
            int visible = 0; DwmSetWindowAttribute(hwnd, 13, ref visible, 4);
            app.ScreenRefresh(); Program.Pump(100);
            ControllerResponseTests((object)document, (object)window, hwnd, check);
        }
        finally
        {
            // Only this test-created document is closed. Never terminate an existing
            // Word process; if another document appeared, leave the application alive.
            if (ownsProcess)
            {
                try { document?.Close(0); } catch { }
                try { if (documents != null && (int)documents.Count == 0) app.Quit(0); else { int cloak = 0; DwmSetWindowAttribute(hwnd, 13, ref cloak, 4); } } catch { }
            }
            foreach (object value in new object[] { range, window, document, documents, app })
                if (value != null && Marshal.IsComObject(value)) try { Marshal.ReleaseComObject(value); } catch { }
        }
    }
    private static void FocusModeTests(object applicationObject, object windowObject, WordObserver observer, Action<bool, string> check)
    {
        dynamic app = applicationObject, window = windowObject;
        dynamic commands = app.CommandBars;
        bool initiallyFocused = commands.GetPressedMso("ViewFocusModeView");
        var initial = observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd, DeferContentCheck = true });
        WordFrame ReadFocus(bool expected, bool command = false)
        {
            long deadline = Environment.TickCount64 + 5000; WordFrame current;
            do
            {
                Program.Pump(60);
                current = observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd, DeferContentCheck = true,
                    ExpectedDocumentKey = initial.DocumentKey, TargetFocusMode = command ? expected : null });
                if (current.Status == "Ready" && current.IsFocusMode == expected) return current;
            } while (Environment.TickCount64 < deadline);
            return current;
        }
        try
        {
            check(!initiallyFocused, "Focus fixture unexpectedly inherited a user's document mode");
            var focused = ReadFocus(true, command: true);
            check(focused.Status == "Ready" && focused.IsFocusMode, "Actual Word Focus was not detected");
            check(ReadFocus(true, command: true).IsFocusMode, "Retrying Enter Focus toggled Word back out of Focus");
            observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd, ExpectedDocumentKey = new string('c', 64),
                TargetFocusMode = false, DeferContentCheck = true });
            check((bool)commands.GetPressedMso("ViewFocusModeView"), "A stale document command exited another document's Focus mode");
            int reads = observer.FocusReads;
            for (int i = 0; i < 5; i++)
                check(observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd, ExpectedDocumentKey = focused.DocumentKey,
                    ScrollPreview = true, DeferContentCheck = true }).IsFocusMode, "Warm tracking lost Word Focus");
            check(observer.FocusReads == reads, "Ordinary tracking repeatedly queried Office UI state");
            int warmProbes = observer.FocusReads - reads;
            var normal = ReadFocus(false, command: true);
            check(normal.Status == "Ready" && !normal.IsFocusMode && normal.Revision == focused.Revision,
                "Leaving actual Word Focus failed to restore mode without altering document identity");
            Console.WriteLine($"Real Word Focus: enter/exit commands, retry and document guard verified; UI probes during five warm tracking queries={warmProbes}");
        }
        finally
        {
            try { if ((bool)commands.GetPressedMso("ViewFocusModeView") != initiallyFocused) commands.ExecuteMso("ViewFocusModeView"); } catch { }
            Marshal.ReleaseComObject(commands);
        }
    }

    private static void ControllerResponseTests(object documentObject, object windowObject, IntPtr hwnd, Action<bool, string> check)
    {
        Environment.SetEnvironmentVariable("ICC_WORD_LINK_TRACE", "1");
        dynamic document = documentObject, window = windowObject;
        var canvas = new Ink_Canvas.Controls.HighlighterInkCanvas();
        using var surface = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Word end-to-end fixture")
            { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new System.Windows.Size(1200, 1000));
        canvas.Arrange(new System.Windows.Rect(0, 0, 1200, 1000));
        var bar = new Ink_Canvas.Controls.WordLectureBar();
        string helper = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(WordObserver).Assembly.Location), "InkCanvas.WordLinkHost.exe");
        using var host = new WordHostClient(helper);
        Program.Wait(host.WarmAsync(CancellationToken.None));
        var countedHost = new CountedSource(host);
        int previewFrames = 0, hiddenFrames = 0;
        uint inputStamp = 1;
        for (int connection = 0; connection < 3; connection++)
        {
            using var coldHost = new WordHostClient(helper);
            WordFrame snapshot = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = Program.Wait(coldHost.ExchangeAsync(new WordRequest { WindowHandle = hwnd.ToInt64() }, CancellationToken.None));
                if (snapshot?.Status == "Ready") break;
                Program.Pump(100);
            }
            check(snapshot?.Status == "Ready", $"Cold Word connection {connection + 1} failed: {snapshot?.Status}; {coldHost.LastError}");
            if (snapshot.NeedsRefinement)
            {
                var refinement = Stopwatch.StartNew();
                snapshot = Program.Wait(coldHost.ExchangeAsync(new WordRequest { WindowHandle = hwnd.ToInt64(),
                    ExpectedDocumentKey = snapshot.DocumentKey, VerifyCachedView = true, DeferContentCheck = true }, CancellationToken.None));
                check(snapshot.Status == "Ready" && !snapshot.NeedsRefinement, "Cold view never completed its second-stage calibration");
                Console.WriteLine($"Cold connection {connection + 1} edge refinement: {refinement.ElapsedMilliseconds}ms");
            }
            var warmWatch = Stopwatch.StartNew();
            var warm = Program.Wait(coldHost.ExchangeAsync(new WordRequest { WindowHandle = hwnd.ToInt64(),
                ExpectedDocumentKey = snapshot.DocumentKey, VerifyCachedView = true, DeferContentCheck = true }, CancellationToken.None));
            check(warm.Status == "Ready" && warmWatch.ElapsedMilliseconds < 500,
                $"Warm helper view verification exceeded its 500ms budget: {warmWatch.ElapsedMilliseconds}ms, {warm.Status}");
        }
        using var controller = new WordLectureController(canvas, bar,
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ICC_WordLatency_" + Guid.NewGuid().ToString("N")),
            () => true, () => false, _ => { }, action => action(), (_, _) => { },
            countedHost, _ => hwnd.ToInt64(), inputStamp: () => inputStamp,
            pausePresentation: paused => { if (paused) hiddenFrames++; canvas.PausePresentation(paused); },
            previewTransform: matrix => { if (matrix.HasValue) previewFrames++; canvas.SetPresentationTransform(matrix); });
        var activationWatch = Stopwatch.StartNew();
        controller.Configure(true, true);
        long deadline = Environment.TickCount64 + 8000;
        while (bar.Status != "Ready" && Environment.TickCount64 < deadline) Program.Pump();
        check(bar.Status == "Ready", $"Real helper/controller did not attach to Word: {bar.Status}; {host.LastError}");
        Console.WriteLine($"Prepared helper → first calibrated Word frame: {activationWatch.ElapsedMilliseconds}ms");
        while (controller.BlocksInput && Environment.TickCount64 < deadline) Program.Pump();
        check(!controller.BlocksInput, "Initial Word page refinement did not finish");
        check(host.Starts == 1, "Controller discarded the prepared helper during initial attachment");
        var timings = new List<double>();
        foreach (int page in new[] { 1, 3, 2 })
        {
            dynamic target = document.GoTo(What: 1, Which: 1, Count: page);
            var watch = Stopwatch.StartNew();
            try { window.ScrollIntoView(target, true); }
            finally { Marshal.ReleaseComObject(target); }
            deadline = Environment.TickCount64 + 8000;
            while ((bar.Status != "Ready" || bar.Page != page) && Environment.TickCount64 < deadline) Program.Pump();
            watch.Stop();
            check(bar.Status == "Ready" && bar.Page == page, $"Word/controller reported {bar.Page} instead of visible page {page}");
            timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine("Word scroll → helper IPC → controller page update ms: " + string.Join(", ", timings.Select(t => t.ToString("F1"))));
        var visible = countedHost.LastFrame;
        var inkPoint = canvas.PointFromScreen(new System.Windows.Point(visible.Viewport.X + visible.Viewport.Width / 2,
            visible.Viewport.Y + visible.Viewport.Height / 2));
        canvas.Strokes.Add(Program.Stroke(inkPoint.X, inkPoint.Y)); Program.Pump();
        int previewsBefore = previewFrames, hidesBefore = hiddenFrames;
        long lastWheelAt = WheelDeliveryTests(documentObject, windowObject, hwnd, check, controller.NotifyScrolled, () => countedHost.Calls);
        deadline = Environment.TickCount64 + 8000;
        while (controller.BlocksInput && Environment.TickCount64 < deadline) Program.Pump();
        long settleMs = Environment.TickCount64 - lastWheelAt;
        Console.WriteLine($"Real Word last wheel → editable ink: {settleMs}ms");
        check(settleMs < 500, $"Post-scroll input recovery exceeded 500ms: {settleMs}ms");
        check(previewFrames > previewsBefore && hiddenFrames == hidesBefore && !controller.BlocksInput,
            $"Real Word scrolling did not keep editable ink visible through preview and settlement: previews={previewFrames - previewsBefore}, hides={hiddenFrames - hidesBefore}");
        Console.WriteLine($"Real Word live ink: preview frames={previewFrames - previewsBefore}, hidden frames={hiddenFrames - hidesBefore}");
        previewsBefore = previewFrames; hidesBefore = hiddenFrames;
        // Deliver real input to the fixture, but let Word's native view hooks
        // wake the controller. Only the input timestamp is simulated: posted
        // wheel messages do not advance Windows' physical-input timestamp.
        lastWheelAt = WheelDeliveryTests(documentObject, windowObject, hwnd, check, () => inputStamp++, () => countedHost.Calls);
        deadline = Environment.TickCount64 + 8000;
        while (controller.BlocksInput && Environment.TickCount64 < deadline) Program.Pump();
        settleMs = Environment.TickCount64 - lastWheelAt;
        check(settleMs < 500 && previewFrames > previewsBefore && hiddenFrames == hidesBefore && !controller.BlocksInput,
            $"Native Word view notifications failed to track and settle ink: settle={settleMs}ms, previews={previewFrames - previewsBefore}");
        Console.WriteLine($"Native Word notifications: preview frames={previewFrames - previewsBefore}, last wheel → editable ink={settleMs}ms");
        controller.Suspend();
        Program.Wait(controller.PollAsync());
        check(bar.Status == "Ready" && host.Starts == 1, "Returning to real Word restarted the helper or failed after releasing its document");
    }
    private static long WheelDeliveryTests(object documentObject, object windowObject, IntPtr hwnd, Action<bool, string> check, Action forwarded = null,
        Func<int> queryCount = null)
    {
        dynamic document = documentObject, window = windowObject;
        // Exercise real WM_MOUSEWHEEL delivery to this fixture's document HWND;
        // never select or send input to another application on the desktop.
        IntPtr documentWindow = Native.FindDocument(hwnd);
        uint thread = Native.GetWindowThreadProcessId(documentWindow, out uint process);
        Native.GetWindowRect(documentWindow, out var viewport);
        dynamic pageStart = document.GoTo(What: 1, Which: 1, Count: 2);
        int start = (int)pageStart.Start;
        Marshal.ReleaseComObject(pageStart);
        dynamic mark = document.Range(start + 600, start + 600);
        try
        {
            int x = 0, beforeY = 0, width = 0, height = 0;
            window.GetPoint(out x, out beforeY, out width, out height, mark);
            using var wheel = new Ink_Canvas.Helpers.MouseWheelForwarder(Dispatcher.CurrentDispatcher, forwarded);
            var wheelWatch = Stopwatch.StartNew();
            int beforeQueries = queryCount?.Invoke() ?? 0;
            int batches = queryCount == null ? 1 : 12;
            long lastWheelAt = Environment.TickCount64;
            for (int batch = 0; batch < batches; batch++)
            {
                for (int i = 0; i < 120 / batches; i++) wheel.Enqueue(new Ink_Canvas.Helpers.WheelPacket(
                    new Ink_Canvas.Helpers.WheelTarget(documentWindow, process, thread), -1, 0,
                    viewport.Left + (viewport.Right - viewport.Left) / 2, viewport.Top + (viewport.Bottom - viewport.Top) / 2));
                wheel.Flush();
                lastWheelAt = Environment.TickCount64;
                if (queryCount != null)
                {
                    Program.Pump(15);
                    check(queryCount() - beforeQueries <= wheelWatch.ElapsedMilliseconds / 16 + 2,
                        "Live scroll updates exceeded the display cadence budget");
                }
            }
            int queriesDuringInput = (queryCount?.Invoke() ?? beforeQueries) - beforeQueries;
            if (queryCount != null) check(queriesDuringInput > 0, "Word view tracking still waited for wheel input to stop");
            int afterY = beforeY;
            long deadline = Environment.TickCount64 + 3000;
            while (afterY == beforeY && Environment.TickCount64 < deadline)
            {
                Program.Pump(15);
                window.GetPoint(out x, out afterY, out width, out height, mark);
            }
            check(afterY < beforeY, "Direct asynchronous wheel forwarding did not scroll real Word");
            Console.WriteLine($"Real Word wheel: 120 deltas / {batches} batches → visible movement={wheelWatch.Elapsed.TotalMilliseconds:F1}ms; queries during input={queriesDuringInput}");
            return lastWheelAt;
        }
        finally { Marshal.ReleaseComObject(mark); }
    }
    private sealed class CountedSource(IWordFrameSource source) : IWordFrameSource
    {
        internal int Calls;
        internal WordFrame LastFrame;
        public async Task<WordFrame> ExchangeAsync(WordRequest request, CancellationToken token)
        { Calls++; var frame = await source.ExchangeAsync(request, token); if (frame?.Status == "Ready") LastFrame = frame; return frame; }
        public void Stop() => source.Stop();
        public Task WarmAsync(CancellationToken token) => source.WarmAsync(token);
        public void Suspend() => source.Suspend();
        public void Dispose() => source.Dispose();
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attribute, ref int value, uint size);
}
