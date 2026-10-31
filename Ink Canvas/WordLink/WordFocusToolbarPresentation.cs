using System;
using System.Windows;

namespace Ink_Canvas.WordLink
{
    /// <summary>A temporary presentation layout that never overwrites desktop preferences.</summary>
    internal sealed class WordFocusToolbarPresentation : IDisposable
    {
        private readonly FrameworkElement _toolbar;
        private readonly FrameworkElement _opacitySurface;
        private readonly Func<Rect> _bounds;
        private readonly Func<double> _opacity;
        private readonly Func<bool> _canPosition;
        private readonly Action _cancelAnimations;
        private Thickness _savedMargin;
        private double _savedOpacity;
        private double _savedSurfaceOpacity;
        private bool _refreshing;
        internal bool IsActive { get; private set; }

        internal WordFocusToolbarPresentation(FrameworkElement toolbar, Func<Rect> bounds,
            Func<double> opacity, Action cancelAnimations, Func<bool> canPosition = null, FrameworkElement opacitySurface = null)
        {
            _toolbar = toolbar; _bounds = bounds; _opacity = opacity;
            _opacitySurface = opacitySurface ?? toolbar;
            _cancelAnimations = cancelAnimations; _canPosition = canPosition ?? (() => true);
            toolbar.SizeChanged += SizeChanged;
        }
        internal void Update(bool active)
        {
            if (active == IsActive) return;
            if (active)
            {
                _savedMargin = _toolbar.Margin; _savedOpacity = _toolbar.Opacity;
                _savedSurfaceOpacity = _opacitySurface.Opacity;
                _toolbar.BeginAnimation(UIElement.OpacityProperty, null);
                IsActive = true; Refresh();
            }
            else
            {
                IsActive = false;
                if (_canPosition()) { _cancelAnimations(); _toolbar.Margin = _savedMargin; }
                _toolbar.BeginAnimation(UIElement.OpacityProperty, null);
                _opacitySurface.Opacity = _savedSurfaceOpacity;
                _toolbar.Opacity = _savedOpacity;
            }
        }
        private void SizeChanged(object sender, SizeChangedEventArgs e) => Refresh();
        internal void Refresh()
        {
            if (!IsActive || _refreshing) return;
            _refreshing = true;
            try
            {
                double opacity = _opacity();
                if (_opacitySurface != _toolbar) _toolbar.Opacity = 1;
                _opacitySurface.Opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, .1, 1) : .5;
                if (!_canPosition()) return;
                Rect bounds = _bounds();
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
                var scaled = _toolbar.LayoutTransform.TransformBounds(new Rect(_toolbar.RenderSize));
                if (scaled.Width <= 0 || scaled.Height <= 0) return;
                double scaleY = _toolbar.RenderSize.Height > 0 ? scaled.Height / _toolbar.RenderSize.Height : 1;
                // Same two-DIP lower-edge overhang as the PPT floating toolbar.
                var margin = new Thickness(Math.Max(bounds.Left, bounds.Left + (bounds.Width - scaled.Width) / 2),
                    Math.Max(bounds.Top, bounds.Bottom - scaled.Height + 2 * scaleY), 0, -20);
                if (_toolbar.Margin == margin) return;
                _cancelAnimations(); _toolbar.Margin = margin;
            }
            finally { _refreshing = false; }
        }
        public void Dispose()
        {
            Update(false);
            _toolbar.SizeChanged -= SizeChanged;
        }
    }
}
