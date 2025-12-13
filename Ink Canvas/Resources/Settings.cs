using Newtonsoft.Json;
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
    }

    public class Canvas
    {
        [JsonProperty("inkWidth")]
        public double InkWidth { get; set; } = 2.5;
        [JsonProperty("highlighterWidth")]
        public double HighlighterWidth { get; set; } = 20;
        [JsonProperty("inkAlpha")]
        public double InkAlpha { get; set; } = 255;
        [JsonProperty("isShowCursor")]
        public bool IsShowCursor { get; set; }
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

        // 墨迹渐隐功能设置
        [JsonProperty("enableInkFade")]
        public bool EnableInkFade { get; set; } = false;
        [JsonProperty("inkFadeTime")]
        public int InkFadeTime { get; set; } = 3000; // 墨迹渐隐时间（毫秒）

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
        public bool IsEnableTwoFingerGesture => IsEnableTwoFingerZoom || IsEnableTwoFingerTranslate || IsEnableTwoFingerRotation;
        [JsonIgnore]
        public bool IsEnableTwoFingerGestureTranslateOrRotation => IsEnableTwoFingerTranslate || IsEnableTwoFingerRotation;
        [JsonProperty("isEnableMultiTouchMode")]
        public bool IsEnableMultiTouchMode { get; set; } = false;
        [JsonProperty("isEnableTwoFingerZoom")]
        public bool IsEnableTwoFingerZoom { get; set; } = true;
        [JsonProperty("isEnableTwoFingerTranslate")]
        public bool IsEnableTwoFingerTranslate { get; set; } = true;
        [JsonProperty("AutoSwitchTwoFingerGesture")]
        public bool AutoSwitchTwoFingerGesture { get; set; } = true;
        [JsonProperty("isEnableTwoFingerRotation")]
        public bool IsEnableTwoFingerRotation { get; set; }
        [JsonProperty("isEnableTwoFingerRotationOnSelection")]
        public bool IsEnableTwoFingerRotationOnSelection { get; set; }
    }

    // 更新通道枚举
    public enum UpdateChannel
    {
        Release,
        Beta
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
        [JsonProperty("skippedVersion")]
        public string SkippedVersion { get; set; } = "";
        [JsonProperty("isEnableNibMode")]
        public bool IsEnableNibMode { get; set; }
        [JsonProperty("isFoldAtStartup")]
        public bool IsFoldAtStartup { get; set; }
        [JsonProperty("crashAction")]
        public int CrashAction { get; set; }
    }

    public class Appearance
    {
        [JsonProperty("isEnableDisPlayNibModeToggler")]
        public bool IsEnableDisPlayNibModeToggler { get; set; } = true;
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
        [JsonProperty("viewboxFloatingBarOpacityInPPTValue")]
        public double ViewboxFloatingBarOpacityInPPTValue { get; set; } = 0.5;
        [JsonProperty("enableViewboxBlackBoardScaleTransform")]
        public bool EnableViewboxBlackBoardScaleTransform { get; set; }
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
        [JsonProperty("isShowQuickPanel")]
        public bool IsShowQuickPanel { get; set; } = true;
        [JsonProperty("chickenSoupSource")]
        public int ChickenSoupSource { get; set; } = 1;
        [JsonProperty("isShowModeFingerToggleSwitch")]
        public bool IsShowModeFingerToggleSwitch { get; set; } = true;
        [JsonProperty("theme")]
        public int Theme { get; set; }

        // 浮动栏按钮显示控制
        [JsonProperty("useLegacyFloatingBarUI")]
        public bool UseLegacyFloatingBarUI { get; set; } = false;
        [JsonProperty("isShowShapeButton")]
        public bool IsShowShapeButton { get; set; } = true;
        [JsonProperty("isShowUndoButton")]
        public bool IsShowUndoButton { get; set; } = true;
        [JsonProperty("isShowRedoButton")]
        public bool IsShowRedoButton { get; set; } = true;
        [JsonProperty("isShowClearButton")]
        public bool IsShowClearButton { get; set; } = true;
        [JsonProperty("isShowWhiteboardButton")]
        public bool IsShowWhiteboardButton { get; set; } = true;
        [JsonProperty("isShowHideButton")]
        public bool IsShowHideButton { get; set; } = true;
        [JsonProperty("isShowLassoSelectButton")]
        public bool IsShowLassoSelectButton { get; set; } = true;
        [JsonProperty("isShowClearAndMouseButton")]
        public bool IsShowClearAndMouseButton { get; set; } = true;
        [JsonProperty("eraserDisplayOption")]
        public int EraserDisplayOption { get; set; }
        [JsonProperty("isShowQuickColorPalette")]
        public bool IsShowQuickColorPalette { get; set; }
        [JsonProperty("quickColorPaletteDisplayMode")]
        public int QuickColorPaletteDisplayMode { get; set; } = 1;
        [JsonProperty("enableHotkeysInMouseMode")]
        public bool EnableHotkeysInMouseMode { get; set; } = false;

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

        [JsonProperty("enablePPTButtonLongPressPageTurn")]
        public bool EnablePPTButtonLongPressPageTurn { get; set; } = true;

        // -- new --

        [JsonProperty("powerPointSupport")]
        public bool PowerPointSupport { get; set; } = true;
        [JsonProperty("isShowCanvasAtNewSlideShow")]
        public bool IsShowCanvasAtNewSlideShow { get; set; } = true;
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
        [JsonProperty("showGestureButtonInSlideShow")]
        public bool ShowGestureButtonInSlideShow { get; set; } = false;
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

        [JsonProperty("isAutoKillPptService")]
        public bool IsAutoKillPptService { get; set; }

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
        public bool IsAutoSaveStrokesAtClear { get; set; }

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

        [JsonProperty("isAutoEnterAnnotationAfterKillHite")]
        public bool IsAutoEnterAnnotationAfterKillHite { get; set; }

        [JsonProperty("isEnableAutoSaveStrokes")]
        public bool IsEnableAutoSaveStrokes { get; set; } = true;

        [JsonProperty("autoSaveStrokesIntervalMinutes")]
        public int AutoSaveStrokesIntervalMinutes { get; set; } = 5;

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
            { "SeewoWhiteboard3Floating", true },
            { "SeewoWhiteboard5Floating", true },
            { "SeewoWhiteboard5CFloating", true },
            { "SeewoPincoSideBarFloating", true },
            { "SeewoPincoDrawingFloating", true },
            { "SeewoPincoBoardService", true },
            { "SeewoPPTFloating", true },
            { "AiClassFloating", true },
            { "HiteAnnotationFloating", true },
            { "ChangYanFloating", true },
            { "ChangYanBrushSettings", true },
            { "ChangYanSwipeClear", true },
            { "ChangYanInteraction", true },
            { "ChangYanSubjectApp", true },
            { "ChangYanControl", true },
            { "ChangYanCommonTools", true },
            { "ChangYanSceneToolbar", true },
            { "ChangYanDrawWindow", true },
            { "ChangYanPptFloating", true },
            { "ChangYanPptPageControl", true },
            { "ChangYanPptGoBack", true },
            { "ChangYanPptPreview", true },
            { "IntelligentClassFloating", true },
            { "IntelligentClassPptFloating", true },
            { "SeewoDesktopAnnotationFloating", true },
            { "SeewoDesktopSideBarFloating", true }
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
        public bool IsEnableAvoidFullScreenHelper { get; set; }

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

        [JsonProperty("windowMode")]
        public bool WindowMode { get; set; } = true; 
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

        [JsonProperty("autoUploadDelayMinutes")]
        public int AutoUploadDelayMinutes { get; set; } = 0;
    }
}
