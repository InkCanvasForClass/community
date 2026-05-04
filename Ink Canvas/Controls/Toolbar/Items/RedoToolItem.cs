using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class RedoToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.redo";
        public override string LocalizationKey => "Board_Redo";
        public override ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarCanvasControls;
        public override int DefaultOrder => 310;
        public override ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.Append;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRedo_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            host.Window.AttachSymbolIconRedo(view);
            view.SetBinding(System.Windows.UIElement.IsEnabledProperty,
                new System.Windows.Data.Binding("IsRedoEnabled") { Source = host.Window });
        }
    }
}