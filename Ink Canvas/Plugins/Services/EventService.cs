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

        private static void Publish(Delegate handlers, Action<Delegate> invoke)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { invoke(handler); }
                catch (Exception ex) { Helpers.LogHelper.WriteLogToFile($"[Plugin event] {handler.Method}: {ex}", Helpers.LogHelper.LogType.Warning); }
            }
        }

        internal void OnWhiteboardModeChanged(bool isBoard) => Publish(WhiteboardModeChanged, h => ((Action<bool>)h)(isBoard));
        internal void OnPenModeChanged(bool isPen) => Publish(PenModeChanged, h => ((Action<bool>)h)(isPen));
        internal void OnSlideChanged(int slide) => Publish(SlideChanged, h => ((Action<int>)h)(slide));
        internal void OnSlideShowStarted() => Publish(SlideShowStarted, h => ((Action)h)());
        internal void OnSlideShowEnded() => Publish(SlideShowEnded, h => ((Action)h)());
        internal void OnTopMostChanged(bool topMost) => Publish(TopMostChanged, h => ((Action<bool>)h)(topMost));
        internal void OnAppExiting() => Publish(AppExiting, h => ((Action)h)());

        internal void OnStrokesChanged(StrokeCollection added, StrokeCollection removed)
            => Publish(StrokesChanged, h => ((Action<StrokeCollection, StrokeCollection>)h)(added, removed));

        internal void OnWhiteboardPageChanged(int pageIndex, int pageCount)
            => Publish(WhiteboardPageChanged, h => ((Action<int, int>)h)(pageIndex, pageCount));

        internal void OnUndoRedoStateChanged(bool canUndo, bool canRedo)
            => Publish(UndoRedoStateChanged, h => ((Action<bool, bool>)h)(canUndo, canRedo));
    }
}
