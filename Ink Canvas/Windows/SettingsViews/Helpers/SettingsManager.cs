using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using ProcessProtectionManager = Ink_Canvas.Helpers.ProcessProtectionManager;
namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class SettingsManager
    {
        public static Settings Settings { get; set; } = new Settings();

        public static string SettingsFileName { get; } = Path.Combine("Configs", "Settings.json");

        public static bool ReadEnableWindowChromeRendering()
        {
            try
            {
                var path = Path.Combine(App.RootPath, SettingsFileName);
                if (!File.Exists(path)) return Settings?.Startup?.EnableWindowChromeRendering ?? false;

                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);
                return obj.SelectToken("startup.enableWindowChromeRendering")?.Value<bool>() ?? false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return Settings?.Startup?.EnableWindowChromeRendering ?? false;
            }
        }

        // 全局 SaveSettingsToFile 串行化：419 个调用点跨 UI 线程、公告轮询线程、插件线程，
        // 互相 File.WriteAllText 同路径写时部分抛 IOException 被 catch 吞掉只记日志，用户感知不到
        // 设置已丢失。先到先写、后到排队。
        private static readonly object _saveGate = new object();

        // 最近一次保存失败原因。清理旧配置链路（CleanupObsoleteSettings）保存失败时
        // 必须回退清理结果并据此决定能否更新启动缓存，否则磁盘仍是旧配置而缓存却是
        // 清理后的内容，后续 ReloadSettingsFromFile 会继续写失败。
        internal static string LastSaveError { get; private set; }

        public static void SaveSettingsToFile() => TrySaveSettingsToFile();

        public static bool TrySaveSettingsToFile()
        {
            lock (_saveGate)
            {
                LastSaveError = null;
                try
                {
                    var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                    string configsDir = Path.Combine(App.RootPath, "Configs");
                    if (!Directory.Exists(configsDir))
                    {
                        ProcessProtectionManager.WithWriteAccess(configsDir, () => Directory.CreateDirectory(configsDir));
                    }

                    var path = Path.Combine(App.RootPath, SettingsFileName);
                    // 临时文件 + File.Replace 原子替换，避免断电/进程被杀导致 Settings.json 半截。
                    // 同目录移动替换是原子操作（Windows 同卷 NTFS 保证）。
                    var tmpPath = path + ".tmp";
                    try
                    {
                        ProcessProtectionManager.WithWriteAccess(tmpPath, () => File.WriteAllText(tmpPath, text));
                        if (File.Exists(path))
                        {
                            ProcessProtectionManager.WithWriteAccess(path, () => File.Replace(tmpPath, path, null));
                        }
                        else
                        {
                            ProcessProtectionManager.WithWriteAccess(path, () => File.Move(tmpPath, path));
                        }
                    }
                    catch
                    {
                        try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                        throw; // Keep the last complete settings file if atomic replacement fails.
                    }

                    App.UpdateCachedSettingsJson(text);
                    return true;
                }
                catch (Exception ex)
                {
                    LastSaveError = ex.Message;
                    // 设置保存失败不能静默：用户感知不到 = 下次启动设置丢失
                    try
                    {
                        LogHelper.WriteLogToFile($"保存 Settings.json 失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                    return false;
                }
            }
        }

        public static bool TrySaveCalibration(Ink.TouchCalibrationRecord record, int? nibWidth = null)
        {
            if (record != null && !record.IsValid()) return false;
            lock (_saveGate)
            {
                var previous = Settings.Advanced.TouchCalibration;
                var previousNib = Settings.Advanced.NibModeBoundsWidth;
                Settings.Advanced.TouchCalibration = record;
                if (nibWidth.HasValue) Settings.Advanced.NibModeBoundsWidth = Math.Clamp(nibWidth.Value, 1, 50);
                if (TrySaveSettingsToFile())
                {
                    (System.Windows.Application.Current.MainWindow as MainWindow)?.ResetTouchCalibrationInput();
                    SettingsActionHub.OnNibModeBoundsWidthChanged();
                    return true;
                }
                Settings.Advanced.TouchCalibration = previous;
                Settings.Advanced.NibModeBoundsWidth = previousNib;
                return false;
            }
        }

        public static bool MigrateAutoStartSetting()
        {
            lock (_saveGate)
            {
                try
                {
                    if (Settings == null) return false;

                    Settings.Startup ??= new Startup();
                    if (Settings.Startup.HasExplicitRunAtStartup) return false;

                    bool hasExistingRegistration = AutoStartHelper.HasAnyRegistration();
                    // 保存失败时仍沿用已有自启状态，但不标记为已持久化，以便重试。
                    Settings.Startup.SetInferredRunAtStartup(hasExistingRegistration);

                    var path = Path.Combine(App.RootPath, SettingsFileName);
                    string originalText;
                    try
                    {
                        originalText = File.ReadAllText(path);
                    }
                    catch (FileNotFoundException)
                    {
                        originalText = null;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        originalText = null;
                    }

                    // 只补写迁移字段，保留文件中的其他配置，不重新序列化整个 Settings。
                    var config = originalText == null
                        ? new JObject()
                        : JsonConvert.DeserializeObject<JObject>(originalText, new JsonSerializerSettings
                        {
                            DateParseHandling = DateParseHandling.None
                        });
                    if (config == null) throw new JsonSerializationException("配置文件必须为 JSON 对象");

                    var startupProperty = config.Property("startup", StringComparison.OrdinalIgnoreCase);
                    if (startupProperty != null && startupProperty.Value.Type != JTokenType.Null
                        && startupProperty.Value is not JObject)
                    {
                        throw new JsonSerializationException("startup 配置必须为 JSON 对象");
                    }

                    var startupConfig = startupProperty?.Value as JObject;
                    if (startupConfig == null)
                    {
                        startupConfig = new JObject();
                        if (startupProperty == null) config["startup"] = startupConfig;
                        else startupProperty.Value = startupConfig;
                    }
                    startupConfig["runAtStartup"] = hasExistingRegistration;
                    string text = config.ToString(Formatting.Indented);
                    string configsDir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(configsDir))
                    {
                        ProcessProtectionManager.WithWriteAccess(configsDir, () => Directory.CreateDirectory(configsDir));
                    }
                    ProcessProtectionManager.WithWriteAccess(path, () => File.WriteAllText(path, text));

                    Settings.Startup.RunAtStartup = hasExistingRegistration;
                    App.UpdateCachedSettingsJson(text);
                    LogHelper.WriteLogToFile(
                        $"[AutoStart] 已迁移开机自启设置，检测到已有快捷方式: {hasExistingRegistration}",
                        LogHelper.LogType.Info);
                    return true;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[AutoStart] 迁移开机自启设置失败: {ex.Message}", LogHelper.LogType.Warning);
                    return false;
                }
            }
        }

        public static void MigrateChickenSoupSettings()
        {
            if (Settings?.Appearance == null) return;

            var appearance = Settings.Appearance;
            if ((appearance.EnabledPresetTipsSources == null || appearance.EnabledPresetTipsSources.Count == 0)
                && appearance.ChickenSoupSource >= 0)
            {
                string presetId = null;
                switch (appearance.ChickenSoupSource)
                {
                    case 0: presetId = "osu"; break;
                    case 1: presetId = "mottos"; break;
                    case 2: presetId = "gaokao"; break;
                    case 3: presetId = "hitokoto"; break;
                    case 4: presetId = "phigros"; break;
                }
                if (presetId != null)
                {
                    appearance.EnabledPresetTipsSources = new List<string> { presetId };
                }
            }
        }
    }
}
