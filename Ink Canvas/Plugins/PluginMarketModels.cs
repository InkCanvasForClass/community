using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件市场索引，对应 index.json 的顶层结构。
    /// </summary>
    public class PluginMarketIndex
    {
        /// <summary>
        /// 插件列表
        /// </summary>
        public List<PluginMarketEntry> Plugins { get; set; } = new List<PluginMarketEntry>();

        /// <summary>
        /// 下载镜像列表，键为镜像名，值为镜像根 URL。
        /// 下载链接中的 {root} 会被替换为所选镜像的值。
        /// </summary>
        public Dictionary<string, string> DownloadMirrors { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 市场中的单个插件条目。
    /// </summary>
    public class PluginMarketEntry
    {
        /// <summary>
        /// 插件元数据
        /// </summary>
        public PluginManifest Manifest { get; set; } = new PluginManifest();

        /// <summary>
        /// 插件图标 URL（可含 {root} 模板）
        /// </summary>
        public string IconUrl { get; set; } = "";

        /// <summary>
        /// 插件包下载 URL（可含 {root} 模板）
        /// </summary>
        public string DownloadUrl { get; set; } = "";

        /// <summary>
        /// 下载文件的 SHA256 校验值（可选）
        /// </summary>
        public string DownloadSha256 { get; set; } = "";

        /// <summary>
        /// 下载量
        /// </summary>
        public long DownloadCount { get; set; }

        /// <summary>
        /// 点赞/收藏数
        /// </summary>
        public long StarsCount { get; set; }

        /// <summary>
        /// README 文档 URL（可含 {root} 模板）
        /// </summary>
        public string ReadmeUrl { get; set; } = "";
    }

    /// <summary>
    /// 插件市场源信息（自定义源配置）。
    /// </summary>
    public class PluginMarketSourceInfo
    {
        /// <summary>
        /// 源唯一标识
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 索引 ZIP 或 JSON 的下载 URL
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// 选择的镜像名
        /// </summary>
        public string SelectedMirror { get; set; } = "";

        /// <summary>
        /// 持久化时不写入，只用于 XAML 绑定显示。
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string Display
        {
            get
            {
                if (string.Equals(Id, PluginMarketSourcesService.OfficialSource.Id, System.StringComparison.OrdinalIgnoreCase))
                    return "官方源";
                return Id;
            }
        }
    }
}
