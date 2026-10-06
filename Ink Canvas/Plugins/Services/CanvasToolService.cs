using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class CanvasToolService : ICanvasToolService
    {
        private readonly MainWindow _mainWindow;

        public CanvasToolService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public bool TryActivateTool(string pluginId, string toolId, out ICanvasToolSession session)
            => _mainWindow.TryActivatePluginCanvasTool(pluginId, toolId, out session);

        public void DeactivateTools(string pluginId)
            => _mainWindow.DeactivatePluginCanvasTools(pluginId);
    }
}
