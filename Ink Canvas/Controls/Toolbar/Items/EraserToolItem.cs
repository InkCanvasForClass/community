using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class EraserToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.eraser";
        public override string LocalizationKey => "FloatingBar_AreaEraser";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => "区域橡皮擦";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.EraserIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachEraserIcon(view);
    }
}
