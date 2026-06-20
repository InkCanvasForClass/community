using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class RandomDrawToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.randomDraw";
        public override string LocalizationKey => "Tools_RandomDraw";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.Tools_RandomDraw;
        public override string IconGeometry => XamlGraphicsIconGeometries.RandomDrawIconGeometry;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRand_MouseUp(sender, e);
    }
}
