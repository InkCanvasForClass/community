using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Controls
{
    public partial class ImageOptionsPopupContent : UserControl
    {
        public Border ScreenshotOption { get; }
        public Border SelectFileOption { get; }
        public FontIcon CloseFontIcon => TitleBar?.CloseFontIcon;

        public ImageOptionsPopupContent()
        {
            InitializeComponent();
            ScreenshotOption = (Border)FindName("_ScreenshotOption");
            SelectFileOption = (Border)FindName("_SelectFileOption");
        }
    }
}
