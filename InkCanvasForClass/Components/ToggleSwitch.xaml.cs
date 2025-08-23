using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.UI.Xaml;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using UIElement = System.Windows.UIElement;
using Visibility = System.Windows.Visibility;

namespace Ink_Canvas.Components {

    /// <summary>
    /// ToggleSwitch 用于在 WPF 中实现功能完整的切换开关控件
    /// </summary>
    public partial class ToggleSwitch : UserControl {
        private bool isToggledByUser = false;
        private bool isAnimating { get; set; } = false;

        public static readonly System.Windows.RoutedEvent OnToggledEvent = EventManager.RegisterRoutedEvent(
            name: "OnToggled",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(System.Windows.RoutedEventHandler),
            ownerType: typeof(ToggleSwitch));

        public event System.Windows.RoutedEventHandler OnToggled {
            add => AddHandler(OnToggledEvent, value);
            remove => RemoveHandler(OnToggledEvent, value);
        }

        private void RaiseOnToggledRoutedEvent() {
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: OnToggledEvent);
            RaiseEvent(routedEventArgs);
        }

        public bool IsOn {
            get => (bool)GetValue(IsOnProperty);
            set {
                if (IsOn == value) return;
                SetValue(IsOnProperty, value);
                UpdateToggleSwitchByState(!isToggledByUser);
                RaiseOnToggledRoutedEvent();
            }
        }

        public static readonly System.Windows.DependencyProperty IsOnProperty =
            System.Windows.DependencyProperty.Register(
                name: nameof(IsOn),
                propertyType: typeof(bool),
                ownerType: typeof(ToggleSwitch),
                typeMetadata: new FrameworkPropertyMetadata(defaultValue: false, propertyChangedCallback: (o, args) => {
                    var toggleSwitch = o as ToggleSwitch;
                    toggleSwitch?.UpdateToggleSwitchByState(!toggleSwitch.isToggledByUser);
                    toggleSwitch?.RaiseOnToggledRoutedEvent();
                }));

        public Brush SwitchBackground {
            get => (Brush)GetValue(SwitchBackgroundProperty);
            set {
                SetValue(SwitchBackgroundProperty, value);
                UpdateToggleSwitchByState(!isToggledByUser);
            }
        }

        public static readonly System.Windows.DependencyProperty SwitchBackgroundProperty =
            System.Windows.DependencyProperty.Register(
                nameof(SwitchBackground),
                typeof(Brush),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(53, 132, 228)), // 默认颜色
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    propertyChangedCallback: OnSwitchBackgroundChanged));

        private static void OnSwitchBackgroundChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e) {
            var toggleSwitch = d as ToggleSwitch;
            toggleSwitch?.UpdateToggleSwitchByState(true);
        }

        public new bool IsEnabled {
            get => (bool)GetValue(IsEnabledProperty);
            set {
                if (IsEnabled == value) return;
                SetValue(IsEnabledProperty, value);
                UpdateToggleSwitchByState();
            }
        }

        public new static readonly System.Windows.DependencyProperty IsEnabledProperty =
            System.Windows.DependencyProperty.Register(
                name: nameof(IsEnabled),
                propertyType: typeof(bool),
                ownerType: typeof(ToggleSwitch),
                typeMetadata: new FrameworkPropertyMetadata(defaultValue: true, propertyChangedCallback: (o, args) => {
                    var toggleSwitch = o as ToggleSwitch;
                    toggleSwitch?.UpdateToggleSwitchByState(!toggleSwitch.isToggledByUser);
                }));

        public ToggleSwitch() {
            InitializeComponent();
            UpdateToggleSwitchByState(true);
            BackgroundBorder.MouseUp += BackgroundBorder_MouseUp;
            BackgroundBorder.MouseDown += BackgroundBorder_MouseDown;
            BackgroundBorder.MouseLeave += BackgroundBorder_MouseLeave;
        }

        private bool isBackgroundBorderMouseDown = false;

        private void BackgroundBorder_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isBackgroundBorderMouseDown || isAnimating) return;
            isBackgroundBorderMouseDown = true;
            OverlayBorder.Visibility = Visibility.Visible;
        }

        private void BackgroundBorder_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isBackgroundBorderMouseDown || isAnimating) return;
            BackgroundBorder_MouseLeave(sender, null);
            isToggledByUser = true;
            IsOn = !IsOn;
            isToggledByUser = false;
        }

        private void BackgroundBorder_MouseLeave(object sender, MouseEventArgs e) {
            if (!isBackgroundBorderMouseDown || isAnimating) return;
            isBackgroundBorderMouseDown = false;
            OverlayBorder.Visibility = Visibility.Hidden;
        }

        private void UpdateToggleSwitchByState(bool isNoAnimation = false) {
            Opacity = IsEnabled ? 1 : 0.5;
            IsHitTestVisible = IsEnabled;
            IsTabStop = IsEnabled;
            if (isNoAnimation) {
                ThumbTranslateTransform.X = IsOn ? 23 : 0;
                BackgroundBorder.Background =
                    IsOn ? SwitchBackground : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            } else {
                isAnimating = true;
                var sb = new Storyboard();
                // 渐变动画
                var ani = new DoubleAnimation {
                    From = IsOn?0:23,
                    To = IsOn?23:0,
                    Duration = TimeSpan.FromMilliseconds(125)
                };
                ani.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(ani, new System.Windows.PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(ani);
                if (IsOn) BackgroundBorder.Background = SwitchBackground;
                sb.Completed += (sender, args) => {
                    isAnimating = false;
                    BackgroundBorder.Background =
                        IsOn ? SwitchBackground : new SolidColorBrush(Color.FromRgb(225, 225, 225));
                };
                ThumbBorder.BeginStoryboard(sb);
            }
        }
    }
}
