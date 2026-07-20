using Microsoft.Extensions.DependencyInjection;
using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件抽象基类。参考 ClassIsland 的 PluginBase 设计。
    /// </summary>
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

        /// <summary>
        /// 初始化插件（旧版签名，保持向后兼容）。
        /// 新插件请使用 Initialize(IPluginHost, IServiceCollection) 重载。
        /// </summary>
        public virtual void Initialize(IPluginHost host)
        {
            Host = host;
        }

        /// <summary>
        /// 初始化插件（新版签名，支持 DI 服务注册）。
        /// 默认调用旧版 Initialize(host) 以保持兼容。
        /// 新插件应重写此方法。
        /// </summary>
        public virtual void Initialize(IPluginHost host, IServiceCollection services)
        {
            Initialize(host);
        }

        /// <summary>
        /// IPlugin.Initialize 的显式实现，转发到新签名。
        /// </summary>
        void IPlugin.Initialize(IPluginHost host)
        {
            Initialize(host, host.Services);
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
