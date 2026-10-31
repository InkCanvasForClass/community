using System;
using System.Collections.Generic;
using System.Linq;

namespace InkCanvasWordLink
{
    public sealed class WordRequest
    {
        public int Version { get; set; } = 1;
        public long WindowHandle { get; set; }
        public string ExpectedDocumentKey { get; set; }
        public int TargetPage { get; set; }
        public int TargetZoomPercentage { get; set; }
        public bool? TargetFocusMode { get; set; }
        public bool DeferContentCheck { get; set; }
        public bool ScrollPreview { get; set; }
        public bool VerifyCachedView { get; set; }
        public bool Handshake { get; set; }
        public bool ReleaseDocument { get; set; }
    }

    public sealed class WordFrame
    {
        public int Version { get; set; } = 1;
        public string Status { get; set; } = "Waiting";
        public string DocumentKey { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public string SessionKey { get; set; } = "";
        public string Revision { get; set; } = "";
        public long WindowHandle { get; set; }
        public int PageCount { get; set; }
        public int CurrentPage { get; set; }
        public int ZoomPercentage { get; set; }
        public bool NeedsRefinement { get; set; }
        public bool CachedGeometry { get; set; }
        public bool IsFocusMode { get; set; }
        public WordRect Viewport { get; set; } = new();
        public List<WordPageFrame> Pages { get; set; } = new();
        public bool IsValid() => Version == 1 && DocumentName != null && Pages != null && Viewport != null
            && DocumentName.Length <= 1024 && Pages.Count <= 16
            && (Status != "Ready" || (IsHash(DocumentKey) && IsHash(Revision) && PageCount is > 0 and <= 100000
                && CurrentPage > 0 && CurrentPage <= PageCount && Viewport.IsValid()
                && (ZoomPercentage == 0 || ZoomPercentage is >= 10 and <= 500)
                && Pages.Count > 0 && Pages.All(p => p != null && p.IsValid(PageCount))
                && Pages.Select(p => p.Number).Distinct().Count() == Pages.Count));
        public static bool IsHash(string value) => value != null && value.Length == 64
            && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    public sealed class WordRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsValid() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width)
            && double.IsFinite(Height) && Width > 0 && Height > 0 && Width <= 100000 && Height <= 100000
            && Math.Abs(X) <= 1000000 && Math.Abs(Y) <= 1000000;
    }

    public sealed class WordPageFrame
    {
        public int Number { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public double WidthPoints { get; set; }
        public double HeightPoints { get; set; }
        public WordRect Bounds { get; set; } = new();
        public string LayoutKey => FormattableString.Invariant($"{Start}:{End}:{WidthPoints:R}:{HeightPoints:R}");
        public bool IsValid(int pages) => Number > 0 && Number <= pages && Start >= 0 && End >= Start
            && double.IsFinite(WidthPoints) && double.IsFinite(HeightPoints)
            && WidthPoints is > 0 and < 20000 && HeightPoints is > 0 and < 20000 && Bounds?.IsValid() == true;
    }

    public readonly record struct WordAnchor(int Page, int Start, double PageX, double PageY, double ScreenX, double ScreenY);

    public static class WordPageGeometry
    {
        // A page margin can remain visible after every sampled character has
        // scrolled offscreen. Move that previously measured page with a common
        // visible neighbour; never invent a page gap or carry across reflow/zoom.
        public static void RetainVisibleNeighbours(WordFrame previous, WordFrame current)
        {
            if (previous == null || previous.DocumentKey != current.DocumentKey || previous.Revision != current.Revision
                || previous.WindowHandle != current.WindowHandle || previous.Viewport.Width != current.Viewport.Width
                || previous.Viewport.Height != current.Viewport.Height) return;
            var shared = current.Pages.Select(p => (Now: p, Old: previous.Pages.FirstOrDefault(o => o.Number == p.Number)))
                .Where(p => p.Old != null).ToArray();
            if (shared.Length == 0) return;
            double dx = shared[0].Now.Bounds.X - shared[0].Old.Bounds.X;
            double dy = shared[0].Now.Bounds.Y - shared[0].Old.Bounds.Y;
            if (shared.Any(p => p.Now.LayoutKey != p.Old.LayoutKey
                || Math.Abs(p.Now.Bounds.Width - p.Old.Bounds.Width) > .5 || Math.Abs(p.Now.Bounds.Height - p.Old.Bounds.Height) > .5
                || Math.Abs(p.Now.Bounds.X - p.Old.Bounds.X - dx) > 2 || Math.Abs(p.Now.Bounds.Y - p.Old.Bounds.Y - dy) > 2)) return;
            foreach (var page in previous.Pages)
            {
                if (current.Pages.Count >= 16) break;
                if (current.Pages.Any(p => p.Number == page.Number)) continue;
                var r = new WordRect { X = page.Bounds.X + dx, Y = page.Bounds.Y + dy,
                    Width = page.Bounds.Width, Height = page.Bounds.Height };
                var v = current.Viewport;
                if (r.X >= v.X + v.Width || r.X + r.Width <= v.X || r.Y >= v.Y + v.Height || r.Y + r.Height <= v.Y) continue;
                current.Pages.Add(new WordPageFrame { Number = page.Number, Start = page.Start, End = page.End,
                    WidthPoints = page.WidthPoints, HeightPoints = page.HeightPoints, Bounds = r });
            }
        }

        public static bool NeedsAdjacentPage(IReadOnlyList<WordPageFrame> pages, WordRect viewport, int total) => pages.Count > 0
            && (pages.Min(p => p.Number) > 1 && pages.Min(p => p.Bounds.Y) > viewport.Y + 20
                || pages.Max(p => p.Number) < total && pages.Max(p => p.Bounds.Y + p.Bounds.Height) < viewport.Y + viewport.Height - 20);

        public static int SelectCurrentPage(IReadOnlyList<WordPageFrame> pages, WordRect viewport, int centerPage = 0, int preferredPage = 0)
        {
            if (pages == null || pages.Count == 0 || viewport?.IsValid() != true) return 0;
            double VisibleArea(WordPageFrame page)
            {
                var r = page.Bounds;
                return Math.Max(0, Math.Min(r.X + r.Width, viewport.X + viewport.Width) - Math.Max(r.X, viewport.X))
                    * Math.Max(0, Math.Min(r.Y + r.Height, viewport.Y + viewport.Height) - Math.Max(r.Y, viewport.Y));
            }
            double centerX = viewport.X + viewport.Width / 2, centerY = viewport.Y + viewport.Height / 2;
            if (pages.Any(p => p.Number == centerPage && p.Bounds.X <= centerX && centerX <= p.Bounds.X + p.Bounds.Width
                && p.Bounds.Y <= centerY && centerY <= p.Bounds.Y + p.Bounds.Height)) return centerPage;
            double largest = pages.Max(VisibleArea);
            // At a side-by-side page gap, tiny pixel-rounding differences must
            // not override an explicit navigation target or make the label flap.
            if (pages.Any(p => p.Number == preferredPage && VisibleArea(p) > 0 && VisibleArea(p) >= largest * .9)) return preferredPage;
            // A full-page centre can be far offscreen. Fall back to visible area,
            // rather than choosing an old page whose centre happens to be closer.
            return pages.OrderByDescending(VisibleArea).ThenBy(p => p.Number).First().Number;
        }

        public static WordRect Solve(IReadOnlyList<WordAnchor> anchors, double width, double height, double expectedScale)
        {
            if (anchors == null || anchors.Count == 0 || anchors.Any(a => !double.IsFinite(a.PageX) || !double.IsFinite(a.PageY)
                || !double.IsFinite(a.ScreenX) || !double.IsFinite(a.ScreenY))
                || !double.IsFinite(expectedScale) || expectedScale is < 0.05 or > 20) return null;
            var direct = SolveConsistent(anchors, width, height, expectedScale);
            if (direct != null || anchors.Count < 3) return direct;
            // Word can briefly report an inconsistent point during layout. Use
            // a strict majority at the known DPI/zoom; never guess from one point
            // or accept equally supported, contradictory page origins.
            WordAnchor[] best = null;
            foreach (var seed in anchors)
            {
                double originX = seed.ScreenX - seed.PageX * expectedScale;
                double originY = seed.ScreenY - seed.PageY * expectedScale;
                var group = anchors.Where(a => Math.Abs(a.ScreenX - a.PageX * expectedScale - originX) <= 4
                    && Math.Abs(a.ScreenY - a.PageY * expectedScale - originY) <= 4).ToArray();
                if (group.Select(a => a.Start).Distinct().Count() < 2 || group.Length <= anchors.Count / 2) continue;
                if (best == null || group.Length > best.Length) best = group;
            }
            return best == null ? null : SolveConsistent(best, width, height, expectedScale);
        }

        private static WordRect SolveConsistent(IReadOnlyList<WordAnchor> anchors, double width, double height, double expectedScale)
        {
            double sx = FitScale(anchors, false, expectedScale), sy = FitScale(anchors, true, expectedScale);
            if (sx is < 0.05 or > 20 || sy is < 0.05 or > 20 || Math.Abs(sx / sy - 1) > 0.08) return null;
            double x = anchors.Average(a => a.ScreenX - a.PageX * sx);
            double y = anchors.Average(a => a.ScreenY - a.PageY * sy);
            // Ambiguous or inconsistent anchors must never move stored ink onto unrelated text.
            if (anchors.Any(a => Math.Abs(a.ScreenX - x - a.PageX * sx) > 4
                || Math.Abs(a.ScreenY - y - a.PageY * sy) > 4)) return null;
            var result = new WordRect { X = x, Y = y, Width = width * sx, Height = height * sy };
            return result.IsValid() ? result : null;
        }

        private static double FitScale(IReadOnlyList<WordAnchor> anchors, bool vertical, double fallback)
        {
            double[] points = anchors.Select(a => vertical ? a.PageY : a.PageX).ToArray();
            if (points.Max() - points.Min() < 40) return fallback;
            double mean = points.Average();
            double screenMean = anchors.Average(a => vertical ? a.ScreenY : a.ScreenX);
            double variance = 0, covariance = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                double delta = points[i] - mean;
                variance += delta * delta;
                covariance += delta * ((vertical ? anchors[i].ScreenY : anchors[i].ScreenX) - screenMean);
            }
            return variance > 0 ? covariance / variance : fallback;
        }
    }
}
