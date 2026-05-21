using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class InkFreezeToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.inkFreeze";
        public override string LocalizationKey => "FloatingBar_Freeze";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => "墨迹冻结";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ToggleInkFreeze_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachInkFreezeBtn(view);
    }
}
