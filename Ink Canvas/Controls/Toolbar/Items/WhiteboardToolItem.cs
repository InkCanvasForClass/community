using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class WhiteboardToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.whiteboard";
        public override string LocalizationKey => "FloatingBar_Whiteboard";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarEnd;
        public override int DefaultOrder => 100;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.AfterAnchor;
        public override string DefaultAnchorName => "FloatingBarEndSeparator";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageBlackboard_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachWhiteboardBtn(view);
    }
}