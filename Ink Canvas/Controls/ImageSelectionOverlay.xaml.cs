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

    public enum SelectionOverlayMode
    {
        Element,
        Stroke
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
        // RotationHandle 的命中区是 XAML 中 28×28 的 Grid（内部才是 14×14 的视觉圆点）。
        // 定位必须用命中区尺寸取中心：若误用视觉点尺寸的一半去定位 Grid 左上角，
        // Grid（连同内部圆点）的中心会相对连线端点右偏/下偏各 7px —— 即"旋转手柄与连线错位"。
        // 注意：修改 ImageSelectionOverlay.xaml 中该 Grid 的 Width/Height 时必须同步此常量。
        private const double RotationHandleHitAreaSize = 28;
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

        // 触摸与鼠标互斥状态，防止触摸屏上系统同时派发 Touch 事件和合成的虚拟 Mouse 事件导致抖动/抽搐
        private int? _activeTouchDeviceId;
        private bool _isMouseInteracting;

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

            TopLeftHandle.TouchDown += (s, e) => BeginResizeTouch(ImageResizeCorner.TopLeft, e, TopLeftHandle);
            TopRightHandle.TouchDown += (s, e) => BeginResizeTouch(ImageResizeCorner.TopRight, e, TopRightHandle);
            BottomLeftHandle.TouchDown += (s, e) => BeginResizeTouch(ImageResizeCorner.BottomLeft, e, BottomLeftHandle);
            BottomRightHandle.TouchDown += (s, e) => BeginResizeTouch(ImageResizeCorner.BottomRight, e, BottomRightHandle);

            TopLeftHandle.TouchMove += ResizeMoveTouch;
            TopRightHandle.TouchMove += ResizeMoveTouch;
            BottomLeftHandle.TouchMove += ResizeMoveTouch;
            BottomRightHandle.TouchMove += ResizeMoveTouch;

            TopLeftHandle.TouchUp += EndResizeTouch;
            TopRightHandle.TouchUp += EndResizeTouch;
            BottomLeftHandle.TouchUp += EndResizeTouch;
            BottomRightHandle.TouchUp += EndResizeTouch;

            RotationHandle.TouchDown += BeginRotateTouch;
            RotationHandle.TouchMove += RotateMoveTouch;
            RotationHandle.TouchUp += EndRotateTouch;

            MoveSurface.TouchDown += BeginMoveTouch;
            MoveSurface.TouchMove += MoveMoveTouch;
            MoveSurface.TouchUp += EndMoveTouch;
        }

        /// <summary>
        /// Position overlay so its logical rect (width × height) is centered at centerCanvas,
        /// then rotated by rotationAngleDegrees around that center to match the target element.
        /// </summary>
        public void SetMode(SelectionOverlayMode mode)
        {
            bool isElementMode = true;
            RotationHandle.Visibility = Visibility.Visible;
            RotationLine.Visibility = Visibility.Visible;
            MoveSurface.Visibility = Visibility.Visible;

            OverlayRoot.Background = isElementMode ? Brushes.Transparent : null;
            MoveSurface.IsHitTestVisible = isElementMode;
            FrameBorder.Visibility = isElementMode ? Visibility.Visible : Visibility.Collapsed;
            TopLeftHandle.Visibility = isElementMode ? Visibility.Visible : Visibility.Collapsed;
            TopRightHandle.Visibility = isElementMode ? Visibility.Visible : Visibility.Collapsed;
            BottomLeftHandle.Visibility = isElementMode ? Visibility.Visible : Visibility.Collapsed;
            BottomRightHandle.Visibility = isElementMode ? Visibility.Visible : Visibility.Collapsed;
        }

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

            double rh = RotationHandleHitAreaSize / 2;
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
            if (_activeTouchDeviceId.HasValue) return; // 忽略触摸提升的合成鼠标点击
            var source = GetSource();
            if (source == null) return;
            _isMouseInteracting = true;
            _isResizing = true;
            _activeCorner = corner;
            _lastPoint = e.GetPosition(source);
            handle.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void ResizeMove(object sender, MouseEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
            if (!_isResizing || !(sender is Ellipse handle) || !handle.IsMouseCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var current = e.GetPosition(source);
            var delta = current - _lastPoint;
            bool lockAspect = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            _lastPoint = current;
            ResizeDelta?.Invoke(this, new ImageResizeDeltaEventArgs(_activeCorner, delta, lockAspect));
            e.Handled = true;
        }

        private void EndResize(object sender, MouseButtonEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
            if (!_isResizing) return;
            if (sender is Ellipse handle) handle.ReleaseMouseCapture();
            _isResizing = false;
            _isMouseInteracting = false;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginRotate(object sender, MouseButtonEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
            var source = GetSource();
            if (source == null) return;
            _isMouseInteracting = true;
            _isRotating = true;
            var p = e.GetPosition(source);
            _lastRotationAngle = AngleFromCenter(p);
            RotationHandle.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void RotateMove(object sender, MouseEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
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
            if (_activeTouchDeviceId.HasValue) return;
            if (!_isRotating) return;
            RotationHandle.ReleaseMouseCapture();
            _isRotating = false;
            _isMouseInteracting = false;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginResizeTouch(ImageResizeCorner corner, TouchEventArgs e, Ellipse handle)
        {
            if (_isMouseInteracting) return;
            var source = GetSource();
            if (source == null) return;
            _activeTouchDeviceId = e.TouchDevice.Id;
            _isResizing = true;
            _activeCorner = corner;
            _lastPoint = e.GetTouchPoint(source).Position;
            handle.CaptureTouch(e.TouchDevice);
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void ResizeMoveTouch(object sender, TouchEventArgs e)
        {
            if (!_isResizing || _activeTouchDeviceId != e.TouchDevice.Id) return;
            if (!(sender is Ellipse handle) || !handle.AreAnyTouchesCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var current = e.GetTouchPoint(source).Position;
            var delta = current - _lastPoint;
            bool lockAspect = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            _lastPoint = current;
            ResizeDelta?.Invoke(this, new ImageResizeDeltaEventArgs(_activeCorner, delta, lockAspect));
            e.Handled = true;
        }

        private void EndResizeTouch(object sender, TouchEventArgs e)
        {
            if (!_isResizing || _activeTouchDeviceId != e.TouchDevice.Id) return;
            if (sender is Ellipse handle) handle.ReleaseTouchCapture(e.TouchDevice);
            _isResizing = false;
            _activeTouchDeviceId = null;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginRotateTouch(object sender, TouchEventArgs e)
        {
            if (_isMouseInteracting) return;
            var source = GetSource();
            if (source == null) return;
            _activeTouchDeviceId = e.TouchDevice.Id;
            _isRotating = true;
            var p = e.GetTouchPoint(source).Position;
            _lastRotationAngle = AngleFromCenter(p);
            RotationHandle.CaptureTouch(e.TouchDevice);
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void RotateMoveTouch(object sender, TouchEventArgs e)
        {
            if (!_isRotating || _activeTouchDeviceId != e.TouchDevice.Id) return;
            if (!RotationHandle.AreAnyTouchesCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var p = e.GetTouchPoint(source).Position;
            double angle = AngleFromCenter(p);
            double delta = angle - _lastRotationAngle;
            if (delta > 180) delta -= 360;
            else if (delta < -180) delta += 360;
            _lastRotationAngle = angle;
            RotateDelta?.Invoke(this, new ImageRotateDeltaEventArgs(delta));
            e.Handled = true;
        }

        private void EndRotateTouch(object sender, TouchEventArgs e)
        {
            if (!_isRotating || _activeTouchDeviceId != e.TouchDevice.Id) return;
            RotationHandle.ReleaseTouchCapture(e.TouchDevice);
            _isRotating = false;
            _activeTouchDeviceId = null;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginMoveTouch(object sender, TouchEventArgs e)
        {
            if (_isMouseInteracting) return;
            var source = GetSource();
            if (source == null) return;
            _activeTouchDeviceId = e.TouchDevice.Id;
            _isMoving = true;
            _lastPoint = e.GetTouchPoint(source).Position;
            MoveSurface.CaptureTouch(e.TouchDevice);
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void MoveMoveTouch(object sender, TouchEventArgs e)
        {
            if (!_isMoving || _activeTouchDeviceId != e.TouchDevice.Id) return;
            if (!MoveSurface.AreAnyTouchesCaptured) return;
            var source = GetSource();
            if (source == null) return;
            var current = e.GetTouchPoint(source).Position;
            var delta = current - _lastPoint;
            _lastPoint = current;
            MoveDelta?.Invoke(this, new ImageMoveDeltaEventArgs(delta));
            e.Handled = true;
        }

        private void EndMoveTouch(object sender, TouchEventArgs e)
        {
            if (!_isMoving || _activeTouchDeviceId != e.TouchDevice.Id) return;
            MoveSurface.ReleaseTouchCapture(e.TouchDevice);
            _isMoving = false;
            _activeTouchDeviceId = null;
            InteractionEnded?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void BeginMove(object sender, MouseButtonEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
            var source = GetSource();
            if (source == null) return;
            _isMouseInteracting = true;
            _isMoving = true;
            _lastPoint = e.GetPosition(source);
            MoveSurface.CaptureMouse();
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void MoveMove(object sender, MouseEventArgs e)
        {
            if (_activeTouchDeviceId.HasValue) return;
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
            if (_activeTouchDeviceId.HasValue) return;
            if (!_isMoving) return;
            MoveSurface.ReleaseMouseCapture();
            _isMoving = false;
            _isMouseInteracting = false;
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
