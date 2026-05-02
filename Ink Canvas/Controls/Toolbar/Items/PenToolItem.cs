using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class PenToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.pen";
        public override string LocalizationKey => "FloatingBar_Annotate";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarMain;
        public override int DefaultOrder => 110;
        public override string MenuPanelName => "PenPalette";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.PenIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachPenIconView(view);
    }
}