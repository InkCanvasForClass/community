using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardScreenshotToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.screenshot";
        public override string LocalizationKey => "Tools_Screenshot";
        public override string Description => "截屏";
        public override string IconGeometry => XamlGraphicsIconGeometries.ScreenshotIconGeometry;

        // 截图相关设置为全局设置，通过自定义面板呈现。
        public override Func<FrameworkElement> CustomSettingsPanelFactory => ScreenshotSettingsPanelBuilder.Build;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.SymbolIconScreenshot_MouseUp(sender, e);
    }
}
