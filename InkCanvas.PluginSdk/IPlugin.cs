namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件接口。每个插件必须有一个实现此接口的类并用 <see cref="PluginEntranceAttribute"/> 标记；
    /// 也可从 <see cref="PluginBase"/> 继承以获得默认实现。
    /// </summary>
    public interface IPlugin
    {
        /// <summary>插件唯一标识，如 "com.example.myplugin"。</summary>
        string Id { get; }
        /// <summary>插件显示名称。</summary>
        string Name { get; }
        /// <summary>插件版本号。</summary>
        string Version { get; }
        /// <summary>插件描述。</summary>
        string Description { get; }
        /// <summary>插件作者。</summary>
        string Author { get; }
        /// <summary>插件列表排序（数值越小越靠前；实际加载顺序由依赖解析决定）。</summary>
        int Order { get; }

        /// <summary>初始化插件。所有注册动作（服务、工具栏项、IPC 处理器等）必须在此时完成。</summary>
        /// <param name="host">宿主 API 入口。</param>
        void Initialize(IPluginHost host);
        /// <summary>卸载插件时调用，释放插件持有的资源。</summary>
        void Shutdown();
        /// <summary>获取插件主视图控件；返回 null 表示无。</summary>
        /// <returns>主视图控件；null 表示无。</returns>
        object GetMainView();
        /// <summary>获取插件设置视图控件；返回 null 表示无。</summary>
        /// <returns>设置视图控件；null 表示无。</returns>
        object GetSettingsView();
    }
}
