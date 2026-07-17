using System.Windows;
using System.Windows.Controls;
using ModernControls = iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Controls
{
    public partial class LabeledToggleSwitch : UserControl
    {
        public ModernControls.ToggleSwitch ToggleSwitchControl => ToggleSwitch;

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(LabeledToggleSwitch), new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
            nameof(Hint), typeof(string), typeof(LabeledToggleSwitch), new PropertyMetadata(string.Empty, OnHintChanged));

        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        private static void OnHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledToggleSwitch control)
            {
                var hint = e.NewValue as string;
                control.HintTextBlock.Visibility = string.IsNullOrEmpty(hint) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
            nameof(IsOn), typeof(bool), typeof(LabeledToggleSwitch), new PropertyMetadata(false));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        public static readonly DependencyProperty ShowWhenProperty = DependencyProperty.Register(
            nameof(ShowWhen), typeof(bool), typeof(LabeledToggleSwitch), new PropertyMetadata(true, OnShowWhenChanged));

        public bool ShowWhen
        {
            get => (bool)GetValue(ShowWhenProperty);
            set => SetValue(ShowWhenProperty, value);
        }

        private static void OnShowWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledToggleSwitch control)
            {
                control.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public event RoutedEventHandler Toggled;

        public LabeledToggleSwitch()
        {
            InitializeComponent();
            Visibility = ShowWhen ? Visibility.Visible : Visibility.Collapsed;
            HintTextBlock.Visibility = string.IsNullOrEmpty(Hint) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Toggled?.Invoke(this, e);
        }
    }
}
