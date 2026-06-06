using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public interface IBoardToolbarHost
    {
        MainWindow Window { get; }

        void RegisterView(string id, FrameworkElement view);

        FrameworkElement FindView(string id);

        void SwitchToPreviousPage();

        void SwitchToNextPage();

        void AddWhiteboardPage();

        void DeleteWhiteboardPage();

        void ToggleGesture();

        void ChangeBackgroundColor();

        void SelectTool();

        void SelectPen();

        void SelectEraser();

        void SelectStrokeEraser();

        void SelectShape();

        void InsertImage();

        void Undo();

        void Redo();

        void ToggleInkFreeze();

        void OpenTools();

        void ExitWhiteboard();

        bool CanUndo { get; }

        bool CanRedo { get; }

        bool CanSwitchToPreviousPage { get; }

        bool CanSwitchToNextPage { get; }

        bool CanAddNewPage { get; }

        bool CanDeletePage { get; }

        string CurrentPageInfo { get; }

        void UpdatePageInfo();
    }
}
