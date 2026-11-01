using System.Diagnostics;
using System.Runtime.InteropServices;
using InkCanvasWordLink;
using InkCanvasWordLinkHost;
using Ink_Canvas.WordLink;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;

internal static partial class WordTrackingLiveTests
{
    internal static void Run(object application, object documentObject, object windowObject, WordObserver observer, WordFrame initial, Action<bool, string> check, bool stabilityOnly = false)
    {
        dynamic app = application, document = documentObject, window = windowObject;
        dynamic selection = window.Selection, caret = document.GoTo(1, 1, 2, Type.Missing);
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Rapid Word scroll ink fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var ink = new WordInkDocument();
        for (int page = 1; page <= initial.PageCount; page++)
            ink.Pages[page] = new StrokeCollection { Program.Stroke(50, 50), Program.Stroke(400, 500) };
        var projection = new WordInkProjection(canvas);
        try
        {
            selection.SetRange((int)caret.Start, (int)caret.Start);
            var visited = new HashSet<int>();
            foreach (int target in new[] { 2, 6, 9, 3, 6 })
            {
                dynamic range = document.GoTo(1, 1, target, Type.Missing);
                try { window.ScrollIntoView(range, true); } finally { Marshal.ReleaseComObject(range); }
                app.ScreenRefresh(); Program.Pump(30);
                var watch = Stopwatch.StartNew(); WordFrame frame = null; var sequence = new List<string>();
                do
                {
                    frame = observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd, ExpectedDocumentKey = initial.DocumentKey,
                        ScrollPreview = true, DeferContentCheck = true });
                    sequence.Add($"{frame.Status}:{frame.CurrentPage}");
                    if (frame.Status == "Ready" && !frame.NeedsRefinement && frame.CurrentPage == target) break;
                    Program.Pump(20);
                } while (watch.ElapsedMilliseconds < 2000);
                Console.WriteLine($"Scroll to {target}, caret at 2: {watch.ElapsedMilliseconds}ms [{string.Join(",", sequence)}]");
                object selectedRange = selection.Range;
                try { check(Convert.ToInt32(new OfficeObject(selectedRange).Get<object>("Information", 3)) == 2,
                    "The tracking fixture moved the caret instead of testing view-only scrolling"); }
                finally { Marshal.ReleaseComObject(selectedRange); }
                check(frame?.Status == "Ready" && frame.CurrentPage == target, "Visible page did not follow native scrolling with an offscreen caret");
                check(sequence.All(value => value.StartsWith("Locating") || value.EndsWith(":" + target)), "A stale page number was published while discovering the visible page");
                if (!visited.Add(target) && !stabilityOnly) check(watch.ElapsedMilliseconds < 500, "Cached page recovery exceeded 500ms in the controlled Word fixture");
            }
            // Burst through distant pages without querying intermediate views,
            // then require the final stationary recalibration to be authoritative.
            foreach (int target in new[] { 9, 2, 8, 3, 9, 6, 2 })
            {
                foreach (int transient in new[] { 3, 9, 2, target })
                {
                    dynamic range = document.GoTo(1, 1, transient, Type.Missing);
                    try { window.ScrollIntoView(range, true); } finally { Marshal.ReleaseComObject(range); }
                }
                app.ScreenRefresh(); Program.Pump(30);
                var settled = observer.Read(new WordRequest { WindowHandle = (int)window.Hwnd,
                    ExpectedDocumentKey = initial.DocumentKey, Recalibrate = true, DeferContentCheck = true });
                check(settled.Status == "Ready" && settled.CurrentPage == target
                    && !WordPageGeometry.HasOverlappingPages(settled.Pages),
                    "Repeated reverse scrolling left stale or overlapping final page geometry");
                projection.Show(settled, ink);
                check(canvas.Strokes.Count == settled.Pages.Count * 2, "Rapid page changes left only part of the ink projection");
                int index = 0;
                foreach (var page in settled.Pages)
                {
                    // WPF rounds screen-to-client conversion at device pixel
                    // boundaries. Interpolate the converted paper rectangle.
                    var origin = canvas.PointFromScreen(new Point(page.Bounds.X, page.Bounds.Y));
                    var corner = canvas.PointFromScreen(new Point(page.Bounds.X + page.Bounds.Width, page.Bounds.Y + page.Bounds.Height));
                    foreach (var source in ink.Pages[page.Number])
                    {
                        var point = source.StylusPoints[0];
                        var expected = new Point(origin.X + point.X * (corner.X - origin.X) / page.WidthPoints,
                            origin.Y + point.Y * (corner.Y - origin.Y) / page.HeightPoints);
                        var actual = canvas.Strokes[index++].StylusPoints[0].ToPoint();
                        check((actual - expected).Length < .01,
                            $"Final ink did not return to its canonical page position after a rapid scroll burst: page={page.Number}, actual={actual}, expected={expected}");
                    }
                    double y = Math.Max(page.Bounds.Y, settled.Viewport.Y) + 1;
                    check(canvas.Clip.FillContains(canvas.PointFromScreen(new Point(page.Bounds.X + page.Bounds.Width * .8, y))),
                        "The final page retained a half-width clip after a scroll burst");
                }
            }
        }
        finally { projection.Reset(); Marshal.ReleaseComObject(caret); Marshal.ReleaseComObject(selection); }
    }
}
