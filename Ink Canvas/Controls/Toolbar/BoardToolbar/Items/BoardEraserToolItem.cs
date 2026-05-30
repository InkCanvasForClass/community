using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardEraserToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.eraser";
        public override string LocalizationKey => "Board_Eraser";
        public override string Description => "橡皮擦";

        protected override string IconGeometry => "F1 M24,24z M0,0z M15.6314,20.7262L22.7921,13.5655C24.3494,12.141,24.2819,9.81776,22.8105,8.34633L16.7793,2.31508C15.3547,0.757753,13.0315,0.825236,11.5601,2.29666L4.38099,9.47574 15.6314,20.7262z M14.2172,22.1404L2.96677,10.89 1.20761,12.6491C-0.34971,14.0737,-0.281711,16.3974,1.18971,17.8688L6.15089,22.83 13.5276,22.83 14.2172,22.1404z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SelectEraser();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
