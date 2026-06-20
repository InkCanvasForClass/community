using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class CursorWithDelToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.cursorWithDel";
        public override string LocalizationKey => "FloatingBar_ClearAndMouse";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_CursorWithDel;
        public override string IconGeometry => XamlGraphicsIconGeometries.CursorWithDelFloatingBarBtnIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.CursorWithDelIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachCursorWithDelBtn(view);
    }
}
