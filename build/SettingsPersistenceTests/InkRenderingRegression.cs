using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;

internal static class InkRenderingRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var canvas = new VisualCanvas { Width = 280, Height = 120 };
        canvas.Measure(new Size(280, 120)); canvas.Arrange(new Rect(0, 0, 280, 120));
        var visual = new StrokeVisual(new DrawingAttributes { Color = Colors.Black, Width = 8, Height = 8, IgnorePressure = false });
        visual.SetVisualCanvas(canvas);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 480; i++)
        {
            visual.Add(new StylusPoint(10 + i * .5, 40, .1f + .8f * i / 479));
            if (i % 4 == 3) visual.Redraw();
        }
        watch.Stop();
        Console.WriteLine($"Production WPF wet preview: 480 pressure points / 120 redraws = {watch.Elapsed.TotalMilliseconds:F1}ms, {GC.GetAllocatedBytesForCurrentThread() - allocated:N0}B, {canvas.Visuals.Count} retained visuals");
        check(visual.Stroke.StylusPoints.Count == 480 && canvas.Visuals.Count <= 12,
            "Realtime rendering dropped samples or retained one visual per input packet");
        byte[] Render()
        {
            var bitmap = new RenderTargetBitmap(280, 120, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new byte[280 * 120 * 4]; bitmap.CopyPixels(pixels, 280 * 4, 0); return pixels;
        }
        bool Ink(byte[] pixels, int x, int y) => pixels[(y * 280 + x) * 4 + 3] > 128;
        var rendered = Render();
        check(Ink(rendered, 12, 40) && Ink(rendered, 248, 40), "A committed segment or end segment disappeared");
        int WidthAt(int x) => Enumerable.Range(20, 40).Count(y => Ink(rendered, x, y));
        check(WidthAt(230) > WidthAt(20), "Realtime preview lost pressure-dependent thickness");
        visual.Add(new StylusPoint(250, 90, .9f)); visual.Redraw();
        check(Ink(Render(), 250, 88), "A partial final segment was omitted after a chunk commit");
        visual.Stroke.StylusPoints = new StylusPointCollection { new StylusPoint(100, 10), new StylusPoint(100, 100) };
        visual.ForceRedraw();
        rendered = Render();
        check(!Ink(rendered, 20, 40) && Ink(rendered, 100, 50), "Replacing a shortened stroke retained stale committed pixels");
        canvas.Clear();

        var history = new TimeMachine();
        Stroke Make(double x) => new(new StylusPointCollection { new StylusPoint(x, 10), new StylusPoint(x, 20) });
        var first = Make(10); var second = Make(20); var processed = Make(11);
        var replace = typeof(TimeMachine).GetMethod("TryReplaceUserInputStroke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        bool Replace(Stroke source, Stroke target) => (bool)replace.Invoke(history, new object[] { source, target });
        history.CommitStrokeUserInputHistory(new StrokeCollection { first });
        history.CommitStrokeUserInputHistory(new StrokeCollection { second });
        check(Replace(first, processed), "A late smoothing result could not find its own history entry");
        check(ReferenceEquals(history.Undo().CurrentStroke[0], second), "Late smoothing overwrote a later stroke's undo entry");
        check(ReferenceEquals(history.Undo().CurrentStroke[0], processed), "Undo retained the unsmoothed source identity");
        check(!Replace(processed, first), "Smoothing changed an entry that was already undone");
        history.ClearStrokeHistory();
        history.CommitStrokeUserInputHistory(new StrokeCollection { first, second });
        check(Replace(first, processed), "Grouped user input could not be updated");
        var grouped = history.Undo().CurrentStroke;
        check(grouped.Count == 2 && grouped.Contains(processed) && grouped.Contains(second), "Smoothing destroyed other members of a grouped undo entry");
    }
}
