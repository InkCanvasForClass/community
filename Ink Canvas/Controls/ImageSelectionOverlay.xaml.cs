using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ink_Canvas.Controls
{
    public enum ImageResizeCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public class ImageResizeDeltaEventArgs : EventArgs
    {
        public ImageResizeCorner Corner { get; }
        public Vector CanvasDelta { get; }
        public bool LockAspectRatio { get; }

        public ImageResizeDeltaEventArgs(ImageResizeCorner corner, Vector canvasDelta, bool lockAspect)
        {
            Corner = corner;
            CanvasDelta = canvasDelta;
            LockAspectRatio = lockAspect;
        }
    }

    public class ImageMoveDeltaEventArgs : EventArgs
    {
        public Vector CanvasDelta { get; }
        public ImageMoveDeltaEventArgs(Vector delta) { CanvasDelta = delta; }
    }

    public class ImageRotateDeltaEventArgs : EventArgs
    {
        public double AngleDelta { get; }
        public ImageRotateDeltaEventArgs(double angleDelta) { AngleDelta = angleDelta; }
    }

    public partial class ImageSelectionOverlay : UserControl
    {
        private const double HandleSize = 12;
        private const double RotationHandleSize = 14;
        private const double RotationHandleOffset = 28;

        public event EventHandler<ImageResizeDeltaEventArgs> ResizeDelta;
        public event EventHandler<ImageMoveDeltaEventArgs> MoveDelta;
        public event EventHandler<ImageRotateDeltaEventArgs> RotateDelta;
        public event EventHandler InteractionStarted;
        public event EventHandler InteractionEnded;

        public IInputElement CoordinateSource { get; set; }

        private Point _rotationCenterCanvas;
        private readonly RotateTransform _overlayRotation = new RotateTransform(0);

        private bool _isResizing;
        private bool _isRotating;
        private bool _isMoving;
        private ImageResizeCorner _activeCorner;
        private Point _lastPoint;
        private double _lastRotationAngle;

        public ImageSelectionOverlay()
        {
            InitializeComponent();
            RenderTransform = _overlayRotation;

            TopLeftHandle.MouseLeftButtonDown += (s, e) => BeginResize(ImageResizeCorner.TopLeft, e, TopLeftHandle);
            TopRightHandle.MouseLeftButtonDown += (s, e) => BeginResize(ImageResizeCorner.TopRight, e, TopRightHandle);
            BottomLeftHandle.MouseLeftButtonDown += (s, e) => BeginResize(ImageResizeCorner.BottomLeft, e, BottomLeftHandle);
            BottomRightHandle.MouseLeftButtonDown += (s, e) => BeginResize(ImageResizeCorner.BottomRight, e, BottomRightHandle);

            TopLeftHandle.MouseMove += ResizeMove;
            TopRightHandle.MouseMove += ResizeMove;
            BottomLeftHandle.MouseMove += ResizeMove;
            BottomRightHandle.MouseMove += ResizeMove;

            TopLeftHandle.MouseLeftButtonUp += EndResize;
            TopRightHandle.MouseLeftButtonUp += EndResize;
            BottomLeftHandle.MouseLeftButtonUp += EndResize;
            BottomRightHandle.MouseLeftButtonUp += EndResize;

            RotationHandle.MouseLeftButtonDown += BeginRotate;
            RotationHandle.MouseMove += RotateMove;
            RotationHandle.MouseLeftButtonUp += EndRotate;

            MoveSurface.MouseLeftButtonDown += BeginMove;
            MoveSurface.MouseMove += MoveMove;
            MoveSurface.MouseLeftButtonUp += EndMove;
        }

        /// <summary>
        /// Position overlay so its logical rect (width × height) is centered at centerCanvas,
        /// then rotated by rotationAngleDegrees around that center to match the target element.
        /// </summary>
        public void UpdateFrame(Point centerCanvas, double width, double height, double rotationAngleDegrees)
        {
            if (width <= 0 || height <= 0) return;

            _rotationCenterCanvas = centerCanvas;

            double left = centerCanvas.X - width / 2;
            double top = centerCanvas.Y - height / 2;
            Margin = new Thickness(left, top, 0, 0);
            Width = width;
            Height = height;

            RenderTransformOrigin = new Point(0, 0);
            _overlayRotation.Angle = rotationAngleDegrees;
            _overlayRotation.CenterX = width / 2;
            _overlayRotation.CenterY = height / 2;

            FrameBorder.Width = width;
            FrameBorder.Height = height;
            System.Windows.Controls.Canvas.SetLeft(FrameBorder, 0);
            System.Windows.Controls.Canvas.SetTop(FrameBorder, 0);

            MoveSurface.Width = width;
            MoveSurface.Height = height;
            System.Windows.Controls.Canvas.SetLeft(MoveSurface, 0);
            System.Windows.Controls.Canvas.SetTop(MoveSurface, 0);

            double h = HandleSize / 2;
            System.Windows.Controls.Canvas.SetLeft(TopLeftHandle, -h);
            System.Windows.Controls.Canvas.SetTop(TopLeftHandle, -h);
            System.Windows.Controls.Canvas.SetLeft(TopRightHandle, width - h);
            System.Windows.Controls.Canvas.SetTop(TopRightHandle, -h);
            System.Windows.Controls.Canvas.SetLeft(BottomLeftHandle, -h);
            System.Windows.Controls.Canvas.SetTop(BottomLeftHandle, height - h);
            System.Windows.Controls.Canvas.SetLeft(BottomRightHandle, width - h);
            System.Windows.Controls.Canvas.SetTop(BottomRightHandle, height - h);

            double rh = RotationHandleSize / 2;
            double midX = width / 2;
            System.Windows.Controls.Canvas.SetLeft(RotationHandle, midX - rh);
            System.Windows.Controls.Canvas.SetTop(RotationHandle, -RotationHandleOffset - rh);

            RotationLine.X1 = midX;
            RotationLine.Y1 = 0;
            RotationLine.X2 = midX;
            RotationLine.Y2 = -RotationHandleOffset;
        }

        private IInputElement GetSource() => CoordinateSource ?? (IInputElement)Parent;

        private void BeginResize(ImageResizeCorner corner, MouseButtonEventArgs e, Ellipse handle)
        {
            var source = GetSource();
            if (source == null) return;
            _isResizing = true;
            _activeCorner = corner;
            _lastPoint = e.GetPosition(source);
            handle.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void ResizeMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing || !(sender is Ellipse handle) || !handle.IsMouseCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var current = e.GetPosition(source);
            var delta = current - _lastPoint;
            bool lockAspect = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            ResizeDelta?.Invoke(this, new ImageResizeDeltaEventArgs(_activeCorner, delta, lockAspect));
            _lastPoint = current;
            e.Handled = true;
        }

        private void EndResize(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing) return;
            if (sender is Ellipse handle) handle.ReleaseMouseCapture();
            _isResizing = false;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginRotate(object sender, MouseButtonEventArgs e)
        {
            var source = GetSource();
            if (source == null) return;
            _isRotating = true;
            var p = e.GetPosition(source);
            _lastRotationAngle = AngleFromCenter(p);
            RotationHandle.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void RotateMove(object sender, MouseEventArgs e)
        {
            if (!_isRotating || !RotationHandle.IsMouseCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var p = e.GetPosition(source);
            double angle = AngleFromCenter(p);
            double delta = angle - _lastRotationAngle;
            if (delta > 180) delta -= 360;
            else if (delta < -180) delta += 360;
            _lastRotationAngle = angle;
            RotateDelta?.Invoke(this, new ImageRotateDeltaEventArgs(delta));
            e.Handled = true;
        }

        private void EndRotate(object sender, MouseButtonEventArgs e)
        {
            if (!_isRotating) return;
            RotationHandle.ReleaseMouseCapture();
            _isRotating = false;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginMove(object sender, MouseButtonEventArgs e)
        {
            var source = GetSource();
            if (source == null) return;
            _isMoving = true;
            _lastPoint = e.GetPosition(source);
            MoveSurface.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void MoveMove(object sender, MouseEventArgs e)
        {
            if (!_isMoving || !MoveSurface.IsMouseCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var current = e.GetPosition(source);
            var delta = current - _lastPoint;
            _lastPoint = current;
            MoveDelta?.Invoke(this, new ImageMoveDeltaEventArgs(delta));
            e.Handled = true;
        }

        private void EndMove(object sender, MouseButtonEventArgs e)
        {
            if (!_isMoving) return;
            MoveSurface.ReleaseMouseCapture();
            _isMoving = false;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private double AngleFromCenter(Point p)
        {
            double dx = p.X - _rotationCenterCanvas.X;
            double dy = p.Y - _rotationCenterCanvas.Y;
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }
    }
}