using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public interface IBoardToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        string IconGeometry { get; }

        FontIconData? IconKey { get; }

        ButtonPosition DefaultPosition { get; }

        FrameworkElement BuildView(IBoardToolbarHost host);

        void ApplyPosition(FrameworkElement view, ButtonPosition position);
    }
}
