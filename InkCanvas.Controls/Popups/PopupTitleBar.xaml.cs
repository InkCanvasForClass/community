using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class PopupTitleBar : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PopupTitleBar),
            new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public Button CloseButtonControl => CloseButton;

        public PopupTitleBar()
        {
            InitializeComponent();
        }
    }
}
