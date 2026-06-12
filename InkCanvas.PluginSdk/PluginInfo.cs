namespace Ink_Canvas.Plugins
{
    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public int Order { get; set; }
        public IPlugin Instance { get; set; }
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
        NotLoaded = 0,
        Loaded = 1,
        Disabled = 2,
        Error = 3
    }
}
