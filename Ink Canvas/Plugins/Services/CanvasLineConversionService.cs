using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class CanvasLineConversionService : ICanvasLineConversionService
    {
        private readonly MainWindow _mainWindow;

        public CanvasLineConversionService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public event EventHandler<CanvasLineFinalizedEventArgs> LineFinalized
        {
            add => _mainWindow.PluginCanvasLineFinalized += value;
            remove => _mainWindow.PluginCanvasLineFinalized -= value;
        }

        public bool TryConvertToPluginState(
            string pluginId,
            string candidateToken,
            string beforeState,
            string afterState)
            => _mainWindow.TryConvertPluginCanvasLine(
                pluginId,
                candidateToken,
                beforeState,
                afterState);
    }
}
