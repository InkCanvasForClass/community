using System;
using System.Runtime.InteropServices;
using System.Windows;
using WpfCanvas = System.Windows.Controls.Canvas;
using System.Windows.Threading;

namespace Ink_Canvas.WordLink
{
    /// <summary>Follow native window geometry without entering Office or waiting for ink queries.</summary>
    internal sealed class WordToolbarPlacement : IDisposable
    {
        private readonly WpfCanvas _overlay;
        private readonly FrameworkElement _left, _right;
        private readonly Window _owner;
        private readonly Func<long, Rect?> _bounds;
        private long _target;
        private bool _queued, _disposed;
        private bool _followWindow = true;
        private double _horizontalInset = 6, _bottomInset = 6;
        private static readonly Point Hidden = new(-10000, -10000);

        internal WordToolbarPlacement(WpfCanvas overlay, FrameworkElement left, FrameworkElement right,
            Func<long, Rect?> bounds = null)
        {
            _overlay = overlay; _left = left; _right = right;
            _bounds = bounds ?? ReadWindowBounds;
            _overlay.SizeChanged += SizeChanged;
            _left.SizeChanged += SizeChanged; _right.SizeChanged += SizeChanged;
            _owner = Window.GetWindow(overlay);
            if (_owner != null)
            {
                _owner.LocationChanged += OwnerMoved;
                _owner.DpiChanged += OwnerDpiChanged;
            }
        }

        internal void Update(long window)
        {
            if (_disposed) return;
            _target = window;
            Queue();
        }
        internal void Configure(bool followWindow, double horizontalInset, double bottomInset)
        {
            _followWindow = followWindow;
            _horizontalInset = double.IsFinite(horizontalInset) ? Math.Clamp(horizontalInset, 0, 300) : 6;
            _bottomInset = double.IsFinite(bottomInset) ? Math.Clamp(bottomInset, 0, 200) : 6;
            Queue();
        }
        private void SizeChanged(object sender, SizeChangedEventArgs e) => Queue();
        private void OwnerMoved(object sender, EventArgs e) => Queue();
        private void OwnerDpiChanged(object sender, DpiChangedEventArgs e) => Queue();
        private void Queue()
        {
            if (_disposed || _queued) return;
            _queued = true;
            _overlay.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _queued = false;
                if (!_disposed) Refresh();
            }));
        }
        private void Refresh()
        {
            var positions = (Left: Hidden, Right: Hidden);
            if (_target != 0 && PresentationSource.FromVisual(_overlay) != null && _bounds(_target) is { } screen)
            {
                // PointFromScreen converts physical pixels to this overlay's DIPs,
                // including negative monitor coordinates and per-monitor DPI.
                var visible = _followWindow
                    ? new Rect(_overlay.PointFromScreen(screen.TopLeft), _overlay.PointFromScreen(screen.BottomRight))
                    : new Rect(_overlay.RenderSize);
                visible.Intersect(new Rect(_overlay.RenderSize));
                positions = Calculate(visible, _left.RenderSize, _right.RenderSize, _horizontalInset, _bottomInset);
            }
            Place(_left, positions.Left); Place(_right, positions.Right);
        }
        private static void Place(FrameworkElement element, Point point)
        {
            // Avoid invalidating layout for repeated stationary view events.
            if (WpfCanvas.GetLeft(element) != point.X) WpfCanvas.SetLeft(element, point.X);
            if (WpfCanvas.GetTop(element) != point.Y) WpfCanvas.SetTop(element, point.Y);
        }

        internal static (Point Left, Point Right) Calculate(Rect visible, Size left, Size right,
            double horizontalInset = 6, double bottomInset = 6)
        {
            if (visible.IsEmpty) return (Hidden, Hidden);
            double xInset = Math.Min(horizontalInset, Math.Max(0, (visible.Width - Math.Max(left.Width, right.Width)) / 2));
            double yInset = Math.Min(bottomInset, Math.Max(0, visible.Height - Math.Max(left.Height, right.Height) - 6));
            bool Fits(Size size) => size.Width > 0 && size.Height > 0
                && size.Width + 2 * xInset <= visible.Width && size.Height + 6 <= visible.Height;
            Point lp = Fits(left) ? new Point(visible.Left + xInset, visible.Bottom - left.Height - yInset) : Hidden;
            Point rp = Fits(right) ? new Point(visible.Right - right.Width - xInset, visible.Bottom - right.Height - yInset) : Hidden;
            if (lp != Hidden && rp != Hidden && lp.X + left.Width + 6 > rp.X)
            {
                // Preserve touch target sizes in a narrow/split Word window.
                double above = rp.Y - left.Height - 6;
                lp = above >= visible.Top + 6 ? new Point(lp.X, above) : Hidden;
            }
            return (lp, rp);
        }

        private static Rect? ReadWindowBounds(long target)
        {
            var hwnd = new IntPtr(target);
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return null;
            // DWM excludes invisible resize borders; fall back for older themes.
            if (DwmGetWindowAttribute(hwnd, 9, out var bounds, Marshal.SizeOf<NativeRect>()) != 0
                && !GetWindowRect(hwnd, out bounds)) return null;
            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top
                ? new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top) : null;
        }
        public void Dispose()
        {
            _disposed = true;
            _overlay.SizeChanged -= SizeChanged;
            _left.SizeChanged -= SizeChanged; _right.SizeChanged -= SizeChanged;
            if (_owner != null)
            {
                _owner.LocationChanged -= OwnerMoved;
                _owner.DpiChanged -= OwnerDpiChanged;
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect bounds);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect bounds, int size);
    }
}
