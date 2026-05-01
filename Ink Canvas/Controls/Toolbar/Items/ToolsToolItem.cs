using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class ToolsToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.tools";
        public override string LocalizationKey => "Board_Tools";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarEnd;
        public override int DefaultOrder => 110;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.AfterAnchor;
        public override string DefaultAnchorName => "FloatingBarEndSeparator";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconTools_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachToolsBtn(view);
    }
}