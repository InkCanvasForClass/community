using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardSelectToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.select";
        public override string LocalizationKey => "Board_Select";
        public override string Description => "选择工具";
        public override ButtonPosition DefaultPosition => ButtonPosition.First;

        protected override string IconGeometry => "F1 M24,24z M0,0z M22.7989,10.1653L1.14304,1.14304 10.1653,22.7989 12.8305,14.9518 19.6892,21.8105 21.8105,19.6892 14.9518,12.8305 22.7989,10.1653z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SelectTool();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
