using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class ShapeDrawToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.shapeDraw";
        public override string LocalizationKey => "FloatingBar_Geometry";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarCanvasControls;
        public override int DefaultOrder => 130;
        public override string MenuPanelName => "BorderDrawShape";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageDrawShape_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachShapeDrawBtn(view);
    }
}