using System.Windows.Data;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardUndoToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.undo";
        public override string LocalizationKey => "Board_Undo";
        public override string Description => "撤销";

        public override string IconGeometry => "F1 M24,24z M0,0z M8.71408,16.8493L0.874451,9.00964 8.71408,1.17001 8.71408,7.42358 15.7239,7.42358C16.7074,7.42358 17.6791,7.62744 18.583,8.02124 19.4866,8.41493 20.3023,8.98966 20.9857,9.70849 21.6689,10.4271 22.2069,11.276 22.5726,12.2047 22.9383,13.1333 23.1256,14.126 23.1256,15.1268 23.1256,16.1276 22.9383,17.1203 22.5726,18.0489 22.2069,18.9776 21.6689,19.8264 20.9857,20.5451 20.3023,21.2639 19.4866,21.8387 18.583,22.2324 17.6791,22.6262 16.7074,22.83 15.7239,22.83L10.437,22.83 10.437,19.6579 15.7239,19.6579C16.2679,19.6579 16.8086,19.5453 17.3159,19.3243 17.8235,19.1031 18.29,18.7767 18.6867,18.3594 19.0835,17.942 19.4023,17.4422 19.6211,16.8866 19.8399,16.3308 19.9534,15.7326 19.9534,15.1268 19.9534,14.5209 19.8399,13.9227 19.6211,13.367 19.4023,12.8114 19.0835,12.3115 18.6867,11.8941 18.29,11.4769 17.8235,11.1505 17.3159,10.9293 16.8086,10.7083 16.2679,10.5957 15.7239,10.5957L8.71408,10.5957 8.71408,16.8493z";

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Undo();

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            view.SetBinding(BoardToolbarButton.IsEnabledBindingProperty,
                new Binding("IsUndoEnabled") { Source = host.Window });
            host.RegisterView(Id, view);
        }
    }
}
