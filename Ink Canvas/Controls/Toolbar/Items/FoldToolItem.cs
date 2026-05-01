using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class FoldToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.fold";
        public override string LocalizationKey => "FloatingBar_Hide";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarEnd;
        public override int DefaultOrder => 120;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.AfterAnchor;
        public override string DefaultAnchorName => "FloatingBarEndSeparator";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.FoldFloatingBar_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachFoldIcon(view);
    }
}