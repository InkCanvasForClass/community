using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardStrokeEraserToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.strokeEraser";
        public override string LocalizationKey => "Board_StrokeEraser";
        public override string Description => "笔画橡皮擦";

        protected override string IconGeometry => "F1 M24,24z M0,0z M11.6199,3.61372C12.8916,2.34202,14.8995,2.2837,16.1307,3.62964L21.3433,8.84225C22.615,10.1139,22.6733,12.1218,21.3274,13.353L15.1877,19.4927 5.46434,9.76928 11.6199,3.61372z M7.33167,21.36C7.08919,21.36 6.86831,21.2676 6.70232,21.116 6.69184,21.1064 6.68155,21.0966 6.67147,21.0865L2.65671,17.0718C1.385,15.8001,1.32668,13.7922,2.67262,12.561L4.14394,11.0897 12.5469,19.4927 21.3367,19.4927C21.8523,19.4927 22.2703,19.9107 22.2703,20.4263 22.2703,20.942 21.8523,21.36 21.3367,21.36L7.33167,21.36z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.SelectStrokeEraser();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
