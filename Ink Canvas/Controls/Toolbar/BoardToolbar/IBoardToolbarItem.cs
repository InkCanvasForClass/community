using Ink_Canvas.Controls;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public interface IBoardToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        ButtonPosition DefaultPosition { get; }

        FrameworkElement BuildView(IBoardToolbarHost host);

        void ApplyPosition(FrameworkElement view, ButtonPosition position);
    }
}
