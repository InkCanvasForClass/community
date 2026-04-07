using System;

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

        object? GetSettingsView();
        object? GetMainView();
    }

    public interface IPluginHost
    {
        void Log(string message);
        void LogError(string message, Exception? ex = null);
        T? GetService<T>() where T : class;
        void RegisterService<T>(T service) where T : class;
    }
}