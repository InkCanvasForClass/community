using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class CursorToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.cursor";
        public override string LocalizationKey => "FloatingBar_Mouse";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Cursor;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.CursorIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachCursorIconView(view);
    }
}
