using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    /// <summary>
    /// 小白板浮动工具栏组件
    /// 提供一个按钮用于打开/关闭浮窗小白板
    /// 用户可通过工具栏配置添加或移除此组件
    /// </summary>
    internal sealed class MiniWhiteboardToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.miniWhiteboard";
        public override string LocalizationKey => "FloatingBar_MiniWhiteboard";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.FloatingBar_MiniWhiteboard;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ToggleMiniWhiteboard();

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachMiniWhiteboardBtn(view);
    }
}
