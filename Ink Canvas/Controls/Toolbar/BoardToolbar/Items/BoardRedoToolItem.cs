using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardRedoToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.redo";
        public override string LocalizationKey => "Board_Redo";
        public override string Description => "重做";
        public override ButtonPosition DefaultPosition => ButtonPosition.Last;

        protected override string IconGeometry => "F1 M24,24z M0,0z M15.2859,16.8493L23.1255,9.00964 15.2859,1.17001 15.2859,7.42358 8.27607,7.42358C7.29262,7.42358 6.32086,7.62744 5.41703,8.02124 4.51341,8.41493 3.69773,8.98966 3.01434,9.70849 2.33111,10.4271 1.79312,11.276 1.42741,12.2047 1.06174,13.1333 0.874422,14.126 0.874422,15.1268 0.874422,16.1276 1.06174,17.1203 1.42741,18.0489 1.79312,18.9776 2.33111,19.8264 3.01434,20.5451 3.69773,21.2639 4.51341,21.8387 5.41703,22.2324 6.32086,22.6262 7.29262,22.83 8.27607,22.83L13.563,22.83 13.563,19.6579 8.27607,19.6579C7.7321,19.6579 7.19139,19.5453 6.68406,19.3243 6.17652,19.1031 5.70999,18.7767 5.31333,18.3594 4.91651,17.942 4.59775,17.4422 4.37894,16.8866 4.1601,16.3308 4.04656,15.7326 4.04656,15.1268 4.04656,14.5209 4.1601,13.9227 4.37894,13.367 4.59775,12.8114 4.91651,12.3115 5.31333,11.8941 5.70999,11.4769 6.17652,11.1505 6.68406,10.9293 7.19139,10.7083 7.7321,10.5957 8.27607,10.5957L15.2859,10.5957 15.2859,16.8493z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Redo();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
            => host.RegisterView(Id, view);
    }
}
