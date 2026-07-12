using Ink_Canvas.Helpers;
using System;
using System.Windows.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal event Action<bool> PluginWhiteboardModeChanged;
        internal event Action<bool> PluginPenModeChanged;
        internal event Action<int> PluginSlideChanged;
        internal event Action<bool> PluginSlideShowStateChanged;

        private int _currentMode;
        private bool? _lastPluginPenMode;
        private bool? _lastPluginSlideShowState;

        internal int currentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode == value) return;

                bool wasWhiteboardMode = _currentMode == 1;
                _currentMode = value;
                bool isWhiteboardMode = _currentMode == 1;

                if (wasWhiteboardMode != isWhiteboardMode)
                {
                    RaisePluginEvent(PluginWhiteboardModeChanged, isWhiteboardMode, nameof(PluginWhiteboardModeChanged));
                }
            }
        }

        internal bool IsWhiteboardMode => currentMode == 1;

        private void NotifyPluginPenModeChanged(InkCanvasEditingMode editingMode)
        {
            bool? isPenMode = editingMode switch
            {
                InkCanvasEditingMode.Ink => true,
                InkCanvasEditingMode.None => false,
                _ => null,
            };

            if (!isPenMode.HasValue || _lastPluginPenMode == isPenMode) return;

            _lastPluginPenMode = isPenMode;
            RaisePluginEvent(PluginPenModeChanged, isPenMode.Value, nameof(PluginPenModeChanged));
        }

        private void NotifyPluginSlideChanged(int slideNumber)
        {
            if (slideNumber <= 0) return;
            RaisePluginEvent(PluginSlideChanged, slideNumber, nameof(PluginSlideChanged));
        }

        private void NotifyPluginSlideShowStateChanged(bool isActive)
        {
            if (_lastPluginSlideShowState == isActive) return;

            _lastPluginSlideShowState = isActive;
            RaisePluginEvent(PluginSlideShowStateChanged, isActive, nameof(PluginSlideShowStateChanged));
        }

        private static void RaisePluginEvent<T>(Action<T> handlers, T value, string eventName)
        {
            if (handlers == null) return;

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"转发插件事件 {eventName} 失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }
    }
}
