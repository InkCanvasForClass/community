using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class QuickColorPaletteToolItem : IToolbarItem
    {
        public string Id => "builtin.quickColorPalette";
        public string DisplayName => FloatingBarStrings.FloatingBar_QuickPaletteMode;
        public string Description => FloatingBarStrings.ToolbarItem_Desc_QuickColorPalette;
        public string IconGeometry => XamlGraphicsIconGeometries.ColorPaletteIconGeometry;
        public FontIconData? IconKey => null;
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public bool DefaultShowSeparateBorder => false;
        public bool DefaultPreventHideOnDragClick => false;

        public IReadOnlyList<PluginToolbarSettingInfo> CustomSettings { get; } = new List<PluginToolbarSettingInfo>
        {
            new PluginToolbarSettingInfo
            {
                Key = ComponentSettingKeys.DisplayMode,
                DisplayName = FloatingBarStrings.ToolbarPage_DisplayMode,
                Description = FloatingBarStrings.ToolbarPage_QuickPaletteDisplayModeDesc,
                Type = PluginToolbarSettingType.ComboBox,
                Options = new List<string>
                {
                    ThemeStrings.Theme_QuickPalette_DoubleRow,
                    ThemeStrings.Theme_QuickPalette_SingleRow
                },
                OptionValues = new List<string> { "1", "0" },
                DefaultValue = "1"
            }
        };

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
