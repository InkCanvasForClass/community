using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Controls
{
    public partial class GesturePopupContent : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(GesturePopupContent),
            new PropertyMetadata(string.Empty, OnTitleChanged));

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GesturePopupContent)d;
            if (control.TitleBar != null)
                control.TitleBar.Title = (string)e.NewValue;
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ToggleSwitch MultiTouchToggle => ToggleSwitchEnableMultiTouchMode;
        public ToggleSwitch TwoFingerTranslateToggle => ToggleSwitchEnableTwoFingerTranslate;
        public ToggleSwitch TwoFingerZoomToggle => ToggleSwitchEnableTwoFingerZoom;
        public ToggleSwitch TwoFingerRotationToggle => ToggleSwitchEnableTwoFingerRotation;

        public FontIcon CloseFontIcon => TitleBar?.CloseFontIcon;

        public FrameworkElement TwoFingerGestureSimpleStackPanel { get; }

        public GesturePopupContent()
        {
            InitializeComponent();
            TwoFingerGestureSimpleStackPanel = (FrameworkElement)FindName("_OpacityPanel");
        }
    }
}
