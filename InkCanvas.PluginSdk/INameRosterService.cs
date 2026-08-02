using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 点名花名册服务：供插件管理宿主「随机点名」功能的学生花名册。
    /// 与宿主设置里的花名册管理共用同一存储（Names.txt / Replace.txt / 方案目录）。
    /// </summary>
    public interface INameRosterService
    {
        /// <summary>当前选中的花名册（含名单内容与替换规则）。</summary>
        PluginNameRoster GetSelectedRoster();

        /// <summary>读取当前生效的 Names.txt / Replace.txt 原始内容。</summary>
        (string NamesContent, string ReplaceContent) ReadCurrentFiles();

        /// <summary>写入当前生效的 Names.txt / Replace.txt。</summary>
        void WriteCurrentFiles(string namesContent, string replaceContent);

        /// <summary>把给定花名册内容应用为当前生效名单。返回是否成功。</summary>
        bool ApplyRoster(PluginNameRoster roster);

        /// <summary>按 guid 选中并应用对应花名册。返回是否成功。</summary>
        bool SelectAndApply(string guid);

        /// <summary>把当前生效名单内容保存到指定 guid 的花名册。返回是否成功。</summary>
        bool SaveCurrentFilesToRoster(string guid);

        /// <summary>新建一个花名册，返回其 guid。</summary>
        string AddRoster(string name);

        /// <summary>重命名指定花名册。返回是否成功。</summary>
        bool RenameRoster(string guid, string newName);

        /// <summary>删除指定花名册。返回是否成功。</summary>
        bool DeleteRoster(string guid);
    }

    /// <summary>
    /// 花名册（与宿主 Settings.NameRoster 一致）。
    /// </summary>
    public sealed class PluginNameRoster
    {
        /// <summary>唯一标识。</summary>
        public string Guid { get; set; } = "";

        /// <summary>方案名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>名单内容（每行一人）。</summary>
        public string NamesContent { get; set; } = "";

        /// <summary>替换规则内容（每行一条）。</summary>
        public string ReplaceContent { get; set; } = "";
    }
}
