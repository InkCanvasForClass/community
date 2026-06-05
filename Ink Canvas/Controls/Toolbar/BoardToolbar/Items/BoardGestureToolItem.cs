using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardGestureToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.gesture";
        public override string LocalizationKey => "Board_Gesture";
        public override string Description => "手势";
        public override ButtonPosition DefaultPosition => ButtonPosition.First;

        protected override string IconGeometry => XamlGraphicsIconGeometries.DisabledGestureIcon;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.ToggleGesture();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
