using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class LabeledSettingsCard : UserControl
    {
        public iNKORE.UI.WPF.Modern.Controls.ToggleSwitch ToggleSwitchControl => ToggleSwitch;

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header), typeof(string), typeof(LabeledSettingsCard), new PropertyMetadata(string.Empty));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(LabeledSettingsCard), new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(FontIconData?), typeof(LabeledSettingsCard), new PropertyMetadata(null, OnIconChanged));

        public FontIconData? Icon
        {
            get => (FontIconData?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSettingsCard control)
                control.ApplyIcon();
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
            nameof(IconSource), typeof(ImageSource), typeof(LabeledSettingsCard), new PropertyMetadata(null, OnIconSourceChanged));

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSettingsCard control)
                control.ApplyIcon();
        }

        public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
            nameof(HeaderIcon), typeof(object), typeof(LabeledSettingsCard), new PropertyMetadata(null, OnHeaderIconChanged));

        public object HeaderIcon
        {
            get => GetValue(HeaderIconProperty);
            set => SetValue(HeaderIconProperty, value);
        }

        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSettingsCard control)
                control.ApplyIcon();
        }

        private void ApplyIcon()
        {
            if (SettingsCard == null) return;

            if (IconSource != null)
            {
                SettingsCard.HeaderIcon = new Image
                {
                    Source = IconSource,
                    Width = 16,
                    Height = 16,
                };
            }
            else if (Icon.HasValue)
            {
                SettingsCard.HeaderIcon = new iNKORE.UI.WPF.Modern.Controls.FontIcon(Icon.Value);
            }
            else if (HeaderIcon != null)
            {
                SettingsCard.HeaderIcon = HeaderIcon;
            }
            else
            {
                SettingsCard.HeaderIcon = null;
            }
        }

        public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
            nameof(IsOn), typeof(bool), typeof(LabeledSettingsCard), new PropertyMetadata(false));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        public static readonly DependencyProperty ShowWhenProperty = DependencyProperty.Register(
            nameof(ShowWhen), typeof(bool), typeof(LabeledSettingsCard), new PropertyMetadata(true, OnShowWhenChanged));

        public bool ShowWhen
        {
            get => (bool)GetValue(ShowWhenProperty);
            set => SetValue(ShowWhenProperty, value);
        }

        public static readonly DependencyProperty SwitchNameProperty = DependencyProperty.Register(
            nameof(SwitchName), typeof(string), typeof(LabeledSettingsCard), new PropertyMetadata(string.Empty, OnSwitchNameChanged));

        public string SwitchName
        {
            get => (string)GetValue(SwitchNameProperty);
            set => SetValue(SwitchNameProperty, value);
        }

        private static void OnSwitchNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSettingsCard control && control.ToggleSwitch != null)
                control.ToggleSwitch.Name = (string)e.NewValue;
        }

        private static void OnShowWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LabeledSettingsCard control)
                control.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public event RoutedEventHandler Toggled;

        public LabeledSettingsCard()
        {
            InitializeComponent();
            Loaded += LabeledSettingsCard_Loaded;
        }

        private void LabeledSettingsCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SwitchName) && ToggleSwitch != null)
                ToggleSwitch.Name = SwitchName;
            ApplyIcon();
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Toggled?.Invoke(this, e);
        }
    }
}
