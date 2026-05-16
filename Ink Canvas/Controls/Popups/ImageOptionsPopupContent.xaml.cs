using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class ImageOptionsPopupContent : UserControl
    {
        public Border ScreenshotOption { get; }
        public Border SelectFileOption { get; }
        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public ImageOptionsPopupContent()
        {
            InitializeComponent();
            ScreenshotOption = (Border)FindName("_ScreenshotOption");
            SelectFileOption = (Border)FindName("_SelectFileOption");
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
