using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class QuickPanelButton : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(QuickPanelButton),
            new PropertyMetadata(string.Empty, OnLabelChanged));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (QuickPanelButton)d;
            if (button.LabelTextBlock != null)
            {
                var text = (string)e.NewValue;
                button.LabelTextBlock.Text = text;
                button.LabelTextBlock.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
                button.ButtonImage.Margin = string.IsNullOrEmpty(text)
                    ? new Thickness(0, 3, 0, 3)
                    : new Thickness(0, 3, 0, 0);
            }
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
            nameof(IconSource), typeof(ImageSource), typeof(QuickPanelButton),
            new PropertyMetadata(null, OnIconSourceChanged));

        private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (QuickPanelButton)d;
            if (button.ButtonImage != null)
                button.ButtonImage.Source = (ImageSource)e.NewValue;
        }

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public static readonly DependencyProperty LabelFontSizeProperty = DependencyProperty.Register(
            nameof(LabelFontSize), typeof(double), typeof(QuickPanelButton),
            new PropertyMetadata(8.0, OnLabelFontSizeChanged));

        private static void OnLabelFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (QuickPanelButton)d;
            if (button.LabelTextBlock != null)
                button.LabelTextBlock.FontSize = (double)e.NewValue;
        }

        public double LabelFontSize
        {
            get => (double)GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        public event MouseButtonEventHandler ButtonMouseUp;

        public QuickPanelButton()
        {
            InitializeComponent();
        }

        private void ButtonPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
