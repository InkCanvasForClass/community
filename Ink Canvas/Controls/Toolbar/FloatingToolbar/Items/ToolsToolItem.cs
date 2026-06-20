using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class ToolsToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.tools";
        public override string LocalizationKey => "Board_Tools";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.Board_Tools;
        public override string IconGeometry => XamlGraphicsIconGeometries.ToolsFloatingBarBtnIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconTools_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachToolsBtn(view);
    }
}
