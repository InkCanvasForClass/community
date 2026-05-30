using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardVideoBoothToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override string Description => "视频展台";
        public override ButtonPosition DefaultPosition => ButtonPosition.Last;

        protected override string IconGeometry => "F1 M24,24z M0,0z M12,10.5C12,9.67157 12.6716,9 13.5,9 14.3284,9 15,9.67157 15,10.5L15,13.5C15,14.3284 14.3284,15 13.5,15 12.6716,15 12,14.3284 12,13.5L12,10.5z M17.25,7.5C17.25,6.87868 16.8713,6.32233 16.3223,6.08825 15.7733,5.85418 15.1267,5.97756 14.7246,6.31365 14.3225,6.64974 14.1426,7.22727 14.25,7.7793L14.5,9 9.5,9 9.75,7.7793C9.85744,7.22727 9.67753,6.64974 9.27539,6.31365 8.87326,5.97756 8.22669,5.85418 7.67766,6.08825 7.12864,6.32233 6.75,6.87868 6.75,7.5L6.75,18 17.25,18 17.25,7.5z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.Dispatcher.Invoke(() =>
            {
                var mw = host.Window as MainWindow;
                mw?.ToggleVideoPresenterSidebarPublic();
            });
        }

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
