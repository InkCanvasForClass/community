using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Controls;
using Ink_Canvas.Ink;
using Ink_Canvas.Ink.WinRT;

internal static class HighlighterRenderingTests
{
    internal static void Run(Action<bool, string> check, Action pump)
    {
        var baseline = Read(Render(new StrokeCollection(), Colors.White), 40, 40);
        if (baseline.A == 0)
            throw new PlatformNotSupportedException("WPF returned a transparent bitmap for a plain white background; desktop rendering is unavailable.");
        if (!Near(baseline, Colors.White))
            throw new InvalidOperationException($"WPF baseline rendering failed: {baseline}");
        foreach (byte alpha in new byte[] { 0, 64, 128, 255 })
        foreach (bool overlap in new[] { false, true })
        foreach (Color background in new[] { Colors.White, Colors.Black, Color.FromRgb(30, 70, 110) })
        {
            var source = new StrokeCollection { Make(alpha, overlap, false), Make(alpha, overlap, true) };
            var pixels = Render(source, background);
            var single = Read(pixels, 20, 40);
            var crossing = Read(pixels, 40, 40);
            var expected = Mix(background, Colors.Yellow, HighlighterRendering.Opacity(alpha));
            check(Near(single, expected), $"Single highlighter alpha {alpha} rendered {single}, expected {expected}");
            var expectedCrossing = overlap ? Mix(expected, Colors.Yellow, HighlighterRendering.Opacity(alpha)) : expected;
            check(Near(crossing, expectedCrossing), $"Crossing alpha {alpha}, overlap {overlap}: {crossing}, expected {expectedCrossing}");
            using var stream = new System.IO.MemoryStream();
            source.Save(stream); stream.Position = 0;
            var loaded = new StrokeCollection(stream);
            check(Near(Read(Render(loaded, background), 40, 40), expectedCrossing), "ISF roundtrip lost highlighter rendering policy");
            var style = PenToolStylePolicy.Capture(source[0].DrawingAttributes, 1, 12, overlap, true, false);
            check(Math.Abs(style.WetOpacity * (overlap ? 1 : .5) - HighlighterRendering.Opacity(alpha)) < .00001,
                "Native composition opacity disagrees with dry output");
        }

        var canvas = new HighlighterInkCanvas { Width = 80, Height = 80, Background = Brushes.Transparent };
        var first = Make(128, false, false);
        var second = Make(128, false, true);
        canvas.Strokes.Add(first); canvas.Strokes.Add(second);
        check(ReferenceEquals(canvas.Strokes[0], first) && canvas.Strokes.Count == 2,
            "Rendering replaced editing/history stroke identities");
        canvas.Measure(new Size(80, 80)); canvas.Arrange(new Rect(0, 0, 80, 80)); canvas.UpdateLayout(); pump();
        check(Near(Read(RenderCanvas(canvas), 40, 40), Mix(Colors.White, Colors.Yellow, HighlighterRendering.Opacity(128))),
            "Integrated InkCanvas still used WPF fixed opacity");
        first.DrawingAttributes.Color = Color.FromArgb(64, 255, 255, 0); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Mix(Colors.White, Colors.Yellow, HighlighterRendering.Opacity(64))),
            "Attribute changes did not invalidate the presentation");
        canvas.Select(new StrokeCollection { first });
        check(ReferenceEquals(canvas.GetSelectedStrokes()[0], first), "Selection targeted render proxies");
        canvas.Select(new StrokeCollection());
        canvas.Strokes.Remove(second); pump();
        check(Near(Read(RenderCanvas(canvas), 40, 20), Colors.White), "Removed highlighter left a ghost visual");
        canvas.Strokes = new StrokeCollection { second }; pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Colors.White), "Replaced page retained the previous presentation");
        first.DrawingAttributes.Color = Colors.Red; pump();
        check(canvas.Strokes.Count == 1 && ReferenceEquals(canvas.Strokes[0], second), "Detached page still influenced editing");
        var erased = second.GetEraseResult(new[] { new Point(40, 40) }, new EllipseStylusShape(15, 15));
        canvas.Strokes.Replace(second, erased); pump();
        check(Near(Read(RenderCanvas(canvas), 40, 40), Colors.White), "Point erase did not remove highlighter pixels");
        check(erased.All(HighlighterRendering.IsHighlighter), "Point erase lost persistent tool identity");
        canvas.Strokes.Clear();
        var ordinary = Make(128, true, false);
        HighlighterRendering.SetTool(ordinary.DrawingAttributes, false);
        ordinary.DrawingAttributes.Color = Color.FromArgb(128, 255, 0, 0);
        canvas.Strokes.Add(ordinary); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Mix(Colors.White, Colors.Red, 128 / 255.0)),
            "Ordinary ink inherited the highlighter opacity multiplier");
        ordinary.DrawingAttributes.IsHighlighter = true; pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Mix(Colors.White, Colors.Red, HighlighterRendering.Opacity(128))),
            "Changing ordinary ink to highlighter failed to rebuild presentation");
        ordinary.Transform(new Matrix(1, 0, 0, 1, 0, 20), false); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Colors.White)
            && Near(Read(RenderCanvas(canvas), 20, 60), Mix(Colors.White, Colors.Red, HighlighterRendering.Opacity(128))),
            "Transforming highlighter geometry retained the old pixels");
        ordinary.DrawingAttributes.IsHighlighter = false; pump();
        check(Near(Read(RenderCanvas(canvas), 20, 60), Mix(Colors.White, Colors.Red, 128 / 255.0)),
            "Changing highlighter back to ordinary ink failed to restore WPF rendering");
        canvas.EditingMode = InkCanvasEditingMode.Ink;
        canvas.PausePresentation(true); canvas.PausePresentation(true); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 60), Colors.White)
            && ReferenceEquals(canvas.Strokes[0], ordinary) && canvas.EditingMode == InkCanvasEditingMode.Ink,
            "Pausing Word scroll presentation changed editing data or left stale pixels");
        ordinary.Transform(new Matrix(1, 0, 0, 1, 0, -20), false); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Colors.White), "A pending view update resurrected paused ink");
        canvas.PausePresentation(false); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Mix(Colors.White, Colors.Red, 128 / 255.0)),
            "Restoring scroll presentation lost current geometry or opacity");
        int invalidations = 0;
        ordinary.Invalidated += (_, _) => invalidations++;
        var originalPoint = ordinary.StylusPoints[0];
        for (int i = 0; i < 300; i++) canvas.SetPresentationTransform(new Matrix(1, 0, 0, 1, 0, i % 21));
        canvas.SetPresentationTransform(new Matrix(1, 0, 0, 1, 0, 20)); pump();
        check(invalidations == 0 && ordinary.StylusPoints[0] == originalPoint
            && Near(Read(RenderCanvas(canvas), 20, 40), Colors.White)
            && Near(Read(RenderCanvas(canvas), 20, 60), Mix(Colors.White, Colors.Red, 128 / 255.0)),
            "Presentation-only scrolling hid ink or rebuilt stroke geometry");
        canvas.SetPresentationTransform(null); pump();
        check(Near(Read(RenderCanvas(canvas), 20, 40), Mix(Colors.White, Colors.Red, 128 / 255.0))
            && canvas.EditingMode == InkCanvasEditingMode.Ink, "Ending scroll preview did not restore pen coordinates");
        canvas.Strokes.Clear();
        canvas.Strokes.Add(Make(128, false, false)); canvas.Strokes.Add(Make(128, false, true));
        canvas.SetPresentationTransform(new Matrix(1, 0, 0, 1, 10, 10)); pump();
        check(Near(Read(RenderCanvas(canvas), 50, 50), Mix(Colors.White, Colors.Yellow, HighlighterRendering.Opacity(128))),
            "Scroll preview lost highlighter alpha or non-darkening overlap");
        canvas.SetPresentationTransform(null);
        canvas.Strokes.Clear();
    }

    private static Stroke Make(byte alpha, bool overlap, bool vertical)
    {
        var attributes = new DrawingAttributes { Color = Color.FromArgb(alpha, 255, 255, 0), Width = 12, Height = 12,
            StylusTip = StylusTip.Rectangle, IsHighlighter = !overlap, IgnorePressure = true, FitToCurve = false };
        HighlighterRendering.SetTool(attributes, true);
        return new Stroke(new StylusPointCollection(vertical
            ? new[] { new StylusPoint(40, 10), new StylusPoint(40, 70) }
            : new[] { new StylusPoint(10, 40), new StylusPoint(70, 40) }), attributes);
    }

    private static RenderTargetBitmap Render(StrokeCollection source, Color background)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, 80, 80));
            HighlighterRendering.Draw(context, source);
        }
        var bitmap = new RenderTargetBitmap(80, 80, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); return bitmap;
    }

    private static RenderTargetBitmap RenderCanvas(HighlighterInkCanvas canvas)
    {
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(80, 80, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 80, 80));
        bitmap.Render(visual); bitmap.Render(canvas); return bitmap;
    }

    private static Color Read(RenderTargetBitmap bitmap, int x, int y)
    {
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
    private static Color Mix(Color background, Color foreground, double opacity) => Color.FromRgb(
        (byte)Math.Round(background.R * (1 - opacity) + foreground.R * opacity),
        (byte)Math.Round(background.G * (1 - opacity) + foreground.G * opacity),
        (byte)Math.Round(background.B * (1 - opacity) + foreground.B * opacity));
    private static bool Near(Color a, Color b) => a.A == b.A && Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2;
}
