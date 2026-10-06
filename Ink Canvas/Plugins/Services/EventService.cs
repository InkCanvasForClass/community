using System;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    internal class EventService : IEventService
    {
        public event Action<bool> WhiteboardModeChanged;
        public event Action<bool> PenModeChanged;
        public event Action<int> SlideChanged;
        public event Action SlideShowStarted;
        public event Action SlideShowEnded;
        public event Action<bool> TopMostChanged;
        public event Action AppExiting;
        public event Action<StrokeCollection, StrokeCollection> StrokesChanged;
        public event Action<int, int> WhiteboardPageChanged;
        public event Action<bool, bool> UndoRedoStateChanged;

        private readonly MainWindow _mainWindow;

        public EventService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            if (_mainWindow != null)
            {
                _mainWindow.PluginWhiteboardModeChanged += OnWhiteboardModeChanged;
                _mainWindow.PluginPenModeChanged += OnPenModeChanged;
                _mainWindow.PluginSlideChanged += OnSlideChanged;
                _mainWindow.PluginSlideShowStateChanged += OnSlideShowStateChanged;
                _mainWindow.PluginStrokesChanged += OnStrokesChanged;
                _mainWindow.PluginWhiteboardPageChanged += OnWhiteboardPageChanged;
                _mainWindow.PluginUndoRedoStateChanged += OnUndoRedoStateChanged;
                _mainWindow.PluginTopMostChanged += OnTopMostChanged;
            }
        }

        public bool IsWhiteboardMode => _mainWindow?.IsWhiteboardMode == true;

        private void OnSlideShowStateChanged(bool isActive)
        {
            if (isActive) OnSlideShowStarted();
            else OnSlideShowEnded();
        }

        internal void OnWhiteboardModeChanged(bool isBoard) => WhiteboardModeChanged?.Invoke(isBoard);
        internal void OnPenModeChanged(bool isPen) => PenModeChanged?.Invoke(isPen);
        internal void OnSlideChanged(int slide) => SlideChanged?.Invoke(slide);
        internal void OnSlideShowStarted() => SlideShowStarted?.Invoke();
        internal void OnSlideShowEnded() => SlideShowEnded?.Invoke();
        internal void OnTopMostChanged(bool topMost) => TopMostChanged?.Invoke(topMost);
        internal void OnAppExiting() => AppExiting?.Invoke();

        internal void OnStrokesChanged(StrokeCollection added, StrokeCollection removed)
            => StrokesChanged?.Invoke(added, removed);

        internal void OnWhiteboardPageChanged(int pageIndex, int pageCount)
            => WhiteboardPageChanged?.Invoke(pageIndex, pageCount);

        internal void OnUndoRedoStateChanged(bool canUndo, bool canRedo)
            => UndoRedoStateChanged?.Invoke(canUndo, canRedo);
    }
}
