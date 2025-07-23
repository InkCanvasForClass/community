using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

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
        public bool IsShowCursor { get; set; } = false;
        [JsonProperty("inkStyle")]
        public int InkStyle { get; set; } = 0;
        [JsonProperty("eraserSize")]
        public int EraserSize { get; set; } = 2;
        [JsonProperty("eraserType")] 
        public int EraserType { get; set; } = 0; // 0 - 图标切换模式      1 - 面积擦     2 - 线条擦
        [JsonProperty("eraserShapeType")]
        public int EraserShapeType { get; set; } = 0; // 0 - 圆形擦  1 - 黑板擦
        [JsonProperty("hideStrokeWhenSelecting")]
        public bool HideStrokeWhenSelecting { get; set; } = true;
        [JsonProperty("fitToCurve")]
        public bool FitToCurve { get; set; } = false; // 默认关闭原来的贝塞尔平滑
        [JsonProperty("useAdvancedBezierSmoothing")]
        public bool UseAdvancedBezierSmoothing { get; set; } = true; // 默认启用高级贝塞尔曲线平滑
        [JsonProperty("clearCanvasAndClearTimeMachine")]
        public bool ClearCanvasAndClearTimeMachine { get; set; } = false;
        [JsonProperty("enablePressureTouchMode")]
        public bool EnablePressureTouchMode { get; set; } = false; // 是否启用压感触屏模式
        [JsonProperty("disablePressure")]
        public bool DisablePressure { get; set; } = false; // 是否屏蔽压感
        [JsonProperty("autoStraightenLine")]
        public bool AutoStraightenLine { get; set; } = true; // 是否启用直线自动拉直
        [JsonProperty("autoStraightenLineThreshold")]
        public int AutoStraightenLineThreshold { get; set; } = 30; // 直线自动拉直的长度阈值（像素）
        [JsonProperty("highPrecisionLineStraighten")]
        public bool HighPrecisionLineStraighten { get; set; } = true; // 是否启用高精度直线拉直
        [JsonProperty("lineEndpointSnapping")]
        public bool LineEndpointSnapping { get; set; } = true; // 是否启用直线端点吸附
        [JsonProperty("lineEndpointSnappingThreshold")]
        public int LineEndpointSnappingThreshold { get; set; } = 15; // 直线端点吸附的距离阈值（像素）

        [JsonProperty("usingWhiteboard")]
        public bool UsingWhiteboard { get; set; } = false;

        [JsonProperty("customBackgroundColor")]
        public string CustomBackgroundColor { get; set; } = "#162924";

        [JsonProperty("hyperbolaAsymptoteOption")]
        public OptionalOperation HyperbolaAsymptoteOption { get; set; } = OptionalOperation.Ask;
        [JsonProperty("isCompressPicturesUploaded")]
        public bool IsCompressPicturesUploaded { get; set; } = false;
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
        public bool IsEnableMultiTouchMode { get; set; } = true;
        [JsonProperty("isEnableTwoFingerZoom")]
        public bool IsEnableTwoFingerZoom { get; set; } = true;
        [JsonProperty("isEnableTwoFingerTranslate")]
        public bool IsEnableTwoFingerTranslate { get; set; } = true;
        [JsonProperty("AutoSwitchTwoFingerGesture")]
        public bool AutoSwitchTwoFingerGesture { get; set; } = true;
        [JsonProperty("isEnableTwoFingerRotation")]
        public bool IsEnableTwoFingerRotation { get; set; } = false;
        [JsonProperty("isEnableTwoFingerRotationOnSelection")]
        public bool IsEnableTwoFingerRotationOnSelection { get; set; } = false;
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
        public bool IsAutoUpdateWithSilence { get; set; } = false;
        [JsonProperty("isAutoUpdateWithSilenceStartTime")]
        public string AutoUpdateWithSilenceStartTime { get; set; } = "06:00";
        [JsonProperty("isAutoUpdateWithSilenceEndTime")]
        public string AutoUpdateWithSilenceEndTime { get; set; } = "22:00";
        [JsonProperty("updateChannel")]
        public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Release;
        [JsonProperty("skippedVersion")]
        public string SkippedVersion { get; set; } = "";
        [JsonProperty("isEnableNibMode")]
        public bool IsEnableNibMode { get; set; } = false;
        [JsonProperty("isFoldAtStartup")]
        public bool IsFoldAtStartup { get; set; } = false;
        [JsonProperty("crashAction")]
        public int CrashAction { get; set; } = 0;
    }

    public class Appearance
    {
        [JsonProperty("isEnableDisPlayNibModeToggler")]
        public bool IsEnableDisPlayNibModeToggler { get; set; } = true;
        [JsonProperty("isColorfulViewboxFloatingBar")]
        public bool IsColorfulViewboxFloatingBar { get; set; } = false;
        // [JsonProperty("enableViewboxFloatingBarScaleTransform")]
        // public bool EnableViewboxFloatingBarScaleTransform { get; set; } = false;
        [JsonProperty("viewboxFloatingBarScaleTransformValue")]
        public double ViewboxFloatingBarScaleTransformValue { get; set; } = 1.0;
        [JsonProperty("floatingBarImg")] 
        public int FloatingBarImg { get; set; } = 0;
        [JsonProperty("customFloatingBarImgs")]
        public List<CustomFloatingBarIcon> CustomFloatingBarImgs { get; set; } = new List<CustomFloatingBarIcon>();
        [JsonProperty("viewboxFloatingBarOpacityValue")]
        public double ViewboxFloatingBarOpacityValue { get; set; } = 1.0;
        [JsonProperty("enableTrayIcon")]
        public bool EnableTrayIcon { get; set; } = true;
        [JsonProperty("viewboxFloatingBarOpacityInPPTValue")]
        public double ViewboxFloatingBarOpacityInPPTValue { get; set; } = 0.5;
        [JsonProperty("enableViewboxBlackBoardScaleTransform")]
        public bool EnableViewboxBlackBoardScaleTransform { get; set; } = false;
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
        public bool IsShowHideControlButton { get; set; } = false;
        [JsonProperty("unFoldButtonImageType")]
        public int UnFoldButtonImageType { get; set; } = 0;
        [JsonProperty("isShowLRSwitchButton")]
        public bool IsShowLRSwitchButton { get; set; } = false;
        [JsonProperty("isShowQuickPanel")]
        public bool IsShowQuickPanel { get; set; } = true;
        [JsonProperty("chickenSoupSource")]
        public int ChickenSoupSource { get; set; } = 1;
        [JsonProperty("isShowModeFingerToggleSwitch")]
        public bool IsShowModeFingerToggleSwitch { get; set; } = true;
        [JsonProperty("theme")]
        public int Theme { get; set; } = 0;            
    }

    public class PowerPointSettings
    {
        // -- new --

        [JsonProperty("showPPTButton")]
        public bool ShowPPTButton { get; set; } = true;

        // 每一个数位代表一个选项，2就是开启，1就是关闭
        [JsonProperty("pptButtonsDisplayOption")]
        public int PPTButtonsDisplayOption { get; set; } = 2222;

        // 0居中，+就是往上，-就是往下
        [JsonProperty("pptLSButtonPosition")]
        public int PPTLSButtonPosition { get; set; } = 0;

        // 0居中，+就是往上，-就是往下
        [JsonProperty("pptRSButtonPosition")]
        public int PPTRSButtonPosition { get; set; } = 0;

        [JsonProperty("pptSButtonsOption")]
        public int PPTSButtonsOption { get; set; } = 221;

        [JsonProperty("pptBButtonsOption")]
        public int PPTBButtonsOption { get; set; } = 121;

        [JsonProperty("enablePPTButtonPageClickable")]
        public bool EnablePPTButtonPageClickable { get; set; } = true;

        // -- new --

        [JsonProperty("powerPointSupport")]
        public bool PowerPointSupport { get; set; } = true;
        [JsonProperty("isShowCanvasAtNewSlideShow")]
        public bool IsShowCanvasAtNewSlideShow { get; set; } = true;
        [JsonProperty("isNoClearStrokeOnSelectWhenInPowerPoint")]
        public bool IsNoClearStrokeOnSelectWhenInPowerPoint { get; set; } = true;
        [JsonProperty("isShowStrokeOnSelectInPowerPoint")]
        public bool IsShowStrokeOnSelectInPowerPoint { get; set; } = false;
        [JsonProperty("isAutoSaveStrokesInPowerPoint")]
        public bool IsAutoSaveStrokesInPowerPoint { get; set; } = true;
        [JsonProperty("isAutoSaveScreenShotInPowerPoint")]
        public bool IsAutoSaveScreenShotInPowerPoint { get; set; } = false;
        [JsonProperty("isNotifyPreviousPage")]
        public bool IsNotifyPreviousPage { get; set; } = false;
        [JsonProperty("isNotifyHiddenPage")]
        public bool IsNotifyHiddenPage { get; set; } = true;
        [JsonProperty("isNotifyAutoPlayPresentation")]
        public bool IsNotifyAutoPlayPresentation { get; set; } = true;
        [JsonProperty("isEnableTwoFingerGestureInPresentationMode")]
        public bool IsEnableTwoFingerGestureInPresentationMode { get; set; } = false;
        [JsonProperty("isEnableFingerGestureSlideShowControl")]
        public bool IsEnableFingerGestureSlideShowControl { get; set; } = true;
        [JsonProperty("isSupportWPS")]
        public bool IsSupportWPS { get; set; } = true;
        [JsonProperty("enableWppProcessKill")]
        public bool EnableWppProcessKill { get; set; } = true;
        [JsonProperty("isAlwaysGoToFirstPageOnReenter")]
        public bool IsAlwaysGoToFirstPageOnReenter { get; set; } = false;
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
        public bool IsAutoEnterAnnotationModeWhenExitFoldMode { get; set; } = false;

        [JsonProperty("isAutoFoldInEasiNote")]
        public bool IsAutoFoldInEasiNote { get; set; } = false;

        [JsonProperty("isAutoFoldInEasiNoteIgnoreDesktopAnno")]
        public bool IsAutoFoldInEasiNoteIgnoreDesktopAnno { get; set; } = false;

        [JsonProperty("isAutoFoldInEasiCamera")]
        public bool IsAutoFoldInEasiCamera { get; set; } = false;

        [JsonProperty("isAutoFoldInEasiNote3")]
        public bool IsAutoFoldInEasiNote3 { get; set; } = false;
        [JsonProperty("isAutoFoldInEasiNote3C")]
        public bool IsAutoFoldInEasiNote3C { get; set; } = false;

        [JsonProperty("isAutoFoldInEasiNote5C")]
        public bool IsAutoFoldInEasiNote5C { get; set; } = false;

        [JsonProperty("isAutoFoldInSeewoPincoTeacher")]
        public bool IsAutoFoldInSeewoPincoTeacher { get; set; } = false;

        [JsonProperty("isAutoFoldInHiteTouchPro")]
        public bool IsAutoFoldInHiteTouchPro { get; set; } = false;
        [JsonProperty("isAutoFoldInHiteLightBoard")]
        public bool IsAutoFoldInHiteLightBoard { get; set; } = false;

        [JsonProperty("isAutoFoldInHiteCamera")]
        public bool IsAutoFoldInHiteCamera { get; set; } = false;

        [JsonProperty("isAutoFoldInWxBoardMain")]
        public bool IsAutoFoldInWxBoardMain { get; set; } = false;
        /*
        [JsonProperty("isAutoFoldInZySmartBoard")]
        public bool IsAutoFoldInZySmartBoard { get; set; } = false;
        */
        [JsonProperty("isAutoFoldInOldZyBoard")]
        public bool IsAutoFoldInOldZyBoard { get; set; } = false;

        [JsonProperty("isAutoFoldInMSWhiteboard")]
        public bool IsAutoFoldInMSWhiteboard { get; set; } = false;

        [JsonProperty("isAutoFoldInAdmoxWhiteboard")]
        public bool IsAutoFoldInAdmoxWhiteboard { get; set; } = false;

        [JsonProperty("isAutoFoldInAdmoxBooth")]
        public bool IsAutoFoldInAdmoxBooth { get; set; } = false;

        [JsonProperty("isAutoFoldInQPoint")]
        public bool IsAutoFoldInQPoint { get; set; } = false;

        [JsonProperty("isAutoFoldInYiYunVisualPresenter")]
        public bool IsAutoFoldInYiYunVisualPresenter { get; set; } = false;

        [JsonProperty("isAutoFoldInMaxHubWhiteboard")]
        public bool IsAutoFoldInMaxHubWhiteboard { get; set; } = false;

        [JsonProperty("isAutoFoldInPPTSlideShow")]
        public bool IsAutoFoldInPPTSlideShow { get; set; } = false;

        [JsonProperty("isAutoFoldAfterPPTSlideShow")]
        public bool IsAutoFoldAfterPPTSlideShow { get; set; } = false;

        [JsonProperty("isAutoKillPptService")]
        public bool IsAutoKillPptService { get; set; } = false;

        [JsonProperty("isAutoKillEasiNote")]
        public bool IsAutoKillEasiNote { get; set; } = false;

        [JsonProperty("isAutoKillHiteAnnotation")]
        public bool IsAutoKillHiteAnnotation { get; set; } = false;

        [JsonProperty("isAutoKillVComYouJiao")]
        public bool IsAutoKillVComYouJiao { get; set; } = false;

        [JsonProperty("isAutoKillSeewoLauncher2DesktopAnnotation")]
        public bool IsAutoKillSeewoLauncher2DesktopAnnotation { get; set; } = false;

        [JsonProperty("isAutoKillInkCanvas")]
        public bool IsAutoKillInkCanvas { get; set; } = false;

        [JsonProperty("isAutoKillICA")]
        public bool IsAutoKillICA { get; set; } = false;

        [JsonProperty("isAutoKillIDT")]
        public bool IsAutoKillIDT { get; set; } = false;

        [JsonProperty("isSaveScreenshotsInDateFolders")]
        public bool IsSaveScreenshotsInDateFolders { get; set; } = false;

        [JsonProperty("isAutoSaveStrokesAtScreenshot")]
        public bool IsAutoSaveStrokesAtScreenshot { get; set; } = false;

        [JsonProperty("isAutoSaveStrokesAtClear")]
        public bool IsAutoSaveStrokesAtClear { get; set; } = false;

        [JsonProperty("isAutoClearWhenExitingWritingMode")]
        public bool IsAutoClearWhenExitingWritingMode { get; set; } = false;

        [JsonProperty("minimumAutomationStrokeNumber")]
        public int MinimumAutomationStrokeNumber { get; set; } = 0;

        [JsonProperty("autoSavedStrokesLocation")]
        public string AutoSavedStrokesLocation = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "saves");

        [JsonProperty("autoDelSavedFiles")]
        public bool AutoDelSavedFiles = false;

        [JsonProperty("autoDelSavedFilesDaysThreshold")]
        public int AutoDelSavedFilesDaysThreshold = 15;
        
        [JsonProperty("isSaveFullPageStrokes")]
        public bool IsSaveFullPageStrokes = false;

        [JsonProperty("isAutoEnterAnnotationAfterKillHite")]
        public bool IsAutoEnterAnnotationAfterKillHite { get; set; } = false;
    }

    public class Advanced
    {
        [JsonProperty("isSpecialScreen")]
        public bool IsSpecialScreen { get; set; } = false;

        [JsonProperty("isQuadIR")]
        public bool IsQuadIR { get; set; } = false;

        [JsonProperty("touchMultiplier")]
        public double TouchMultiplier { get; set; } = 0.25;

        [JsonProperty("nibModeBoundsWidth")]
        public int NibModeBoundsWidth { get; set; } = 10;

        [JsonProperty("fingerModeBoundsWidth")]
        public int FingerModeBoundsWidth { get; set; } = 30;

        [JsonProperty("eraserBindTouchMultiplier")]
        public bool EraserBindTouchMultiplier { get; set; } = false;

        [JsonProperty("isLogEnabled")]
        public bool IsLogEnabled { get; set; } = true;
        
        [JsonProperty("isSaveLogByDate")]
        public bool IsSaveLogByDate { get; set; } = true;

        [JsonProperty("isEnableFullScreenHelper")]
        public bool IsEnableFullScreenHelper { get; set; } = false;

        [JsonProperty("isEnableEdgeGestureUtil")]
        public bool IsEnableEdgeGestureUtil { get; set; } = false;

        [JsonProperty("edgeGestureUtilOnlyAffectBlackboardMode")]
        public bool EdgeGestureUtilOnlyAffectBlackboardMode { get; set; } = false;

        [JsonProperty("isEnableForceFullScreen")]
        public bool IsEnableForceFullScreen { get; set; } = false;

        [JsonProperty("isEnableResolutionChangeDetection")]
        public bool IsEnableResolutionChangeDetection { get; set; } = false;

        [JsonProperty("isEnableDPIChangeDetection")]
        public bool IsEnableDPIChangeDetection { get; set; } = false;

        [JsonProperty("isSecondConfirmWhenShutdownApp")]
        public bool IsSecondConfirmWhenShutdownApp { get; set; } = false;

        [JsonProperty("isEnableAvoidFullScreenHelper")]
        public bool IsEnableAvoidFullScreenHelper { get; set; } = false;
        
        [JsonProperty("isAutoBackupBeforeUpdate")]
        public bool IsAutoBackupBeforeUpdate { get; set; } = true;
    }

    public class InkToShape
    {
        [JsonProperty("isInkToShapeEnabled")]
        public bool IsInkToShapeEnabled { get; set; } = true;
        [JsonProperty("isInkToShapeNoFakePressureRectangle")]
        public bool IsInkToShapeNoFakePressureRectangle { get; set; } = false;
        [JsonProperty("isInkToShapeNoFakePressureTriangle")]
        public bool IsInkToShapeNoFakePressureTriangle { get; set; } = false;
        [JsonProperty("isInkToShapeTriangle")]
        public bool IsInkToShapeTriangle { get; set; } = true;
        [JsonProperty("isInkToShapeRectangle")]
        public bool IsInkToShapeRectangle { get; set; } = true;
        [JsonProperty("isInkToShapeRounded")]
        public bool IsInkToShapeRounded { get; set; } = true;
        [JsonProperty("lineStraightenSensitivity")]
        public double LineStraightenSensitivity { get; set; } = 0.20; // 直线检测灵敏度，值越小越严格（0.05-2.0）
    }

    public class RandSettings {
        [JsonProperty("displayRandWindowNamesInputBtn")]
        public bool DisplayRandWindowNamesInputBtn { get; set; } = false;
        [JsonProperty("randWindowOnceCloseLatency")]
        public double RandWindowOnceCloseLatency { get; set; } = 2.5;
        [JsonProperty("randWindowOnceMaxStudents")]
        public int RandWindowOnceMaxStudents { get; set; } = 10;
        [JsonProperty("showRandomAndSingleDraw")]
        public bool ShowRandomAndSingleDraw { get; set; } = true;
        [JsonProperty("directCallCiRand")]
        public bool DirectCallCiRand { get; set; } = false;
        [JsonProperty("selectedBackgroundIndex")]
        public int SelectedBackgroundIndex { get; set; } = 0;
        [JsonProperty("customPickNameBackgrounds")]
        public List<CustomPickNameBackground> CustomPickNameBackgrounds { get; set; } = new List<CustomPickNameBackground>();
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
}
