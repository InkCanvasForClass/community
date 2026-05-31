using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardAddNewPageToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.addNewPage";
        public override string LocalizationKey => "Board_NewPage";
        public override string Description => "新增页面";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override string IconGeometry => "F0 M24,24z M0,0z M7.39778,13.723L10.7693,13.723 10.7693,10.3514 13.2307,10.3514 13.2307,13.723 16.6022,13.723 16.6022,16.1843 13.2307,16.1843 13.2307,19.5559 10.7693,19.5559 10.7693,16.1843 7.39778,16.1843 7.39778,13.723z M3.1391,1.17001L3.1391,22.83 20.8609,22.83 20.8609,6.66948 15.3614,1.17002 3.1391,1.17001z M12.9846,3.13911L5.10819,3.1391 5.10819,20.8609 18.8918,20.8609 18.8918,9.04638 12.9846,9.04638 12.9846,3.13911z M18.484,7.07729L14.9536,3.54692 14.9536,7.07729 18.484,7.07729z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.AddWhiteboardPage();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
