using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class InkFreezeToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.inkFreeze";
        public override string LocalizationKey => "FloatingBar_Freeze";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarMain;
        public override int DefaultOrder => 120;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.AfterAnchor;
        public override string DefaultAnchorName => "QuickColorPaletteSingleRowPanel";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ToggleInkFreeze_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachInkFreezeBtn(view);
    }
}
