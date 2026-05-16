using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class ColorPickerButton : UserControl
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color), typeof(Color), typeof(ColorPickerButton),
            new PropertyMetadata(Colors.Black, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ColorPickerButton)d;
            if (button.ButtonBorder != null)
                button.ButtonBorder.Background = new SolidColorBrush((Color)e.NewValue);
        }

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked), typeof(bool), typeof(ColorPickerButton),
            new PropertyMetadata(false, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ColorPickerButton)d;
            if (button.CheckPath != null)
            {
                button.CheckPath.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
                if ((bool)e.NewValue && button.CheckPath.Fill == null && button.CheckIconFill != null)
                    button.CheckPath.Fill = button.CheckIconFill;
            }
        }

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty CheckIconFillProperty = DependencyProperty.Register(
            nameof(CheckIconFill), typeof(Brush), typeof(ColorPickerButton),
            new PropertyMetadata(Brushes.White, OnCheckIconFillChanged));

        private static void OnCheckIconFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ColorPickerButton)d;
            if (button.CheckPath != null)
                button.CheckPath.Fill = (Brush)e.NewValue;
        }

        public Brush CheckIconFill
        {
            get => (Brush)GetValue(CheckIconFillProperty);
            set => SetValue(CheckIconFillProperty, value);
        }

        public static readonly DependencyProperty ButtonSizeProperty = DependencyProperty.Register(
            nameof(ButtonSize), typeof(double), typeof(ColorPickerButton),
            new PropertyMetadata(13.0, OnButtonSizeChanged));

        private static void OnButtonSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ColorPickerButton)d;
            if (button.ButtonBorder != null)
            {
                button.ButtonBorder.Width = (double)e.NewValue;
                button.ButtonBorder.Height = (double)e.NewValue;
            }
        }

        public double ButtonSize
        {
            get => (double)GetValue(ButtonSizeProperty);
            set => SetValue(ButtonSizeProperty, value);
        }

        public static readonly DependencyProperty CheckIconSizeProperty = DependencyProperty.Register(
            nameof(CheckIconSize), typeof(double), typeof(ColorPickerButton),
            new PropertyMetadata(8.0, OnCheckIconSizeChanged));

        private static void OnCheckIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ColorPickerButton)d;
            if (button.CheckPath != null)
            {
                button.CheckPath.Width = (double)e.NewValue;
                button.CheckPath.Height = (double)e.NewValue;
            }
        }

        public double CheckIconSize
        {
            get => (double)GetValue(CheckIconSizeProperty);
            set => SetValue(CheckIconSizeProperty, value);
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event RoutedEventHandler ButtonMouseUp;

        public ColorPickerButton()
        {
            InitializeComponent();
            Loaded += ColorPickerButton_Loaded;
        }

        private void ColorPickerButton_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyAllProperties();
        }

        private void ApplyAllProperties()
        {
            if (ButtonBorder != null)
            {
                ButtonBorder.Background = new SolidColorBrush(Color);
                ButtonBorder.Width = ButtonSize;
                ButtonBorder.Height = ButtonSize;
            }
            if (CheckPath != null)
            {
                CheckPath.Fill = CheckIconFill;
                CheckPath.Width = CheckIconSize;
                CheckPath.Height = CheckIconSize;
                CheckPath.Visibility = IsChecked ? Visibility.Visible : Visibility.Collapsed;
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
