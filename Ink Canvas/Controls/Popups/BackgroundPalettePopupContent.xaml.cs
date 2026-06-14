using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class BackgroundPalettePopupContent : UserControl
    {
        public Slider RSlider => BackgroundRSlider;
        public Slider GSlider => BackgroundGSlider;
        public Slider BSlider => BackgroundBSlider;
        public TextBlock RValue => BackgroundRValue;
        public TextBlock GValue => BackgroundGValue;
        public TextBlock BValue => BackgroundBValue;
        public Border ColorPreview => BackgroundColorPreview;
        public Button ApplyBtn => ApplyBackgroundColorBtn;
        public Border WhiteboardBtn => WhiteboardModeBtn;
        public Border BlackboardBtn => BlackboardModeBtn;
        public Border DarkModeBtnControl => DarkModeBtn;
        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public BackgroundPalettePopupContent()
        {
            InitializeComponent();
            Shell.InnerContent = InnerContentHost.Content;
        }
    }
}
