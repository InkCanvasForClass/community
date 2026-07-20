using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas.Controls
{
    public partial class BoardRoamingPopupContent : UserControl
    {
        private bool _isDragging;
        private Vector _dragOffset;
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
            PreviewCanvas.MouseLeave += PreviewCanvas_MouseLeave;
            PreviewCanvas.StylusDown += PreviewCanvas_StylusDown;
            PreviewCanvas.StylusMove += PreviewCanvas_StylusMove;
            PreviewCanvas.StylusUp += PreviewCanvas_StylusUp;
        }

        public void SetViewport(Rect viewport, Rect movementBounds, string hint)
        {
            _viewportMovementBounds = movementBounds;
            ViewportBorder.Width = Math.Max(1, Math.Min(PreviewCanvas.Width, viewport.Width));
            ViewportBorder.Height = Math.Max(1, Math.Min(PreviewCanvas.Height, viewport.Height));
            WpfCanvas.SetLeft(ViewportBorder, viewport.X);
            WpfCanvas.SetTop(ViewportBorder, viewport.Y);
            ScaleHintText.Text = hint ?? string.Empty;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(PreviewCanvas);
            BeginDrag(point, IsInsideViewport(point));
            if (!IsInsideViewport(point)) MoveDrag(point);
            e.Handled = true;
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            MoveDrag(e.GetPosition(PreviewCanvas));
            e.Handled = true;
        }

        private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
                EndDrag();
        }

        private void PreviewCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            var point = e.GetPosition(PreviewCanvas);
            BeginDrag(point, IsInsideViewport(point));
            if (!IsInsideViewport(point)) MoveDrag(point);
            e.Handled = true;
        }

        private void PreviewCanvas_StylusMove(object sender, StylusEventArgs e)
        {
            if (!_isDragging) return;
            MoveDrag(e.GetPosition(PreviewCanvas));
            e.Handled = true;
        }

        private void PreviewCanvas_StylusUp(object sender, StylusEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private bool IsInsideViewport(Point point)
        {
            var left = WpfCanvas.GetLeft(ViewportBorder);
            var top = WpfCanvas.GetTop(ViewportBorder);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            return new Rect(left, top, ViewportBorder.ActualWidth, ViewportBorder.ActualHeight).Contains(point);
        }

        private void BeginDrag(Point point, bool preservePointerOffset)
        {
            if (_isDragging) return;

            _isDragging = true;
            ViewportDragStarted?.Invoke();
            PreviewCanvas.CaptureMouse();
            PreviewCanvas.CaptureStylus();

            var left = WpfCanvas.GetLeft(ViewportBorder);
            var top = WpfCanvas.GetTop(ViewportBorder);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            _dragOffset = preservePointerOffset
                ? point - new Point(left, top)
                : new Vector(ViewportBorder.ActualWidth / 2, ViewportBorder.ActualHeight / 2);
        }

        private void MoveDrag(Point point)
        {
            var viewportWidth = ViewportBorder.ActualWidth > 0 ? ViewportBorder.ActualWidth : ViewportBorder.Width;
            var viewportHeight = ViewportBorder.ActualHeight > 0 ? ViewportBorder.ActualHeight : ViewportBorder.Height;
            var minX = _viewportMovementBounds.IsEmpty ? 0 : _viewportMovementBounds.X;
            var minY = _viewportMovementBounds.IsEmpty ? 0 : _viewportMovementBounds.Y;
            var maxX = _viewportMovementBounds.IsEmpty
                ? PreviewCanvas.Width - viewportWidth
                : _viewportMovementBounds.Right - viewportWidth;
            var maxY = _viewportMovementBounds.IsEmpty
                ? PreviewCanvas.Height - viewportHeight
                : _viewportMovementBounds.Bottom - viewportHeight;
            var x = Math.Max(minX, Math.Min(maxX, point.X - _dragOffset.X));
            var y = Math.Max(minY, Math.Min(maxY, point.Y - _dragOffset.Y));

            WpfCanvas.SetLeft(ViewportBorder, x);
            WpfCanvas.SetTop(ViewportBorder, y);
            ViewportPositionChanged?.Invoke(new Point(x, y));
        }

        private void EndDrag()
        {
            if (!_isDragging) return;

            _isDragging = false;
            PreviewCanvas.ReleaseMouseCapture();
            PreviewCanvas.ReleaseStylusCapture();
            ViewportDragCompleted?.Invoke();
        }
    }
}
