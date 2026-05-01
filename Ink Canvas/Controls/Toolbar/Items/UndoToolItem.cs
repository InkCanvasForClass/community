using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class UndoToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.undo";
        public override string LocalizationKey => "Board_Undo";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarCanvasControls;
        public override int DefaultOrder => 300;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.Append;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconUndo_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            host.Window.AttachSymbolIconUndo(view);
            view.SetBinding(System.Windows.UIElement.IsEnabledProperty,
                new System.Windows.Data.Binding("IsEnabled") { ElementName = "BtnUndo" });
        }
    }
}