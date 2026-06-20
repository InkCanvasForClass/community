using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class UndoToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.undo";
        public override string LocalizationKey => "Board_Undo";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.Board_Undo;
        public override string IconGeometry => XamlGraphicsIconGeometries.UndoIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconUndo_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            host.Window.AttachSymbolIconUndo(view);
            view.SetBinding(System.Windows.UIElement.IsEnabledProperty,
                new System.Windows.Data.Binding("IsUndoEnabled") { Source = host.Window });
        }
    }
}
