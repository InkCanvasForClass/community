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

        public static void SaveSettingsToFile()
        {
            var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            lock (_saveGate)
            {
                try
                {
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
                        // 回退到直接覆盖，保持旧行为仍可用
                        ProcessProtectionManager.WithWriteAccess(path, () => File.WriteAllText(path, text));
                    }

                    App.UpdateCachedSettingsJson(text);
                }
                catch (Exception ex)
                {
                    // 设置保存失败不能静默：用户感知不到 = 下次启动设置丢失
                    try
                    {
                        LogHelper.WriteLogToFile($"保存 Settings.json 失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
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
