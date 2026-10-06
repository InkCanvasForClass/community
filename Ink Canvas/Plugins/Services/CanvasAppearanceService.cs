using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class CanvasAppearanceService : ICanvasAppearanceService
    {
        private readonly MainWindow _mainWindow;

        public CanvasAppearanceService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public string GetContrastingForegroundColor()
            => _mainWindow.GetPluginCanvasContrastingForegroundColor();
    }
}
