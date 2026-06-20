using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardPreviousPageToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.previousPage";
        public override string LocalizationKey => "Board_PreviousPage";
        public override string Description => "上一页";
        public override ButtonPosition DefaultPosition => ButtonPosition.First;

        public override string IconGeometry => "F1 M24,24z M0,0z M7.40091,10.456L14.5033,3.35357 12.3198,1.17001 1.48978,12 12.3198,22.83 14.5033,20.6465 7.40089,13.544 22.5102,13.544 22.5102,10.456 7.40091,10.456z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SwitchToPreviousPage();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
