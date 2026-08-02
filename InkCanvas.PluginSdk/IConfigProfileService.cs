using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 配置方案服务：供插件管理宿主的「配置方案」（一套 Settings.json 的快照）。
    /// 与设置窗口里的配置方案管理共用同一存储目录。
    /// </summary>
    public interface IConfigProfileService
    {
        /// <summary>列出所有已保存的方案名。</summary>
        IReadOnlyList<string> ListProfiles();

        /// <summary>获取指定方案的存储文件路径。</summary>
        string GetProfilePath(string profileName);

        /// <summary>把 settingsJson 内容保存为指定名称的方案。</summary>
        bool SaveAsProfile(string profileName, string settingsJson);

        /// <summary>应用指定方案（覆盖当前 Settings.json 并热重载）。返回是否成功。</summary>
        bool ApplyProfile(string profileName);

        /// <summary>删除指定方案。返回是否成功。</summary>
        bool DeleteProfile(string profileName);
    }
}
