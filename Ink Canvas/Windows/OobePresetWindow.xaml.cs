using Ink_Canvas.Helpers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Windows
{
    public partial class OobePresetWindow : Window
    {
        public enum PresetKind { None, Standard, Lite }

        public PresetKind SelectedPreset { get; private set; } = PresetKind.None;

        public OobePresetWindow()
        {
            InitializeComponent();
            WindowBackdropHelper.Apply(this);
        }

        private void SelectPreset(PresetKind kind)
        {
            SelectedPreset = kind;

            // 重置所有卡片边框
            var defaultBrush = (Brush)FindResource("SystemControlForegroundBaseLowBrush");
            CardStandard.BorderBrush = defaultBrush;
            CardLite.BorderBrush = defaultBrush;
            IconStandard.Opacity = 0;
            IconLite.Opacity = 0;

            var accentBrush = (Brush)FindResource("SystemControlForegroundAccentBrush");
            switch (kind)
            {
                case PresetKind.Standard:
                    CardStandard.BorderBrush = accentBrush;
                    IconStandard.Opacity = 1;
                    break;
                case PresetKind.Lite:
                    CardLite.BorderBrush = accentBrush;
                    IconLite.Opacity = 1;
                    break;
            }

            BtnApply.IsEnabled = kind != PresetKind.None;
        }

        private void CardStandard_Click(object sender, MouseButtonEventArgs e) => SelectPreset(PresetKind.Standard);

        private void CardLite_Click(object sender, MouseButtonEventArgs e) => SelectPreset(PresetKind.Lite);

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPreset == PresetKind.None) return;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── 预设定义 ────────────────────────────────────────────────────────

        /// <summary>
        /// 课堂标准配置：适合大多数教学场景，启用 PPT 联动、自动保存、手势等。
        /// </summary>
        public static void ApplyStandard(Settings settings)
        {
            if (settings == null) return;

            // 启动与隐私
            settings.Startup.IsFoldAtStartup = true;
            settings.Startup.IsAutoUpdate = true;
            settings.Startup.CrashAction = 0; // 静默重启
            settings.Startup.TelemetryUploadLevel = settings.Startup.HasAcceptedTelemetryPrivacy
                ? TelemetryUploadLevel.Basic
                : TelemetryUploadLevel.None;

            // 画板与墨迹
            settings.Canvas.IsShowCursor = false;
            settings.Canvas.DisablePressure = false;
            settings.Canvas.HideStrokeWhenSelecting = true;
            settings.Canvas.EnablePalmEraser = true;

            // 墨迹纠正
            settings.InkToShape.IsInkToShapeEnabled = true;

            // 手势
            settings.Gesture.IsEnableTwoFingerZoom = true;
            settings.Gesture.IsEnableTwoFingerTranslate = true;
            settings.Gesture.AutoSwitchTwoFingerGesture = true;

            // 个性化
            settings.Appearance.Theme = 2; // 跟随系统
            settings.Appearance.WindowBackdrop = "None";
            settings.Appearance.EnableSplashScreen = false;
            settings.Appearance.EnableTrayIcon = true;
            settings.Appearance.IsShowQuickPanel = true;
            settings.Appearance.EnableHotkeysInMouseMode = false;

            // PPT 联动
            settings.PowerPointSettings.PowerPointSupport = true;
            settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            settings.PowerPointSettings.EnablePPTTimeCapsule = true;

            // 自动化
            settings.Automation.IsAutoFoldInPPTSlideShow = false;
            settings.Automation.IsEnableAutoSaveStrokes = true;
            settings.Automation.IsAutoSaveStrokesAtClear = true;
            settings.Automation.IsSaveScreenshotsInDateFolders = true;
            if (settings.Automation.FloatingWindowInterceptor != null)
                settings.Automation.FloatingWindowInterceptor.IsEnabled = true;

            // 随机点名
            settings.RandSettings.ShowRandomAndSingleDraw = true;

            // 高级
            settings.Advanced.IsLogEnabled = true;
        }

        /// <summary>
        /// 简洁轻量配置：最小化后台行为，适合简单批注场景。
        /// </summary>
        public static void ApplyLite(Settings settings)
        {
            if (settings == null) return;

            // 启动与隐私
            settings.Startup.IsFoldAtStartup = true;
            settings.Startup.IsAutoUpdate = true;
            settings.Startup.CrashAction = 0;
            settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;

            // 画板与墨迹
            settings.Canvas.IsShowCursor = false;
            settings.Canvas.DisablePressure = false;
            settings.Canvas.HideStrokeWhenSelecting = true;
            settings.Canvas.EnablePalmEraser = false;

            // 墨迹纠正
            settings.InkToShape.IsInkToShapeEnabled = false;

            // 手势
            settings.Gesture.IsEnableTwoFingerZoom = false;
            settings.Gesture.IsEnableTwoFingerTranslate = false;
            settings.Gesture.AutoSwitchTwoFingerGesture = false;

            // 个性化
            settings.Appearance.Theme = 2; // 跟随系统
            settings.Appearance.WindowBackdrop = "None";
            settings.Appearance.EnableSplashScreen = false;
            settings.Appearance.EnableTrayIcon = true;
            settings.Appearance.IsShowQuickPanel = false;
            settings.Appearance.EnableHotkeysInMouseMode = false;

            // PPT 联动
            settings.PowerPointSettings.PowerPointSupport = true;
            settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            settings.PowerPointSettings.EnablePPTTimeCapsule = false;

            // 自动化
            settings.Automation.IsAutoFoldInPPTSlideShow = false;
            settings.Automation.IsEnableAutoSaveStrokes = true;
            settings.Automation.IsAutoSaveStrokesAtClear = true;
            settings.Automation.IsSaveScreenshotsInDateFolders = false;
            if (settings.Automation.FloatingWindowInterceptor != null)
                settings.Automation.FloatingWindowInterceptor.IsEnabled = false;

            // 随机点名
            settings.RandSettings.ShowRandomAndSingleDraw = true;

            // 高级
            settings.Advanced.IsLogEnabled = true;
        }
    }
}
