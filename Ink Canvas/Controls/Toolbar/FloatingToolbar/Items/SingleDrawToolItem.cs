using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class SingleDrawToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.singleDraw";
        public override string LocalizationKey => "QuickPanel_SingleDraw";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.QuickPanel_SingleDraw;
        public override string IconGeometry => XamlGraphicsIconGeometries.SingleDrawIconGeometry;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRandOne_MouseUp(sender, e);
    }
}
