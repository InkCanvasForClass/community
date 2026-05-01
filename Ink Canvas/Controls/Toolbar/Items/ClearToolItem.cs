using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    /// <summary>
    /// 清空按钮。位置：夹在颜色面板与 StackPanelCanvasControls 之间，
    /// 所以用 BeforeAnchor 锚到 StackPanelCanvasControls。
    /// </summary>
    internal sealed class ClearToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.clear";
        public override string LocalizationKey => "FloatingBar_Clear";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarMain;
        public override int DefaultOrder => 0;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.BeforeAnchor;
        public override string DefaultAnchorName => "StackPanelCanvasControls";

        protected override string IconBrushResourceKey => "RedBrush";
        protected override string LabelBrushResourceKey => "RedBrush";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconDelete_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachSymbolIconDelete(view);
    }
}