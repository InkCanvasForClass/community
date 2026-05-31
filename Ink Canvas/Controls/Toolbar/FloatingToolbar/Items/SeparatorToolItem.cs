using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
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

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            if (view is Border border)
            {
                if (orientation == Orientation.Vertical)
                {
                    border.Margin = new Thickness(0, 2, 0, 2);
                    border.Width = 36;
                    border.MinWidth = 36;
                    border.Height = 2;
                    border.MinHeight = 2;
                    border.HorizontalAlignment = HorizontalAlignment.Center;
                    border.VerticalAlignment = VerticalAlignment.Stretch;
                    border.BorderThickness = new Thickness(0, 1, 0, 0);
                }
                else
                {
                    border.Margin = new Thickness(2, 0, 2, 0);
                    border.Width = 2;
                    border.MinWidth = 2;
                    border.Height = 36;
                    border.MinHeight = 36;
                    border.HorizontalAlignment = HorizontalAlignment.Stretch;
                    border.VerticalAlignment = VerticalAlignment.Center;
                    border.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
        }
    }
}
