using System;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="INameRosterService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.NameRosterManager"/>。
    /// 变更类方法先做前置检查（设置/方案存在），再委托给宿主，返回是否有意义的结果。
    /// </summary>
    internal sealed class NameRosterService : INameRosterService
    {
        private static Ink_Canvas.RandSettings RandSettings
            => Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.Settings?.RandSettings;

        public PluginNameRoster GetSelectedRoster()
        {
            try
            {
                var roster = Ink_Canvas.Helpers.NameRosterManager.GetSelectedRoster();
                return roster == null ? null : Map(roster);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.GetSelectedRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public (string NamesContent, string ReplaceContent) ReadCurrentFiles()
        {
            try
            {
                var (names, replace) = Ink_Canvas.Helpers.NameRosterManager.ReadCurrentFiles();
                return (names, replace);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.ReadCurrentFiles failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return ("", "");
            }
        }

        public void WriteCurrentFiles(string namesContent, string replaceContent)
        {
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.WriteCurrentFiles(namesContent, replaceContent);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.WriteCurrentFiles failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public bool ApplyRoster(PluginNameRoster roster)
        {
            if (roster == null) return false;
            if (RandSettings == null) return false;
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.ApplyRoster(Map(roster));
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.ApplyRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool SelectAndApply(string guid)
        {
            if (string.IsNullOrEmpty(guid) || RandSettings == null) return false;
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.SelectAndApply(guid);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.SelectAndApply failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool SaveCurrentFilesToRoster(string guid)
        {
            if (RandSettings?.NameRosters == null
                || !RandSettings.NameRosters.Any(r => string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                return false;
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.SaveCurrentFilesToRoster(guid);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.SaveCurrentFilesToRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public string AddRoster(string name)
        {
            try { return Ink_Canvas.Helpers.NameRosterManager.AddRoster(name); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.AddRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public bool RenameRoster(string guid, string newName)
        {
            if (RandSettings?.NameRosters == null
                || !RandSettings.NameRosters.Any(r => string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                return false;
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.RenameRoster(guid, newName);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.RenameRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool DeleteRoster(string guid)
        {
            if (RandSettings?.NameRosters == null
                || !RandSettings.NameRosters.Any(r => string.Equals(r.Guid, guid, StringComparison.OrdinalIgnoreCase)))
                return false;
            try
            {
                Ink_Canvas.Helpers.NameRosterManager.DeleteRoster(guid);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"NameRosterService.DeleteRoster failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        private static PluginNameRoster Map(Ink_Canvas.NameRoster r)
        {
            return new PluginNameRoster
            {
                Guid = r.Guid ?? "",
                Name = r.Name ?? "",
                NamesContent = r.NamesContent ?? "",
                ReplaceContent = r.ReplaceContent ?? "",
            };
        }

        private static Ink_Canvas.NameRoster Map(PluginNameRoster r)
        {
            return new Ink_Canvas.NameRoster
            {
                Guid = r.Guid ?? "",
                Name = r.Name ?? "",
                NamesContent = r.NamesContent ?? "",
                ReplaceContent = r.ReplaceContent ?? "",
            };
        }
    }
}
