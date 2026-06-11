using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class GeometryButton : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(GeometryButton),
            new PropertyMetadata(string.Empty, OnLabelChanged));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (GeometryButton)d;
            if (button.LabelControl != null)
                button.LabelControl.Content = (string)e.NewValue;
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
            nameof(IconSource), typeof(ImageSource), typeof(GeometryButton),
            new PropertyMetadata(null, OnIconSourceChanged));

        private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (GeometryButton)d;
            if (button.ButtonImage != null)
                button.ButtonImage.Source = (ImageSource)e.NewValue;
        }

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public static readonly DependencyProperty IconRotationProperty = DependencyProperty.Register(
            nameof(IconRotation), typeof(double), typeof(GeometryButton),
            new PropertyMetadata(0.0, OnIconRotationChanged));

        private static void OnIconRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (GeometryButton)d;
            if (button.ButtonImage != null)
            {
                var angle = (double)e.NewValue;
                if (angle != 0)
                {
                    button.ButtonImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    button.ButtonImage.RenderTransform = new RotateTransform(angle);
                }
                else
                {
                    button.ButtonImage.RenderTransform = Transform.Identity;
                }
            }
        }

        public double IconRotation
        {
            get => (double)GetValue(IconRotationProperty);
            set => SetValue(IconRotationProperty, value);
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseButtonEventHandler ButtonMouseUp;

        public GeometryButton()
        {
            InitializeComponent();
        }

        private void ButtonPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
