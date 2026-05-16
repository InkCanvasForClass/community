using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class ToolsPopupContent : UserControl
    {
        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(ToolsPopupContent),
            new PropertyMetadata(false, OnIsBoardModeChanged));

        private static void OnIsBoardModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ToolsPopupContent)d;
            if ((bool)e.NewValue)
            {
                control.RandomDrawToolBtn.Visibility = Visibility.Collapsed;
                control.SingleDrawToolBtn.Visibility = Visibility.Collapsed;
            }
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        public ToolMenuButton TimerBtn => TimerToolBtn;
        public ToolMenuButton RandomDrawBtn => RandomDrawToolBtn;
        public ToolMenuButton SingleDrawBtn => SingleDrawToolBtn;
        public ToolMenuButton SaveBtn => SaveToolBtn;
        public ToolMenuButton OpenBtn => OpenToolBtn;
        public ToolMenuButton ReplayBtn => ReplayToolBtn;
        public ToolMenuButton ScreenshotBtn => ScreenshotToolBtn;
        public ToolMenuButton ManualBtn => ManualToolBtn;
        public ToolMenuButton SettingsBtn => SettingsToolBtn;

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public ToolsPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
