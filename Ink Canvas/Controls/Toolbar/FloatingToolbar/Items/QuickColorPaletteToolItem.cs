using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class QuickColorPaletteToolItem : IToolbarItem
    {
        public string Id => "builtin.quickColorPalette";
        public string DisplayName => FloatingBarStrings.FloatingBar_QuickPaletteMode;
        public string Description => FloatingBarStrings.ToolbarItem_Desc_QuickColorPalette;
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var control = new QuickColorPaletteControl
            {
                Tag = "ToolbarRegistryInjected"
            };

            control.ColorClicked += (s, e) =>
            {
                if (e.OriginalSource is string colorName)
                {
                    host.Window.ApplyQuickColorByName(colorName);
                }
            };

            return control;
        }

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            if (view is QuickColorPaletteControl control)
            {
                control.ApplyOrientation(orientation == Orientation.Vertical);
            }
        }
    }
}
