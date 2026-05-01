using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class CursorToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.cursor";
        public override string LocalizationKey => "FloatingBar_Mouse";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarMain;
        public override int DefaultOrder => 100;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.CursorIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachCursorIconView(view);
    }
}