using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Controls
{
    public partial class GesturePopupContent : UserControl
    {
        public string Title
        {
            get => Shell?.Title;
            set { if (Shell != null) Shell.Title = value; }
        }

        public ToggleSwitch MultiTouchToggle => ToggleSwitchEnableMultiTouchMode;
        public ToggleSwitch TwoFingerTranslateToggle => ToggleSwitchEnableTwoFingerTranslate;
        public ToggleSwitch TwoFingerZoomToggle => ToggleSwitchEnableTwoFingerZoom;
        public ToggleSwitch TwoFingerRotationToggle => ToggleSwitchEnableTwoFingerRotation;

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public FrameworkElement TwoFingerGestureSimpleStackPanel { get; }

        public GesturePopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
            TwoFingerGestureSimpleStackPanel = (FrameworkElement)FindName("_OpacityPanel");
        }
    }
}
