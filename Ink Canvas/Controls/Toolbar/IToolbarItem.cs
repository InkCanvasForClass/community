using System.Windows;

namespace Ink_Canvas.Controls.Toolbar
{
    public interface IToolbarItem
    {
        string Id { get; }

        ToolbarSlot DefaultSlot { get; }

        int DefaultOrder { get; }

        bool DefaultVisible { get; }

        ToolbarInsertPosition DefaultPosition { get; }

        string DefaultAnchorName { get; }

        string DisplayName { get; }

        string MenuPanelName { get; }

        FrameworkElement BuildView(IToolbarHost host);
    }
}