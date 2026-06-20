using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardRandomDrawToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.randomDraw";
        public override string LocalizationKey => "Tools_RandomDraw";
        public override string Description => "随机抽";
        public override string IconGeometry => XamlGraphicsIconGeometries.RandomDrawIconGeometry;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRand_MouseUp(sender, e);
    }
}
