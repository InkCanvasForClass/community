using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class CursorWithDelToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.cursorWithDel";
        public override string LocalizationKey => "FloatingBar_ClearAndMouse";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => "清除并切换光标";

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.CursorWithDelIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachCursorWithDelBtn(view);
    }
}
