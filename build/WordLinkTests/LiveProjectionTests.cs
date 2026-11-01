using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void LiveProjectionTests(string root)
    {
        var canvas = new HighlighterInkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Live ink projection fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var frame = Frame(); var document = new WordInkDocument();
        document.Pages[1] = new StrokeCollection(Enumerable.Range(0, 300).Select(i => new Stroke(new StylusPointCollection(
            Enumerable.Range(0, 80).Select(j => new StylusPoint(50 + j * 2, 50 + i % 250 + j * .1))))));
        var projection = new WordInkProjection(canvas, canvas.SetPresentationTransform);
        projection.Show(frame, document);
        var shown = canvas.Strokes.ToArray(); var origin = shown[0].StylusPoints[0];
        double originalWidth = shown[0].DrawingAttributes.Width;
        var originalTip = shown[0].DrawingAttributes.StylusTipTransform;
        int invalidations = 0;
        foreach (var stroke in shown) stroke.Invalidated += (_, _) => invalidations++;
        var watch = Stopwatch.StartNew();
        bool allPreviewed = true;
        for (int i = 0; i < 120; i++)
        {
            frame.Pages[0].Bounds.Y = 100 - i;
            allPreviewed &= projection.TryPreview(frame, document);
        }
        Console.WriteLine($"Live transform: 300 strokes / 24,000 points, 120 updates={watch.Elapsed.TotalMilliseconds:F1}ms; stroke invalidations={invalidations}");
        Check(allPreviewed && projection.IsPreviewing && invalidations == 0 && canvas.Strokes.SequenceEqual(shown)
            && shown[0].StylusPoints[0] == origin, "Live scroll rebuilt stroke geometry or changed editable coordinates");
        projection.Show(frame, document);
        double settledY = canvas.PointFromScreen(new Point(150, 31)).Y;
        Check(!projection.IsPreviewing && Math.Abs(shown[0].StylusPoints[0].Y - settledY) < .01
            && document.Pages[1][0].StylusPoints[0].Y == 50, "Settling the view accumulated transforms or changed stored ink");
        for (int i = 0; i < 100; i++)
        {
            frame.Pages[0].Bounds.Y = i % 2 == 0 ? -580 : 100;
            projection.TryPreview(frame, document);
            projection.Show(frame, document);
        }
        Check(Math.Abs(shown[0].StylusPoints[0].Y - canvas.PointFromScreen(new Point(150, 150)).Y) < .01
            && canvas.Clip.FillContains(canvas.PointFromScreen(new Point(550, 150)))
            && document.Pages[1][0].StylusPoints[0].Y == 50,
            "Repeated reversed previews accumulated ink offsets or retained a partial final clip");
        frame.Pages[0].Bounds.Width *= 1.5; frame.Pages[0].Bounds.Height *= 1.5;
        Check(projection.TryPreview(frame, document), "Zoom preview did not reuse the presentation");
        projection.Show(frame, document);
        Check(Math.Abs(shown[0].DrawingAttributes.StylusTipTransform.M11 - originalTip.M11 * 1.5) < .001
            && shown[0].DrawingAttributes.Width == originalWidth,
            "Zoom settlement applied the pen width twice");
        projection.Reset(); canvas.Strokes.Clear();
        canvas.SetPresentationTransform(new Matrix(1, 0, 0, 1, 0, 5)); Pump(10);
        canvas.SetPresentationTransform(new Matrix(1, 0, 0, 1, 0, 25)); Pump(50);
        var animated = (MatrixTransform)typeof(HighlighterInkCanvas).GetField("_viewTransform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(canvas);
        Check(animated.Matrix.OffsetY == 25 && animated.Matrix.M11 == 1, "Scroll interpolation did not settle on the authoritative transform");
        canvas.SetPresentationTransform(null);
        Check(!animated.HasAnimatedProperties, "A settled or reset scroll retained an animation clock");

        frame.Viewport = new InkCanvasWordLink.WordRect { X = 200, Y = 200, Width = 200, Height = 200 };
        projection.Show(frame, document);
        var stableClip = canvas.Clip;
        frame.Pages[0].Bounds.Y -= 5;
        Check(projection.TryPreview(frame, document) && ReferenceEquals(stableClip, canvas.Clip),
            "Scrolling inside a page rebuilt an unchanged viewport clip");
        projection.Reset(); canvas.Strokes.Clear();

        var transport = new FrameSource { Frame = Frame() };
        int previews = 0, hides = 0;
        using var controller = new WordLectureController(canvas, new WordLectureBar(), Path.Combine(root, "live-projection"),
            () => true, () => false, _ => { }, action => action(), (_, _) => { }, transport, _ => 10,
            automaticPolling: false, pausePresentation: paused => { if (paused) hides++; canvas.PausePresentation(paused); },
            previewTransform: matrix => { if (matrix.HasValue) previews++; canvas.SetPresentationTransform(matrix); });
        controller.Configure(true, true); Wait(controller.PollAsync());
        var local = canvas.PointFromScreen(new Point(150, 180));
        canvas.Strokes.Add(Stroke(local.X, local.Y)); Pump();
        var committed = canvas.Strokes[0];
        double committedY = committed.StylusPoints[0].Y;
        for (int i = 0; i < 10; i++)
        {
            controller.NotifyScrolled(); transport.Frame.Pages[0].Bounds.Y += 2;
            Wait(controller.PollAsync());
        }
        Check(previews == 10 && hides == 0 && controller.BlocksInput && !canvas.IsHitTestVisible
            && committed.StylusPoints[0].Y == committedY,
            $"Controller hid live ink or repeatedly reprojected editable points: previews={previews}, hides={hides}, blocked={controller.BlocksInput}, hit={canvas.IsHitTestVisible}, y={committed.StylusPoints[0].Y}, before={committedY}");
        transport.Frame.Status = "Locating"; controller.NotifyScrolled(); Wait(controller.PollAsync());
        Check(hides == 0 && canvas.Strokes.Contains(committed), "One disappearing text anchor blanked the moving canvas");
        transport.Frame.Status = "Ready";
        Pump(160); Wait(controller.PollAsync());
        double controllerSettledY = canvas.PointFromScreen(new Point(150, 200)).Y;
        Check(!controller.BlocksInput && canvas.IsHitTestVisible && Math.Abs(committed.StylusPoints[0].Y - controllerSettledY) < .01,
            "Settled scrolling did not restore exact editing coordinates");
        Check(transport.LastRequest.Recalibrate && transport.LastRequest.DeferContentCheck,
            "Recovery from lost geometry failed to recalibrate without scanning document contents");
        Pump(270); Wait(controller.PollAsync());
        Check(transport.LastRequest.Recalibrate, "Word's delayed final layout was never rechecked");
        Wait(controller.PollAsync());
        Check(!transport.LastRequest.Recalibrate, "A completed settle verification kept forcing full Office reads");
        controller.Undo(false); controller.Undo(true);
        Check(canvas.Strokes.Count == 1 && Math.Abs(canvas.Strokes[0].StylusPoints[0].Y - controllerSettledY) < .01,
            "Live scrolling polluted undo history or restored ink at stale coordinates");
        controller.NotifyScrolled(); transport.Frame.Pages[0].Bounds.Y += 10; Wait(controller.PollAsync());
        canvas.Strokes.Clear(); Wait(controller.PollAsync());
        Pump(160); Wait(controller.PollAsync());
        Check(canvas.Strokes.Count == 0, "Scrolling resurrected ink cleared using the toolbar");
        controller.Undo(false);
        Check(canvas.Strokes.Count == 1
            && Math.Abs(canvas.Strokes[0].StylusPoints[0].Y - canvas.PointFromScreen(new Point(150, 210)).Y) < .01,
            "Undoing a clear during scroll restored the wrong page coordinates");
        controller.NotifyScrolled(); Wait(controller.PollAsync());
        controller.Suspend();
        Check(!controller.IsActive && canvas.Strokes.Count == 0, "Suspending during live preview did not restore the desktop");
    }
}
