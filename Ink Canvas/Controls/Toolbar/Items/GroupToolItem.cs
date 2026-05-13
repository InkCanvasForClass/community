using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class GroupToolItem : IToolbarItem
    {
        public string Id => "builtin.group";
        public string DisplayName => "分组";
        public string Description => "将多个工具组合在一起显示";
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            return panel;
        }
    }
}
