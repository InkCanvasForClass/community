using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    internal sealed class CanvasLayerService : ICanvasLayerService
    {
        private readonly MainWindow _mainWindow;

        public CanvasLayerService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void RegisterLayer(
            string pluginId,
            string layerId,
            CanvasLayerPlacement placement,
            Func<FrameworkElement> layerFactory,
            bool isHitTestVisible = false)
            => _mainWindow.RegisterPluginCanvasLayer(
                pluginId,
                layerId,
                placement,
                layerFactory,
                isHitTestVisible);

        public bool RemoveLayer(string pluginId, string layerId)
            => _mainWindow.RemovePluginCanvasLayer(pluginId, layerId);

        public void RemoveLayers(string pluginId)
            => _mainWindow.RemovePluginCanvasLayers(pluginId);
    }
}
