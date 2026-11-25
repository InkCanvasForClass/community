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

        // 点击打开图形面板：等工具栏动画落定再执行，避免挂 Popup 的重活把动画拖住。
        protected override bool OpensUiOnClick => true;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageDrawShape_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachShapeDrawBtn(view);
    }
}
