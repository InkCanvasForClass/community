using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ink_Canvas.Services
{
    /// <summary>
    /// 设置存储服务（M11 寄生提取首站）。承载从 MainWindow 搬出的纯设置读写逻辑。
    /// 全局设置实例经 <see cref="SettingsManager.Settings"/> 静态可达，故本类零依赖注入；
    /// 禁止持有 MainWindow 或任何 Visual 类型引用。
    /// </summary>
    internal sealed class SettingsStore
    {
        /// <summary>
        /// 将当前内存中的 Settings 序列化为格式化的 JSON 并写入应用程序配置文件（位于 App.RootPath 下的 Configs 目录或根设置文件）。
        /// </summary>
        /// <remarks>
        /// 在写入前会确保目标目录/文件具有写入权限（使用 ProcessProtectionManager）。任何写入失败或异常都会被吞掉，调用方不会收到异常抛出。
        /// </remarks>
        public static void SaveSettingsToFile() => SettingsManager.SaveSettingsToFile();

        /// <summary>
        /// 将应用设置重置为推荐的默认配置。
        /// </summary>
        /// <remarks>
        /// 该方法会重新创建全局 Settings 实例并应用推荐值，覆盖大部分子模块配置（如外观、画布、自动化、PPT、手势、高级选项等）。
        /// 在重置过程中会保留并恢复当前 Settings.Automation 中的 AutoDelSavedFiles 与 AutoDelSavedFilesDaysThreshold 两项值以避免意外删除策略变化。
        /// </remarks>
        public static void SetSettingsToRecommendation()
        {
            var AutoDelSavedFilesDays = SettingsManager.Settings.Automation.AutoDelSavedFiles;
            var AutoDelSavedFilesDaysThreshold = SettingsManager.Settings.Automation.AutoDelSavedFilesDaysThreshold;
            SettingsManager.Settings = new Settings();
            SettingsManager.Settings.Advanced.IsSpecialScreen = true;
            SettingsManager.Settings.Advanced.IsQuadIR = false;
            SettingsManager.Settings.Advanced.TouchMultiplier = 0.3;
            SettingsManager.Settings.Advanced.NibModeBoundsWidth = 5;
            SettingsManager.Settings.Advanced.FingerModeBoundsWidth = 20;
            SettingsManager.Settings.Advanced.NibModeBoundsWidthThresholdValue = 2.5;
            SettingsManager.Settings.Advanced.FingerModeBoundsWidthThresholdValue = 2.5;
            SettingsManager.Settings.Advanced.NibModeBoundsWidthEraserSize = 0.8;
            SettingsManager.Settings.Advanced.FingerModeBoundsWidthEraserSize = 0.8;
            SettingsManager.Settings.Advanced.EraserBindTouchMultiplier = true;
            SettingsManager.Settings.Advanced.IsLogEnabled = true;
            SettingsManager.Settings.Advanced.IsSecondConfirmWhenShutdownApp = false;
            SettingsManager.Settings.Advanced.IsEnableEdgeGestureUtil = false;
            SettingsManager.Settings.Advanced.EdgeGestureUtilOnlyAffectBlackboardMode = false;
            SettingsManager.Settings.Advanced.IsEnableFullScreenHelper = false;
            SettingsManager.Settings.Advanced.IsEnableAvoidFullScreenHelper = OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows11;
            SettingsManager.Settings.Advanced.IsEnableForceFullScreen = false;
            SettingsManager.Settings.Advanced.IsEnableDPIChangeDetection = false;
            SettingsManager.Settings.Advanced.IsEnableResolutionChangeDetection = false;
            SettingsManager.Settings.Advanced.EnableMultiScreenSupport = true;
            SettingsManager.Settings.Advanced.FollowMouseForScreenSelection = true;

            SettingsManager.Settings.Appearance.IsColorfulViewboxFloatingBar = false;
            SettingsManager.Settings.Appearance.ViewboxFloatingBarScaleTransformValue = 1;
            SettingsManager.Settings.Appearance.ViewboxBlackBoardScaleTransformValue = 0.8;
            SettingsManager.Settings.Appearance.IsTransparentButtonBackground = true;
            SettingsManager.Settings.Appearance.IsShowExitButton = true;
            SettingsManager.Settings.Appearance.IsShowEraserButton = true;
            SettingsManager.Settings.Appearance.IsShowHideControlButton = false;
            SettingsManager.Settings.Appearance.IsShowLRSwitchButton = false;
            SettingsManager.Settings.Appearance.IsShowModeFingerToggleSwitch = true;
            SettingsManager.Settings.Appearance.IsShowQuickPanel = true;
            SettingsManager.Settings.Appearance.Theme = 0;
            SettingsManager.Settings.Appearance.EnableChickenSoupInWhiteboardMode = true;
            SettingsManager.Settings.Appearance.EnableTimeDisplayInWhiteboardMode = true;
            SettingsManager.Settings.Appearance.ChickenSoupSource = 1;
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityValue = 1.0;
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = 1.0;
            SettingsManager.Settings.Appearance.EnableTrayIcon = true;

            // 浮动栏按钮显示控制默认值
            SettingsManager.Settings.Appearance.IsShowQuickColorPalette = false;
            SettingsManager.Settings.Appearance.QuickColorPaletteDisplayMode = 1;
            SettingsManager.Settings.Appearance.EraserDisplayOption = 0;

            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote = true;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno = true;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiCamera = true;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3C = false;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3 = false;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote5C = true;
            SettingsManager.Settings.Automation.IsAutoFoldInSeewoPincoTeacher = false;
            SettingsManager.Settings.Automation.IsAutoFoldInHiteTouchPro = false;
            SettingsManager.Settings.Automation.IsAutoFoldInHiteCamera = false;
            SettingsManager.Settings.Automation.IsAutoFoldInWxBoardMain = false;
            SettingsManager.Settings.Automation.IsAutoFoldInOldZyBoard = false;
            SettingsManager.Settings.Automation.IsAutoFoldInMSWhiteboard = false;
            SettingsManager.Settings.Automation.IsAutoFoldInAdmoxWhiteboard = false;
            SettingsManager.Settings.Automation.IsAutoFoldInAdmoxBooth = false;
            SettingsManager.Settings.Automation.IsAutoFoldInQPoint = false;
            SettingsManager.Settings.Automation.IsAutoFoldInYiYunVisualPresenter = false;
            SettingsManager.Settings.Automation.IsAutoFoldInMaxHubWhiteboard = false;
            SettingsManager.Settings.Automation.IsAutoFoldInPPTSlideShow = false;
            SettingsManager.Settings.Automation.IsAutoKillPPTService = false;
            SettingsManager.Settings.Automation.IsAutoKillEasiNote = false;
            SettingsManager.Settings.Automation.IsAutoKillVComYouJiao = false;
            SettingsManager.Settings.Automation.IsAutoKillInkCanvas = false;
            SettingsManager.Settings.Automation.IsAutoKillICA = false;
            SettingsManager.Settings.Automation.IsAutoKillIDT = false;
            SettingsManager.Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = false;
            SettingsManager.Settings.Automation.IsSaveScreenshotsInDateFolders = false;
            SettingsManager.Settings.Automation.ScreenshotSaveFormat = 0;
            SettingsManager.Settings.Automation.ScreenshotJpegQuality = 90;
            SettingsManager.Settings.Automation.ScreenshotScaleMode = 0;
            SettingsManager.Settings.Automation.IsAutoSaveStrokesAtScreenshot = true;
            SettingsManager.Settings.Automation.IsAutoSaveScreenshotAtClear = true;
            SettingsManager.Settings.Automation.IsAutoClearWhenExitingWritingMode = false;
            SettingsManager.Settings.Automation.MinimumAutomationStrokeNumber = 0;
            SettingsManager.Settings.Automation.AutoDelSavedFiles = AutoDelSavedFilesDays;
            SettingsManager.Settings.Automation.AutoDelSavedFilesDaysThreshold = AutoDelSavedFilesDaysThreshold;

            //Settings.PowerPointSettings.IsShowPPTNavigation = true;
            //Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = false;
            //Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = true;
            SettingsManager.Settings.PowerPointSettings.PowerPointSupport = true;
            SettingsManager.Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = false;
            SettingsManager.Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = true;
            SettingsManager.Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = false;
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            SettingsManager.Settings.PowerPointSettings.IsNotifyPreviousPage = false;
            SettingsManager.Settings.PowerPointSettings.IsNotifyHiddenPage = false;
            SettingsManager.Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = false;
            SettingsManager.Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = false;
            SettingsManager.Settings.PowerPointSettings.IsSupportWPS = false;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview = false;
            SettingsManager.Settings.PowerPointSettings.ShowPPTEnhancedPreviewLoadingAnimation = true;

            SettingsManager.Settings.Canvas.InkWidth = 2.5;
            SettingsManager.Settings.Canvas.IsShowCursor = false;
            SettingsManager.Settings.Canvas.InkStyle = 0;
            SettingsManager.Settings.Canvas.HighlighterWidth = 20;
            SettingsManager.Settings.Canvas.EraserSize = 1;
            SettingsManager.Settings.Canvas.EraserType = 0;
            SettingsManager.Settings.Canvas.EraserShapeType = 1;
            SettingsManager.Settings.Canvas.HideStrokeWhenSelecting = false;
            SettingsManager.Settings.Canvas.ClearCanvasAndClearTimeMachine = false;
            SettingsManager.Settings.Canvas.FitToCurve = false;
            SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = true;
            SettingsManager.Settings.Canvas.MergeInkSmoothingWithUndo = false;
            SettingsManager.Settings.Canvas.EnablePressureTouchMode = false;
            SettingsManager.Settings.Canvas.DisablePressure = false;
            SettingsManager.Settings.Canvas.AutoStraightenLine = true;
            SettingsManager.Settings.Canvas.AutoStraightenLineThreshold = 80;
            SettingsManager.Settings.Canvas.PauseStraightenLine = false;
            SettingsManager.Settings.Canvas.PauseStraightenDelay = 300;
            SettingsManager.Settings.Canvas.LineEndpointSnapping = true;
            SettingsManager.Settings.Canvas.LineEndpointSnappingThreshold = 15;
            SettingsManager.Settings.Canvas.UsingWhiteboard = false;
            SettingsManager.Settings.Canvas.HyperbolaAsymptoteOption = 0;

            SettingsManager.Settings.Gesture.IsEnableTwoFingerTranslate = true;
            SettingsManager.Settings.Gesture.IsEnableTwoFingerZoom = false;
            SettingsManager.Settings.Gesture.IsEnableTwoFingerRotation = false;
            SettingsManager.Settings.Gesture.IsEnableTwoFingerRotationOnSelection = false;

            SettingsManager.Settings.InkToShape.IsInkToShapeEnabled = true;
            SettingsManager.Settings.InkToShape.IsInkToShapeNoFakePressureRectangle = false;
            SettingsManager.Settings.InkToShape.IsInkToShapeNoFakePressureTriangle = false;
            SettingsManager.Settings.InkToShape.IsInkToShapeTriangle = true;
            SettingsManager.Settings.InkToShape.IsInkToShapeRectangle = true;
            SettingsManager.Settings.InkToShape.IsInkToShapeRounded = true;
            SettingsManager.Settings.InkToShape.EnableWinRtHandwritingStrokeBeautify = false;
            SettingsManager.Settings.InkToShape.HandwritingCorrectionFontFamily = "Ink Free,KaiTi,Segoe Script";
            SettingsManager.Settings.InkToShape.HandwritingLanguageOverrideLcid = 0;
            SettingsManager.Settings.InkToShape.HandwritingBeautifyDebounceMs = 2000;

            SettingsManager.Settings.Startup.IsEnableNibMode = false;
            SettingsManager.Settings.Startup.IsAutoUpdate = true;
            SettingsManager.Settings.Startup.IsAutoUpdateWithSilence = true;
            SettingsManager.Settings.Startup.AutoUpdateWithSilenceStartTime = "06:00";
            SettingsManager.Settings.Startup.AutoUpdateWithSilenceEndTime = "22:00";
            SettingsManager.Settings.Startup.IsFoldAtStartup = false;
            SettingsManager.Settings.Startup.StartupMode = StartupMode.Default;
        }

        public string GetCorrectIcon(string iconType, bool isSolid = false)
        {
            if (SettingsManager.Settings.Appearance.UseLegacyFloatingBarUI)
            {
                // 使用老版图标
                switch (iconType)
                {
                    case "cursor":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidCursorIcon : XamlGraphicsIconGeometries.LegacyLinedCursorIcon;
                    case "pen":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidPenIcon : XamlGraphicsIconGeometries.LegacyLinedPenIcon;
                    case "eraserStroke":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidEraserStrokeIcon : XamlGraphicsIconGeometries.LegacyLinedEraserStrokeIcon;
                    case "eraserCircle":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidEraserCircleIcon : XamlGraphicsIconGeometries.LegacyLinedEraserCircleIcon;
                    case "lassoSelect":
                        return isSolid ? XamlGraphicsIconGeometries.LegacySolidLassoSelectIcon : XamlGraphicsIconGeometries.LegacyLinedLassoSelectIcon;
                }
            }
            else
            {
                // 使用新版图标
                switch (iconType)
                {
                    case "cursor":
                        return isSolid ? XamlGraphicsIconGeometries.SolidCursorIcon : XamlGraphicsIconGeometries.LinedCursorIcon;
                    case "pen":
                        return isSolid ? XamlGraphicsIconGeometries.SolidPenIcon : XamlGraphicsIconGeometries.LinedPenIcon;
                    case "eraserStroke":
                        return isSolid ? XamlGraphicsIconGeometries.SolidEraserStrokeIcon : XamlGraphicsIconGeometries.LinedEraserStrokeIcon;
                    case "eraserCircle":
                        return isSolid ? XamlGraphicsIconGeometries.SolidEraserCircleIcon : XamlGraphicsIconGeometries.LinedEraserCircleIcon;
                    case "lassoSelect":
                        return isSolid ? XamlGraphicsIconGeometries.SolidLassoSelectIcon : XamlGraphicsIconGeometries.LinedLassoSelectIcon;
                }
            }
            return "";
        }

        /// <summary>笔锋下拉 UI 顺序：0 实时笔锋，1 基于点集，2 基于速率，3 关闭。与存储值 InkStyle：3,0,1,2 对应。</summary>
        internal static int PenStyleUiIndexFromInkStyle(int inkStyle)
        {
            switch (inkStyle)
            {
                case 3: return 0;
                case 0: return 1;
                case 1: return 2;
                case 2: return 3;
                default: return 1;
            }
        }

        internal static int InkStyleFromPenStyleUiIndex(int uiIndex)
        {
            switch (uiIndex)
            {
                case 0: return 3;
                case 1: return 0;
                case 2: return 1;
                case 3: return 2;
                default: return 0;
            }
        }

        internal string BuildHitokotoRequestUrl()
        {
            var cats = SettingsManager.Settings.Appearance.HitokotoCategories;
            if (cats == null || cats.Count == 0)
                cats = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l" };

            var urlBuilder = new StringBuilder("https://v1.hitokoto.cn/?encode=text");
            foreach (var category in cats)
            {
                urlBuilder.Append($"&c={category}");
            }

            return urlBuilder.ToString();
        }

        /// <summary>
        /// 清理配置文件中的过期设置
        /// </summary>
        /// <param name="userConfigJson">用户配置的JSON字符串</param>
        /// <remarks>
        /// 清理过期设置时：
        /// 1. 创建默认配置对象
        /// 2. 将默认配置和用户配置都序列化为JObject
        /// 3. 递归比较并删除用户配置中多余的键
        /// 4. 如果有清理操作，重新反序列化并保存
        /// 5. 记录清理结果到日志
        /// </remarks>
        internal void CleanupObsoleteSettings(string userConfigJson)
        {
            try
            {
                // 创建默认配置对象
                Settings defaultSettings = new Settings();

                // 将默认配置和用户配置都序列化为JObject
                JObject defaultConfigObj = JObject.FromObject(defaultSettings); EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(defaultConfigObj);
                JObject userConfigObj = JObject.Parse(userConfigJson);

                // 记录是否有清理或迁移操作
                bool hasChanges = false;
                MigrateLegacyStartupMode(userConfigObj, ref hasChanges);

                // 递归比较并删除用户配置中多余的键
                RemoveObsoleteProperties(userConfigObj, defaultConfigObj, ref hasChanges);

                // 如果有清理操作，重新反序列化并保存
                if (hasChanges)
                {
                    string cleanedJson = userConfigObj.ToString(Formatting.Indented);
                    SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(cleanedJson);
                    SaveSettingsToFile();
                    App.UpdateCachedSettingsJson(cleanedJson);
                    LogHelper.WriteLogToFile("已清理过期配置项", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理过期配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 递归删除用户配置中多余的属性
        /// </summary>
        /// <param name="userObj">用户配置的JObject</param>
        /// <param name="defaultObj">默认配置的JObject</param>
        /// <param name="hasChanges">是否有变更的引用标志</param>
        /// <remarks>
        /// 递归删除多余属性时：
        /// 1. 检查用户配置和默认配置是否为空
        /// 2. 获取需要删除的键列表
        /// 3. 遍历用户配置的所有属性
        /// 4. 如果默认配置中不存在该属性，标记为删除
        /// 5. 如果两个属性都是对象类型，递归比较
        /// 6. 处理数组中的对象（如自定义图标列表等）
        /// 7. 删除标记的键
        /// 8. 设置变更标志
        /// </remarks>
        internal static void MigrateLegacyStartupMode(JObject userConfigObj, ref bool hasChanges)
        {
            if (!(userConfigObj?["startup"] is JObject startup)) return;

            if (startup["startupMode"] == null && startup["enableFastStartup"]?.Type == JTokenType.Boolean)
            {
                startup["startupMode"] = startup["enableFastStartup"].Value<bool>()
                    ? (int)StartupMode.Fastest
                    : (int)StartupMode.Faster;
                hasChanges = true;
            }

            if (startup.Remove("enableFastStartup"))
            {
                hasChanges = true;
            }
        }

        internal static void EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(JObject defaultConfigObj)
        {
            if (defaultConfigObj == null) return;
            if (defaultConfigObj["appearance"] is JObject appearance)
            {
                // 这些属性同时具备 NullValueHandling.Ignore 且默认值为 null，
                // 不会出现在默认 JObject 中。CleanupObsoleteSettings 会把它们
                // 误判为"过期"并立即 SaveSettingsToFile 删除，于是用户自建语录
                // 被静默清空。补成 null 占位让 RemoveObsoleteProperties 放行。
                foreach (var ignoredKey in new[] { "hitokotoCategories", "customTipsSchemes", "enabledPresetTipsSources" })
                {
                    if (!appearance.ContainsKey(ignoredKey))
                        appearance[ignoredKey] = JValue.CreateNull();
                }
            }
        }

        internal void RemoveObsoleteProperties(JObject userObj, JObject defaultObj, ref bool hasChanges)
        {
            if (userObj == null || defaultObj == null)
                return;

            // 获取需要删除的键列表（避免在遍历时修改集合）
            List<string> keysToRemove = new List<string>();

            foreach (var property in userObj.Properties())
            {
                string propertyName = property.Name;

                // 如果默认配置中不存在该属性，标记为删除
                if (!defaultObj.ContainsKey(propertyName))
                {
                    keysToRemove.Add(propertyName);
                    continue;
                }

                // 如果两个属性都是对象类型，递归比较
                JToken userValue = property.Value;
                JToken defaultValue = defaultObj[propertyName];

                if (userValue != null && defaultValue != null)
                {
                    if (userValue.Type == JTokenType.Object && defaultValue.Type == JTokenType.Object)
                    {
                        RemoveObsoleteProperties(userValue as JObject, defaultValue as JObject, ref hasChanges);
                    }
                    // 处理数组中的对象（如自定义图标列表等）
                    else if (userValue.Type == JTokenType.Array && defaultValue.Type == JTokenType.Array)
                    {
                        JArray userArray = userValue as JArray;
                        JArray defaultArray = defaultValue as JArray;

                        if (userArray != null && defaultArray != null && userArray.Count > 0 && defaultArray.Count > 0)
                        {
                            // 如果数组元素是对象，比较第一个元素的属性结构
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

            // 删除标记的键
            foreach (string key in keysToRemove)
            {
                userObj.Remove(key);
                hasChanges = true;
            }
        }
    }
}
