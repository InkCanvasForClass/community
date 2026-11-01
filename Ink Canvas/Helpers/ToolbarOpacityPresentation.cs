using System;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    internal static class ToolbarOpacityPresentation
    {
        internal static void Apply(FrameworkElement toolbar, FrameworkElement surface, double opacity)
        {
            if (toolbar == null || surface == null) return;
            toolbar.BeginAnimation(UIElement.OpacityProperty, null);
            surface.BeginAnimation(UIElement.OpacityProperty, null);
            toolbar.Opacity = 1;
            surface.Opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, .1, 1) : 1;
        }
    }
}
