using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 名言（鸡汤/一言）服务：供插件读取宿主内置的名言预设、触发白板水印名言刷新。
    /// <para>预设来源：osu 玩家语录、励志名言、高考祝福、Phigros Tips、一言（Hitokoto API）。</para>
    /// </summary>
    public interface IQuoteService
    {
        /// <summary>列出宿主内置的名言预设方案。</summary>
        IReadOnlyList<PluginQuoteScheme> GetPresetSchemes();

        /// <summary>
        /// 获取指定预设的全部语录数组。
        /// </summary>
        /// <param name="presetId">预设 ID（如 "osu"、"mottos"、"gaokao"、"phigros"）。</param>
        /// <returns>语录数组；hitokoto 或未知 ID 返回 null。</returns>
        string[] GetTipsFromPreset(string presetId);

        /// <summary>触发宿主立即刷新白板水印名言（随机选取一条，异步）。</summary>
        Task RefreshAsync();
    }

    /// <summary>
    /// 名言预设方案描述。
    /// </summary>
    public sealed class PluginQuoteScheme
    {
        /// <summary>预设 ID，供 <see cref="IQuoteService.GetTipsFromPreset"/> 使用。</summary>
        public string PresetId { get; set; } = "";

        /// <summary>预设显示名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>是否为内置预设（false 为自定义方案）。</summary>
        public bool IsPreset { get; set; }

        /// <summary>当前是否启用。</summary>
        public bool IsEnabled { get; set; }
    }
}
