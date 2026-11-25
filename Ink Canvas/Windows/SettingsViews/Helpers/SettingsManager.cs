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
                    var preserveTempFileOnFailure = false;
                    try
                    {
                        ProcessProtectionManager.WithWriteAccess(tmpPath, () => File.WriteAllText(tmpPath, text));
                        if (File.Exists(path))
                        {
                            ProcessProtectionManager.WithWriteAccess(path, () =>
                            {
                                try
                                {
                                    File.Replace(tmpPath, path, null);
                                }
                                catch (IOException replaceException)
                                {
                                    // File.Replace requires Windows to remove the destination file.
                                    // If that fails (for example, due to a file-system filter or an
                                    // external handle), fall back to overwriting the existing file
                                    // in place. This is less crash-safe than Replace, but avoids
                                    // losing an otherwise writable settings file.
                                    try
                                    {
                                        File.Copy(tmpPath, path, true);
                                        try { File.Delete(tmpPath); }
                                        catch (Exception cleanupException)
                                        {
                                            LogHelper.WriteLogToFile(
                                                $"[Settings] 回退写入成功，但清理临时文件失败: {cleanupException.Message}",
                                                LogHelper.LogType.Warning);
                                        }
                                        LogHelper.WriteLogToFile(
                                            $"[Settings] File.Replace 失败，已回退到覆盖写入: {replaceException.Message}",
                                            LogHelper.LogType.Warning);
                                    }
                                    catch (Exception copyException)
                                    {
                                        // If copying also fails, make one final attempt to write the
                                        // serialized settings directly to the destination file.
                                        // This can truncate the old file before the new content is
                                        // fully written, so it is intentionally the last resort.
                                        try
                                        {
                                            File.WriteAllText(path, text);
                                            try { File.Delete(tmpPath); }
                                            catch (Exception cleanupException)
                                            {
                                                LogHelper.WriteLogToFile(
                                                    $"[Settings] 直接写入成功，但清理临时文件失败: {cleanupException.Message}",
                                                    LogHelper.LogType.Warning);
                                            }
                                            LogHelper.WriteLogToFile(
                                                $"[Settings] File.Replace 和覆盖写入均失败，已回退到直接写入。Replace: {replaceException.Message}; Copy: {copyException.Message}",
                                                LogHelper.LogType.Warning);
                                        }
                                        catch (Exception directWriteException)
                                        {
                                            // The direct write may have truncated the destination.
                                            // Keep the complete serialized temp file for manual recovery.
                                            preserveTempFileOnFailure = true;
                                            throw new AggregateException(
                                                "File.Replace、覆盖写入和直接写入均失败。",
                                                replaceException,
                                                copyException,
                                                directWriteException);
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            ProcessProtectionManager.WithWriteAccess(path, () => File.Move(tmpPath, path));
                        }
                    }
                    catch
                    {
                        if (!preserveTempFileOnFailure)
                        {
                            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile(
                                    $"[Settings] 保存设置失败后清理临时文件 {tmpPath} 失败: {ex.Message}",
                                    LogHelper.LogType.Info);
                            }
                        }
                        throw; // Keep the temp file if the final direct write fails, as it may be the only complete copy.
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

        // 旧版本会把自动推导出的架构/运行时写进配置；统一迁回“跟随当前版本”，避免升级后仍锁死在旧值。
        public static bool MigrateUpdatePackageDefaults()
        {
            if (Settings == null) return false;
            if (Settings.UpdatePackageDefaultsMigrated) return false;

            Settings.Startup ??= new Startup();
            Settings.Startup.UpdatePackageArchitecture = UpdatePackageArchitecture.Auto;
            Settings.Startup.UpdatePackageRuntime = UpdatePackageRuntime.Auto;
            Settings.Startup.UpdatePackageDotNetVersion = UpdatePackageDotNetVersion.Auto;
            Settings.UpdatePackageDefaultsMigrated = true;
            return true;
        }

        public static bool MigrateChickenSoupSettings()
        {
            if (Settings?.Appearance == null) return false;

            var appearance = Settings.Appearance;
            if (appearance.EnabledPresetTipsSources == null && appearance.ChickenSoupSource >= 0)
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
                appearance.EnabledPresetTipsSources = presetId != null
                    ? new List<string> { presetId }
                    : new List<string> { "mottos" };
                appearance.ChickenSoupSource = -1;
                return true;
            }
            else if (appearance.EnabledPresetTipsSources != null && appearance.ChickenSoupSource >= 0)
            {
                appearance.ChickenSoupSource = -1;
                return true;
            }

            return false;
        }

        public static void MigrateVideoBoothSettings()
        {
            if (Settings?.Canvas == null) return;

            if (Settings.Canvas.LaunchSeewoVideoShowcaseForWhiteboardBooth && Settings.Canvas.VideoBoothMode == VideoBoothMode.BuiltIn)
            {
                Settings.Canvas.VideoBoothMode = VideoBoothMode.Seewo;
            }
        }
        // 旧版"退出白板 / 退出 PPT 时自动收纳"都是布尔开关，新版改成三选一的形态策略。
        // 只在用户从未写过新字段（仍是默认的"跟随进入前形态"）时把旧值映射成"收纳"，
        // 映射完清空旧字段，避免之后每次启动都重复覆盖用户的新选择。
        public static bool MigrateExitFoldModeSettings()
        {
            var automation = Settings?.Automation;
            if (automation == null) return false;

            bool changed = false;

            if (automation.IsAutoFoldWhenExitWhiteboard != null)
            {
                if (automation.IsAutoFoldWhenExitWhiteboard == true
                    && automation.ExitWhiteboardFoldMode == ExitFoldMode.RestorePrevious)
                {
                    automation.ExitWhiteboardFoldMode = ExitFoldMode.Fold;
                }

                automation.IsAutoFoldWhenExitWhiteboard = null;
                changed = true;
            }

            if (automation.IsAutoFoldAfterPPTSlideShow != null)
            {
                if (automation.IsAutoFoldAfterPPTSlideShow == true
                    && automation.ExitPPTFoldMode == ExitFoldMode.RestorePrevious)
                {
                    automation.ExitPPTFoldMode = ExitFoldMode.Fold;
                }

                automation.IsAutoFoldAfterPPTSlideShow = null;
                changed = true;
            }

            return changed;
        }
    }
}
