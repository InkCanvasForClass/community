using System.Windows.Media;
using Ink_Canvas.Controls;

namespace Ink_Canvas.Helpers
{
    internal static class BoardPenIconPresentation
    {
        internal static void Apply(BoardToolbarButton button, Brush fill, Brush outline, bool needsOutline)
        {
            if (!SameBrush(button.IconBrush, fill)) button.IconBrush = fill;
            // Keep the ink color while retaining a visible silhouette on a similar
            // toolbar background. Clear the old pen when switching to a vivid color.
            var drawing = button.IconGeometryDrawing;
            if (!needsOutline)
            {
                if (drawing.Pen != null) drawing.Pen = null;
                return;
            }
            if (drawing.Pen?.Thickness == 1 && SameBrush(drawing.Pen.Brush, outline)) return;
            var pen = new Pen(outline, 1);
            if (outline?.IsFrozen == true) pen.Freeze();
            drawing.Pen = pen;
        }

        private static bool SameBrush(Brush left, Brush right)
        {
            if (ReferenceEquals(left, right)) return true;
            return left is SolidColorBrush a && right is SolidColorBrush b
                && !a.HasAnimatedProperties && !b.HasAnimatedProperties
                && a.Color == b.Color && a.Opacity == b.Opacity
                && a.Transform.Value == b.Transform.Value && a.RelativeTransform.Value == b.RelativeTransform.Value;
        }
    }
}
