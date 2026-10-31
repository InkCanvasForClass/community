using Ink_Canvas.Properties;
using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class ScreenshotToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.screenshot";
        public override string LocalizationKey => "Tools_Screenshot";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.Tools_Screenshot;
        public override string IconGeometry => XamlGraphicsIconGeometries.ScreenshotIconGeometry;

        // 截图相关设置为全局设置，通过自定义面板呈现。
        public override Func<FrameworkElement> CustomSettingsPanelFactory => ScreenshotSettingsPanelBuilder.Build;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconScreenshot_MouseUp(sender, e);
    }
}
