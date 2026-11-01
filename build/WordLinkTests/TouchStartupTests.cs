using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void TouchStartupTests(string root)
    {
        var gate = new WordRefreshGate();
        gate.Scrolled(1000); gate.Complete(1010, 400, true, false);
        Check(gate.Delay(1010) == 16, "Newly discovered anchors inherited the expensive discovery cooldown");
        gate.Scrolled(1030); gate.Complete(1040, 400, true, false);
        Check(gate.Delay(1040) > 16, "Repeated expensive discovery bypassed backpressure");
        gate.Scrolled(1050); gate.Complete(1060, 4, true, true);
        Check(gate.Delay(1060) == 12, "A cached touch frame retained the old pagination cost");

        string storage = Path.Combine(root, "touch-startup");
        var source = new FrameSource { Frame = Frame() };
        source.Frame.NeedsRefinement = true;
        var stored = new WordInkDocument(); stored.Pages[1] = new StrokeCollection { Stroke(50, 50) };
        stored.Layout[1] = source.Frame.Pages[0].LayoutKey;
        var store = new WordAnnotationStore(storage);
        Check(Wait(store.SaveAsync(source.Frame.DocumentKey, source.Frame.Revision, stored)), "Could not seed startup annotation fixture");
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Touch/startup fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        using var controller = new WordLectureController(canvas, new WordLectureBar(), storage, () => true, () => false,
            _ => { }, action => action(), (_, _) => { }, source, _ => 10, automaticPolling: false, inputStamp: () => 1);
        controller.Configure(true, true); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 1 && controller.BlocksInput, "First calibrated page waited for edge discovery or accepted unconfirmed ink input");
        source.Frame.NeedsRefinement = false; source.Frame.CachedGeometry = true;
        Wait(controller.PollAsync());
        Check(!controller.BlocksInput && canvas.Strokes.Count == 1, "Startup refinement failed to restore editable ink");
        source.BeforeReply = async () => { controller.NotifyNativeScroll(false); await Task.Delay(10); };
        Wait(controller.PollAsync());
        Wait(controller.PollAsync());
        Check(!controller.BlocksInput, "Query-generated location feedback started a scrolling gesture");
        source.BeforeReply = async () => { controller.NotifyNativeScroll(true); await Task.Delay(10); };
        Wait(controller.PollAsync());
        Check(controller.BlocksInput, "Native touch/inertia scroll was discarded while a query was in flight");
        source.BeforeReply = null;
        Wait(controller.PollAsync());
        Check(source.LastRequest.ScrollPreview, "Native inertia did not enter the lightweight tracking path");
        Pump(140); Wait(controller.PollAsync());
        Check(!controller.BlocksInput, "Native touch tracking never settled after notifications stopped");
    }
}
