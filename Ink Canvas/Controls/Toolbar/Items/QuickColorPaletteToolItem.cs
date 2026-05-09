using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class QuickColorPaletteToolItem : IToolbarItem
    {
        public string Id => "builtin.quickColorPalette";

        public ToolbarSlot DefaultSlot => ToolbarSlot.FloatingBarMain;

        public int DefaultOrder => 105;

        public bool DefaultVisible => true;

        public ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.Prepend;

        public string DefaultAnchorName => null;

        public string DisplayName => "Quick Color Palette";

        public string MenuPanelName => null;

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
    }
}
