using System;

namespace Ink_Canvas.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        protected IPluginHost Host { get; private set; }

        /// <summary>
        /// 插件清单信息，从 manifest.json 加载。如果清单存在，则 Id/Name/Version 等属性优先从清单读取。
        /// </summary>
        public PluginManifest Manifest { get; set; }

        /// <summary>
        /// 插件配置目录路径
        /// </summary>
        public string PluginConfigFolder { get; set; } = "";

        /// <summary>
        /// 插件所在目录路径
        /// </summary>
        public string PluginFolder { get; set; } = "";

        public virtual string Id => Manifest?.Id ?? "";
        public virtual string Name => Manifest?.Name ?? "";
        public virtual string Version => Manifest?.Version ?? "";
        public virtual string Description => Manifest?.Description ?? "";
        public virtual string Author => Manifest?.Author ?? "";
        public virtual int Order => 0;

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
