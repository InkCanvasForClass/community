using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// iuwm 的 SegoeFluentIcons/FontIconData 字形模型 → WPF-UI 的 SymbolRegular 枚举 + SymbolIcon。
// 按需别名引入，避免 Wpf.Ui.Controls 与 System.Windows.Controls 的同名类型（Image/TextBlock 等）产生 CS0104 歧义。
using ImageIcon = Wpf.Ui.Controls.ImageIcon;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Ink_Canvas.Controls
{
    public partial class LabeledSettingsCard : UserControl
    {
        public Wpf.Ui.Controls.ToggleSwitch ToggleSwitchControl => ToggleSwitch;

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header), typeof(string), typeof(LabeledSettingsCard), new PropertyMetadata(string.Empty));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(LabeledSettingsCard),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // CardControl 没有 Description 属性，描述文字放在 Header 的第二行；为空时整行隐藏。
            if (d is LabeledSettingsCard control && control.DescriptionText != null)
            {
                control.DescriptionText.Visibility = string.IsNullOrEmpty(e.NewValue as string)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        /// <summary>
        /// 图标符号名（<see cref="SymbolRegular"/> 的成员名，如 "Settings24"）。
        /// 以字符串暴露，便于 XAML 直接书写（枚举无法由 XAML 字符串自动转换）。
        /// </summary>
        public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
            nameof(Symbol), typeof(string), typeof(LabeledSettingsCard), new PropertyMetadata(null, OnSymbolChanged));

        public string Symbol
        {
            get => (string)GetValue(SymbolProperty);
            set => SetValue(SymbolProperty, value);
        }

        private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

            // iuwm SettingsCard.HeaderIcon → WPF-UI CardControl.Icon（类型为 IconElement）
            if (IconSource != null)
            {
                SettingsCard.Icon = new ImageIcon
                {
                    Source = IconSource,
                    Width = 16,
                    Height = 16,
                };
            }
            else if (!string.IsNullOrWhiteSpace(Symbol)
                     && Enum.TryParse<SymbolRegular>(Symbol, true, out var parsed))
            {
                SettingsCard.Icon = new SymbolIcon(parsed);
            }
            else if (HeaderIcon is Wpf.Ui.Controls.IconElement iconElement)
            {
                SettingsCard.Icon = iconElement;
            }
            else
            {
                SettingsCard.Icon = null;
            }
        }

        public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
            nameof(IsOn), typeof(bool), typeof(LabeledSettingsCard), new PropertyMetadata(false));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        /// <summary>
        /// <see cref="IsOn"/> 的别名，类型为 <see cref="bool"/>? 以与 WPF-UI 原生
        /// <c>ToggleSwitch.IsChecked</c>（三态）保持一致，避免调用方出现 bool/bool? 转换错误。
        /// </summary>
        public bool? IsChecked
        {
            get => IsOn;
            set => IsOn = value ?? false;
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
            if (DescriptionText != null)
                DescriptionText.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
            ApplyIcon();
        }

        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
            Toggled?.Invoke(this, e);
        }

        private void ToggleSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            Toggled?.Invoke(this, e);
        }
    }
}