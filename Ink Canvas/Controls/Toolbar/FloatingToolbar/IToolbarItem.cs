using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    public interface IToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        string IconGeometry { get; }

        FontIconData? IconKey { get; }

        ToolbarRuleset DefaultHidingRuleset { get; }

        bool DefaultShowSeparateBorder { get; }

        bool DefaultPreventHideOnDragClick { get; }

        FrameworkElement BuildView(IToolbarHost host);

        void ApplyOrientation(FrameworkElement view, Orientation orientation) { }
    }
}
