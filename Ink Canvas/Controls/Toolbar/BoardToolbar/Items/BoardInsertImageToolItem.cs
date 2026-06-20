using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardInsertImageToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.insertImage";
        public override string LocalizationKey => "Board_InsertImage";
        public override string Description => "插入图片";

        public override string IconGeometry => "F1 M24,24z M0,0z M19,3H5C3.9,3 3,3.9 3,5v14c0,1.1 0.9,2 2,2h14c1.1,0 2-0.9 2-2V5C21,3.9 20.1,3 19,3zM19,19H5V5h14V19z M17,7c-1.1,0-2,0.9-2,2s0.9,2 2,2 2-0.9 2-2S18.1,7 17,7zM7,17l2.5-3.01 1.96,2.36 2.54-3.21L17,17H7z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.InsertImage();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
