using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class ShapeDrawToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.shapeDraw";
        public override string LocalizationKey => "FloatingBar_Geometry";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => GeometryStrings.Title;
        public override string IconGeometry => XamlGraphicsIconGeometries.ShapesIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageDrawShape_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachShapeDrawBtn(view);
    }
}
