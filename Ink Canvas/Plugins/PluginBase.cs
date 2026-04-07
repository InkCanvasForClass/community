using System;

namespace Ink_Canvas.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Version { get; }
        public abstract string Description { get; }
        public virtual string Author => "Unknown";
        public virtual int Order => 0;

        protected IPluginHost? Host { get; private set; }

        public virtual void Initialize(IPluginHost host)
        {
            Host = host;
            Host.Log($"[Plugin:{Name}] Initialized");
        }

        public virtual void Shutdown()
        {
            Host?.Log($"[Plugin:{Name}] Shutdown");
            Host = null;
        }

        public virtual object? GetSettingsView() => null;
        public virtual object? GetMainView() => null;

        protected void Log(string message) => Host?.Log($"[Plugin:{Name}] {message}");
        protected void LogError(string message, Exception? ex = null) => Host?.LogError($"[Plugin:{Name}] {message}", ex);
    }
}