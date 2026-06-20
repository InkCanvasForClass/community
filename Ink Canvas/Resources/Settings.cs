using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Newtonsoft.Json;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.IO;

namespace Ink_Canvas
{
    public class Settings
    {
        [JsonProperty("advanced")]
        public Advanced Advanced { get; set; } = new Advanced();

        [JsonProperty("appearance")]
        public Appearance Appearance { get; set; } = new Appearance();

        [JsonProperty("automation")]
        public Automation Automation { get; set; } = new Automation();

        [JsonProperty("behavior")]
        public PowerPointSettings PowerPointSettings { get; set; } = new PowerPointSettings();

        [JsonProperty("canvas")]
        public Canvas Canvas { get; set; } = new Canvas();

        [JsonProperty("gesture")]
        public Gesture Gesture { get; set; } = new Gesture();

        [JsonProperty("inkToShape")]
        public InkToShape InkToShape { get; set; } = new InkToShape();

        [JsonProperty("startup")]
        public Startup Startup { get; set; } = new Startup();

        [JsonProperty("randSettings")]
        public RandSettings RandSettings { get; set; } = new RandSettings();

        [JsonProperty("modeSettings")]
        public ModeSettings ModeSettings { get; set; } = new ModeSettings();

        [JsonProperty("camera")]
        public CameraSettings Camera { get; set; } = new CameraSettings();

        [JsonProperty("dlass")]
        public DlassSettings Dlass { get; set; } = new DlassSettings();

        [JsonProperty("upload")]
        public UploadSettings Upload { get; set; } = new UploadSettings();

        [JsonProperty("security")]
        public Security Security { get; set; } = new Security();

        [JsonProperty("notification")]
        public NotificationSettings Notification { get; set; } = new NotificationSettings();

        [JsonProperty("toolbar")]
        public ToolbarLayoutSettings Toolbar { get; set; } = new ToolbarLayoutSettings();

        [JsonProperty("toolbarConfigName")]
        public string ToolbarConfigName { get; set; } = "default";

        [JsonProperty("boardToolbarConfigName")]
        public string BoardToolbarConfigName { get; set; } = "default";

        [JsonProperty("performance")]
        public PerformanceSettings Performance { get; set; } = new PerformanceSettings();

        [JsonProperty("miniWhiteboard")]
        public MiniWhiteboardSettings MiniWhiteboard { get; set; } = new MiniWhiteboardSettings();
    }

    public class PerformanceSettings
    {
        [JsonProperty("isMonitoringEnabled")]
        public bool IsMonitoringEnabled { get; set; } = false;

        [JsonProperty("history")]
        public List<PerformanceRunRecord> History { get; set; } = new List<PerformanceRunRecord>();

        [JsonProperty("deviceScore")]
        public int DeviceScore { get; set; } = -1;

        [JsonProperty("cpuScore")]
        public int CpuScore { get; set; } = -1;

        [JsonProperty("memoryScore")]
        public int MemoryScore { get; set; } = -1;

        [JsonProperty("diskScore")]
        public int DiskScore { get; set; } = -1;

        [JsonProperty("lastTestTime")]
        public string LastTestTime { get; set; } = string.Empty;
    }

    public class PerformanceRunRecord
    {
        [JsonProperty("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [JsonProperty("endTime")]
        public string EndTime { get; set; } = string.Empty;

        [JsonProperty("durationSeconds")]
        public double DurationSeconds { get; set; }

        [JsonProperty("avgCpuPercent")]
        public double AvgCpuPercent { get; set; }

        [JsonProperty("peakCpuPercent")]
        public double PeakCpuPercent { get; set; }

        [JsonProperty("avgMemoryMb")]
        public double AvgMemoryMb { get; set; }

        [JsonProperty("peakMemoryMb")]
        public double PeakMemoryMb { get; set; }

        [JsonProperty("sampleCount")]
        public int SampleCount { get; set; }

        [JsonProperty("smoothingSampleCount")]
        public int SmoothingSampleCount { get; set; }

        [JsonProperty("smoothingAvgTotalMs")]
        public double SmoothingAvgTotalMs { get; set; }

        [JsonProperty("smoothingMaxTotalMs")]
        public double SmoothingMaxTotalMs { get; set; }

        [JsonProperty("smoothingAvgBezierMs")]
        public double SmoothingAvgBezierMs { get; set; }

        [JsonProperty("smoothingAvgResampleMs")]
        public double SmoothingAvgResampleMs { get; set; }

        [JsonProperty("smoothingAvgInputPoints")]
        public double SmoothingAvgInputPoints { get; set; }

        [JsonProperty("smoothingAvgOutputPoints")]
        public double SmoothingAvgOutputPoints { get; set; }
    }

    public class NotificationSettings
    {
        [JsonProperty("isAnnouncementEnabled")]
        public bool IsAnnouncementEnabled { get; set; } = true;

        [JsonProperty("isDynamicNotificationEnabled")]
        public bool IsDynamicNotificationEnabled { get; set; } = true;

        [JsonProperty("isWindowsToastEnabled")]
        public bool IsWindowsToastEnabled { get; set; } = true;

        [JsonProperty("isForcePopupEnabled")]
        public bool IsForcePopupEnabled { get; set; } = true;

        [JsonIgnore]
        public string AnnouncementApiBaseUrl => "https://dev-api.dy.ci/api/announcement/client/announcements/";

        [JsonIgnore]
        public string AnnouncementWebSocketUrl => string.Empty;

        [JsonIgnore]
        public string AnnouncementSoftwareToken => "092fb28012b3985e2b84341c0643eab0";

        public const string BuiltInSoftwareToken = "492e41ea8eb61fc9a1d336b3852a4478";

        [JsonProperty("placement")]
        public string Placement { get; set; } = "TopCenter";

        [JsonProperty("animationMode")]
        public string AnimationMode { get; set; } = "Standard";

        [JsonProperty("updateDurationSeconds")]
        public int UpdateDurationSeconds { get; set; } = 3;

        [JsonProperty("urgentDurationSeconds")]
        public int UrgentDurationSeconds { get; set; } = 10;

        [JsonProperty("importantDurationSeconds")]
        public int ImportantDurationSeconds { get; set; } = 10;

        [JsonProperty("reminderDurationSeconds")]
        public int ReminderDurationSeconds { get; set; } = 10;

        [JsonProperty("otherDurationSeconds")]
        public int OtherDurationSeconds { get; set; } = 5;

        [JsonProperty("readAnnouncementIds")]
        public List<string> ReadAnnouncementIds { get; set; } = new List<string>();

        [JsonProperty("isDictationDoNotDisturbEnabled")]
        public bool IsDictationDoNotDisturbEnabled { get; set; } = false;

        [JsonProperty("isDictationDoNotDisturbInPPTEnabled")]
        public bool IsDictationDoNotDisturbInPPTEnabled { get; set; } = true;

        [JsonProperty("isDictationDoNotDisturbInWhiteboardEnabled")]
        public bool IsDictationDoNotDisturbInWhiteboardEnabled { get; set; } = true;
    }

    public class Security
    {
        [JsonProperty("passwordEnabled")]
        public bool PasswordEnabled { get; set; } = false;
        [JsonProperty("passwordSalt")]
        public string PasswordSalt { get; set; } = "";
        [JsonProperty("passwordHash")]
        public string PasswordHash { get; set; } = "";
        [JsonProperty("totpEnabled")]
        public bool TotpEnabled { get; set; } = false;
        [JsonProperty("totpSecret")]
        public string TotpSecret { get; set; } = "";
        [JsonProperty("totpOnlyMode")]
        public bool TotpOnlyMode { get; set; } = false;
        [JsonProperty("requirePasswordOnExit")]
        public bool RequirePasswordOnExit { get; set; } = false;
        [JsonProperty("requirePasswordOnEnterSettings")]
        public bool RequirePasswordOnEnterSettings { get; set; } = false;
        [JsonProperty("requirePasswordOnResetConfig")]
        public bool RequirePasswordOnResetConfig { get; set; } = false;
        [JsonProperty("requirePasswordOnModifyOrClearNameList")]
        public bool RequirePasswordOnModifyOrClearNameList { get; set; } = false;
        [JsonProperty("enableProcessProtection")]
        public bool EnableProcessProtection { get; set; } = true;

        [JsonProperty("usbVerificationEnabled")]
        public bool UsbVerificationEnabled { get; set; } = false;
        [JsonProperty("usbAuthorizedSns")]
        public string UsbAuthorizedSns { get; set; } = "";
    }

    public class Canvas
    {
        [JsonProperty("inkWidth")]
        public double InkWidth { get; set; } = 2.5;

        [JsonProperty("highlighterWidth")]
        public double HighlighterWidth { get; set; } = 20;

        [JsonProperty("highlighterOverlapEnabled")]
        public bool HighlighterOverlapEnabled { get; set; } = false;

        [JsonProperty("inkAlpha")]
        public double InkAlpha { get; set; } = 255;

        [JsonProperty("highlighterAlpha")]
        public double HighlighterAlpha { get; set; } = 255;

        [JsonProperty("isShowCursor")]
        public bool IsShowCursor { get; set; }
        /// <summary>画笔光标类型：0 系统光标，1 软件内置光标（默认），2 用户自定义光标。</summary>
        [JsonProperty("penCursorType")]
        public int PenCursorType { get; set; } = 1;
        /// <summary>用户自定义光标文件路径（当 PenCursorType == 2 时使用）。</summary>
        [JsonProperty("customPenCursorPath")]
        public string CustomPenCursorPath { get; set; } = "";
        /// <summary>笔锋存储值：0 基于点集，1 基于速率，2 关闭，3 实时笔锋（速度与压感混合）。界面下拉顺序为实时笔锋、点集、速率、关闭。</summary>
        [JsonProperty("inkStyle")]
        public int InkStyle { get; set; }
        [JsonProperty("eraserSize")]
        public int EraserSize { get; set; } = 2;
        [JsonProperty("eraserType")]
        public int EraserType { get; set; } // 0 - 图标切换模式      1 - 面积擦     2 - 线条擦
        [JsonProperty("eraserShapeType")]
        public int EraserShapeType { get; set; } // 0 - 圆形擦  1 - 黑板擦
        [JsonProperty("hideStrokeWhenSelecting")]
        public bool HideStrokeWhenSelecting { get; set; } = true;
        [JsonProperty("fitToCurve")]
        public bool FitToCurve { get; set; } // 默认关闭原来的贝塞尔平滑
        [JsonProperty("useAdvancedBezierSmoothing")]
        public bool UseAdvancedBezierSmoothing { get; set; } = true; // 默认启用高级贝塞尔曲线平滑
        [JsonProperty("mergeInkSmoothingWithUndo")]
        public bool MergeInkSmoothingWithUndo { get; set; } = false;
        [JsonProperty("useAsyncInkSmoothing")]
        public bool UseAsyncInkSmoothing { get; set; } = true; // 默认启用异步墨迹平滑
        [JsonProperty("useHardwareAcceleration")]
        public bool UseHardwareAcceleration { get; set; } = true; // 默认启用硬件加速
        [JsonProperty("inkSmoothingQuality")]
        public int InkSmoothingQuality { get; set; } = 2; // 0-低质量高性能, 1-平衡, 2-高质量低性能，默认为高质量
        [JsonProperty("maxConcurrentSmoothingTasks")]
        public int MaxConcurrentSmoothingTasks { get; set; } // 0表示自动检测CPU核心数
        [JsonProperty("clearCanvasAndClearTimeMachine")]
        public bool ClearCanvasAndClearTimeMachine { get; set; }
        [JsonProperty("enablePressureTouchMode")]
        public bool EnablePressureTouchMode { get; set; } // 是否启用压感触屏模式
        [JsonProperty("disablePressure")]
        public bool DisablePressure { get; set; } // 是否屏蔽压感
        [JsonProperty("autoStraightenLine")]
        public bool AutoStraightenLine { get; set; } = true; // 是否启用直线自动拉直
        [JsonProperty("autoStraightenLineThreshold")]
        public int AutoStraightenLineThreshold { get; set; } = 80; // 直线自动拉直的长度阈值（像素）
        [JsonProperty("highPrecisionLineStraighten")]
        public bool HighPrecisionLineStraighten { get; set; } = true; // 是否启用高精度直线拉直
        [JsonProperty("pauseStraightenLine")]
        public bool PauseStraightenLine { get; set; } = false; // 是否启用停顿拉直（书写中停顿时自动拉直笔画）
        [JsonProperty("pauseStraightenDelay")]
        public int PauseStraightenDelay { get; set; } = 300; // 停顿拉直触发延迟（毫秒）
        [JsonProperty("lineEndpointSnapping")]
        public bool LineEndpointSnapping { get; set; } = true; // 是否启用直线端点吸附
        [JsonProperty("lineEndpointSnappingThreshold")]
        public int LineEndpointSnappingThreshold { get; set; } = 15; // 直线端点吸附的距离阈值（像素）
        [JsonProperty("usingWhiteboard")]
        public bool UsingWhiteboard { get; set; }
        [JsonProperty("customBackgroundColor")]
        public string CustomBackgroundColor { get; set; } = "#162924";
        [JsonProperty("hyperbolaAsymptoteOption")]
        public OptionalOperation HyperbolaAsymptoteOption { get; set; } = OptionalOperation.Ask;
        [JsonProperty("isCompressPicturesUploaded")]
        public bool IsCompressPicturesUploaded { get; set; }
        [JsonProperty("enablePalmEraser")]
        public bool EnablePalmEraser { get; set; } = true;
        [JsonProperty("palmEraserSensitivity")]
        public int PalmEraserSensitivity { get; set; } = 0; // 0-低敏感度, 1-中敏感度, 2-高敏感度
        [JsonProperty("clearCanvasAlsoClearImages")]
        public bool ClearCanvasAlsoClearImages { get; set; } = true;
        [JsonProperty("showCircleCenter")]
        public bool ShowCircleCenter { get; set; }
        [JsonProperty("showCoordinateUnitMarks")]
        public bool ShowCoordinateUnitMarks { get; set; }
        [JsonProperty("enableInkFade")]
        public bool EnableInkFade { get; set; } = false;
        [JsonProperty("inkFadeTime")]
        public int InkFadeTime { get; set; } = 3000;
        [JsonProperty("inkFadeSpeedMultiplier")]
        public double InkFadeSpeedMultiplier { get; set; } = 1.0;
        [JsonProperty("laserPenWidth")]
        public double LaserPenWidth { get; set; } = 5;
        [JsonProperty("laserPenAlpha")]
        public int LaserPenAlpha { get; set; } = 128;
        [JsonProperty("enableBrushAutoRestore")]
        public bool EnableBrushAutoRestore { get; set; } = false;
        [JsonProperty("brushAutoRestoreDelaySeconds")]
        public int BrushAutoRestoreDelaySeconds { get; set; } = 30;
        [JsonProperty("brushAutoRestoreTimes")]
        public string BrushAutoRestoreTimes { get; set; } = "";
        [JsonProperty("brushAutoRestoreColor")]
        public string BrushAutoRestoreColor { get; set; } = "#FFFF0000";
        [JsonProperty("brushAutoRestoreWidth")]
        public double BrushAutoRestoreWidth { get; set; } = 5;
        [JsonProperty("brushAutoRestoreAlpha")]
        public int BrushAutoRestoreAlpha { get; set; } = 255;
        [JsonProperty("enableEraserAutoSwitchBack")]
        public bool EnableEraserAutoSwitchBack { get; set; } = false;
        [JsonProperty("eraserAutoSwitchBackDelaySeconds")]
        public int EraserAutoSwitchBackDelaySeconds { get; set; } = 10; // 默认10秒
        [JsonProperty("velocityBrushTipMix")]
        public double VelocityBrushTipMix { get; set; } = 0.45;
        [JsonProperty("realtimeBrushTipMinDistanceScale")]
        public double RealtimeBrushTipMinDistanceScale { get; set; } = 0.5;
        [JsonProperty("enableVelocityBrushTip")]
        public bool EnableVelocityBrushTip { get; set; }

        /// <summary>为 true 时，白板工具栏「展台」按钮启动希沃视频展台（sweclauncher），否则使用内置展台。</summary>
        [JsonProperty("launchSeewoVideoShowcaseForWhiteboardBooth")]
        public bool LaunchSeewoVideoShowcaseForWhiteboardBooth { get; set; } = false;

    }

    public enum OptionalOperation
    {
        Yes,
        No,
        Ask
    }

    public class Gesture
    {
        [JsonIgnore]
        public bool IsEnableTwoFingerGesture => IsEnableTwoFingerZoom || IsEnableTwoFingerTranslate || IsEnableTwoFingerRotation
            || IsEnableTwoFingerZoomBoard || IsEnableTwoFingerTranslateBoard || IsEnableTwoFingerRotationBoard;
        [JsonIgnore]
        public bool IsEnableTwoFingerGestureTranslateOrRotation => IsEnableTwoFingerTranslate || IsEnableTwoFingerRotation
            || IsEnableTwoFingerTranslateBoard || IsEnableTwoFingerRotationBoard;
        [JsonProperty("isEnableMultiTouchMode")]
        public bool IsEnableMultiTouchMode { get; set; } = false;
        [JsonProperty("isEnableTwoFingerZoom")]
        public bool IsEnableTwoFingerZoom { get; set; } = true;
        [JsonProperty("isEnableTwoFingerTranslate")]
        public bool IsEnableTwoFingerTranslate { get; set; } = true;
        [JsonProperty("isEnableTwoFingerRotation")]
        public bool IsEnableTwoFingerRotation { get; set; }
        [JsonProperty("isEnableTwoFingerRotationOnSelection")]
        public bool IsEnableTwoFingerRotationOnSelection { get; set; }

        [JsonProperty("isEnableMultiTouchModeBoard")]
        public bool IsEnableMultiTouchModeBoard { get; set; } = false;
        [JsonProperty("isEnableTwoFingerZoomBoard")]
        public bool IsEnableTwoFingerZoomBoard { get; set; } = true;
        [JsonProperty("isEnableTwoFingerTranslateBoard")]
        public bool IsEnableTwoFingerTranslateBoard { get; set; } = true;
        [JsonProperty("isEnableTwoFingerRotationBoard")]
        public bool IsEnableTwoFingerRotationBoard { get; set; }
    }

    // 更新通道枚举
    public enum UpdateChannel
    {
        Release,
        Preview,
        Beta
    }

    /// <summary>自动更新要下载的安装包架构。默认跟随当前软件进程架构；64 位包对应发布物 ZIP 文件名在 .zip 前增加 -x64。</summary>
    public enum UpdatePackageArchitecture
    {
        /// <summary>32 位包，例如 InkCanvasForClass.CE.1.7.0.0.zip</summary>
        X86 = 0,
        /// <summary>64 位包，例如 InkCanvasForClass.CE.1.7.0.0-x64.zip</summary>
        X64 = 1
    }

    /// <summary>
    /// 遥测上传等级
    /// </summary>
    public enum TelemetryUploadLevel
    {
        /// <summary>
        /// 不上传任何匿名使用数据
        /// </summary>
        None = 0,
        /// <summary>
        /// 仅上传基础数据
        /// </summary>
        Basic = 1,
        /// <summary>
        /// 上传基础数据 + 可选数据
        /// </summary>
        Extended = 2
    }

    public class Startup
    {
        [JsonProperty("isAutoUpdate")]
        public bool IsAutoUpdate { get; set; } = true;
        [JsonProperty("isAutoUpdateWithSilence")]
        public bool IsAutoUpdateWithSilence { get; set; }
        [JsonProperty("isAutoUpdateWithSilenceStartTime")]
        public string AutoUpdateWithSilenceStartTime { get; set; } = "06:00";
        [JsonProperty("isAutoUpdateWithSilenceEndTime")]
        public string AutoUpdateWithSilenceEndTime { get; set; } = "22:00";
        [JsonProperty("updateChannel")]
        public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Release;
        [JsonProperty("updatePackageArchitecture")]
        public UpdatePackageArchitecture UpdatePackageArchitecture { get; set; } = Environment.Is64BitProcess ? UpdatePackageArchitecture.X64 : UpdatePackageArchitecture.X86;
        [JsonProperty("isSmartUpdate")]
        public bool IsSmartUpdate { get; set; } = true;
        [JsonProperty("skippedVersion")]
        public string SkippedVersion { get; set; } = "";
        [JsonProperty("autoUpdatePauseUntilDate")]
        public string AutoUpdatePauseUntilDate { get; set; } = "";
        [JsonProperty("isEnableNibMode")]
        public bool IsEnableNibMode { get; set; }
        [JsonProperty("isFoldAtStartup")]
        public bool IsFoldAtStartup { get; set; }
        [JsonProperty("crashAction")]
        public int CrashAction { get; set; } = 2;
        [JsonProperty("telemetryUploadLevel")]
        public TelemetryUploadLevel TelemetryUploadLevel { get; set; } = TelemetryUploadLevel.None;
        [JsonProperty("hasAcceptedTelemetryPrivacy")]
        public bool HasAcceptedTelemetryPrivacy { get; set; } = false;
        [JsonProperty("hasShownOobe")]
        public bool HasShownOobe { get; set; } = false;
        [JsonProperty("enableWindowChromeRendering")]
        public bool EnableWindowChromeRendering { get; set; } = false;
    }

    public enum TrayClickAction
    {
        ShowMenu = 0,
        HideShowMainWindow = 1,
        TempShowMainWindow = 2,
        OpenSettings = 3,
        DisableAllHotkeys = 4,
        ForceFullScreen = 5,
        ToggleFoldFloatingBar = 6,
        ResetFloatingBarPosition = 7,
        RestartApp = 8,
        CloseApp = 9,
        NoAction = 10
    }

    public enum ToolbarPosition
    {
        Right = 0,
        Left = 1,
        Top = 2,
        Bottom = 3
    }

    public class Appearance
    {
        [JsonProperty("isColorfulViewboxFloatingBar")]
        public bool IsColorfulViewboxFloatingBar { get; set; }
        // [JsonProperty("enableViewboxFloatingBarScaleTransform")]
        // public bool EnableViewboxFloatingBarScaleTransform { get; set; } = false;
        [JsonProperty("viewboxFloatingBarScaleTransformValue")]
        public double ViewboxFloatingBarScaleTransformValue { get; set; } = 1.0;
        [JsonProperty("floatingBarImg")]
        public int FloatingBarImg { get; set; }
        [JsonProperty("customFloatingBarImgs")]
        public List<CustomFloatingBarIcon> CustomFloatingBarImgs { get; set; } = new List<CustomFloatingBarIcon>();
        [JsonProperty("viewboxFloatingBarOpacityValue")]
        public double ViewboxFloatingBarOpacityValue { get; set; } = 1.0;
        [JsonProperty("enableTrayIcon")]
        public bool EnableTrayIcon { get; set; } = true;
        [JsonProperty("trayLeftClickAction")]
        public TrayClickAction TrayLeftClickAction { get; set; } = TrayClickAction.ShowMenu;
        [JsonProperty("trayRightClickAction")]
        public TrayClickAction TrayRightClickAction { get; set; } = TrayClickAction.ShowMenu;
        [JsonProperty("viewboxFloatingBarOpacityInPPTValue")]
        public double ViewboxFloatingBarOpacityInPPTValue { get; set; } = 0.5;
        [JsonProperty("floatingBarMenuOpacity")]
        public double FloatingBarMenuOpacity { get; set; } = 1.0;
        [JsonProperty("floatingBarMenuOpacityInPPT")]
        public double FloatingBarMenuOpacityInPPT { get; set; } = 1.0;
        [JsonProperty("boardMenuOpacity")]
        public double BoardMenuOpacity { get; set; } = 1.0;
        [JsonProperty("viewboxBlackBoardScaleTransformValue")]
        public double ViewboxBlackBoardScaleTransformValue { get; set; } = 1;
        [JsonProperty("viewboxBlackBoardLeftScaleTransformValue")]
        public double ViewboxBlackBoardLeftScaleTransformValue { get; set; } = 1;
        [JsonProperty("viewboxBlackBoardRightScaleTransformValue")]
        public double ViewboxBlackBoardRightScaleTransformValue { get; set; } = 1;
        [JsonProperty("boardToolbarLeftOpacity")]
        public double BoardToolbarLeftOpacity { get; set; } = 0.77;
        [JsonProperty("boardToolbarCenterOpacity")]
        public double BoardToolbarCenterOpacity { get; set; } = 0.77;
        [JsonProperty("boardToolbarRightOpacity")]
        public double BoardToolbarRightOpacity { get; set; } = 0.77;
        [JsonProperty("isTransparentButtonBackground")]
        public bool IsTransparentButtonBackground { get; set; } = true;
        [JsonProperty("isShowExitButton")]
        public bool IsShowExitButton { get; set; } = true;
        [JsonProperty("isShowEraserButton")]
        public bool IsShowEraserButton { get; set; } = true;
        [JsonProperty("enableTimeDisplayInWhiteboardMode")]
        public bool EnableTimeDisplayInWhiteboardMode { get; set; } = true;
        [JsonProperty("enableChickenSoupInWhiteboardMode")]
        public bool EnableChickenSoupInWhiteboardMode { get; set; } = true;
        [JsonProperty("isShowHideControlButton")]
        public bool IsShowHideControlButton { get; set; }
        [JsonProperty("unFoldButtonImageType")]
        public int UnFoldButtonImageType { get; set; }
        [JsonProperty("isShowLRSwitchButton")]
        public bool IsShowLRSwitchButton { get; set; }
        [JsonProperty("enableSplashScreen")]
        public bool EnableSplashScreen { get; set; } = false;
        [JsonProperty("splashScreenStyle")]
        public int SplashScreenStyle { get; set; } = 1; // 0-随机, 1-跟随四季, 2-春季, 3-夏季, 4-秋季, 5-冬季, 6-马年限定 
        [JsonProperty("customSplashImagePath")]
        public string CustomSplashImagePath { get; set; } = string.Empty;
        [JsonProperty("customSplashTextPosition")]
        public int CustomSplashTextPosition { get; set; } = 1; // 0-左下, 1-中下, 2-右下
        [JsonProperty("isShowQuickPanel")]
        public bool IsShowQuickPanel { get; set; } = true;
        [JsonProperty("chickenSoupSource")]
        public int ChickenSoupSource { get; set; } = 1;
        [JsonProperty("chickenSoupPosition")]
        public string ChickenSoupPosition { get; set; } = "TopRight";
        [JsonProperty("hitokotoCategories", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> HitokotoCategories { get; set; }
        [JsonProperty("isShowModeFingerToggleSwitch")]
        public bool IsShowModeFingerToggleSwitch { get; set; } = true;
        [JsonProperty("theme")]
        public int Theme { get; set; } = 2;
        [JsonProperty("windowBackdrop")]
        public string WindowBackdrop { get; set; } = "Mica";

        [JsonProperty("useLegacyFloatingBarUI")]
        public bool UseLegacyFloatingBarUI { get; set; } = false;

        [JsonProperty("compactFloatingBar")]
        public bool CompactFloatingBar { get; set; } = false;

        [JsonProperty("hideFloatingBarBorder")]
        public bool HideFloatingBarBorder { get; set; } = false;

        [JsonProperty("eraserDisplayOption")]
        public int EraserDisplayOption { get; set; }

        [JsonProperty("isShowQuickColorPalette")]
        public bool IsShowQuickColorPalette { get; set; }

        [JsonProperty("quickColorPaletteDisplayMode")]
        public int QuickColorPaletteDisplayMode { get; set; } = 1;

        [JsonProperty("enableHotkeysInMouseMode")]
        public bool EnableHotkeysInMouseMode { get; set; } = false;

        [JsonProperty("language")]
        public string Language { get; set; } = "";

        [JsonProperty("use24HourTimeFormat")]
        public bool Use24HourTimeFormat { get; set; } = false;

        [JsonProperty("quickPanelBottomOffset")]
        public double QuickPanelBottomOffset { get; set; } = -150;

        [JsonProperty("useMinimalistGrabHandle")]
        public bool UseMinimalistGrabHandle { get; set; } = true;

        [JsonProperty("showGrabHandleChevron")]
        public bool ShowGrabHandleChevron { get; set; } = false;

        [JsonProperty("useFloatingQuickPanel")]
        public bool UseFloatingQuickPanel { get; set; } = true;

        [JsonProperty("showPenColorOnFloatingBarIcon")]
        public bool ShowPenColorOnFloatingBarIcon { get; set; } = false;

        [JsonProperty("allowDragSidePanel")]
        public bool AllowDragSidePanel { get; set; } = true;

        [JsonProperty("quickPanelOpacity")]
        public double QuickPanelOpacity { get; set; } = 1.0;

        [JsonProperty("isAutoCollapseQuickPanel")]
        public bool IsAutoCollapseQuickPanel { get; set; } = false;

        [JsonProperty("autoCollapseQuickPanelDelay")]
        public double AutoCollapseQuickPanelDelay { get; set; } = 3.0;

        [JsonProperty("toolbarPosition")]
        public ToolbarPosition ToolbarPosition { get; set; } = ToolbarPosition.Right;

        [JsonProperty("reverseToolbarContent")]
        public bool ReverseToolbarContent { get; set; } = false;

        [JsonProperty("autoFlipWhenSpaceInsufficient")]
        public bool AutoFlipWhenSpaceInsufficient { get; set; } = true;

        [JsonProperty("flipContentOnAutoFlip")]
        public bool FlipContentOnAutoFlip { get; set; } = false;

        [JsonProperty("disableToolbarAnimation")]
        public bool DisableToolbarAnimation { get; set; } = false;
    }

    public enum PPTLinkMode
    {
        Com = 0,
        Rot = 1,
        Agent = 2
    }

    public class PowerPointSettings
    {
        [JsonProperty("showPPTButton")]
        public bool ShowPPTButton { get; set; } = true;

        // 每一个数位代表一个选项，2就是开启，1就是关闭
        [JsonProperty("pptButtonsDisplayOption")]
        public int PPTButtonsDisplayOption { get; set; } = 2222;

        // 0居中，+就是往上，-就是往下
        [JsonProperty("pptLSButtonPosition")]
        public int PPTLSButtonPosition { get; set; }

        // 0居中，+就是往上，-就是往下
        [JsonProperty("pptRSButtonPosition")]
        public int PPTRSButtonPosition { get; set; }

        // 0居中，+就是往右，-就是往左
        [JsonProperty("pptLBButtonPosition")]
        public int PPTLBButtonPosition { get; set; }

        // 0居中，+就是往右，-就是往左
        [JsonProperty("pptRBButtonPosition")]
        public int PPTRBButtonPosition { get; set; }

        [JsonProperty("pptSButtonsOption")]
        public int PPTSButtonsOption { get; set; } = 221;

        [JsonProperty("pptBButtonsOption")]
        public int PPTBButtonsOption { get; set; } = 121;

        [JsonProperty("enablePPTButtonPageClickable")]
        public bool EnablePPTButtonPageClickable { get; set; } = true;

        [JsonProperty("enablePPTButtonEnhancedPreview")]
        public bool EnablePPTButtonEnhancedPreview { get; set; } = false;

        [JsonProperty("enablePPTButtonLongPressPageTurn")]
        public bool EnablePPTButtonLongPressPageTurn { get; set; } = true;

        [JsonProperty("pptLSButtonOpacity")]
        public double PPTLSButtonOpacity { get; set; } = 0.5;

        [JsonProperty("pptRSButtonOpacity")]
        public double PPTRSButtonOpacity { get; set; } = 0.5;

        [JsonProperty("pptLBButtonOpacity")]
        public double PPTLBButtonOpacity { get; set; } = 0.5;

        [JsonProperty("pptRBButtonOpacity")]
        public double PPTRBButtonOpacity { get; set; } = 0.5;

        [JsonProperty("pptNavBarScale")]
        public double PPTNavBarScale { get; set; } = 1.0;

        // -- new --

        [JsonProperty("powerPointSupport")]
        public bool PowerPointSupport { get; set; } = true;
        [JsonProperty("isShowCanvasAtNewSlideShow")]
        public bool IsShowCanvasAtNewSlideShow { get; set; } = false;
        [JsonProperty("isNoClearStrokeOnSelectWhenInPowerPoint")]
        public bool IsNoClearStrokeOnSelectWhenInPowerPoint { get; set; } = true;
        [JsonProperty("isShowStrokeOnSelectInPowerPoint")]
        public bool IsShowStrokeOnSelectInPowerPoint { get; set; }
        [JsonProperty("isAutoSaveStrokesInPowerPoint")]
        public bool IsAutoSaveStrokesInPowerPoint { get; set; } = true;
        [JsonProperty("isAutoSaveScreenShotInPowerPoint")]
        public bool IsAutoSaveScreenShotInPowerPoint { get; set; }
        [JsonProperty("isNotifyPreviousPage")]
        public bool IsNotifyPreviousPage { get; set; }
        [JsonProperty("isNotifyHiddenPage")]
        public bool IsNotifyHiddenPage { get; set; } = true;
        [JsonProperty("isNotifyAutoPlayPresentation")]
        public bool IsNotifyAutoPlayPresentation { get; set; } = true;
        [JsonProperty("isEnableTwoFingerGestureInPresentationMode")]
        public bool IsEnableTwoFingerGestureInPresentationMode { get; set; }
        [JsonProperty("isEnableFingerGestureSlideShowControl")]
        public bool IsEnableFingerGestureSlideShowControl { get; set; } = true;
        [JsonProperty("isSupportWPS")]
        public bool IsSupportWPS { get; set; }
        [JsonProperty("enableWppProcessKill")]
        public bool EnableWppProcessKill { get; set; } = true;
        [JsonProperty("isAlwaysGoToFirstPageOnReenter")]
        public bool IsAlwaysGoToFirstPageOnReenter { get; set; }
        [JsonProperty("enablePowerPointEnhancement")]
        public bool EnablePowerPointEnhancement { get; set; } = false;
        [JsonProperty("skipAnimationsWhenGoNext")]
        public bool SkipAnimationsWhenGoNext { get; set; } = false;
        [JsonProperty("enablePPTTimeCapsule")]
        public bool EnablePPTTimeCapsule { get; set; } = true;
        [JsonProperty("pptTimeCapsulePosition")]
        public int PPTTimeCapsulePosition { get; set; } = 1;
        [JsonProperty("pptTimeCapsuleOpacity")]
        public double PPTTimeCapsuleOpacity { get; set; } = 1.0;
        [JsonProperty("pptTimeCapsuleScale")]
        public double PPTTimeCapsuleScale { get; set; } = 1.0;
        [JsonProperty("pptTimeCapsuleOffsetX")]
        public double PPTTimeCapsuleOffsetX { get; set; } = 0;
        [JsonProperty("pptTimeCapsuleOffsetY")]
        public double PPTTimeCapsuleOffsetY { get; set; } = 0;
        [JsonProperty("pptLinkMode")]
        public PPTLinkMode PPTLinkMode { get; set; } = PPTLinkMode.Com;

        [JsonProperty("showPPTSidebarByDefault")]
        public bool ShowPPTSidebarByDefault { get; set; } = false;

        [JsonProperty("showPPTModePrompt")]
        public bool ShowPPTModePrompt { get; set; } = false;
    }

    public class Automation
    {
        [JsonIgnore]
        public bool IsEnableAutoFold =>
            IsAutoFoldInEasiNote
            || IsAutoFoldInEasiCamera
            || IsAutoFoldInEasiNote3C
            || IsAutoFoldInEasiNote5C
            || IsAutoFoldInSeewoPincoTeacher
            || IsAutoFoldInHiteTouchPro
            || IsAutoFoldInHiteCamera
            || IsAutoFoldInWxBoardMain
            || IsAutoFoldInOldZyBoard
            || IsAutoFoldInPPTSlideShow
            || IsAutoFoldInMSWhiteboard
            || IsAutoFoldInAdmoxWhiteboard
            || IsAutoFoldInAdmoxBooth
            || IsAutoFoldInQPoint
            || IsAutoFoldInYiYunVisualPresenter
            || IsAutoFoldInMaxHubWhiteboard;

        [JsonProperty("isAutoEnterAnnotationModeWhenExitFoldMode")]
        public bool IsAutoEnterAnnotationModeWhenExitFoldMode { get; set; }

        [JsonProperty("isAutoFoldWhenExitWhiteboard")]
        public bool IsAutoFoldWhenExitWhiteboard { get; set; }

        [JsonProperty("isAutoFoldInEasiNote")]
        public bool IsAutoFoldInEasiNote { get; set; }

        [JsonProperty("isAutoFoldInEasiNoteIgnoreDesktopAnno")]
        public bool IsAutoFoldInEasiNoteIgnoreDesktopAnno { get; set; }

        [JsonProperty("isAutoFoldInEasiCamera")]
        public bool IsAutoFoldInEasiCamera { get; set; }

        [JsonProperty("isAutoFoldInEasiNote3")]
        public bool IsAutoFoldInEasiNote3 { get; set; }

        [JsonProperty("isAutoFoldInEasiNote3C")]
        public bool IsAutoFoldInEasiNote3C { get; set; }

        [JsonProperty("isAutoFoldInEasiNote5C")]
        public bool IsAutoFoldInEasiNote5C { get; set; }

        [JsonProperty("isAutoFoldInSeewoPincoTeacher")]
        public bool IsAutoFoldInSeewoPincoTeacher { get; set; }

        [JsonProperty("isAutoFoldInHiteTouchPro")]
        public bool IsAutoFoldInHiteTouchPro { get; set; }
        [JsonProperty("isAutoFoldInHiteLightBoard")]
        public bool IsAutoFoldInHiteLightBoard { get; set; }

        [JsonProperty("isAutoFoldInHiteCamera")]
        public bool IsAutoFoldInHiteCamera { get; set; }

        [JsonProperty("isAutoFoldInWxBoardMain")]
        public bool IsAutoFoldInWxBoardMain { get; set; }
        /*
        [JsonProperty("isAutoFoldInZySmartBoard")]
        public bool IsAutoFoldInZySmartBoard { get; set; } = false;
        */
        [JsonProperty("isAutoFoldInOldZyBoard")]
        public bool IsAutoFoldInOldZyBoard { get; set; }

        [JsonProperty("isAutoFoldInMSWhiteboard")]
        public bool IsAutoFoldInMSWhiteboard { get; set; }

        [JsonProperty("isAutoFoldInAdmoxWhiteboard")]
        public bool IsAutoFoldInAdmoxWhiteboard { get; set; }

        [JsonProperty("isAutoFoldInAdmoxBooth")]
        public bool IsAutoFoldInAdmoxBooth { get; set; }

        [JsonProperty("isAutoFoldInQPoint")]
        public bool IsAutoFoldInQPoint { get; set; }

        [JsonProperty("isAutoFoldInYiYunVisualPresenter")]
        public bool IsAutoFoldInYiYunVisualPresenter { get; set; }

        [JsonProperty("isAutoFoldInMaxHubWhiteboard")]
        public bool IsAutoFoldInMaxHubWhiteboard { get; set; }

        [JsonProperty("isAutoFoldInPPTSlideShow")]
        public bool IsAutoFoldInPPTSlideShow { get; set; }

        [JsonProperty("isAutoFoldAfterPPTSlideShow")]
        public bool IsAutoFoldAfterPPTSlideShow { get; set; }

        [JsonProperty("isAutoKillPPTService")]
        public bool IsAutoKillPPTService { get; set; }

        [JsonProperty("isAutoKillEasiNote")]
        public bool IsAutoKillEasiNote { get; set; }

        [JsonProperty("isAutoKillHiteAnnotation")]
        public bool IsAutoKillHiteAnnotation { get; set; }

        [JsonProperty("isAutoKillVComYouJiao")]
        public bool IsAutoKillVComYouJiao { get; set; }

        [JsonProperty("isAutoKillSeewoLauncher2DesktopAnnotation")]
        public bool IsAutoKillSeewoLauncher2DesktopAnnotation { get; set; }

        [JsonProperty("isAutoKillInkCanvas")]
        public bool IsAutoKillInkCanvas { get; set; }

        [JsonProperty("isAutoKillICA")]
        public bool IsAutoKillICA { get; set; }

        [JsonProperty("isAutoKillIDT")]
        public bool IsAutoKillIDT { get; set; }

        [JsonProperty("isSaveScreenshotsInDateFolders")]
        public bool IsSaveScreenshotsInDateFolders { get; set; }

        [JsonProperty("isAutoSaveStrokesAtScreenshot")]
        public bool IsAutoSaveStrokesAtScreenshot { get; set; }

        [JsonProperty("isAutoSaveStrokesAtClear")]
        public bool IsAutoSaveScreenshotAtClear { get; set; }

        [JsonProperty("isEnablePhotoCorrection")]
        public bool IsEnablePhotoCorrection { get; set; } = false;

        [JsonProperty("isAutoClearWhenExitingWritingMode")]
        public bool IsAutoClearWhenExitingWritingMode { get; set; }

        [JsonProperty("minimumAutomationStrokeNumber")]
        public int MinimumAutomationStrokeNumber { get; set; }

        [JsonProperty("autoSavedStrokesLocation")]
        public string AutoSavedStrokesLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves");

        [JsonProperty("autoDelSavedFiles")]
        public bool AutoDelSavedFiles;

        [JsonProperty("autoDelSavedFilesDaysThreshold")]
        public int AutoDelSavedFilesDaysThreshold = 15;

        [JsonProperty("keepFoldAfterSoftwareExit")]
        public bool KeepFoldAfterSoftwareExit { get; set; } = false;

        [JsonProperty("isSaveFullPageStrokes")]
        public bool IsSaveFullPageStrokes;

        [JsonProperty("isUseCustomSaveFileName")]
        public bool IsUseCustomSaveFileName { get; set; } = false;

        [JsonProperty("customSaveFileNameTemplate")]
        public string CustomSaveFileNameTemplate { get; set; } = "{datetime}";

        [JsonProperty("isSaveStrokesAsXML")]
        public bool IsSaveStrokesAsXML { get; set; } = false;

        [JsonProperty("isAutoEnterAnnotationAfterKillHite")]
        public bool IsAutoEnterAnnotationAfterKillHite { get; set; }

        [JsonProperty("isEnableAutoSaveStrokes")]
        public bool IsEnableAutoSaveStrokes { get; set; } = true;

        [JsonProperty("autoSaveStrokesIntervalMinutes")]
        public int AutoSaveStrokesIntervalMinutes { get; set; } = 5;

        [JsonProperty("thoroughlyHideWhenFolded")]
        public bool ThoroughlyHideWhenFolded { get; set; } = false;

        [JsonProperty("floatingWindowInterceptor")]
        public FloatingWindowInterceptorSettings FloatingWindowInterceptor { get; set; } = new FloatingWindowInterceptorSettings();
    }

    public class FloatingWindowInterceptorSettings
    {
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = false;

        [JsonProperty("scanIntervalMs")]
        public int ScanIntervalMs { get; set; } = 5000;

        [JsonProperty("autoStart")]
        public bool AutoStart { get; set; } = false;

        [JsonProperty("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        [JsonProperty("interceptRules")]
        public Dictionary<string, bool> InterceptRules { get; set; } = new Dictionary<string, bool>
        {
            { "SeewoWhiteboard3Floating", false },
            { "SeewoWhiteboard5Floating", false },
            { "SeewoWhiteboard5CFloating", false },
            { "SeewoPincoSideBarFloating", false },
            { "SeewoPincoDrawingFloating", false },
            { "SeewoPincoBoardService", false },
            { "SeewoPPTFloating", false },
            { "AiClassFloating", false },
            { "HiteAnnotationFloating", false },
            { "ChangYanFloating", false },
            { "ChangYanBrushSettings", false },
            { "ChangYanSwipeClear", false },
            { "ChangYanInteraction", false },
            { "ChangYanSubjectApp", false },
            { "ChangYanControl", false },
            { "ChangYanCommonTools", false },
            { "ChangYanSceneToolbar", false },
            { "ChangYanDrawWindow", false },
            { "ChangYanPPTFloating", false },
            { "ChangYanPPTPageControl", false },
            { "ChangYanPPTGoBack", false },
            { "ChangYanPPTPreview", false },
            { "IntelligentClassFloating", false },
            { "IntelligentClassPPTFloating", false },
            { "SeewoDesktopAnnotationFloating", false },
            { "SeewoDesktopSideBarFloating", false }
        };
    }

    public class Advanced
    {
        [JsonProperty("isSpecialScreen")]
        public bool IsSpecialScreen { get; set; }

        [JsonProperty("isQuadIR")]
        public bool IsQuadIR { get; set; }

        [JsonProperty("touchMultiplier")]
        public double TouchMultiplier { get; set; } = 0.25;

        [JsonProperty("nibModeBoundsWidth")]
        public int NibModeBoundsWidth { get; set; } = 10;

        [JsonProperty("fingerModeBoundsWidth")]
        public int FingerModeBoundsWidth { get; set; } = 30;

        [JsonProperty("nibModeBoundsWidthThresholdValue")]
        public double NibModeBoundsWidthThresholdValue { get; set; } = 2.5;

        [JsonProperty("fingerModeBoundsWidthThresholdValue")]
        public double FingerModeBoundsWidthThresholdValue { get; set; } = 2.5;

        [JsonProperty("nibModeBoundsWidthEraserSize")]
        public double NibModeBoundsWidthEraserSize { get; set; } = 0.8;

        [JsonProperty("fingerModeBoundsWidthEraserSize")]
        public double FingerModeBoundsWidthEraserSize { get; set; } = 0.8;

        [JsonProperty("eraserBindTouchMultiplier")]
        public bool EraserBindTouchMultiplier { get; set; }

        [JsonProperty("isLogEnabled")]
        public bool IsLogEnabled { get; set; } = true;

        [JsonProperty("isSaveLogByDate")]
        public bool IsSaveLogByDate { get; set; } = true;

        [JsonProperty("isDebugConsoleEnabled")]
        public bool IsDebugConsoleEnabled { get; set; } = false;

        [JsonProperty("isPPTComDebugProbeEnabled")]
        public bool IsPPTComDebugProbeEnabled { get; set; } = false;

        [JsonProperty("isEnableFullScreenHelper")]
        public bool IsEnableFullScreenHelper { get; set; }

        [JsonProperty("isEnableEdgeGestureUtil")]
        public bool IsEnableEdgeGestureUtil { get; set; }

        [JsonProperty("edgeGestureUtilOnlyAffectBlackboardMode")]
        public bool EdgeGestureUtilOnlyAffectBlackboardMode { get; set; }

        [JsonProperty("isEnableForceFullScreen")]
        public bool IsEnableForceFullScreen { get; set; }

        [JsonProperty("isEnableResolutionChangeDetection")]
        public bool IsEnableResolutionChangeDetection { get; set; }

        [JsonProperty("isEnableDPIChangeDetection")]
        public bool IsEnableDPIChangeDetection { get; set; }

        [JsonProperty("isSecondConfirmWhenShutdownApp")]
        public bool IsSecondConfirmWhenShutdownApp { get; set; }

        [JsonProperty("isEnableAvoidFullScreenHelper")]
        public bool IsEnableAvoidFullScreenHelper { get; set; } = OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows11;

        [JsonProperty("isAutoBackupBeforeUpdate")]
        public bool IsAutoBackupBeforeUpdate { get; set; } = true;

        [JsonProperty("isAutoBackupEnabled")]
        public bool IsAutoBackupEnabled { get; set; } = true;

        [JsonProperty("autoBackupIntervalDays")]
        public int AutoBackupIntervalDays { get; set; } = 7;

        [JsonProperty("lastAutoBackupTime")]
        public DateTime LastAutoBackupTime { get; set; } = DateTime.MinValue;

        [JsonProperty("isNoFocusMode")]
        public bool IsNoFocusMode { get; set; } = true;

        [JsonProperty("isAlwaysOnTop")]
        public bool IsAlwaysOnTop { get; set; } = true;

        [JsonProperty("enableUIAccessTopMost")]
        public bool EnableUIAccessTopMost { get; set; } = false;

        [JsonProperty("isEnableUriScheme")]
        public bool IsEnableUriScheme { get; set; } = false;

        [JsonProperty("windowMode")]
        public bool WindowMode { get; set; } = true;

        [JsonProperty("enableMultiScreenSupport")]
        public bool EnableMultiScreenSupport { get; set; } = true;

        [JsonProperty("followMouseForScreenSelection")]
        public bool FollowMouseForScreenSelection { get; set; } = true;
    }

    public class InkToShape
    {
        [JsonProperty("isInkToShapeEnabled")]
        public bool IsInkToShapeEnabled { get; set; } = true;
        [JsonProperty("isInkToShapeNoFakePressureRectangle")]
        public bool IsInkToShapeNoFakePressureRectangle { get; set; }
        [JsonProperty("isInkToShapeNoFakePressureTriangle")]
        public bool IsInkToShapeNoFakePressureTriangle { get; set; }
        [JsonProperty("isInkToShapeTriangle")]
        public bool IsInkToShapeTriangle { get; set; } = true;
        [JsonProperty("isInkToShapeRectangle")]
        public bool IsInkToShapeRectangle { get; set; } = true;
        [JsonProperty("isInkToShapeRounded")]
        public bool IsInkToShapeRounded { get; set; } = true;
        [JsonProperty("lineStraightenSensitivity")]
        public double LineStraightenSensitivity { get; set; } = 0.20;
        [JsonProperty("lineNormalizationThreshold")]
        public double LineNormalizationThreshold { get; set; } = 0.5;
        [JsonProperty("shapeRecognitionEngine")]
        public int ShapeRecognitionEngine { get; set; }
        [JsonProperty("enableWinRtHandwritingStrokeBeautify")]
        public bool EnableWinRtHandwritingStrokeBeautify { get; set; }
        [JsonProperty("handwritingCorrectionFontFamily")]
        public string HandwritingCorrectionFontFamily { get; set; } = "Ink Free,KaiTi,Segoe Script";
    }

    public class RandSettings
    {
        [JsonProperty("displayRandWindowNamesInputBtn")]
        public bool DisplayRandWindowNamesInputBtn { get; set; }
        [JsonProperty("randWindowOnceCloseLatency")]
        public double RandWindowOnceCloseLatency { get; set; } = 2.5;
        [JsonProperty("randWindowOnceMaxStudents")]
        public int RandWindowOnceMaxStudents { get; set; } = 10;
        [JsonProperty("showRandomAndSingleDraw")]
        public bool ShowRandomAndSingleDraw { get; set; } = true;
        [JsonProperty("directCallCiRand")]
        public bool DirectCallCiRand { get; set; }
        [JsonProperty("externalCallerType")]
        public int ExternalCallerType { get; set; } = 0;
        [JsonProperty("selectedBackgroundIndex")]
        public int SelectedBackgroundIndex { get; set; }
        [JsonProperty("customPickNameBackgrounds")]
        public List<CustomPickNameBackground> CustomPickNameBackgrounds { get; set; } = new List<CustomPickNameBackground>();
        [JsonProperty("useLegacyTimerUI")]
        public bool UseLegacyTimerUI { get; set; } = false;
        [JsonProperty("useNewStyleUI")]
        public bool UseNewStyleUI { get; set; } = true;
        [JsonProperty("timerVolume")]
        public double TimerVolume { get; set; } = 1.0;
        [JsonProperty("customTimerSoundPath")]
        public string CustomTimerSoundPath { get; set; } = "";
        [JsonProperty("enableOvertimeCountUp")]
        public bool EnableOvertimeCountUp { get; set; } = false;
        [JsonProperty("enableOvertimeRedText")]
        public bool EnableOvertimeRedText { get; set; } = false;
        [JsonProperty("enableProgressiveReminder")]
        public bool EnableProgressiveReminder { get; set; } = false;
        [JsonProperty("progressiveReminderVolume")]
        public double ProgressiveReminderVolume { get; set; } = 1.0;
        [JsonProperty("progressiveReminderSoundPath")]
        public string ProgressiveReminderSoundPath { get; set; } = "";
        [JsonProperty("useNewRollCallUI")]
        public bool UseNewRollCallUI { get; set; } = true;
        [JsonProperty("enableMLAvoidance")]
        public bool EnableMLAvoidance { get; set; } = true;
        [JsonProperty("mlAvoidanceHistoryCount")]
        public int MLAvoidanceHistoryCount { get; set; } = 50;
        [JsonProperty("mlAvoidanceWeight")]
        public double MLAvoidanceWeight { get; set; } = 1.0;
        [JsonProperty("enableQuickDraw")]
        public bool EnableQuickDraw { get; set; } = true;
    }

    public class CustomPickNameBackground
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        public CustomPickNameBackground(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        // 用于JSON序列化
        public CustomPickNameBackground() { }
    }

    public class CustomFloatingBarIcon
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        public CustomFloatingBarIcon(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        // 用于JSON序列化
        public CustomFloatingBarIcon() { }
    }

    public class ModeSettings
    {
        [JsonProperty("isPPTOnlyMode")]
        public bool IsPPTOnlyMode { get; set; } = false; // 是否为仅PPT模式，默认为false（正常模式）
    }

    public class CameraSettings
    {
        [JsonProperty("rotationAngle")]
        public int RotationAngle { get; set; } = 0;

        [JsonProperty("resolutionWidth")]
        public int ResolutionWidth { get; set; } = 1920;

        [JsonProperty("resolutionHeight")]
        public int ResolutionHeight { get; set; } = 1080;

        [JsonProperty("selectedCameraIndex")]
        public int SelectedCameraIndex { get; set; } = 0;
    }

    public class DlassSettings
    {
        [JsonProperty("userToken")]
        public string UserToken { get; set; } = string.Empty;

        [JsonProperty("savedTokens")]
        public List<string> SavedTokens { get; set; } = new List<string>();

        [JsonProperty("selectedClassName")]
        public string SelectedClassName { get; set; } = string.Empty;

        [JsonProperty("apiBaseUrl")]
        public string ApiBaseUrl { get; set; } = "https://dlass.tech";

        [JsonProperty("isAutoUploadNotes")]
        public bool IsAutoUploadNotes { get; set; } = false;

        private int _autoUploadDelayMinutes = 0;
        [JsonProperty("autoUploadDelayMinutes")]
        public int AutoUploadDelayMinutes
        {
            get { return _autoUploadDelayMinutes; }
            set { _autoUploadDelayMinutes = Math.Max(0, value); }
        }

        [JsonProperty("webDavUrl")]
        public string WebDavUrl { get; set; } = string.Empty;

        [JsonProperty("webDavUsername")]
        public string WebDavUsername { get; set; } = string.Empty;

        [JsonProperty("webDavPassword")]
        public string WebDavPassword { get; set; } = string.Empty;

        [JsonProperty("webDavRootDirectory")]
        public string WebDavRootDirectory { get; set; } = string.Empty;
    }

    public class UploadSettings
    {
        [JsonProperty("uploadDelayMinutes")]
        public int UploadDelayMinutes
        {
            get { return _uploadDelayMinutes; }
            set { _uploadDelayMinutes = Math.Max(0, Math.Min(60, value)); }
        }
        private int _uploadDelayMinutes = 0;

        [JsonProperty("enabledProviders")]
        public List<string> EnabledProviders
        {
            get { return _enabledProviders; }
            set { _enabledProviders = value ?? new List<string>(); }
        }
        private List<string> _enabledProviders = new List<string>();
    }

    public class MiniWhiteboardSettings
    {
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonProperty("defaultWidth")]
        public double DefaultWidth { get; set; } = 400;

        [JsonProperty("defaultHeight")]
        public double DefaultHeight { get; set; } = 300;

        [JsonProperty("defaultOpacity")]
        public double DefaultOpacity { get; set; } = 0.95;

        [JsonProperty("backgroundColor")]
        public string BackgroundColor { get; set; } = "#FF2A2A2A";

        [JsonProperty("syncWithPPTPages")]
        public bool SyncWithPPTPages { get; set; } = true;

        [JsonProperty("penWidth")]
        public double PenWidth { get; set; } = 3;

        [JsonProperty("penColor")]
        public string PenColor { get; set; } = "#FFFFFFFF";

        [JsonProperty("currentColorIndex")]
        public int CurrentColorIndex { get; set; } = 0; // 0=White, 1=Black, 2=Red, 3=Orange, 4=Yellow, 5=Green, 6=Blue, 7=Purple
    }
}
