using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class EraserByStrokesToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.eraserByStrokes";
        public override string LocalizationKey => "FloatingBar_StrokeEraser";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarCanvasControls;
        public override int DefaultOrder => 110;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.EraserIconByStrokes_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachEraserByStrokesIcon(view);
    }
}