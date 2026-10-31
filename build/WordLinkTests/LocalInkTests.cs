using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Input;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void DeferredInkPostprocessTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word deferred pressure fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas;
        canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var transport = new FrameSource { Frame = Frame() };
        string storage = Path.Combine(root, "deferred-pressure");
        using var controller = new WordLectureController(canvas, new Ink_Canvas.Controls.WordLectureBar(), storage,
            () => true, () => false, _ => { }, action => action(), (_, _) => { }, transport, _ => 10, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        var origin = canvas.PointFromScreen(new Point(180, 200));
        var stroke = new Stroke(new StylusPointCollection(Enumerable.Range(0, 20)
            .Select(i => new StylusPoint(origin.X + i * 3, origin.Y + i, .5f))));
        var outer = controller.DeferInkCommit();
        var recognition = controller.DeferInkCommit();
        canvas.Strokes.Add(stroke);
        outer.Dispose(); outer.Dispose();
        Pump();
        int calls = transport.Calls;
        Wait(controller.PollAsync());
        Check(ReferenceEquals(canvas.Strokes.Single(), stroke) && transport.Calls == calls,
            "Word replaced an input stroke before asynchronous pressure processing completed");
        var points = stroke.StylusPoints.Clone();
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i]; point.PressureFactor = .8f - .7f * i / (points.Count - 1); points[i] = point;
        }
        stroke.StylusPoints = points;
        stroke.DrawingAttributes.IgnorePressure = false;
        recognition.Dispose(); Pump();
        Check(canvas.Strokes.Count == 1 && canvas.Strokes[0].StylusPoints[0].PressureFactor > .7
            && canvas.Strokes[0].StylusPoints[^1].PressureFactor < .2 && !canvas.Strokes[0].DrawingAttributes.IgnorePressure,
            "Delayed brush-tip pressure was lost during the page projection");
        Check(controller.FlushForRestart(), "Finished pressure stroke could not be saved");
        var saved = Wait(new WordAnnotationStore(storage).LoadAsync(new string('a', 64), new string('b', 64)));
        Check(saved.Pages[1][0].StylusPoints[^1].PressureFactor < .2, "Saved Word ink lost its tapered tip");
        var stale = controller.DeferInkCommit();
        controller.Suspend();
        Wait(controller.PollAsync());
        var current = controller.DeferInkCommit();
        stale.Dispose();
        var nextStroke = Stroke(origin.X + 100, origin.Y); canvas.Strokes.Add(nextStroke); Pump();
        Check(canvas.Strokes.Contains(nextStroke), "An old session's completion released the new session's input guard");
        current.Dispose(); Pump();
        Check(controller.FlushForRestart(), "A stale completion left ink commits permanently paused");
    }

    private static void LocalInkCommitTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Local Word ink fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas;
        canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var transport = new FrameSource { Frame = Frame() };
        bool contact = false, canUndo = false;
        string storage = Path.Combine(root, "local-ink");
        var controller = new WordLectureController(canvas, new Ink_Canvas.Controls.WordLectureBar(), storage,
            () => true, () => contact, _ => { }, action => action(), (undo, _) => canUndo = undo,
            transport, _ => 10, automaticPolling: false);
        try
        {
            controller.Configure(true, true); Wait(controller.PollAsync());
            int calls = transport.Calls;
            Point point = canvas.PointFromScreen(new Point(150, 150));
            contact = true;
            canvas.Strokes.Add(Stroke(point.X, point.Y)); Pump();
            Check(!canUndo, "A contact still drawing was committed prematurely");
            contact = false; controller.NotifyInkInputCompleted(); Pump();
            Check(canUndo && transport.Calls == calls, "Finished ink waited for a Word query to enable undo");
            canvas.Strokes.Add(Stroke(point.X + 40, point.Y)); Pump();
            Check(canvas.Strokes.Count == 2 && transport.Calls == calls, "Local commit refreshed Word or lost a stroke");
            controller.Undo(false);
            Check(canvas.Strokes.Count == 1, "Two separate input gestures became a single undo step");
            controller.Undo(false);
            Check(canvas.Strokes.Count == 0, "First local stroke could not be undone");
            controller.Undo(true); controller.Undo(true);
            Check(canvas.Strokes.Count == 2, "Redo lost local ink");

            // A pending COM/IPC reply must not block local stroke commits.
            var reply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.BeforeReply = () => reply.Task;
            var pending = controller.PollAsync();
            canvas.Strokes.Add(Stroke(point.X + 80, point.Y)); Pump();
            controller.Undo(false);
            Check(canvas.Strokes.Count == 2 && !pending.IsCompleted, "Local undo waited for a busy Word request");
            reply.SetResult(); Wait(pending); transport.BeforeReply = null;
            canvas.Strokes.Add(Stroke(point.X + 100, point.Y)); Pump();
        }
        finally { controller.Dispose(); }
        var loaded = Wait(new WordAnnotationStore(storage).LoadAsync(new string('a', 64), new string('b', 64)));
        Check(loaded.Pages[1].Count == 3, "Shutdown during the save debounce lost the newest ink");
    }

    private static void CanonicalSnapshotTests(string root)
    {
        var store = new WordAnnotationStore(Path.Combine(root, "canonical"));
        string key = new('c', 64), revision = new('d', 64);
        var document = new WordInkDocument();
        for (int i = 1; i <= 24; i++)
            document.Pages[i] = new StrokeCollection(Enumerable.Range(0, 80).Select(j => Stroke(j, i)));
        Check(Wait(store.SaveCanonicalAsync(key, revision, document)), "Initial canonical snapshot failed");
        long before = GC.GetAllocatedBytesForCurrentThread();
        var saved = store.SaveCanonicalAsync(key, revision, document);
        long reusedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(Wait(saved), "Reused page snapshot failed");
        before = GC.GetAllocatedBytesForCurrentThread();
        saved = store.SaveAsync(key, revision, document);
        long copiedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(Wait(saved), "Deep snapshot comparison failed");
        Console.WriteLine($"24-page save snapshot UI allocations: reused={reusedBytes:N0}B, deep-copy={copiedBytes:N0}B");
        Check(reusedBytes * 10 < copiedBytes, "Saving unchanged pages still copied their stroke data");
        document.Pages[1] = new StrokeCollection(document.Pages[1]) { Stroke(99, 100) };
        before = GC.GetAllocatedBytesForCurrentThread();
        saved = store.SaveCanonicalAsync(key, revision, document);
        long editedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(Wait(saved) && editedBytes * 10 < copiedBytes,
            "One added stroke copied every existing stroke on the page");
        Console.WriteLine($"One-stroke edit snapshot UI allocations: {editedBytes:N0}B");
        // Replace pages, as the controller does; never mutate canonical pages.
        document.Pages[1] = new StrokeCollection { Stroke(123, 234) };
        Check(Wait(store.SaveCanonicalAsync(key, revision, document)), "Changed canonical page was not saved");
        var restored = Wait(store.LoadAsync(key, revision));
        Check(restored.Pages.Count == 24 && restored.Pages[1].Count == 1
            && Math.Abs(restored.Pages[1][0].StylusPoints[0].X - 123) < .05
            && restored.Pages[24].Count == 80, "Snapshot reuse saved stale or incomplete page ink");
    }
}
