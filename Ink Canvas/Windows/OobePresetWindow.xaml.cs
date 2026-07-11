using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Screen = System.Windows.Forms.Screen;

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

        #region 高DPI/多屏自适应窗口控制

        private HwndSource _hwndSource;

        private void GetWorkAreaSize(out double workAreaWidthDip, out double workAreaHeightDip, out double screenLeftDip, out double screenTopDip)
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            var currentScreen = Screen.FromHandle(windowHandle);
            var workingArea = currentScreen.WorkingArea;
            var screenBounds = currentScreen.Bounds;

            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            workAreaWidthDip = workingArea.Width / dpiScaleX;
            workAreaHeightDip = workingArea.Height / dpiScaleY;
            screenLeftDip = screenBounds.Left / dpiScaleX;
            screenTopDip = screenBounds.Top / dpiScaleY;
        }

        private void SetMaxSizeAndCenter()
        {
            if (!this.IsLoaded) return;

            GetWorkAreaSize(out double workAreaWidthDip, out double workAreaHeightDip, out double screenLeftDip, out double screenTopDip);

            this.MaxWidth = workAreaWidthDip;
            this.MaxHeight = workAreaHeightDip;

            this.Left = screenLeftDip + (workAreaWidthDip - this.ActualWidth) / 2;
            this.Top = screenTopDip + (workAreaHeightDip - this.ActualHeight) / 2;
        }

        private void RegisterDpiChangedListener()
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(DpiChangedWndProc);
        }

        private void UnregisterDpiChangedListener()
        {
            _hwndSource?.RemoveHook(DpiChangedWndProc);
            _hwndSource = null;
        }

        private IntPtr DpiChangedWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DPICHANGED = 0x02E0;
            if (msg == WM_DPICHANGED)
            {
                SetMaxSizeAndCenter();
                handled = true;
            }
            return IntPtr.Zero;
        }

        #endregion

        private void OobePresetWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetMaxSizeAndCenter();
            RegisterDpiChangedListener();
        }

        private void OobePresetWindow_OnClosing(object sender, CancelEventArgs e)
        {
            UnregisterDpiChangedListener();
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
            settings.Startup.CrashAction = 2; // 弹窗重启
            settings.Startup.TelemetryUploadLevel = settings.Startup.HasAcceptedTelemetryPrivacy
                ? TelemetryUploadLevel.Extended
                : TelemetryUploadLevel.None;

            // 画板与墨迹
            settings.Canvas.IsShowCursor = false;
            settings.Canvas.DisablePressure = false;
            settings.Canvas.HideStrokeWhenSelecting = false;
            settings.Canvas.EnablePalmEraser = false;

            // 墨迹纠正
            settings.InkToShape.IsInkToShapeEnabled = true;

            // 手势
            settings.Gesture.IsEnableTwoFingerZoom = false;
            settings.Gesture.IsEnableTwoFingerTranslate = true;

            // 个性化
            settings.Appearance.Theme = 2;
            settings.Appearance.WindowBackdrop = "Acrylic";
            settings.Appearance.EnableSplashScreen = true;
            settings.Appearance.EnableTrayIcon = true;
            settings.Appearance.IsShowQuickPanel = true;
            settings.Appearance.EnableHotkeysInMouseMode = false;

            // PPT 联动
            settings.PowerPointSettings.PowerPointSupport = true;
            settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            settings.PowerPointSettings.EnablePPTTimeCapsule = true;
            settings.PowerPointSettings.PPTTimeCapsulePosition = 1;
            settings.PowerPointSettings.PPTTimeCapsuleOpacity = 1.0;
            settings.PowerPointSettings.PPTTimeCapsuleScale = 1.0;
            settings.PowerPointSettings.PPTLinkMode = PPTLinkMode.Agent;
            settings.PowerPointSettings.ShowPPTButton = true;
            settings.PowerPointSettings.PPTButtonsDisplayOption = 2222;
            settings.PowerPointSettings.EnablePPTButtonPageClickable = true;
            settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn = true;
            settings.PowerPointSettings.ShowPPTSidebarByDefault = false;
            settings.PowerPointSettings.ShowPPTModePrompt = false;
            settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = false;
            settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = true;
            settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = false;
            settings.PowerPointSettings.IsNotifyPreviousPage = true;
            settings.PowerPointSettings.IsNotifyHiddenPage = false;
            settings.PowerPointSettings.IsNotifyAutoPlayPresentation = true;
            settings.PowerPointSettings.EnableWppProcessKill = true;
            settings.PowerPointSettings.EnablePowerPointEnhancement = false;
            settings.PowerPointSettings.SkipAnimationsWhenGoNext = false;

            // 自动化
            settings.Automation.IsAutoFoldInPPTSlideShow = false;
            settings.Automation.IsEnableAutoSaveStrokes = true;
            settings.Automation.IsAutoSaveScreenshotAtClear = false;
            settings.Automation.IsSaveScreenshotsInDateFolders = false;
            if (settings.Automation.FloatingWindowInterceptor != null)
                settings.Automation.FloatingWindowInterceptor.IsEnabled = false;

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
            settings.Automation.IsAutoSaveScreenshotAtClear = true;
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
