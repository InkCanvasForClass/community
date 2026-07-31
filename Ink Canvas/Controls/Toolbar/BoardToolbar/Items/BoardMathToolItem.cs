using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardMathToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.math";

        public override string LocalizationKey => "Board_Math";

        public override string Description => LocalizationKey;

        public override string IconGeometry =>
            "M2,20 L7,4 L12,20 M4,14 L10,14 M15,6 L22,6 M18.5,2.5 L18.5,9.5 M15,15 L22,15";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.OpenMathInsert();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
