using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardPenToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.pen";
        public override string LocalizationKey => "Board_Pen";
        public override string Description => "画笔工具";

        protected override string IconGeometry => "F1 M24,24z M0,0z M20.4786,1.42438C19.9985,1.23743 19.4847,1.15194 18.9698,1.17319 18.4549,1.19444 17.9499,1.32197 17.4869,1.54789 17.0368,1.76752 16.6358,2.07554 16.3083,2.45361L3.85516,14.9067 9.08243,20.134 21.5311,7.68529C21.9113,7.36382 22.223,6.96912 22.447,6.52438 22.6786,6.06462 22.8113,5.56167 22.8365,5.04763 22.8616,4.5336 22.7787,4.02012 22.593,3.54002 22.4073,3.05994 22.1232,2.62403 21.759,2.25988 21.3949,1.89574 20.9587,1.61132 20.4786,1.42438z M7.28056,21.1605L2.8286,16.7086 1.15912,22.83 7.28056,21.1605z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SelectPen();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
