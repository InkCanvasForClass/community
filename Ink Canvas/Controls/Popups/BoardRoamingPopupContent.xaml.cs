using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas.Controls
{
    public partial class BoardRoamingPopupContent : UserControl
    {
        private enum DragInputDevice
        {
            None,
            Mouse,
            Stylus
        }

        private bool _isDragging;
        private DragInputDevice _dragInputDevice;
        private Point _lastDragPoint;
        private Rect _viewportMovementBounds;

        public event Action<Point> ViewportPositionChanged;
        public event Action ViewportDragStarted;
        public event Action ViewportDragCompleted;

        public Image PreviewImageControl => PreviewImage;
        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public BoardRoamingPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;

            PreviewCanvas.MouseLeftButtonDown += PreviewCanvas_MouseLeftButtonDown;
            PreviewCanvas.MouseMove += PreviewCanvas_MouseMove;
            PreviewCanvas.MouseLeftButtonUp += PreviewCanvas_MouseLeftButtonUp;
            PreviewCanvas.LostMouseCapture += PreviewCanvas_LostMouseCapture;
            PreviewCanvas.StylusDown += PreviewCanvas_StylusDown;
            PreviewCanvas.StylusMove += PreviewCanvas_StylusMove;
            PreviewCanvas.StylusUp += PreviewCanvas_StylusUp;
            PreviewCanvas.LostStylusCapture += PreviewCanvas_LostStylusCapture;
            Unloaded += (s, e) => EndDrag();
        }

        internal void CompleteViewportDrag() => EndDrag();

        public void SetViewport(Rect viewport, Rect movementBounds, string hint)
        {
            if (viewport.IsEmpty || movementBounds.IsEmpty
                || !double.IsFinite(viewport.X) || !double.IsFinite(viewport.Y)
                || !double.IsFinite(viewport.Width) || !double.IsFinite(viewport.Height)
                || !double.IsFinite(movementBounds.X) || !double.IsFinite(movementBounds.Y)
                || !double.IsFinite(movementBounds.Width) || !double.IsFinite(movementBounds.Height)
                || viewport.Width <= 0 || viewport.Height <= 0
                || movementBounds.Width <= 0 || movementBounds.Height <= 0) return;

            _viewportMovementBounds = movementBounds;
            ViewportBorder.Width = Math.Max(1, Math.Min(PreviewCanvas.Width, viewport.Width));
            ViewportBorder.Height = Math.Max(1, Math.Min(PreviewCanvas.Height, viewport.Height));
            WpfCanvas.SetLeft(ViewportBorder, viewport.X);
            WpfCanvas.SetTop(ViewportBorder, viewport.Y);
            ScaleHintText.Text = hint ?? string.Empty;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginDrag(e.GetPosition(PreviewCanvas), DragInputDevice.Mouse);
            e.Handled = true;
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragInputDevice != DragInputDevice.Mouse || !PreviewCanvas.IsMouseCaptured) return;
            MoveDrag(e.GetPosition(PreviewCanvas));
            e.Handled = true;
        }

        private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragInputDevice == DragInputDevice.Mouse)
                EndDrag();
            e.Handled = true;
        }

        private void PreviewCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragInputDevice == DragInputDevice.Mouse)
                EndDrag();
        }

        private void PreviewCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            BeginDrag(e.GetPosition(PreviewCanvas), DragInputDevice.Stylus);
            e.Handled = true;
        }

        private void PreviewCanvas_StylusMove(object sender, StylusEventArgs e)
        {
            if (_dragInputDevice != DragInputDevice.Stylus || !PreviewCanvas.IsStylusCaptured) return;
            MoveDrag(e.GetPosition(PreviewCanvas));
            e.Handled = true;
        }

        private void PreviewCanvas_StylusUp(object sender, StylusEventArgs e)
        {
            if (_dragInputDevice == DragInputDevice.Stylus)
                EndDrag();
            e.Handled = true;
        }

        private void PreviewCanvas_LostStylusCapture(object sender, StylusEventArgs e)
        {
            if (_dragInputDevice == DragInputDevice.Stylus)
                EndDrag();
        }


        private bool IsInsideViewport(Point point)
        {
            var left = WpfCanvas.GetLeft(ViewportBorder);
            var top = WpfCanvas.GetTop(ViewportBorder);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            return new Rect(left, top, GetViewportWidth(), GetViewportHeight()).Contains(point);
        }

        private void BeginDrag(Point point, DragInputDevice inputDevice)
        {
            if (_isDragging || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;

            _isDragging = true;
            _dragInputDevice = inputDevice;
            _lastDragPoint = point;
            ViewportDragStarted?.Invoke();
            if (!IsInsideViewport(point))
            {
                SetViewportPosition(
                    point.X - GetViewportWidth() / 2,
                    point.Y - GetViewportHeight() / 2);
            }

            var captured = inputDevice == DragInputDevice.Mouse
                ? PreviewCanvas.CaptureMouse()
                : PreviewCanvas.CaptureStylus();
            if (!captured) EndDrag();
        }

        private void MoveDrag(Point point)
        {
            if (!_isDragging) return;

            var delta = point - _lastDragPoint;
            if (delta.X != 0 || delta.Y != 0)
            {
                var left = WpfCanvas.GetLeft(ViewportBorder);
                var top = WpfCanvas.GetTop(ViewportBorder);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                SetViewportPosition(left + delta.X, top + delta.Y);
            }

            _lastDragPoint = point;
        }

        private void SetViewportPosition(double left, double top)
        {
            if (!double.IsFinite(left) || !double.IsFinite(top)) return;
            var viewportWidth = GetViewportWidth();
            var viewportHeight = GetViewportHeight();
            var minX = _viewportMovementBounds.IsEmpty ? 0 : _viewportMovementBounds.X;
            var minY = _viewportMovementBounds.IsEmpty ? 0 : _viewportMovementBounds.Y;
            var maxX = _viewportMovementBounds.IsEmpty
                ? PreviewCanvas.Width - viewportWidth
                : _viewportMovementBounds.Right - viewportWidth;
            var maxY = _viewportMovementBounds.IsEmpty
                ? PreviewCanvas.Height - viewportHeight
                : _viewportMovementBounds.Bottom - viewportHeight;
            var x = Math.Max(minX, Math.Min(maxX, left));
            var y = Math.Max(minY, Math.Min(maxY, top));

            WpfCanvas.SetLeft(ViewportBorder, x);
            WpfCanvas.SetTop(ViewportBorder, y);
            ViewportPositionChanged?.Invoke(new Point(x, y));
        }

        private double GetViewportWidth()
            => double.IsFinite(ViewportBorder.Width) ? ViewportBorder.Width : ViewportBorder.ActualWidth;

        private double GetViewportHeight()
            => double.IsFinite(ViewportBorder.Height) ? ViewportBorder.Height : ViewportBorder.ActualHeight;

        private void EndDrag()
        {
            if (!_isDragging) return;

            _isDragging = false;
            _dragInputDevice = DragInputDevice.None;
            if (PreviewCanvas.IsMouseCaptured)
                PreviewCanvas.ReleaseMouseCapture();
            if (PreviewCanvas.IsStylusCaptured)
                PreviewCanvas.ReleaseStylusCapture();
            ViewportDragCompleted?.Invoke();
        }
    }
}
