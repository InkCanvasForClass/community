using System.Windows;

namespace Ink_Canvas.Controls.Toolbar
{
    public interface IToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        ToolbarRuleset DefaultHidingRuleset { get; }

        bool DefaultShowSeparateBorder { get; }

        bool DefaultPreventHideOnDragClick { get; }

        FrameworkElement BuildView(IToolbarHost host);
    }
}
