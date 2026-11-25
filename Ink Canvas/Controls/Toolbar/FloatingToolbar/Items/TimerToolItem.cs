using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class TimerToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.timer";
        public override string LocalizationKey => "QuickPanel_Timer";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.QuickPanel_Timer;
        public override string IconGeometry => XamlGraphicsIconGeometries.TimerIconGeometry;

        // 点击要打开计时器窗口：等工具栏动画落定再执行，避免建窗的重活把动画拖住。
        protected override bool OpensUiOnClick => true;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageCountdownTimer_MouseUp(sender, e);
    }
}
