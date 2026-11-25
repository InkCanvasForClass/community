using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class SingleDrawToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.singleDraw";
        public override string LocalizationKey => "QuickPanel_SingleDraw";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.QuickPanel_SingleDraw;
        public override string IconGeometry => XamlGraphicsIconGeometries.SingleDrawIconGeometry;

        // 点击要打开随机抽人窗口：等工具栏动画落定再执行，避免建窗的重活把动画拖住。
        protected override bool OpensUiOnClick => true;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconRandOne_MouseUp(sender, e);
    }
}
