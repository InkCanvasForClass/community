using System;

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
            }
        }

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
    }
}
