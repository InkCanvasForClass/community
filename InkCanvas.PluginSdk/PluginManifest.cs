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
        /// 最低宿主版本要求，例如 "1.7.18"。低于此版本的宿主不允许加载插件。
        /// </summary>
        public string MinHostVersion { get; set; } = "";

        /// <summary>
        /// 插件版本兼容范围，例如 "^1.0.0"、">=1.0.0,<2.0.0"。留空时只比较主版本号与 API 版本。
        /// </summary>
        public string VersionRange { get; set; } = "";

        /// <summary>
        /// 插件申请的权限列表，例如 "Settings", "Hotkeys", "Network", "FileSystem"。
        /// 主机在加载插件时可向用户提示。
        /// </summary>
        public List<string> Permissions { get; set; } = new List<string>();

        /// <summary>
        /// 许可协议，例如 "MIT"、"Apache-2.0"。
        /// </summary>
        public string License { get; set; } = "";

        /// <summary>
        /// 标签列表。
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 源码仓库 URL。
        /// </summary>
        public string SourceUrl { get; set; } = "";

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
