using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Controls
{
    public partial class EraserPopupContent : UserControl
    {
        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(EraserPopupContent),
            new PropertyMetadata(false, OnIsBoardModeChanged));

        private static void OnIsBoardModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (EraserPopupContent)d;
            control.ClearInkAndHistoryButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        public ComboBox EraserSizeComboBox => ComboBoxEraserSize;
        public Border CircleTab => CircleEraserTabButton;
        public Border RectangleTab => RectangleEraserTabButton;
        public Button ClearInkBtn => ClearInkButton;
        public Button ClearInkAndHistoryBtn => ClearInkAndHistoryButton;

        public TextBlock CircleTabText => CircleEraserTabButtonText;
        public TextBlock RectangleTabText => RectangleEraserTabButtonText;
        public FrameworkElement CircleTabIndicator => CircleEraserTabButtonIndicator;
        public FrameworkElement RectangleTabIndicator => RectangleEraserTabButtonIndicator;

        public FontIcon CloseFontIcon => Shell?.CloseFontIcon;

        public EraserPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
