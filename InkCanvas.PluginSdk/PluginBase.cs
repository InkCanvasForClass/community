using Microsoft.Extensions.DependencyInjection;
using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件抽象基类。
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        /// <summary>
        /// 宿主 API 入口。在 Initialize 阶段由宿主注入，用于日志、服务注册/获取与工具栏注册。
        /// </summary>
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

        /// <summary>插件唯一标识，从 <see cref="Manifest"/> 读取；无清单时为空字符串。</summary>
        public virtual string Id => Manifest?.Id ?? "";
        /// <summary>插件显示名称，从 <see cref="Manifest"/> 读取；无清单时为空字符串。</summary>
        public virtual string Name => Manifest?.Name ?? "";
        /// <summary>插件版本号，从 <see cref="Manifest"/> 读取；无清单时为空字符串。</summary>
        public virtual string Version => Manifest?.Version ?? "";
        /// <summary>插件描述，从 <see cref="Manifest"/> 读取；无清单时为空字符串。</summary>
        public virtual string Description => Manifest?.Description ?? "";
        /// <summary>插件作者，从 <see cref="Manifest"/> 读取；无清单时为空字符串。</summary>
        public virtual string Author => Manifest?.Author ?? "";
        /// <summary>插件列表排序（数值越小越靠前；实际加载顺序由依赖解析决定）。默认 0。</summary>
        public virtual int Order => 0;

        /// <summary>
        /// 初始化插件（旧版签名，保持向后兼容）。
        /// 新插件请使用 Initialize(IPluginHost, IServiceCollection) 重载。
        /// </summary>
        /// <param name="host">宿主 API 入口。</param>
        public virtual void Initialize(IPluginHost host)
        {
            Host = host;
        }

        /// <summary>
        /// 初始化插件（新版签名，支持 DI 服务注册）。
        /// 默认调用旧版 Initialize(host) 以保持兼容。
        /// 新插件应重写此方法。
        /// </summary>
        /// <param name="host">宿主 API 入口。</param>
        /// <param name="services">依赖注入服务集合，插件可在此阶段注册服务。</param>
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

        /// <summary>卸载插件时调用，释放插件持有的资源。</summary>
        public virtual void Shutdown()
        {
        }

        /// <summary>获取插件主视图控件（宿主在浮动栏/组件库中渲染）。返回 null 表示无主视图。</summary>
        /// <returns>主视图控件；null 表示无。</returns>
        public virtual object GetMainView()
        {
            return null;
        }

        /// <summary>获取插件设置视图控件（宿主在插件设置页中渲染）。返回 null 表示无设置视图。</summary>
        /// <returns>设置视图控件；null 表示无。</returns>
        public virtual object GetSettingsView()
        {
            return null;
        }

        /// <summary>记录普通日志（经宿主写入当前插件日志目录）。</summary>
        /// <param name="message">日志消息。</param>
        protected void Log(string message)
        {
            if (Host != null)
            {
                Host.Log(message);
            }
        }

        /// <summary>记录错误日志（经宿主写入当前插件日志目录）。</summary>
        /// <param name="message">错误描述。</param>
        /// <param name="ex">关联异常；可为 null。</param>
        protected void LogError(string message, Exception ex = null)
        {
            if (Host != null)
            {
                Host.LogError(message, ex);
            }
        }

        /// <summary>从宿主 DI 容器获取服务；未注册时返回 null。</summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <returns>服务实例；未注册时返回 null。</returns>
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
