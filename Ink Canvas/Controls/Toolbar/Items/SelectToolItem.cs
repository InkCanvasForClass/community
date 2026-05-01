using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class SelectToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.select";
        public override string LocalizationKey => "FloatingBar_LassoSelect";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarCanvasControls;
        public override int DefaultOrder => 120;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconSelect_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachSymbolIconSelect(view);
    }
}