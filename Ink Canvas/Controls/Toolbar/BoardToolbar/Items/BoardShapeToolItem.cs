using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardShapeToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.shape";
        public override string LocalizationKey => "Board_Shape";
        public override string Description => "几何图形";

        protected override string IconGeometry => "F1 M24,24z M0,0z M17.8604,10.7597L12.0068,1.17001 6.13961,10.7597 17.8604,10.7597z M10.9345,13.2403L1.34476,13.2403 1.34476,22.83 10.9345,22.83 10.9345,13.2403z M17.8604,13.2403C15.2122,13.2403 13.0655,15.387 13.0655,18.0352 13.0655,20.6833 15.2122,22.83 17.8604,22.83 20.5085,22.83 22.6552,20.6833 22.6552,18.0352 22.6552,15.387 20.5085,13.2403 17.8604,13.2403z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SelectShape();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
