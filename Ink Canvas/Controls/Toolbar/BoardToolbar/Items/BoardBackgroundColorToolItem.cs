using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardBackgroundColorToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.backgroundColor";
        public override string LocalizationKey => "Board_Background";
        public override string Description => "背景颜色";
        public override ButtonPosition DefaultPosition => ButtonPosition.Last;

        protected override string IconGeometry => "F0 M24,24z M0,0z M4.71815,3.98345C6.64142,2.23541 9.19629,1.17001 12,1.17001 17.9812,1.17001 22.83,6.01877 22.83,12 22.83,17.9813 17.9812,22.83 12,22.83 11.6262,22.83 11.2568,22.8111 10.8927,22.7741 5.8167,22.2586 1.77699,18.2377 1.2325,13.1703 1.22536,13.1039 1.21882,13.0373 1.21289,12.9705 1.20871,12.9234 1.20483,12.8762 1.20125,12.8289 1.18054,12.5553 1.17,12.2789 1.17,12 1.17,9.41057 2.07878,7.03339 3.59479,5.17001 3.9391,4.74681 4.31473,4.35011 4.71815,3.98345z M12,20.83C16.8767,20.83 20.83,16.8767 20.83,12 20.83,7.12334 16.8767,3.17001 12,3.17001L12,20.83z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.ChangeBackgroundColor();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
