using System.Windows;
using System.Windows.Controls;

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
        public TabControl EraserTypeTab => EraserTypeTabControl;
        public Button ClearInkBtn => ClearInkButton;
        public Button ClearInkAndHistoryBtn => ClearInkAndHistoryButton;

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public EraserPopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
