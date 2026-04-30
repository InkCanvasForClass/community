namespace Ink_Canvas.Plugins
{
    public interface IPlugin
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }
        string Description { get; }
        string Author { get; }
        int Order { get; }

        void Initialize(IPluginHost host);
        void Shutdown();
        object GetMainView();
        object GetSettingsView();
    }
}
