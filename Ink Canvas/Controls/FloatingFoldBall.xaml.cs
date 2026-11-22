using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Controls
{
    public partial class FloatingFoldBall : UserControl
    {
        public event RoutedEventHandler Click;
        public event EventHandler PositionChanged;
        public event EventHandler DragMoved;
        internal bool IsPointerPressed => _pressed;

        private Point _pointerStart;
        private Point _positionStart;
        private bool _pressed;
        private bool _dragged;
        private TouchDevice _touch;

        public FloatingFoldBall()
        {
            InitializeComponent();
            IsVisibleChanged += (_, _) =>
            {
                if (!IsVisible)
                {
                    EndDrag(true);
                    ReleaseMouseCapture();
                    _touch?.Capture(null);
                    _touch = null;
                }
            };
        }

        internal static Point ClampPosition(Point point, Size bounds, Size size) => new Point(
            Math.Clamp(double.IsFinite(point.X) ? point.X : 0, 0, Math.Max(0, bounds.Width - size.Width)),
            Math.Clamp(double.IsFinite(point.Y) ? point.Y : 0, 0, Math.Max(0, bounds.Height - size.Height)));

        public void SetPosition(Point point)
        {
            // 首次布局前也保留配置位置，待宿主取得尺寸后再约束到可见区域。
            if (Parent is FrameworkElement parent && parent.ActualWidth > 0 && parent.ActualHeight > 0)
                point = ClampPosition(point, parent.RenderSize, new Size(Width, Height));
            else
                point = new Point(double.IsFinite(point.X) ? Math.Max(0, point.X) : 0,
                    double.IsFinite(point.Y) ? Math.Max(0, point.Y) : 0);
            Margin = new Thickness(point.X, point.Y, 0, 0);
        }

        internal void ShowMenuState(bool isOpen, Vector offset)
        {
            MenuOffset.X = offset.X;
            MenuOffset.Y = offset.Y;
            PenIcon.Visibility = isOpen ? Visibility.Collapsed : Visibility.Visible;
            CloseIcon.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BeginDrag(Point pointer)
        {
            _pointerStart = pointer;
            _positionStart = new Point(Margin.Left + MenuOffset.X, Margin.Top + MenuOffset.Y);
            _pressed = true;
            _dragged = false;
        }

        private void MoveDrag(Point pointer)
        {
            if (!_pressed) return;
            var delta = pointer - _pointerStart;
            if (!_dragged && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragged = true;
            SetPosition(_positionStart + delta);
            DragMoved?.Invoke(this, EventArgs.Empty);
        }

        private void EndDrag(bool cancelled)
        {
            if (!_pressed) return;
            _pressed = false;
            if (_dragged) PositionChanged?.Invoke(this, EventArgs.Empty);
            else if (!cancelled) Click?.Invoke(this, new RoutedEventArgs());
            _dragged = false;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_touch != null) return;
            if (CaptureMouse()) BeginDrag(e.GetPosition(Parent as IInputElement));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_touch == null && _pressed)
            {
                MoveDrag(e.GetPosition(Parent as IInputElement));
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_touch != null) return;
            MoveDrag(e.GetPosition(Parent as IInputElement));
            EndDrag(false);
            ReleaseMouseCapture();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (_touch == null) EndDrag(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key != Key.Enter && e.Key != Key.Space) return;
            if (!e.IsRepeat) Click?.Invoke(this, new RoutedEventArgs());
            e.Handled = true;
        }

        protected override void OnTouchDown(TouchEventArgs e)
        {
            base.OnTouchDown(e);
            e.Handled = true;
            if (_pressed || _touch != null) return;
            if (e.TouchDevice.Capture(this))
            {
                _touch = e.TouchDevice;
                BeginDrag(e.GetTouchPoint(Parent as IInputElement).Position);
            }
        }

        protected override void OnTouchMove(TouchEventArgs e)
        {
            base.OnTouchMove(e);
            e.Handled = true;
            if (e.TouchDevice == _touch) MoveDrag(e.GetTouchPoint(Parent as IInputElement).Position);
        }

        protected override void OnTouchUp(TouchEventArgs e)
        {
            base.OnTouchUp(e);
            e.Handled = true;
            if (e.TouchDevice != _touch) return;
            MoveDrag(e.GetTouchPoint(Parent as IInputElement).Position);
            EndDrag(false);
            _touch = null;
            e.TouchDevice.Capture(null);
        }

        protected override void OnLostTouchCapture(TouchEventArgs e)
        {
            base.OnLostTouchCapture(e);
            if (e.TouchDevice != _touch) return;
            EndDrag(true);
            _touch = null;
        }
    }
}
