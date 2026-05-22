using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class ClearToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.clear";
        public override string LocalizationKey => "FloatingBar_Clear";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Clear;

        protected override string IconBrushResourceKey => "RedBrush";
        protected override string LabelBrushResourceKey => "RedBrush";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconDelete_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachSymbolIconDelete(view);
    }
}
