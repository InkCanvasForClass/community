using System.Collections.Generic;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    /// <summary>Assign horizontal page margins to the adjacent page without changing its coordinate origin.</summary>
    internal static class PluginPageInkBounds
    {
        internal static Rect ExpandHorizontally(Rect page, IReadOnlyList<Rect> pages, Rect ink)
        {
            if (page.IsEmpty || ink.IsEmpty) return page;
            double left = System.Math.Min(page.Left, ink.Left);
            double right = System.Math.Max(page.Right, ink.Right);
            foreach (Rect other in pages)
            {
                if (other.IsEmpty || other.Bottom <= page.Top || other.Top >= page.Bottom) continue;
                // In two-page layouts, divide the gutter between neighbors. Extending both
                // pages across the whole canvas would duplicate the opposite page's ink.
                if (other.Right <= page.Left)
                    left = System.Math.Max(left, (other.Right + page.Left) / 2);
                else if (other.Left >= page.Right)
                    right = System.Math.Min(right, (page.Right + other.Left) / 2);
            }
            return new Rect(left, page.Top, right - left, page.Height);
        }
    }
}
