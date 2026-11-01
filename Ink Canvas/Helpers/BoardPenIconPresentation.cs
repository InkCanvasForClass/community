using System.Windows.Media;
using Ink_Canvas.Controls;

namespace Ink_Canvas.Helpers
{
    internal static class BoardPenIconPresentation
    {
        internal static void Apply(BoardToolbarButton button, Brush fill, Brush outline, bool needsOutline)
        {
            button.IconBrush = fill;
            // Keep the ink color while retaining a visible silhouette on a similar
            // toolbar background. Clear the old pen when switching to a vivid color.
            button.IconGeometryDrawing.Pen = needsOutline ? new Pen(outline, 1) : null;
        }
    }
}
