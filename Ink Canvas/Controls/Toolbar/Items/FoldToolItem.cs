using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class FoldToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.fold";
        public override string LocalizationKey => "FloatingBar_Hide";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override bool DefaultPreventHideOnDragClick => true;
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Fold;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.FoldFloatingBar_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachFoldIcon(view);
    }
}
