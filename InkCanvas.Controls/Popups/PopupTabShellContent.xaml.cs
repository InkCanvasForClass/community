using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Ink_Canvas.Controls
{
    [ContentProperty("InnerContent")]
    public partial class PopupTabShellContent : UserControl
    {
        public static readonly DependencyProperty InnerContentProperty = DependencyProperty.Register(
            nameof(InnerContent), typeof(object), typeof(PopupTabShellContent),
            new PropertyMetadata(null, OnInnerContentChanged));

        private static void OnInnerContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var shell = (PopupTabShellContent)d;
            shell.ContentArea.Content = e.NewValue;
        }

        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public PopupTabTitleBar TabBar => TabTitleBar;

        public int SelectedTabIndex
        {
            get => TabTitleBar.SelectedIndex;
            set => TabTitleBar.SelectedIndex = value;
        }

        public Button CloseButtonControl => TabTitleBar?.CloseButtonControl;

        public PopupTabShellContent()
        {
            InitializeComponent();
            if (InnerContent != null)
                ContentArea.Content = InnerContent;
        }
    }
}
