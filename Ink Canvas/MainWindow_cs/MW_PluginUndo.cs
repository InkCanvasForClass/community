using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Dictionary<string, Action<string>> _pluginUndoStateHandlers =
            new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);

        internal void RegisterPluginUndoStateHandler(string pluginId, Action<string> restoreState)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("插件 ID 不能为空。", nameof(pluginId));
            if (restoreState == null) throw new ArgumentNullException(nameof(restoreState));

            RunOnUiThread(() =>
            {
                if (_pluginUndoStateHandlers.ContainsKey(pluginId))
                    throw new InvalidOperationException($"插件 {pluginId} 已注册撤销恢复处理器。");
                _pluginUndoStateHandlers.Add(pluginId, restoreState);
            });
        }

        internal void UnregisterPluginUndoStateHandler(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return;
            RunOnUiThread(() => _pluginUndoStateHandlers.Remove(pluginId));
        }

        internal bool CommitPluginUndoState(string pluginId, string beforeState, string afterState)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new ArgumentException("插件 ID 不能为空。", nameof(pluginId));
            if (beforeState == null) throw new ArgumentNullException(nameof(beforeState));
            if (afterState == null) throw new ArgumentNullException(nameof(afterState));
            if (string.Equals(beforeState, afterState, StringComparison.Ordinal)) return false;

            var committed = false;
            RunOnUiThread(() =>
            {
                if (TryBlockFrozenPageMutation("修改插件画布内容")) return;
                if (!_pluginUndoStateHandlers.ContainsKey(pluginId))
                    throw new InvalidOperationException($"插件 {pluginId} 尚未注册撤销恢复处理器。");

                timeMachine.CommitPluginStateHistory(pluginId, beforeState, afterState);
                committed = true;
            });
            return committed;
        }

        private void ApplyPluginUndoState(string pluginId, string state)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || state == null) return;
            if (!_pluginUndoStateHandlers.TryGetValue(pluginId, out var handler)) return;

            try
            {
                handler(state);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"插件 {pluginId} 恢复撤销状态失败: {ex}",
                    LogHelper.LogType.Error);
            }
        }
    }
}
