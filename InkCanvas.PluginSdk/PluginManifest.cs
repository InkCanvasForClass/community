using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件清单元数据，从 manifest.json 文件加载。
    /// </summary>
    public class PluginManifest
    {
        /// <summary>
        /// 插件唯一标识符，例如 "com.example.myplugin"
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 插件版本号，例如 "1.0.0"
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// 插件描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 插件作者
        /// </summary>
        public string Author { get; set; } = "";

        /// <summary>
        /// 入口程序集文件名，例如 "MyPlugin.dll"
        /// </summary>
        public string EntranceAssembly { get; set; } = "";

        /// <summary>
        /// 目标 InkCanvas API 版本
        /// </summary>
        public string ApiVersion { get; set; } = "";

        /// <summary>
        /// 插件图标路径，默认为 "icon.png"
        /// </summary>
        public string Icon { get; set; } = "icon.png";

        /// <summary>
        /// 插件项目 URL
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// 插件依赖列表
        /// </summary>
        public List<PluginDependency> Dependencies { get; set; } = new List<PluginDependency>();
    }

    /// <summary>
    /// 插件依赖描述
    /// </summary>
    public class PluginDependency
    {
        /// <summary>
        /// 依赖的插件 ID
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 依赖的最低版本
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// 是否为必需依赖
        /// </summary>
        public bool IsRequired { get; set; } = true;
    }
}
