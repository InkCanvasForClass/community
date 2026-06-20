using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardNextPageToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.nextPage";
        public override string LocalizationKey => "Board_NextPage";
        public override string Description => "下一页";
        public override ButtonPosition DefaultPosition => ButtonPosition.Last;

        public override string IconGeometry => "F1 M24,24z M0,0z M16.5991,10.456L9.49667,3.35357 11.6802,1.17001 22.5102,12 11.6802,22.83 9.49667,20.6465 16.5991,13.544 1.48978,13.544 1.48978,10.456 16.5991,10.456z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SwitchToNextPage();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
