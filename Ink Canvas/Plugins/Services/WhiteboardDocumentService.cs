using System;

namespace Ink_Canvas.Plugins
{
    internal sealed class WhiteboardDocumentService : IWhiteboardDocumentService
    {
        private readonly MainWindow _mainWindow;

        public WhiteboardDocumentService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public WhiteboardPageInfo CurrentPage => _mainWindow.GetCurrentPluginWhiteboardPage();

        public event EventHandler<WhiteboardPageChangingEventArgs> PageChanging
        {
            add => _mainWindow.PluginWhiteboardPageChanging += value;
            remove => _mainWindow.PluginWhiteboardPageChanging -= value;
        }

        public event EventHandler<WhiteboardPageChangedEventArgs> PageChanged
        {
            add => _mainWindow.PluginWhiteboardDocumentChanged += value;
            remove => _mainWindow.PluginWhiteboardDocumentChanged -= value;
        }

        public event EventHandler<WhiteboardPageRemovedEventArgs> PageRemoved
        {
            add => _mainWindow.PluginWhiteboardPageRemoved += value;
            remove => _mainWindow.PluginWhiteboardPageRemoved -= value;
        }

        public event EventHandler PageClearing
        {
            add => _mainWindow.PluginWhiteboardPageClearing += value;
            remove => _mainWindow.PluginWhiteboardPageClearing -= value;
        }

        public bool TryBeginMutation(string action)
            => _mainWindow.TryBeginPluginWhiteboardMutation(action);

        public void RegisterPageStateProvider(string pluginId, IWhiteboardPageStateProvider provider)
            => _mainWindow.RegisterPluginPageStateProvider(pluginId, provider);

        public void RegisterLegacyStateImporter(string pluginId, IWhiteboardLegacyStateImporter importer)
            => _mainWindow.RegisterPluginLegacyStateImporter(pluginId, importer);

        public void UnregisterPageStateProvider(string pluginId)
            => _mainWindow.UnregisterPluginPageStateProvider(pluginId);
    }
}
