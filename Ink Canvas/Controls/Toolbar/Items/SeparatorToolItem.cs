using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class SeparatorToolItem : IToolbarItem
    {
        public string Id => "builtin.separator";
        public string DisplayName => FloatingBarStrings.ToolbarItem_Desc_Separator;
        public string Description => FloatingBarStrings.ToolbarItem_Desc_Separator;
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var border = new Border
            {
                Name = "FloatingBarSeparator",
                Margin = new Thickness(2, 0, 2, 0),
                Width = 2,
                MinWidth = 2,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717a")),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Tag = ToolbarRegistry.InjectedTag
            };
            return border;
        }
    }
}
