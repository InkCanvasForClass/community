using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 随机点名"选择方案"（学生档案）管理器。
    /// 把 Names.txt / Replace.txt 当作"当前生效的方案"，把各方案内容持久化到 Settings.json，
    /// 切换方案时把对应方案内容写回 Names.txt / Replace.txt。
    /// </summary>
    public static class NameRosterManager
    {
        public static string NamesFilePath => App.RootPath + "Names.txt";
        public static string ReplaceFilePath => App.RootPath + "Replace.txt";

        /// <summary>
        /// 当前选中的方案，若未配置或找不到则返回 null。
        /// </summary>
        public static NameRoster GetSelectedRoster()
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return null;
            if (settings.NameRosters == null || settings.NameRosters.Count == 0) return null;

            string guid = settings.SelectedNameRosterGuid;
            if (string.IsNullOrEmpty(guid)) return null;

            return settings.NameRosters.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Guid) && string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 读取当前 Names.txt / Replace.txt 的内容（用于"保存为方案"）。
        /// </summary>
        public static (string namesContent, string replaceContent) ReadCurrentFiles()
        {
            string names = "";
            string replace = "";
            try { if (File.Exists(NamesFilePath)) names = File.ReadAllText(NamesFilePath); } catch { }
            try { if (File.Exists(ReplaceFilePath)) replace = File.ReadAllText(ReplaceFilePath); } catch { }
            return (names, replace);
        }

        /// <summary>
        /// 直接写入 Names.txt / Replace.txt（用于临时切换或恢复快照）。
        /// </summary>
        public static void WriteCurrentFiles(string namesContent, string replaceContent)
        {
            try
            {
                ProcessProtectionManager.WithWriteAccess(NamesFilePath, () =>
                {
                    File.WriteAllText(NamesFilePath, namesContent ?? "");
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"写 Names.txt 失败: {ex.Message}", LogHelper.LogType.Error);
            }

            try
            {
                ProcessProtectionManager.WithWriteAccess(ReplaceFilePath, () =>
                {
                    File.WriteAllText(ReplaceFilePath, replaceContent ?? "");
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"写 Replace.txt 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 把指定方案的内容写回 Names.txt / Replace.txt，使其成为当前生效名单。
        /// </summary>
        public static void ApplyRoster(NameRoster roster)
        {
            if (roster == null) return;
            WriteCurrentFiles(roster.NamesContent, roster.ReplaceContent);
        }

        /// <summary>
        /// 选中方案并应用到当前名单文件。
        /// </summary>
        public static void SelectAndApply(string guid)
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return;

            settings.SelectedNameRosterGuid = guid ?? "";
            SettingsManager.SaveSettingsToFile();

            var roster = GetSelectedRoster();
            if (roster != null) ApplyRoster(roster);
        }

        /// <summary>
        /// 把当前 Names.txt / Replace.txt 保存到指定方案（覆盖该方案内容）。
        /// </summary>
        public static void SaveCurrentFilesToRoster(string guid)
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return;

            var roster = settings.NameRosters?.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Guid) && string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase));
            if (roster == null) return;

            var (names, replace) = ReadCurrentFiles();
            roster.NamesContent = names;
            roster.ReplaceContent = replace;
            SettingsManager.SaveSettingsToFile();
        }

        /// <summary>
        /// 新建一个方案，内容取自当前 Names.txt / Replace.txt（若为空则留空）。
        /// 返回新方案的 Guid。
        /// </summary>
        public static string AddRoster(string name)
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return null;

            if (settings.NameRosters == null) settings.NameRosters = new System.Collections.Generic.List<NameRoster>();

            var (names, replace) = ReadCurrentFiles();
            var roster = new NameRoster(System.Guid.NewGuid().ToString("N"), name)
            {
                NamesContent = names,
                ReplaceContent = replace
            };
            settings.NameRosters.Add(roster);
            SettingsManager.SaveSettingsToFile();
            return roster.Guid;
        }

        /// <summary>
        /// 重命名方案。
        /// </summary>
        public static void RenameRoster(string guid, string newName)
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return;

            var roster = settings.NameRosters?.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Guid) && string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase));
            if (roster == null) return;

            roster.Name = newName;
            SettingsManager.SaveSettingsToFile();
        }

        /// <summary>
        /// 删除方案。若删除的是当前方案，清空选中状态（保留当前 Names.txt 内容）。
        /// </summary>
        public static void DeleteRoster(string guid)
        {
            var settings = SettingsManager.Settings?.RandSettings;
            if (settings == null) return;
            if (settings.NameRosters == null) return;

            var roster = settings.NameRosters.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Guid) && string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase));
            if (roster == null) return;

            settings.NameRosters.Remove(roster);

            if (string.Equals(settings.SelectedNameRosterGuid, guid, StringComparison.OrdinalIgnoreCase))
            {
                settings.SelectedNameRosterGuid = "";
            }

            SettingsManager.SaveSettingsToFile();
        }
    }
}