using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 提供多配置文件保存、切换与热重载支持。
    /// 方案保存在 Configs/Profiles 目录下，当前生效的配置仍为 Configs/Settings.json。
    /// </summary>
    public static class ConfigProfileManager
    {
        private static readonly string ProfilesDir = Path.Combine(App.RootPath, "Configs", "Profiles");
        private static readonly string SettingsFilePath = Path.Combine(App.RootPath, "Configs", "Settings.json");
        private const string ProfileExtension = ".json";

        /// <summary>将配置文件名称转为安全文件名（去掉非法字符）。</summary>
        private static string ToSafeFileName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return "未命名";
            var invalid = Path.GetInvalidFileNameChars();
            var name = string.Join("_", profileName.Trim().Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrEmpty(name) ? "未命名" : name;
        }

        /// <summary>确保配置文件目录存在。</summary>
        public static void EnsureProfilesDirectory()
        {
            try
            {
                if (!Directory.Exists(ProfilesDir))
                {
                    ProcessProtectionManager.WithWriteAccess(ProfilesDir, () => Directory.CreateDirectory(ProfilesDir));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建配置文件目录失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>获取所有配置文件名称（不含扩展名），按名称排序。</summary>
        public static IReadOnlyList<string> ListProfileNames()
        {
            try
            {
                EnsureProfilesDirectory();
                if (!Directory.Exists(ProfilesDir)) return Array.Empty<string>();
                var files = Directory.GetFiles(ProfilesDir, "*" + ProfileExtension);
                return files
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"列举配置文件失败: {ex.Message}", LogHelper.LogType.Error);
                return Array.Empty<string>();
            }
        }

        /// <summary>获取某配置文件对应的文件路径。</summary>
        public static string GetProfilePath(string profileName)
        {
            var safe = ToSafeFileName(profileName);
            return Path.Combine(ProfilesDir, safe + ProfileExtension);
        }

        /// <summary>将当前配置的 JSON 内容保存为指定名称的配置文件。</summary>
        /// <param name="profileName">配置文件显示名称（会转为安全文件名）。</param>
        /// <param name="settingsJson">已序列化好的 Settings JSON 字符串。</param>
        /// <returns>成功返回 true。</returns>
        public static bool SaveAsProfile(string profileName, string settingsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settingsJson))
                {
                    LogHelper.WriteLogToFile("配置文件保存失败：内容为空", LogHelper.LogType.Warning);
                    return false;
                }
                EnsureProfilesDirectory();
                var path = GetProfilePath(profileName);
                ProcessProtectionManager.WithWriteAccess(path, () => File.WriteAllText(path, settingsJson));
                LogHelper.WriteLogToFile($"配置文件已保存: {ToSafeFileName(profileName)}", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存配置文件失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>将指定配置文件应用到当前配置（覆盖 Configs/Settings.json），供主窗口随后热重载。</summary>
        /// <param name="profileName">配置文件名称（与 ListProfileNames 中一致，或与保存时使用的显示名一致）。</param>
        /// <returns>成功返回 true；文件不存在或复制失败返回 false。</returns>
        public static bool ApplyProfile(string profileName)
        {
            try
            {
                var path = GetProfilePath(profileName);
                if (!File.Exists(path))
                {
                    LogHelper.WriteLogToFile($"配置文件文件不存在: {path}", LogHelper.LogType.Warning);
                    return false;
                }
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    LogHelper.WriteLogToFile("配置文件内容为空", LogHelper.LogType.Warning);
                    return false;
                }
                // 可选：校验是否为合法 Settings JSON
                try
                {
                    JsonConvert.DeserializeObject<Settings>(json);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"配置文件格式无效: {ex.Message}", LogHelper.LogType.Error);
                    return false;
                }
                var configsDir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(configsDir) && !Directory.Exists(configsDir))
                {
                    ProcessProtectionManager.WithWriteAccess(configsDir, () => Directory.CreateDirectory(configsDir));
                }
                ProcessProtectionManager.WithWriteAccess(SettingsFilePath, () => File.WriteAllText(SettingsFilePath, json));
                LogHelper.WriteLogToFile($"已应用配置文件: {profileName}（请热重载以生效）", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用配置文件失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>删除指定名称的配置文件。</summary>
        public static bool DeleteProfile(string profileName)
        {
            try
            {
                var path = GetProfilePath(profileName);
                if (!File.Exists(path)) return true;
                ProcessProtectionManager.WithWriteAccess(path, () => File.Delete(path));
                LogHelper.WriteLogToFile($"已删除配置文件: {profileName}", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除配置文件失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }
    }
}
