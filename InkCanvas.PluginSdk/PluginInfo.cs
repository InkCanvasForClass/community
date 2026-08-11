namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件运行时信息。宿主加载插件时创建，记录插件元数据与加载状态。
    /// </summary>
    public class PluginInfo
    {
        /// <summary>插件唯一标识，如 "com.example.myplugin"。</summary>
        public string Id { get; set; }
        /// <summary>插件显示名称。</summary>
        public string Name { get; set; }
        /// <summary>插件版本号。</summary>
        public string Version { get; set; }
        /// <summary>插件描述。</summary>
        public string Description { get; set; }
        /// <summary>插件作者。</summary>
        public string Author { get; set; }
        /// <summary>插件列表排序（数值越小越靠前；实际加载顺序由依赖解析决定）。旧式 DLL 插件从插件实例读取，其余默认 0。</summary>
        public int Order { get; set; }
        /// <summary>已加载的插件实例；未加载时为 null。</summary>
        public IPlugin Instance { get; set; }
        /// <summary>插件是否已成功加载。</summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// 插件清单信息
        /// </summary>
        public PluginManifest Manifest { get; set; }

        /// <summary>
        /// 插件所在目录路径
        /// </summary>
        public string PluginFolderPath { get; set; }

        /// <summary>
        /// 插件配置目录路径
        /// </summary>
        public string PluginConfigFolder { get; set; }

        /// <summary>
        /// 插件加载状态
        /// </summary>
        public PluginLoadStatus LoadStatus { get; set; } = PluginLoadStatus.NotLoaded;

        /// <summary>
        /// 加载失败时的异常信息
        /// </summary>
        public System.Exception Exception { get; set; }
    }

    /// <summary>
    /// 插件加载状态
    /// </summary>
    public enum PluginLoadStatus
    {
        /// <summary>未加载。</summary>
        NotLoaded = 0,
        /// <summary>已加载成功。</summary>
        Loaded = 1,
        /// <summary>已禁用（用户禁用或加载被拦截）。</summary>
        Disabled = 2,
        /// <summary>加载失败（详见 <see cref="PluginInfo.Exception"/>）。</summary>
        Error = 3
    }
}
