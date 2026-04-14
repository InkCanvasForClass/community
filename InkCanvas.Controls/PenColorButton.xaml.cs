using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class PenColorButton : UserControl
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color), typeof(Color), typeof(PenColorButton),
            new PropertyMetadata(Colors.Black, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (PenColorButton)d;
            if (button.ColorBorder != null)
            {
                button.ColorBorder.Background = new SolidColorBrush((Color)e.NewValue);
            }
        }

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty BorderBrushColorProperty = DependencyProperty.Register(
            nameof(BorderBrushColor), typeof(Color), typeof(PenColorButton),
            new PropertyMetadata(Colors.Gray, OnBorderBrushColorChanged));

        private static void OnBorderBrushColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (PenColorButton)d;
            if (button.ButtonBorder != null)
            {
                button.ButtonBorder.BorderBrush = new SolidColorBrush((Color)e.NewValue);
            }
        }

        public Color BorderBrushColor
        {
            get => (Color)GetValue(BorderBrushColorProperty);
            set => SetValue(BorderBrushColorProperty, value);
        }

        public static readonly DependencyProperty IsHighlighterProperty = DependencyProperty.Register(
            nameof(IsHighlighter), typeof(bool), typeof(PenColorButton),
            new PropertyMetadata(false, OnIsHighlighterChanged));

        private static void OnIsHighlighterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (PenColorButton)d;
            if (button.TransparentGridImage != null && button.ColorBorder != null)
            {
                bool isHighlighter = (bool)e.NewValue;
                button.TransparentGridImage.Visibility = isHighlighter ? Visibility.Visible : Visibility.Collapsed;
                button.ColorBorder.Opacity = isHighlighter ? 0.75 : 1;
            }
        }

        public bool IsHighlighter
        {
            get => (bool)GetValue(IsHighlighterProperty);
            set => SetValue(IsHighlighterProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked), typeof(bool), typeof(PenColorButton),
            new PropertyMetadata(false, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (PenColorButton)d;
            if (button.CheckViewbox != null)
            {
                button.CheckViewbox.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty CheckIconSourceProperty = DependencyProperty.Register(
            nameof(CheckIconSource), typeof(string), typeof(PenColorButton),
            new PropertyMetadata("/Resources/new-icons/checked-white.png", OnCheckIconSourceChanged));

        private static void OnCheckIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (PenColorButton)d;
            if (button.CheckImage != null && e.NewValue is string source)
            {
                button.CheckImage.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(source, System.UriKind.Relative));
            }
        }

        public string CheckIconSource
        {
            get => (string)GetValue(CheckIconSourceProperty);
            set => SetValue(CheckIconSourceProperty, value);
        }

        public event MouseButtonEventHandler ButtonMouseUp;

        public PenColorButton()
        {
            InitializeComponent();
            Loaded += PenColorButton_Loaded;
        }

        private void PenColorButton_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            if (ColorBorder != null)
                ColorBorder.Background = new SolidColorBrush(Color);
            if (ButtonBorder != null)
                ButtonBorder.BorderBrush = new SolidColorBrush(BorderBrushColor);
            if (TransparentGridImage != null)
                TransparentGridImage.Visibility = IsHighlighter ? Visibility.Visible : Visibility.Collapsed;
            if (ColorBorder != null)
                ColorBorder.Opacity = IsHighlighter ? 0.75 : 1;
            if (CheckViewbox != null)
                CheckViewbox.Visibility = IsChecked ? Visibility.Visible : Visibility.Collapsed;
            if (CheckImage != null && !string.IsNullOrEmpty(CheckIconSource))
                CheckImage.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(CheckIconSource, System.UriKind.Relative));
        }

        private void ButtonBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
