using System;

namespace Ink_Canvas.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        protected IPluginHost Host { get; private set; }

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Version { get; }
        public abstract string Description { get; }
        public abstract string Author { get; }
        public abstract int Order { get; }

        public virtual void Initialize(IPluginHost host)
        {
            Host = host;
        }

        public virtual void Shutdown()
        {
        }

        public virtual object GetMainView()
        {
            return null;
        }

        public virtual object GetSettingsView()
        {
            return null;
        }

        protected void Log(string message)
        {
            if (Host != null)
            {
                Host.Log(message);
            }
        }

        protected void LogError(string message, Exception ex = null)
        {
            if (Host != null)
            {
                Host.LogError(message, ex);
            }
        }

        protected T GetService<T>() where T : class
        {
            if (Host != null)
            {
                return Host.GetService<T>();
            }
            return null;
        }
    }
}
