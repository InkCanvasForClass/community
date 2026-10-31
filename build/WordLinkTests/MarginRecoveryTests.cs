using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using Ink_Canvas.WordLink;
using InkCanvasWordLink;

internal static partial class Program
{
    private static void MarginInkTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word margin ink fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var frame = Frame();
        var document = new WordInkDocument();
        var projection = new WordInkProjection(canvas);
        projection.Show(frame, document);
        Point Local(double x, double y) => canvas.PointFromScreen(new Point(x, y));
        var left = Local(30, 150); var right = Local(750, 200);
        Check(canvas.Clip.FillContains(left) && canvas.Clip.FillContains(right), "Left/right workspace margins are still clipped to the paper");
        Check(!canvas.Clip.FillContains(Local(30, 50)), "Margin expansion covered Word's area above the page");
        canvas.Strokes.Add(Stroke(left.X, left.Y)); canvas.Strokes.Add(Stroke(right.X, right.Y));
        document.Pages = projection.Capture(frame, document);
        Check(document.Pages[1].Count == 2 && document.Pages[1][0].StylusPoints[0].X < 0
            && document.Pages[1][1].StylusPoints[0].X > frame.Pages[0].WidthPoints,
            "Margin ink was discarded instead of anchored outside the page in document coordinates");
        projection.Show(frame, document);
        frame.Pages[0].Bounds = new WordRect { X = 150, Y = 140, Width = 750, Height = 1050 };
        projection.Show(frame, document);
        var expected = Local(45, 215);
        Check((canvas.Strokes[0].StylusPoints[0].ToPoint() - expected).Length < .1,
            "Margin ink did not follow the page during scroll/zoom");
        var store = new WordAnnotationStore(Path.Combine(root, "margin-ink"));
        Check(Wait(store.SaveCanonicalAsync(frame.DocumentKey, frame.Revision, document)), "Margin ink could not be saved");
        document = Wait(store.LoadAsync(frame.DocumentKey, frame.Revision)); projection.Reset(); projection.Show(frame, document);
        Check(canvas.Strokes.Count == 2 && (canvas.Strokes[0].StylusPoints[0].ToPoint() - expected).Length < .2,
            "Margin ink moved or disappeared after reload");

        // Facing pages must partition the shared gutter without duplicate ink.
        frame = Frame(); frame.Pages[0].Bounds = new WordRect { X = 100, Y = 100, Width = 300, Height = 700 };
        frame.Pages.Add(new WordPageFrame { Number = 2, Start = 100, End = 200, WidthPoints = 500, HeightPoints = 700,
            Bounds = new WordRect { X = 500, Y = 100, Width = 300, Height = 700 } });
        document = new WordInkDocument(); projection.Reset(); projection.Show(frame, document);
        foreach (double x in new[] { 30.0, 420, 470, 900 })
        { var p = Local(x, 150); canvas.Strokes.Add(Stroke(p.X, p.Y)); }
        document.Pages = projection.Capture(frame, document);
        Check(document.Pages[1].Count == 2 && document.Pages[2].Count == 2,
            "Facing-page margins were lost, duplicated or assigned across the gutter");
        projection.Reset();
    }

    private static void TransientRecoveryTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word busy recovery fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var bar = new Ink_Canvas.Controls.WordLectureBar();
        var transport = new FrameSource { Frame = Frame() };
        bool paused = false, contact = false;
        using var controller = new WordLectureController(canvas, bar, Path.Combine(root, "transient-recovery"),
            () => true, () => contact, _ => { }, action => action(), (_, _) => { }, transport, _ => 10,
            automaticPolling: false, pausePresentation: value => paused = value);
        controller.Configure(true, true); Wait(controller.PollAsync());
        Point p = canvas.PointFromScreen(new Point(30, 150)); canvas.Strokes.Add(Stroke(p.X, p.Y)); Pump();
        var visible = canvas.Strokes[0];
        foreach (string status in new[] { "Busy", "Timeout", "Locating" })
        {
            transport.Frame = new WordFrame { Status = status }; Wait(controller.PollAsync());
            Check(controller.BlocksInput && !canvas.IsHitTestVisible && paused && canvas.Strokes.Count == 1
                && ReferenceEquals(visible, canvas.Strokes[0]), "A temporary Word failure discarded ink or accepted stale input");
            // A contact on the disabled canvas must not prevent recovery.
            contact = true;
            transport.Frame = Frame(); Wait(controller.PollAsync());
            contact = false;
            Check(!controller.BlocksInput && canvas.IsHitTestVisible && !paused && ReferenceEquals(visible, canvas.Strokes[0]),
                "Word recovery rebuilt the ink or left the canvas paused at an unchanged viewport");
        }
        bar.Go(1);
        transport.Frame = new WordFrame { Status = "Busy" }; Wait(controller.PollAsync());
        transport.Frame = Frame(); transport.Frame.CurrentPage = 2; transport.Frame.Pages[0].Number = 2;
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetPage == 2 && !controller.BlocksInput,
            "A busy Word reply silently dropped the requested page navigation");
        transport.Frame = new WordFrame { Status = "Timeout" }; Wait(controller.PollAsync());
        controller.Suspend();
        Check(!paused && !controller.IsActive, "Leaving Word after a transient failure kept desktop ink hidden");
    }
}
