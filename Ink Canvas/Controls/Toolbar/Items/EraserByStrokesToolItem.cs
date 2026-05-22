using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class EraserByStrokesToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.eraserByStrokes";
        public override string LocalizationKey => "FloatingBar_StrokeEraser";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_StrokeEraser;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.EraserIconByStrokes_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachEraserByStrokesIcon(view);
    }
}
