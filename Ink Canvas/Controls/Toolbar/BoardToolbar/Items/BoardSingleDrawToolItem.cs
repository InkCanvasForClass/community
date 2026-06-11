using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardSingleDrawToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.singleDraw";
        public override string LocalizationKey => "QuickPanel_SingleDraw";
        public override string Description => "单次抽";
        protected override string IconGeometry => XamlGraphicsIconGeometries.SingleDrawIconGeometry;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRandOne_MouseUp(sender, e);
    }
}
