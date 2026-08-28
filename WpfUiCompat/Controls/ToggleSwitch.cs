using System.Windows;
using System.Windows.Controls;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 兼容 iNKORE ToggleSwitch API 的开关控件，基于 WPF-UI ToggleSwitch 实现。
    /// 提供 iNKORE 风格的 <see cref="IsOn"/> 属性与 <see cref="Toggled"/> 路由事件。
    /// </summary>
    public class ToggleSwitch : Wpf.Ui.Controls.ToggleSwitch
    {
        public ToggleSwitch()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.ToggleSwitch));
        }

        /// <summary>
        /// 标识 <see cref="Toggled"/> 路由事件。
        /// </summary>
        public static readonly RoutedEvent ToggledEvent = EventManager.RegisterRoutedEvent(
            nameof(Toggled), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleSwitch));

        public event RoutedEventHandler Toggled
        {
            add { AddHandler(ToggledEvent, value); }
            remove { RemoveHandler(ToggledEvent, value); }
        }

        /// <summary>
        /// 标识 <see cref="IsOn"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(
                nameof(IsOn),
                typeof(bool),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal, OnIsOnChanged));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        static ToggleSwitch()
        {
            IsCheckedProperty.OverrideMetadata(typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal, OnIsCheckedChanged));
        }

        private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToggleSwitch ts)
            {
                var newValue = (bool)e.NewValue;
                if (ts.IsChecked != newValue)
                {
                    ts.SetCurrentValue(IsCheckedProperty, newValue);
                }
            }
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToggleSwitch ts)
            {
                var newValue = e.NewValue is true;
                if (ts.IsOn != newValue)
                {
                    ts.SetCurrentValue(IsOnProperty, newValue);
                }
                ts.RaiseEvent(new RoutedEventArgs(ToggledEvent));
            }
        }

        protected override void OnToggle()
        {
            IsOn = !IsOn;
        }
    }
}