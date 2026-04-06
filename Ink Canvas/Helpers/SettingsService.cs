using Ink_Canvas;
using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using UpdateChannel = Ink_Canvas.UpdateChannel;
using UpdatePackageArchitecture = Ink_Canvas.UpdatePackageArchitecture;
using TelemetryUploadLevel = Ink_Canvas.TelemetryUploadLevel;

namespace Ink_Canvas.Services
{
    public class SettingsService
    {
        private static SettingsService _instance;
        private static readonly object _lock = new object();

        private Settings _settings;
        private MainWindow _mainWindow;
        private bool _isChangingUpdateChannelInternally = false;
        private bool _isChangingUpdatePackageArchInternally = false;

        public static readonly string settingsFileName = Path.Combine("Configs", "Settings.json");

        public event Action<string, object> SettingChanged;

        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SettingsService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SettingsService() { }

        public void Initialize(Settings settings, MainWindow mainWindow = null)
        {
            _settings = settings;
            _mainWindow = mainWindow;
        }

        public Settings Settings => _settings;

        #region Core Settings Management

        public Settings LoadSettings(bool isStartup = false, bool skipAutoUpdateCheck = false)
        {
            Settings loadedSettings = null;

            try
            {
                if (File.Exists(App.RootPath + settingsFileName))
                {
                    try
                    {
                        string text = File.ReadAllText(App.RootPath + settingsFileName);
                        loadedSettings = JsonConvert.DeserializeObject<Settings>(text);

                        if (loadedSettings != null)
                        {
                            CleanupObsoleteSettings(text, ref loadedSettings);
                        }

                        if (loadedSettings == null)
                        {
                            LogHelper.WriteLogToFile("配置文件解析失败，尝试从备份恢复", LogHelper.LogType.Warning);
                            if (AutoBackupManager.TryRestoreFromBackup())
                            {
                                text = File.ReadAllText(App.RootPath + settingsFileName);
                                loadedSettings = JsonConvert.DeserializeObject<Settings>(text);
                                if (loadedSettings != null)
                                {
                                    CleanupObsoleteSettings(text, ref loadedSettings);
                                }
                            }

                            if (loadedSettings == null)
                            {
                                LogHelper.WriteLogToFile("从备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                                loadedSettings = new Settings();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"配置文件加载失败: {ex.Message}", LogHelper.LogType.Error);

                        LogHelper.WriteLogToFile("尝试从备份恢复配置文件", LogHelper.LogType.Warning);
                        if (AutoBackupManager.TryRestoreFromBackup())
                        {
                            try
                            {
                                string text = File.ReadAllText(App.RootPath + settingsFileName);
                                loadedSettings = JsonConvert.DeserializeObject<Settings>(text);
                                if (loadedSettings != null)
                                {
                                    CleanupObsoleteSettings(text, ref loadedSettings);
                                }
                            }
                            catch (Exception restoreEx)
                            {
                                LogHelper.WriteLogToFile($"从备份恢复后重新加载失败: {restoreEx.Message}", LogHelper.LogType.Error);
                                loadedSettings = new Settings();
                            }
                        }

                        if (loadedSettings == null)
                        {
                            LogHelper.WriteLogToFile("从备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                            loadedSettings = new Settings();
                        }
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("配置文件不存在，尝试从备份恢复", LogHelper.LogType.Warning);
                    if (AutoBackupManager.TryRestoreFromBackup())
                    {
                        try
                        {
                            string text = File.ReadAllText(App.RootPath + settingsFileName);
                            loadedSettings = JsonConvert.DeserializeObject<Settings>(text);
                            if (loadedSettings != null)
                            {
                                CleanupObsoleteSettings(text, ref loadedSettings);
                            }
                        }
                        catch (Exception restoreEx)
                        {
                            LogHelper.WriteLogToFile($"从备份恢复后加载失败: {restoreEx.Message}", LogHelper.LogType.Error);
                            loadedSettings = new Settings();
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                        loadedSettings = new Settings();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
                loadedSettings = new Settings();
            }

            try
            {
                if (loadedSettings?.Appearance != null)
                {
                    var preferredLanguage = loadedSettings.Appearance.Language ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(preferredLanguage))
                    {
                        LocalizationHelper.TrySetCulture(preferredLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置应用界面语言失败: {ex.Message}", LogHelper.LogType.Error);
            }

            try
            {
                ProcessProtectionManager.ApplyFromSettings();
            }
            catch
            {
            }

            _settings = loadedSettings;
            return loadedSettings;
        }

        public void SaveSettingsToFile()
        {
            if (_settings == null) return;

            var text = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            try
            {
                string configsDir = Path.Combine(App.RootPath, "Configs");
                if (!Directory.Exists(configsDir))
                {
                    ProcessProtectionManager.WithWriteAccess(configsDir, () => Directory.CreateDirectory(configsDir));
                }

                var path = App.RootPath + settingsFileName;
                ProcessProtectionManager.WithWriteAccess(path, () => File.WriteAllText(path, text));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void CleanupObsoleteSettings(string userConfigJson, ref Settings settings)
        {
            try
            {
                Settings defaultSettings = new Settings();

                JObject defaultConfigObj = JObject.FromObject(defaultSettings);
                EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(defaultConfigObj);
                JObject userConfigObj = JObject.Parse(userConfigJson);

                bool hasChanges = false;

                RemoveObsoleteProperties(userConfigObj, defaultConfigObj, ref hasChanges);

                if (hasChanges)
                {
                    string cleanedJson = userConfigObj.ToString(Formatting.Indented);
                    settings = JsonConvert.DeserializeObject<Settings>(cleanedJson);
                    SaveSettingsToFile();
                    LogHelper.WriteLogToFile("已清理过期配置项", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理过期配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(JObject defaultConfigObj)
        {
            if (defaultConfigObj == null) return;
            if (defaultConfigObj["appearance"] is JObject appearance && !appearance.ContainsKey("hitokotoCategories"))
                appearance["hitokotoCategories"] = JValue.CreateNull();
        }

        private void RemoveObsoleteProperties(JObject userObj, JObject defaultObj, ref bool hasChanges)
        {
            if (userObj == null || defaultObj == null)
                return;

            List<string> keysToRemove = new List<string>();

            foreach (var property in userObj.Properties())
            {
                string propertyName = property.Name;

                if (!defaultObj.ContainsKey(propertyName))
                {
                    keysToRemove.Add(propertyName);
                    continue;
                }

                JToken userValue = property.Value;
                JToken defaultValue = defaultObj[propertyName];

                if (userValue != null && defaultValue != null)
                {
                    if (userValue.Type == JTokenType.Object && defaultValue.Type == JTokenType.Object)
                    {
                        RemoveObsoleteProperties(userValue as JObject, defaultValue as JObject, ref hasChanges);
                    }
                    else if (userValue.Type == JTokenType.Array && defaultValue.Type == JTokenType.Array)
                    {
                        JArray userArray = userValue as JArray;
                        JArray defaultArray = defaultValue as JArray;

                        if (userArray != null && defaultArray != null && userArray.Count > 0 && defaultArray.Count > 0)
                        {
                            if (userArray[0].Type == JTokenType.Object && defaultArray[0].Type == JTokenType.Object)
                            {
                                for (int i = 0; i < userArray.Count; i++)
                                {
                                    if (userArray[i] is JObject userItemObj && defaultArray[0] is JObject defaultItemObj)
                                    {
                                        RemoveObsoleteProperties(userItemObj, defaultItemObj, ref hasChanges);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                userObj.Remove(key);
                hasChanges = true;
                LogHelper.WriteLogToFile($"已删除过期配置项: {key}", LogHelper.LogType.Event);
            }
        }

        #endregion

        #region Generic Setting Methods

        public void Set<T>(Expression<Func<Settings, T>> propertyExpression, T value, bool saveToFile = true)
        {
            if (_settings == null) return;

            var propertyName = GetPropertyName(propertyExpression);
            var property = typeof(Settings).GetProperty(propertyName);

            if (property != null)
            {
                property.SetValue(_settings, value);
                if (saveToFile) SaveSettingsToFile();
                SettingChanged?.Invoke(propertyName, value);
            }
        }

        public void Set<TCategory, TProperty>(Expression<Func<Settings, TCategory>> categoryExpression, Expression<Func<TCategory, TProperty>> propertyExpression, TProperty value, bool saveToFile = true)
        {
            if (_settings == null) return;

            var categoryPropertyName = GetPropertyName(categoryExpression);
            var categoryProperty = typeof(Settings).GetProperty(categoryPropertyName);

            if (categoryProperty != null)
            {
                var categoryInstance = categoryProperty.GetValue(_settings);
                if (categoryInstance != null)
                {
                    var propertyName = GetPropertyName(propertyExpression);
                    var property = typeof(TCategory).GetProperty(propertyName);

                    if (property != null)
                    {
                        property.SetValue(categoryInstance, value);
                        if (saveToFile) SaveSettingsToFile();
                        SettingChanged?.Invoke($"{categoryPropertyName}.{propertyName}", value);
                    }
                }
            }
        }

        public T Get<T>(Expression<Func<Settings, T>> propertyExpression)
        {
            if (_settings == null) return default;

            var propertyName = GetPropertyName(propertyExpression);
            var property = typeof(Settings).GetProperty(propertyName);

            if (property != null)
            {
                return (T)property.GetValue(_settings);
            }
            return default;
        }

        public TProperty Get<TCategory, TProperty>(Expression<Func<Settings, TCategory>> categoryExpression, Expression<Func<TCategory, TProperty>> propertyExpression)
        {
            if (_settings == null) return default;

            var categoryPropertyName = GetPropertyName(categoryExpression);
            var categoryProperty = typeof(Settings).GetProperty(categoryPropertyName);

            if (categoryProperty != null)
            {
                var categoryInstance = categoryProperty.GetValue(_settings);
                if (categoryInstance != null)
                {
                    var propertyName = GetPropertyName(propertyExpression);
                    var property = typeof(TCategory).GetProperty(propertyName);

                    if (property != null)
                    {
                        return (TProperty)property.GetValue(categoryInstance);
                    }
                }
            }
            return default;
        }

        private string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            throw new ArgumentException("Expression is not a property access", nameof(expression));
        }

        #endregion

        #region Convenience Methods for Common Settings

        #region Advanced Settings

        public void SetNoFocusMode(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Advanced, a => a.IsNoFocusMode, enabled, saveToFile);
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mainWindow.ApplyNoFocusMode();
                    if (_settings.Advanced.IsAlwaysOnTop)
                    {
                        _mainWindow.ApplyAlwaysOnTop();
                    }
                }));
            }
        }

        public void SetAlwaysOnTop(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Advanced, a => a.IsAlwaysOnTop, enabled, saveToFile);
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mainWindow.ApplyAlwaysOnTop();
                    _mainWindow.UpdateUIAccessTopMostVisibility();
                }));
            }
        }

        public void SetUIAccessTopMost(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Advanced, a => a.EnableUIAccessTopMost, enabled, saveToFile);
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mainWindow.ApplyUIAccessTopMost();
                }));
            }
            App.IsUIAccessTopMostEnabled = enabled;
        }

        #endregion

        #region Canvas Settings

        public void SetInkFadeEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EnableInkFade, enabled, saveToFile);
        }

        public void SetInkFadeTime(int timeMs, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.InkFadeTime, timeMs, saveToFile);
        }

        public void SetHideInkFadeControlInPenMenu(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.HideInkFadeControlInPenMenu, enabled, saveToFile);
        }

        public void SetEraserAutoSwitchBack(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EnableEraserAutoSwitchBack, enabled, saveToFile);
        }

        public void SetEraserAutoSwitchBackDelay(int seconds, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EraserAutoSwitchBackDelaySeconds, seconds, saveToFile);
        }

        public void SetBrushAutoRestore(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EnableBrushAutoRestore, enabled, saveToFile);
        }

        public void SetBrushAutoRestoreTimes(string times, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.BrushAutoRestoreTimes, times, saveToFile);
        }

        public void SetBrushAutoRestoreColor(string color, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.BrushAutoRestoreColor, color, saveToFile);
        }

        public void SetBrushAutoRestoreWidth(double width, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.BrushAutoRestoreWidth, width, saveToFile);
        }

        public void SetBrushAutoRestoreAlpha(int alpha, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.BrushAutoRestoreAlpha, alpha, saveToFile);
        }

        public void SetInkWidth(double width, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.InkWidth, width, saveToFile);
        }

        public void SetHighlighterWidth(double width, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.HighlighterWidth, width, saveToFile);
        }

        public void SetInkAlpha(double alpha, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.InkAlpha, alpha, saveToFile);
        }

        public void SetEraserSize(int size, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EraserSize, size, saveToFile);
        }

        public void SetEraserType(int type, bool saveToFile = true)
        {
            Set(s => s.Canvas, c => c.EraserType, type, saveToFile);
        }

        #endregion

        #region Appearance Settings

        public void SetTheme(int themeIndex, bool saveToFile = true)
        {
            Set(s => s.Appearance, a => a.Theme, themeIndex, saveToFile);
        }

        public void SetLanguage(string languageCode, bool saveToFile = true)
        {
            Set(s => s.Appearance, a => a.Language, languageCode ?? string.Empty, saveToFile);
            LocalizationHelper.TrySetCulture(languageCode);
        }

        public void SetFloatingBarOpacity(double opacity, bool saveToFile = true)
        {
            Set(s => s.Appearance, a => a.ViewboxFloatingBarOpacityValue, opacity, saveToFile);
        }

        public void SetFloatingBarScale(double scale, bool saveToFile = true)
        {
            Set(s => s.Appearance, a => a.ViewboxFloatingBarScaleTransformValue, scale, saveToFile);
        }

        #endregion

        #region PowerPoint Settings

        public void SetPPTOnlyMode(bool enabled, bool saveToFile = true)
        {
            Set(s => s.ModeSettings, m => m.IsPPTOnlyMode, enabled, saveToFile);
        }

        public void SetPowerPointSupport(bool enabled, bool saveToFile = true)
        {
            Set(s => s.PowerPointSettings, p => p.PowerPointSupport, enabled, saveToFile);
        }

        public void SetShowGestureButtonInSlideShow(bool enabled, bool saveToFile = true)
        {
            Set(s => s.PowerPointSettings, p => p.ShowGestureButtonInSlideShow, enabled, saveToFile);
        }

        public void SetPPTTimeCapsuleEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.PowerPointSettings, p => p.EnablePPTTimeCapsule, enabled, saveToFile);
        }

        public void SetPPTTimeCapsulePosition(int position, bool saveToFile = true)
        {
            Set(s => s.PowerPointSettings, p => p.PPTTimeCapsulePosition, position, saveToFile);
        }

        #endregion

        #region Gesture Settings

        public void SetTwoFingerZoomEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Gesture, g => g.IsEnableTwoFingerZoom, enabled, saveToFile);
        }

        public void SetTwoFingerTranslateEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Gesture, g => g.IsEnableTwoFingerTranslate, enabled, saveToFile);
        }

        public void SetTwoFingerRotationEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Gesture, g => g.IsEnableTwoFingerRotation, enabled, saveToFile);
        }

        public void SetMultiTouchModeEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Gesture, g => g.IsEnableMultiTouchMode, enabled, saveToFile);
        }

        #endregion

        #region Startup Settings

        public void SetAutoUpdate(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.IsAutoUpdate, enabled, saveToFile);
        }

        public void SetUpdateChannel(UpdateChannel channel, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.UpdateChannel, channel, saveToFile);
        }

        public void SetTelemetryUploadLevel(TelemetryUploadLevel level, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.TelemetryUploadLevel, level, saveToFile);
        }

        #endregion

        #region Automation Settings

        public void SetAutoSaveStrokesEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Automation, a => a.IsEnableAutoSaveStrokes, enabled, saveToFile);
        }

        public void SetAutoSaveStrokesInterval(int minutes, bool saveToFile = true)
        {
            Set(s => s.Automation, a => a.AutoSaveStrokesIntervalMinutes, minutes, saveToFile);
        }

        public void SetAutoSavedStrokesLocation(string path, bool saveToFile = true)
        {
            Set(s => s.Automation, a => a.AutoSavedStrokesLocation, path, saveToFile);
        }

        #endregion

        #region InkToShape Settings

        public void SetInkToShapeEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.InkToShape, i => i.IsInkToShapeEnabled, enabled, saveToFile);
        }

        public void SetShapeRecognitionEngine(int engine, bool saveToFile = true)
        {
            Set(s => s.InkToShape, i => i.ShapeRecognitionEngine, engine, saveToFile);
        }

        #endregion

        #region Security Settings

        public void SetPasswordEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Security, s => s.PasswordEnabled, enabled, saveToFile);
        }

        public void SetProcessProtectionEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Security, s => s.EnableProcessProtection, enabled, saveToFile);
        }

        #endregion

        #region Camera Settings

        public void SetCameraRotationAngle(int angle, bool saveToFile = true)
        {
            Set(s => s.Camera, c => c.RotationAngle, angle, saveToFile);
        }

        public void SetCameraResolution(int width, int height, bool saveToFile = true)
        {
            Set(s => s.Camera, c => c.ResolutionWidth, width, saveToFile);
            Set(s => s.Camera, c => c.ResolutionHeight, height, saveToFile);
        }

        #endregion

        #endregion

        #region Startup Settings - Complete Functionality

        public bool CheckRunAtStartup()
        {
            try
            {
                return File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\Ink Canvas Annotation.lnk");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
                return false;
            }
        }

        // Settings 中没有 IsRunAtStartup 属性，所以这个方法被注释掉了
        // public void SetRunAtStartup(bool enabled, bool saveToFile = true)
        // {
        //     Set(s => s.Startup, s => s.IsRunAtStartup, enabled, saveToFile);
        // }

        public void SetFoldAtStartup(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.IsFoldAtStartup, enabled, saveToFile);
        }

        public void SetUpdatePackageArchitecture(UpdatePackageArchitecture arch, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.UpdatePackageArchitecture, arch, saveToFile);
        }

        public void SetAutoUpdateWithSilence(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.IsAutoUpdateWithSilence, enabled, saveToFile);
        }

        public void SetAutoUpdateWithSilenceStartTime(string time, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.AutoUpdateWithSilenceStartTime, time, saveToFile);
        }

        public void SetAutoUpdateWithSilenceEndTime(string time, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.AutoUpdateWithSilenceEndTime, time, saveToFile);
        }

        public void SetNibModeEnabled(bool enabled, bool saveToFile = true)
        {
            Set(s => s.Startup, s => s.IsEnableNibMode, enabled, saveToFile);
        }

        public void CheckUpdateChannelAndTelemetryConsistency(bool isLoaded)
        {
            if (_settings == null) return;

            var currentChannel = _settings.Startup.UpdateChannel;
            if (currentChannel == UpdateChannel.Release) return;

            if (!_settings.Startup.HasAcceptedTelemetryPrivacy)
            {
                _settings.Startup.UpdateChannel = UpdateChannel.Release;
                DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
                SaveSettingsToFile();
                LogHelper.WriteLogToFile($"启动检测 | 用户未同意隐私协议，已切换回 Release 通道");
                return;
            }

            if (_settings.Startup.TelemetryUploadLevel == TelemetryUploadLevel.None)
            {
                _isChangingUpdateChannelInternally = true;
                try
                {
                    _settings.Startup.UpdateChannel = UpdateChannel.Release;
                    DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
                    SaveSettingsToFile();
                }
                finally
                {
                    _isChangingUpdateChannelInternally = false;
                }
                LogHelper.WriteLogToFile($"启动检测 | 用户未启用遥测，已切换回 Release 通道");
            }
        }

        #endregion

        #region Helper Methods

        public static string GetThemeName(int themeIndex)
        {
            return themeIndex switch
            {
                0 => "浅色主题",
                1 => "深色主题",
                2 => "跟随系统",
                _ => "未知主题"
            };
        }

        public static string GetLanguageCode(int languageIndex)
        {
            return languageIndex switch
            {
                1 => "zh-CN",
                2 => "en-US",
                _ => string.Empty
            };
        }

        public static int GetLanguageIndex(string languageCode)
        {
            return languageCode switch
            {
                "zh-CN" => 1,
                "en-US" => 2,
                _ => 0
            };
        }

        #endregion
    }
}
