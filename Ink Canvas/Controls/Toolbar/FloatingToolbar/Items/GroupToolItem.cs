using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class GroupToolItem : IToolbarItem
    {
        public string Id => "builtin.group";
        public string DisplayName => FloatingBarStrings.ToolbarPage_GroupChildren;
        public string Description => FloatingBarStrings.ToolbarItem_Desc_Group;
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

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            if (view is StackPanel panel)
                panel.Orientation = orientation;
        }
    }
}
