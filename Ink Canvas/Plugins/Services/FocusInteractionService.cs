using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class FocusInteractionService : IFocusInteractionService
    {
        private readonly MainWindow _mainWindow;

        internal FocusInteractionService(MainWindow mainWindow)
            => _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        public void SetActive(string pluginId, bool active)
            => _mainWindow.SetPluginFocusInteraction(pluginId, active);
    }
}
