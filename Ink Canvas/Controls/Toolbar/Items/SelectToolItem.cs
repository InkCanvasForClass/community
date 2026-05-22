using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class SelectToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.select";
        public override string LocalizationKey => "FloatingBar_LassoSelect";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Select;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconSelect_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachSymbolIconSelect(view);
    }
}
