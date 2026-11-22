using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas.Controls
{
    public partial class FloatingFoldMenu : UserControl
    {
        private const double ToolRadius = 86;
        private const double OpenStartScale = 0.23;
        private const double OpenRotationDegrees = -70;
        private const int OpenDurationMs = 360;
        private const int OpenFadeMs = 210;
        private const int CloseDurationMs = 220;

        private int _transitionVersion;
        internal bool IsOpen { get; private set; }

        public FloatingFoldMenu()
        {
            InitializeComponent();
            ArrangeButtons();
        }

        internal void SetExitShowVisibility(Visibility visibility)
        {
            ExitShow.Visibility = visibility;
            ArrangeButtons();
        }

        private void ArrangeButtons()
        {
            var buttons = Tools.Children.OfType<Button>().Where(button => button.Visibility == Visibility.Visible).ToArray();
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                double angle = 2 * Math.PI * i / buttons.Length;
                WpfCanvas.SetLeft(button, Width / 2 + ToolRadius * Math.Sin(angle) - button.Width / 2);
                WpfCanvas.SetTop(button, Height / 2 - ToolRadius * Math.Cos(angle) - button.Height / 2);
            }
        }

        internal Vector Place(Point anchor, Size bounds)
        {
            double scale = Math.Min(1, Math.Min(bounds.Width / Width, bounds.Height / Height));
            var fit = (ScaleTransform)LayoutTransform;
            fit.ScaleX = fit.ScaleY = scale;
            var size = new Size(Width * scale, Height * scale);
            var half = new Vector(size.Width / 2, size.Height / 2);
            var origin = FloatingFoldBall.ClampPosition(anchor - half, bounds, size);
            Margin = new Thickness(origin.X, origin.Y, 0, 0);
            return origin + half - anchor;
        }

        internal void PopOpen(bool animate)
        {
            bool wasCollapsed = Visibility == Visibility.Collapsed;
            ++_transitionVersion;
            IsOpen = true;
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;
            if (!animate)
            {
                ResetAnimation();
                return;
            }

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            AnimateTransform(wasCollapsed ? OpenStartScale : PopScale.ScaleX, 1,
                wasCollapsed ? OpenRotationDegrees : PopRotation.Angle, 0, OpenDurationMs, easing);
            Ring.BeginAnimation(OpacityProperty, new DoubleAnimation(wasCollapsed ? 0 : Ring.Opacity, 1,
                TimeSpan.FromMilliseconds(OpenFadeMs)) { EasingFunction = easing });
        }

        internal void Close(bool animate, Action onClosed)
        {
            int version = ++_transitionVersion;
            IsOpen = false;
            IsHitTestVisible = false;
            if (!animate || Visibility == Visibility.Collapsed)
            {
                FinishClose(onClosed);
                return;
            }

            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
            AnimateTransform(PopScale.ScaleX, OpenStartScale, PopRotation.Angle, OpenRotationDegrees, CloseDurationMs, easing);
            var fade = new DoubleAnimation(Ring.Opacity, 0, TimeSpan.FromMilliseconds(CloseDurationMs))
                { EasingFunction = easing };
            fade.Completed += (_, _) =>
            {
                if (version == _transitionVersion) FinishClose(onClosed);
            };
            Ring.BeginAnimation(OpacityProperty, fade);
        }

        private void FinishClose(Action onClosed)
        {
            Visibility = Visibility.Collapsed;
            ResetAnimation();
            onClosed();
        }

        private void AnimateTransform(double fromScale, double toScale, double fromAngle, double toAngle,
            int milliseconds, IEasingFunction easing)
        {
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var scale = new DoubleAnimation(fromScale, toScale, duration) { EasingFunction = easing };
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            PopRotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(fromAngle, toAngle, duration) { EasingFunction = easing });
        }

        private void ResetAnimation()
        {
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PopRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            Ring.BeginAnimation(OpacityProperty, null);
        }
    }
}
