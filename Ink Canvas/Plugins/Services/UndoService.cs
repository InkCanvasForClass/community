using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class UndoService : IUndoService
    {
        private readonly MainWindow _mainWindow;

        public UndoService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void RegisterStateHandler(string pluginId, Action<string> restoreState)
            => _mainWindow.RegisterPluginUndoStateHandler(pluginId, restoreState);

        public void UnregisterStateHandler(string pluginId)
            => _mainWindow.UnregisterPluginUndoStateHandler(pluginId);

        public bool CommitState(string pluginId, string beforeState, string afterState)
            => _mainWindow.CommitPluginUndoState(pluginId, beforeState, afterState);
    }
}
