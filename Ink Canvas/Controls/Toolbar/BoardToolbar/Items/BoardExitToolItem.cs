using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardExitToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.exit";
        public override string LocalizationKey => "OldUI_Exit";
        public override string Description => "退出";
        public override ButtonPosition DefaultPosition => ButtonPosition.Last;

        public override string IconGeometry => "F0 M24,24z M0,0z M12,23.4102C18.3017,23.4102 23.4102,18.3017 23.4102,12 23.4102,5.69834 18.3017,0.589813 12,0.589813 5.69834,0.589813 0.589813,5.69834 0.589813,12 0.589813,18.3017 5.69834,23.4102 12,23.4102z M8.25212,6.38964C7.73781,5.87533 6.90395,5.87533 6.38964,6.38964 5.87533,6.90395 5.87533,7.73781 6.38964,8.25212L10.1375,12 6.38964,15.7479C5.87533,16.2622 5.87533,17.0961 6.38964,17.6104 6.90395,18.1247 7.73781,18.1247 8.25212,17.6104L12,13.8625 15.7479,17.6104C16.2622,18.1247 17.0961,18.1247 17.6104,17.6104 18.1247,17.0961 18.1247,16.2622 17.6104,15.7479L13.8625,12 17.6104,8.25212C18.1247,7.73781 18.1247,6.90395 17.6104,6.38964 17.0961,5.87533 16.2622,5.87533 15.7479,6.38964L12,10.1375 8.25212,6.38964z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.ExitWhiteboard();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
