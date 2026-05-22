using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class PenToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.pen";
        public override string LocalizationKey => "FloatingBar_Annotate";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Pen;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.PenIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachPenIconView(view);
    }
}
