// 此文件已移至 SamplePlugin 项目中，通过插件系统动态加载。
// 保留此文件仅为参考，编译时不会包含在主程序中。
// 如需使用示例组件，请安装 SamplePlugin 插件。
#if false
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    /// <summary>
    /// 示例自定义控件插件 - 已移至 SamplePlugin 项目中，通过插件系统动态加载。
    /// </summary>
    internal sealed class SampleCustomControlToolItem : IToolbarItem
    {
        public string Id => "sample.customControl";
        public string DisplayName => "示例自定义控件";
        public string Description => "示例自定义控件插件，支持滑块、开关、复选框、下拉框等控制类型";
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 245, 245, 245)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Tag = ToolbarRegistry.InjectedTag
            };
            border.SetResourceReference(Border.BackgroundProperty, "FloatBarBackground");

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = "示例控件",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            label.SetResourceReference(ForegroundProperty, "FloatBarForeground");
            panel.Children.Add(label);

            var slider = new Slider
            {
                Width = 80,
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(slider);

            border.Child = panel;
            return border;
        }

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
        }
    }
}
#endif
