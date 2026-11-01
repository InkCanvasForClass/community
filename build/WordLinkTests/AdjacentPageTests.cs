using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using InkCanvasWordLink;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void AdjacentPageTests()
    {
        WordFrame View(double offset, params int[] pages) => new()
        {
            DocumentKey = new string('a', 64), Revision = new string('b', 64), WindowHandle = 10,
            Viewport = new WordRect { Width = 800, Height = 800 }, PageCount = 3,
            Pages = pages.Select(n => new WordPageFrame { Number = n, Start = (n - 1) * 100, End = n * 100,
                WidthPoints = 600, HeightPoints = 800,
                Bounds = new WordRect { X = 100, Y = -650 + (n - 1) * 820 + offset, Width = 600, Height = 800 } }).ToList()
        };
        var previous = View(0, 1, 2);
        var current = View(-50, 2);
        WordPageGeometry.RetainVisibleNeighbours(previous, current);
        Check(current.Pages.Count == 2 && current.Pages.Single(p => p.Number == 1).Bounds.Y == -700,
            "Visible first-page margin vanished when only the second page had a visible text anchor");
        Check(!WordPageGeometry.NeedsAdjacentPage(current.Pages, current.Viewport, 3), "Known boundary triggered needless discovery");
        var edge = View(0, 2);
        Check(WordPageGeometry.NeedsAdjacentPage(edge.Pages, edge.Viewport, 3), "Exposed preceding page was not discovered");
        var offscreen = View(-200, 2);
        WordPageGeometry.RetainVisibleNeighbours(previous, offscreen);
        Check(offscreen.Pages.Count == 1, "Offscreen page was retained");
        var changed = View(-50, 2); changed.Revision = new string('c', 64);
        WordPageGeometry.RetainVisibleNeighbours(previous, changed);
        Check(changed.Pages.Count == 1, "Page geometry crossed a document revision");
        var zoomed = View(-50, 2); zoomed.Pages[0].Bounds.Width *= 1.25;
        WordPageGeometry.RetainVisibleNeighbours(previous, zoomed);
        Check(zoomed.Pages.Count == 1, "Page gap was guessed across a zoom change");
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Adjacent page fixture") { Width = 800, Height = 800 });
        surface.RootVisual = canvas; canvas.Measure(new Size(800, 800)); canvas.Arrange(new Rect(0, 0, 800, 800));
        var document = new WordInkDocument();
        document.Pages[1] = new StrokeCollection { Stroke(50, 760) };
        document.Pages[2] = new StrokeCollection { Stroke(50, 20) };
        var projection = new WordInkProjection(canvas);
        projection.Show(previous, document); projection.Show(current, document);
        Check(canvas.Strokes.Count == 2, "First-page annotations disappeared at the page boundary");
    }
}
