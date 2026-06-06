using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class WhiteboardToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.whiteboard";
        public override string LocalizationKey => "FloatingBar_Whiteboard";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.FloatingBar_Whiteboard;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageBlackboard_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            host.Window.AttachWhiteboardBtn(view);

            // 右键弹出二级菜单：选择全屏白板或小白板
            view.MouseRightButtonUp += (s, e) =>
            {
                host.Window.ShowWhiteboardModeSelectionPopup(view);
                e.Handled = true;
            };
        }
    }
}
