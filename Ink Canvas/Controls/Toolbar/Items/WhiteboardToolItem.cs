using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class WhiteboardToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.whiteboard";
        public override string LocalizationKey => "FloatingBar_Whiteboard";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => "白板";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageBlackboard_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachWhiteboardBtn(view);
    }
}
