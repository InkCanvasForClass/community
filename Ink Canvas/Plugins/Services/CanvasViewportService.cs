using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class CanvasViewportService : ICanvasViewportService
    {
        private readonly MainWindow _mainWindow;

        internal CanvasViewportService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public event EventHandler<CanvasViewportTransformEventArgs> TransformChanged
        {
            add => _mainWindow.PluginCanvasViewportTransformChanged += value;
            remove => _mainWindow.PluginCanvasViewportTransformChanged -= value;
        }
    }
}
