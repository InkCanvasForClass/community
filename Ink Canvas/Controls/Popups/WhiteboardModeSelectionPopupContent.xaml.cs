using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 白板按钮右键二级菜单内容控件
    /// 提供"全屏白板"和"小白板"两个选项
    /// </summary>
    public partial class WhiteboardModeSelectionPopupContent : UserControl
    {
        public Border FullWhiteboardBtn => FullWhiteboardMenuItem;
        public Border MiniWhiteboardBtn => MiniWhiteboardMenuItem;

        public event MouseButtonEventHandler FullWhiteboardClick;
        public event MouseButtonEventHandler MiniWhiteboardClick;

        public WhiteboardModeSelectionPopupContent()
        {
            InitializeComponent();
            FullWhiteboardMenuItem.MouseLeftButtonUp += (s, e) => FullWhiteboardClick?.Invoke(s, e);
            MiniWhiteboardMenuItem.MouseLeftButtonUp += (s, e) => MiniWhiteboardClick?.Invoke(s, e);
        }
    }
}
