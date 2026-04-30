using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class CircularColorButton : UserControl
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color), typeof(Color), typeof(CircularColorButton),
            new PropertyMetadata(Colors.Transparent, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.ColorOverlay != null)
                button.ColorOverlay.Background = new SolidColorBrush((Color)e.NewValue);
        }

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty ColorOpacityProperty = DependencyProperty.Register(
            nameof(ColorOpacity), typeof(double), typeof(CircularColorButton),
            new PropertyMetadata(0.75, OnColorOpacityChanged));

        private static void OnColorOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.ColorOverlay != null)
                button.ColorOverlay.Opacity = (double)e.NewValue;
        }

        public double ColorOpacity
        {
            get => (double)GetValue(ColorOpacityProperty);
            set => SetValue(ColorOpacityProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked), typeof(bool), typeof(CircularColorButton),
            new PropertyMetadata(true, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.CheckViewbox != null)
                button.CheckViewbox.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty ButtonSizeProperty = DependencyProperty.Register(
            nameof(ButtonSize), typeof(double), typeof(CircularColorButton),
            new PropertyMetadata(45.0, OnButtonSizeChanged));

        private static void OnButtonSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.ButtonBorder == null || button.ColorOverlay == null) return;

            var size = (double)e.NewValue;
            button.ButtonBorder.Width = size;
            button.ButtonBorder.Height = size;
            var radius = (size - 3) / 2;
            button.ColorOverlay.Width = radius * 2;
            button.ColorOverlay.Height = radius * 2;
            if (button.ImageClipGeometry != null)
            {
                button.ImageClipGeometry.RadiusX = radius;
                button.ImageClipGeometry.RadiusY = radius;
                button.ImageClipGeometry.Center = new Point(radius, radius);
            }
            if (button.TransparentGridImage != null)
            {
                button.TransparentGridImage.Width = radius * 2;
                button.TransparentGridImage.Height = radius * 2;
            }
        }

        public double ButtonSize
        {
            get => (double)GetValue(ButtonSizeProperty);
            set => SetValue(ButtonSizeProperty, value);
        }

        public static readonly DependencyProperty BorderBrushColorProperty = DependencyProperty.Register(
            nameof(BorderBrushColor), typeof(Color), typeof(CircularColorButton),
            new PropertyMetadata(Color.FromRgb(254, 215, 170), OnBorderBrushColorChanged));

        private static void OnBorderBrushColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.ButtonBorder != null)
                button.ButtonBorder.BorderBrush = new SolidColorBrush((Color)e.NewValue);
        }

        public Color BorderBrushColor
        {
            get => (Color)GetValue(BorderBrushColorProperty);
            set => SetValue(BorderBrushColorProperty, value);
        }

        public static readonly DependencyProperty CheckIconSourceProperty = DependencyProperty.Register(
            nameof(CheckIconSource), typeof(string), typeof(CircularColorButton),
            new PropertyMetadata("/Resources/new-icons/checked-white.png", OnCheckIconSourceChanged));

        private static void OnCheckIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (CircularColorButton)d;
            if (button.CheckImage != null)
                button.CheckImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri((string)e.NewValue, UriKind.Relative));
        }

        public string CheckIconSource
        {
            get => (string)GetValue(CheckIconSourceProperty);
            set => SetValue(CheckIconSourceProperty, value);
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event RoutedEventHandler ButtonMouseUp;

        public CircularColorButton()
        {
            InitializeComponent();
            Loaded += CircularColorButton_Loaded;
        }

        private void CircularColorButton_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyAllProperties();
        }

        private void ApplyAllProperties()
        {
            if (ButtonBorder != null)
            {
                ButtonBorder.BorderBrush = new SolidColorBrush(BorderBrushColor);
                var size = ButtonSize;
                ButtonBorder.Width = size;
                ButtonBorder.Height = size;
            }
            if (ColorOverlay != null)
            {
                ColorOverlay.Background = new SolidColorBrush(Color);
                ColorOverlay.Opacity = ColorOpacity;
                var radius = (ButtonSize - 3) / 2;
                ColorOverlay.Width = radius * 2;
                ColorOverlay.Height = radius * 2;
            }
            if (ImageClipGeometry != null)
            {
                var radius = (ButtonSize - 3) / 2;
                ImageClipGeometry.RadiusX = radius;
                ImageClipGeometry.RadiusY = radius;
                ImageClipGeometry.Center = new Point(radius, radius);
            }
            if (TransparentGridImage != null)
            {
                var radius = (ButtonSize - 3) / 2;
                TransparentGridImage.Width = radius * 2;
                TransparentGridImage.Height = radius * 2;
            }
            if (CheckViewbox != null)
            {
                CheckViewbox.Visibility = IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
            if (CheckImage != null)
            {
                CheckImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(CheckIconSource, UriKind.Relative));
            }
        }

        private void ButtonBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            ButtonMouseLeave?.Invoke(this, e);
        }

        private void ButtonBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseUp?.Invoke(this, new RoutedEventArgs(e.RoutedEvent, this));
        }
    }
}
