using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class EraserToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.eraser";
        public override string LocalizationKey => "FloatingBar_AreaEraser";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Eraser;
        public override string IconGeometry => XamlGraphicsIconGeometries.SolidEraserCircleIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.EraserIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachEraserIcon(view);
    }
}
