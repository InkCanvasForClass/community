// 此文件已移至 SamplePlugin 项目中，通过插件系统动态加载。
// 保留此文件仅为参考，编译时不会包含在主程序中。
// 如需使用示例组件，请安装 SamplePlugin 插件。
#if false
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    /// <summary>
    /// 示例普通组件 - 演示 ToolbarImageButton 的基本用法，支持菜单样式设置。
    /// 已移至 SamplePlugin 项目中，通过插件系统动态加载。
    /// </summary>
    internal sealed class SampleButtonToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "sample.button";
        public override string LocalizationKey => "示例按钮";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => "示例普通组件，演示 ToolbarImageButton 用法";
        public override string DisplayName => "示例按钮";

        protected override string IconGeometry => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z";

        private PopupShellContent _shell;
        private ContentControl _innerContentHost;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            if (_shell != null && _innerContentHost != null)
            {
                _innerContentHost.Visibility = _innerContentHost.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            var grid = new Grid();
            _shell = new PopupShellContent { Title = "示例弹窗" };
            _innerContentHost = new ContentControl { Visibility = Visibility.Collapsed };
            var textBlock = new TextBlock { Text = "这是一个示例弹窗内容", Margin = new Thickness(10), FontSize = 14 };
            _innerContentHost.Content = textBlock;
            grid.Children.Add(_shell);
            grid.Children.Add(_innerContentHost);
        }
    }
}
#endif
