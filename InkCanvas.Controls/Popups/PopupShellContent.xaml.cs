using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Ink_Canvas.Controls
{
    [ContentProperty("InnerContent")]
    public partial class PopupShellContent : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PopupShellContent),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty InnerContentProperty = DependencyProperty.Register(
            nameof(InnerContent), typeof(object), typeof(PopupShellContent),
            new PropertyMetadata(null, OnInnerContentChanged));

        private static void OnInnerContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var shell = (PopupShellContent)d;
            shell.ContentArea.Content = e.NewValue;
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public Button CloseButtonControl => TitleBar?.CloseButtonControl;

        public PopupShellContent()
        {
            InitializeComponent();
            if (InnerContent != null)
                ContentArea.Content = InnerContent;
        }
    }
}
