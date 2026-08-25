using Ink_Canvas.Helpers;
using System;
using System.Windows.Controls;
using System.Windows.Ink;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal event Action<bool> PluginWhiteboardModeChanged;
        internal event Action<bool> PluginPenModeChanged;
        internal event Action<int> PluginSlideChanged;
        internal event Action<bool> PluginSlideShowStateChanged;
        internal event Action<StrokeCollection, StrokeCollection> PluginStrokesChanged;
        internal event Action<int, int> PluginWhiteboardPageChanged;
        internal event Action<bool, bool> PluginUndoRedoStateChanged;
        internal event Action<bool> PluginTopMostChanged;

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
                    if (!isWhiteboardMode) DeactivateActivePluginCanvasToolForModeChange();
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

        private static void RaisePluginEvent<T1, T2>(Action<T1, T2> handlers, T1 value1, T2 value2, string eventName)
        {
            if (handlers == null) return;

            foreach (Action<T1, T2> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value1, value2);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"转发插件事件 {eventName} 失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        /// <summary>向插件通知窗口置顶状态变化。</summary>
        internal void NotifyPluginTopMostChanged(bool topMost)
            => RaisePluginEvent(PluginTopMostChanged, topMost, nameof(PluginTopMostChanged));
    }
}
