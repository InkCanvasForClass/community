using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class SeparatorToolItem : IToolbarItem
    {
        public string Id => "builtin.separator";
        public string DisplayName => "分割线";
        public string Description => "分割线";
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow();
        public bool DefaultShowSeparateBorder => false;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var border = new Border
            {
                Name = "FloatingBarSeparator",
                Margin = new Thickness(2, 0, 2, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717a")),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Tag = ToolbarRegistry.InjectedTag
            };
            return border;
        }
    }
}
