using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardToolsToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.tools";
        public override string LocalizationKey => "Board_Tools";
        public override string Description => "工具";
        public override ButtonPosition DefaultPosition => ButtonPosition.First;

        public override string IconGeometry => "F1 M24,24z M0,0z M3.336,1.17001C2.13975,1.17001,1.17,2.13976,1.17,3.33601L1.17,8.75101C1.17,9.94726,2.13975,10.917,3.336,10.917L8.751,10.917C9.94725,10.917,10.917,9.94726,10.917,8.75101L10.917,3.33601C10.917,2.13976,9.94725,1.17001,8.751,1.17001L3.336,1.17001z M15.249,1.17001C14.0527,1.17001,13.083,2.13976,13.083,3.33601L13.083,8.75101C13.083,9.94726,14.0527,10.917,15.249,10.917L20.664,10.917C21.8602,10.917,22.83,9.94726,22.83,8.75101L22.83,3.33601C22.83,2.13976,21.8602,1.17001,20.664,1.17001L15.249,1.17001z M3.336,13.083C2.13975,13.083,1.17,14.0528,1.17,15.249L1.17,20.664C1.17,21.8603,2.13975,22.83,3.336,22.83L8.751,22.83C9.94725,22.83,10.917,21.8603,10.917,20.664L10.917,15.249C10.917,14.0528,9.94725,13.083,8.751,13.083L3.336,13.083z M15.249,13.083C14.0527,13.083,13.083,14.0528,13.083,15.249L13.083,20.664C13.083,21.8603,14.0527,22.83,15.249,22.83L20.664,22.83C21.8602,22.83,22.83,21.8603,22.83,20.664L22.83,15.249C22.83,14.0528,21.8602,13.083,20.664,13.083L15.249,13.083z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.OpenTools();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
