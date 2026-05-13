using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class PenToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.pen";
        public override string LocalizationKey => "FloatingBar_Annotate";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => "画笔工具";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.PenIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachPenIconView(view);
    }
}
