using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class ClearToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.clear";
        public override string LocalizationKey => "FloatingBar_Clear";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Clear;

        protected override string IconBrushResourceKey => "RedBrush";
        protected override string LabelBrushResourceKey => "RedBrush";
        public override string IconGeometry => XamlGraphicsIconGeometries.ClearInkIconGeometry;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconDelete_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachSymbolIconDelete(view);
    }
}
