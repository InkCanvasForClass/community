using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using NavigationViewPaneDisplayMode = iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using NavDirection = Ink_Canvas.Controls.PPTNavBar.NavDirection;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PPTPageFlipPreviewPage : Page
    {
        private bool _isLoaded = false;
        private NavigationViewPaneDisplayMode _originalPaneDisplayMode;
        private bool _originalIsInPPTPresentationMode;
        private ToolbarPosition _originalToolbarPosition;
        private DelayAction _sliderDelayAction = new DelayAction();
        private int _originalMainWindowExStyle;
        private bool _originalSettingsWindowTopmost;

        // 内嵌预览内直接拖动翻页按钮（Issue #421 第一期 A 方案）
        private const double PreviewDesignWidth = 1920.0;
        private const double DragStartThreshold = 3.0;
        private const int SideOffsetMin = -500;
        private const int SideOffsetMax = 500;
        private const int BottomOffsetMin = -100;
        private const int BottomOffsetMax = 500;
        private const int SnapToZeroThreshold = 10;
        private NavDirection? _dragDirection;
        private Point _dragStartPoint;
        private int _dragStartOffset;
        private bool _isDragging;
        private bool _dragWritesGlobal;
        private bool _isSyncingDrag;
        private bool _positionSettingsDirty;
        private UIElement _dragMouseCapture;
        private UIElement _dragTouchCapture;
        private System.Windows.Input.TouchDevice _dragTouchDevice;

        public PPTPageFlipPreviewPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 根据 Debug 设置决定是否显示左侧内嵌预览，并同步调整设置区左边距
            bool showPreview = SettingsManager.Settings.Advanced.IsPPTPageFlipPreviewVisible;
            PreviewPanel.Visibility = showPreview ? Visibility.Visible : Visibility.Collapsed;
            // 显示预览时左右两列等宽各占一半；隐藏预览时左列收缩不占位
            PreviewColumn.Width = showPreview ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            // 显示预览时顶部提示与设置区左右边距收窄为 12；隐藏时保持 59 与其他页面一致
            double sideMargin = showPreview ? 12 : 59;
            TopInfoPanel.Margin = new Thickness(sideMargin, 12, 0, 12);
            SettingsContentGrid.Margin = new Thickness(sideMargin, 0, sideMargin, 0);

            var settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                _originalPaneDisplayMode = settingsWindow.NavigationViewControl.PaneDisplayMode;
                settingsWindow.NavigationViewControl.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
                settingsWindow.Closed += SettingsWindow_Closed;

                // Temporarily set SettingsWindow topmost to ensure it stays in front of MainWindow
                _originalSettingsWindowTopmost = settingsWindow.Topmost;
                settingsWindow.Topmost = true;
            }

            // Force main window toolbar into PPT mode & bottom center position
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                _originalIsInPPTPresentationMode = mw.IsInPPTPresentationMode;
                _originalToolbarPosition = SettingsManager.Settings.Appearance.ToolbarPosition;

                // Disable AvoidFullScreenHelper hook temporarily without overriding settings
                AvoidFullScreenHelper.SetBoardMode(true);
                AvoidFullScreenHelper.StopAvoidFullScreen(mw);

                mw.IsInPPTPresentationMode = true;
                SettingsManager.Settings.Appearance.ToolbarPosition = ToolbarPosition.Right; // Bottom center layout in PPT mode

                mw.UpdateToolbarComponentVisibility();
                mw.UpdateToolbarPosition();

                // Move MainWindow to fullscreen size so it overlays the whole screen
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                var mwHwnd = new WindowInteropHelper(mw).Handle;
                MainWindow.MoveWindow(mwHwnd, 0, 0, screen.Bounds.Width, screen.Bounds.Height, true);

                // Set WS_EX_NOACTIVATE on MainWindow so clicking it does not take focus away from SettingsWindow
                _originalMainWindowExStyle = PInvoke.GetWindowLong(new HWND(mwHwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                PInvoke.SetWindowLong(new HWND(mwHwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, _originalMainWindowExStyle | NativeWindowHelper.WS_EX_NOACTIVATE);

                // Block/intercept preview input events to disable standard clicks on MainWindow without grey-out and without click-through
                mw.PreviewMouseDown += BlockPreviewInput;
                mw.PreviewMouseUp += BlockPreviewInput;
                mw.PreviewTouchDown += BlockPreviewInput;
                mw.PreviewStylusDown += BlockPreviewInput;
            }

            // Create and show fullscreen preview windows:
            // 1) 背景窗口（位于 SettingsWindow 之下、MainWindow 之上）
            //    注册到 WindowTopmostManager：WS_EX_NOACTIVATE 保证不被激活，ZOrder 固定在
            //    MainWindow 之上、SettingsWindow（激活后 ZOrder 增大）之下。
            // 2) 顶层 Overlay 窗口（承载 4 个翻页按钮，浮在 SettingsWindow 之上）
            //    不注册 manager（SettingsWindow 激活会超越其 ZOrder），自行维持 Z 序。
            var previewWin = new PPTPageFlipPreviewWindow();
            previewWin.Show();
            WindowTopmostManager.RegisterWindow(previewWin);

            var overlayWin = new PPTPageFlipPreviewOverlayWindow(settingsWindow);
            overlayWin.Show();

            // Re-activate settings window so it remains in front
            settingsWindow?.Activate();

            _isLoaded = false;
            TabControlPositionSelect.SelectedIndex = 0; // Trigger load for Global tab

            _isLoaded = true;

            LoadSelectedPositionSettings();
            UpdateInlinePreview();
            previewWin.UpdatePreview();

            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            EndPreviewDrag();

            var settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigationViewControl.PaneDisplayMode = _originalPaneDisplayMode;
                settingsWindow.Closed -= SettingsWindow_Closed;
                settingsWindow.Topmost = _originalSettingsWindowTopmost;
            }

            // Restore main window toolbar mode & position & AvoidFullScreenHelper
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                // Restore original styles
                var mwHwnd = new WindowInteropHelper(mw).Handle;
                PInvoke.SetWindowLong(new HWND(mwHwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, _originalMainWindowExStyle);

                mw.IsInPPTPresentationMode = _originalIsInPPTPresentationMode;
                SettingsManager.Settings.Appearance.ToolbarPosition = _originalToolbarPosition;

                // Restore AvoidFullScreenHelper based directly on the user's active setting
                AvoidFullScreenHelper.SetBoardMode(false);
                if (SettingsManager.Settings.Advanced.IsEnableAvoidFullScreenHelper)
                {
                    AvoidFullScreenHelper.StartAvoidFullScreen(mw);
                }

                mw.UpdateToolbarComponentVisibility();
                mw.UpdateToolbarPosition();

                // Restore MainWindow to working area size
                var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                MainWindow.MoveWindow(mwHwnd, workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height, true);

                // Unsubscribe input blocking events
                mw.PreviewMouseDown -= BlockPreviewInput;
                mw.PreviewMouseUp -= BlockPreviewInput;
                mw.PreviewTouchDown -= BlockPreviewInput;
                mw.PreviewStylusDown -= BlockPreviewInput;
            }

            ClosePreviewWindow();
            SaveDraggedPositionSettings();
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            ClosePreviewWindow();
            SaveDraggedPositionSettings();
        }

        /// <summary>
        /// 内嵌预览拖动只更新内存中的偏移字段，离开页面时统一落盘。
        /// </summary>
        private void SaveDraggedPositionSettings()
        {
            if (!_positionSettingsDirty) return;
            _positionSettingsDirty = false;
            SettingsManager.SaveSettingsToFile();
        }

        private void BlockPreviewInput(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ClosePreviewWindow()
        {
            if (PPTPageFlipPreviewOverlayWindow.ActiveInstance != null)
            {
                try
                {
                    PPTPageFlipPreviewOverlayWindow.ActiveInstance.Close();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[UI] 关闭 PPT 翻页预览浮层窗口失败: {ex.Message}", LogHelper.LogType.Info);
                }
            }
            if (PPTPageFlipPreviewWindow.ActiveInstance != null)
            {
                try
                {
                    WindowTopmostManager.UnregisterWindow(PPTPageFlipPreviewWindow.ActiveInstance);
                    PPTPageFlipPreviewWindow.ActiveInstance.Close();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[UI] 注销置顶并关闭 PPT 翻页预览窗口失败: {ex.Message}", LogHelper.LogType.Info);
                }
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateInlinePreview();
        }

        /// <summary>
        /// PreviewCanvas 尺寸变化（FixedAspectRatioPanel 完成排列）后重算 4 个 Border 的 Margin。
        /// </summary>
        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateInlinePreview();
        }

        /// <summary>
        /// 同时刷新左侧内嵌预览与全屏预览窗口。
        /// </summary>
        private void UpdatePreviews()
        {
            UpdateInlinePreview();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void TabControlPositionSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != TabControlPositionSelect) return;
            if (!_isLoaded) return;
            LoadSelectedPositionSettings();
            UpdateInlinePreview();
        }

        private int GetSelectedPositionIndex()
        {
            if (TabControlPositionSelect == null) return -1;
            return TabControlPositionSelect.SelectedIndex - 1; // 0=全局, 1-4=位置(0-3)
        }

        private bool IsGlobalTabSelected => TabControlPositionSelect?.SelectedIndex == 0;

        private void LoadSelectedPositionSettings()
        {
            if (TabControlPositionSelect == null) return;

            bool wasLoaded = _isLoaded;
            _isLoaded = false;
            LoadPositionSettings();
            _isLoaded = wasLoaded;
        }

        /// <summary>
        /// 加载当前标签的有效阴影设置；跟随全局时仅显示全局值。
        /// </summary>
        private void LoadShadowSettings()
        {
            bool wasLoaded = _isLoaded;
            _isLoaded = false;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var shadow = IsGlobalTabSelected
                ? (Enabled: ppt.EnablePPTNavBarShadow, Size: ppt.PPTNavBarShadowSize)
                : ppt.GetPPTNavBarShadow(GetSelectedShadowDirection());
            ToggleSwitchPPTNavBarShadow.IsOn = shadow.Enabled;
            SliderPPTNavBarShadowSize.Value = shadow.Size;
            UpdateSliderText(SliderPPTNavBarShadowSize, TextBlockPPTNavBarShadowSizeValue, "{0:F0}");
            _isLoaded = wasLoaded;
        }

        private void ToggleSwitchPPTNavBarShadow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || !PositionSettingsPanel.IsEnabled) return;
            SaveSelectedShadowSettings();
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTNavBarShadowChanged();
            UpdatePreviews();
        }

        private void SliderPPTNavBarShadowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderPPTNavBarShadowSize, TextBlockPPTNavBarShadowSizeValue, "{0:F0}");
            if (!_isLoaded || !PositionSettingsPanel.IsEnabled) return;
            SaveSelectedShadowSettings();
            SettingsActionHub.OnPPTNavBarShadowChanged();
            UpdatePreviews();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private NavDirection GetSelectedShadowDirection() => GetSelectedPositionIndex() switch
        {
            0 => NavDirection.LeftSide,
            1 => NavDirection.RightSide,
            2 => NavDirection.LeftBottom,
            3 => NavDirection.RightBottom,
            _ => throw new InvalidOperationException("No PPT navigation position is selected.")
        };

        private void SaveSelectedShadowSettings()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (IsGlobalTabSelected)
            {
                ppt.EnablePPTNavBarShadow = ToggleSwitchPPTNavBarShadow.IsOn;
                ppt.PPTNavBarShadowSize = SliderPPTNavBarShadowSize.Value;
            }
            else
            {
                ppt.SetPPTNavBarShadow(GetSelectedShadowDirection(),
                    ToggleSwitchPPTNavBarShadow.IsOn, SliderPPTNavBarShadowSize.Value);
            }
        }

        private void LoadPositionSettings()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            bool isGlobal = IsGlobalTabSelected;
            int selectedIndex = GetSelectedPositionIndex(); // -1 for global, 0-3 for position

            // CardUseGlobalSettings: only visible on position tabs
            CardUseGlobalSettings.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;

            bool useGlobal = false;
            if (!isGlobal)
            {
                useGlobal = GetUseGlobalSettings(selectedIndex, ppt);
                ToggleSwitchUseGlobalSettings.IsOn = useGlobal;
            }

            // PositionSettingsPanel: enabled when global tab, or position tab with UseGlobalSettings off
            PositionSettingsPanel.IsEnabled = isGlobal || !useGlobal;

            // Header
            string posName = isGlobal ? "全局" : GetPositionName(selectedIndex);
            CardPositionEnabled.Header = "启用" + posName + "按钮";

            // 1. Position enabled ToggleSwitch
            if (isGlobal)
            {
                ToggleSwitchPositionEnabled.IsOn = ppt.PPTGlobalButtonEnabled;
            }
            else
            {
                bool effectiveEnabled = useGlobal ? ppt.PPTGlobalButtonEnabled : IsPositionDisplayEnabled(selectedIndex, ppt);
                ToggleSwitchPositionEnabled.IsOn = effectiveEnabled;
            }

            // 2. Show Page Number ToggleSwitch
            ToggleSwitchShowPageNumber.IsOn = isGlobal ? ppt.PPTGlobalShowPageNumber
                : (useGlobal ? ppt.PPTGlobalShowPageNumber : GetPositionShowPageNumber(selectedIndex, ppt));

            // 3. Black Background ToggleSwitch
            ToggleSwitchBlackBackground.IsOn = isGlobal ? ppt.PPTGlobalBlackBackground
                : (useGlobal ? ppt.PPTGlobalBlackBackground : GetPositionBlackBackground(selectedIndex, ppt));

            // 4. Offset Sliders (Side + Bottom)
            //    全局 tab: 两个都显示；侧边位置: 仅 CardOffset；底部位置: 仅 CardOffsetBottom
            bool isSideContext = isGlobal || selectedIndex == 0 || selectedIndex == 1;
            bool isBottomContext = isGlobal || selectedIndex == 2 || selectedIndex == 3;

            CardOffset.Visibility = isSideContext ? Visibility.Visible : Visibility.Collapsed;
            CardOffsetBottom.Visibility = isBottomContext ? Visibility.Visible : Visibility.Collapsed;

            CardOffset.Header = isGlobal ? "偏移（侧边）" : "偏移";
            CardOffsetBottom.Header = isGlobal ? "偏移（底部）" : "偏移";

            SliderOffset.Minimum = -500;
            int sideOffset = isGlobal ? ppt.PPTGlobalSideButtonPosition
                : (useGlobal ? ppt.PPTGlobalSideButtonPosition : GetPositionOffset(selectedIndex, ppt));
            SliderOffset.Value = sideOffset;
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");

            SliderOffsetBottom.Minimum = -100;
            int bottomOffset = isGlobal ? ppt.PPTGlobalBottomButtonPosition
                : (useGlobal ? ppt.PPTGlobalBottomButtonPosition : GetPositionOffset(selectedIndex, ppt));
            SliderOffsetBottom.Value = bottomOffset;
            UpdateSliderText(SliderOffsetBottom, TextBlockOffsetBottomValue, "{0:F0}");

            // 5. Opacity Slider
            double effectiveOpacity = isGlobal ? ppt.PPTGlobalButtonOpacity
                : (useGlobal ? ppt.PPTGlobalButtonOpacity : GetPositionOpacity(selectedIndex, ppt));
            SliderOpacity.Value = effectiveOpacity;
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");

            // 6. Scale Slider
            double effectiveScale = isGlobal ? ppt.PPTNavBarScale
                : (useGlobal ? ppt.PPTNavBarScale : GetPositionScale(selectedIndex, ppt));
            SliderScale.Value = effectiveScale;
            UpdateSliderText(SliderScale, TextBlockScaleValue, "{0:F2}");
            LoadShadowSettings();
        }

        private string GetPositionName(int index)
        {
            switch (index)
            {
                case 0: return "左侧";
                case 1: return "右侧";
                case 2: return "左下";
                case 3: return "右下";
                default: return "自定义";
            }
        }

        private int MapComboIndexToDisplayOptionIndex(int comboIndex)
        {
            switch (comboIndex)
            {
                case 0: return 2; // 左侧 (Left Side) -> display option index 2
                case 1: return 3; // 右侧 (Right Side) -> display option index 3
                case 2: return 0; // 左下 (Left Bottom) -> display option index 0
                case 3: return 1; // 右下 (Right Bottom) -> display option index 1
                default: return 0;
            }
        }

        private bool GetPositionShowPageNumber(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSShowPageNumber;
                case 1: return ppt.PPTRSShowPageNumber;
                case 2: return ppt.PPTLBShowPageNumber;
                case 3: return ppt.PPTRBShowPageNumber;
                default: return true;
            }
        }

        private void SetPositionShowPageNumber(int index, PowerPointSettings ppt, bool val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSShowPageNumber = val; break;
                case 1: ppt.PPTRSShowPageNumber = val; break;
                case 2: ppt.PPTLBShowPageNumber = val; break;
                case 3: ppt.PPTRBShowPageNumber = val; break;
            }
        }

        private bool GetPositionBlackBackground(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSBlackBackground;
                case 1: return ppt.PPTRSBlackBackground;
                case 2: return ppt.PPTLBBlackBackground;
                case 3: return ppt.PPTRBBlackBackground;
                default: return false;
            }
        }

        private void SetPositionBlackBackground(int index, PowerPointSettings ppt, bool val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSBlackBackground = val; break;
                case 1: ppt.PPTRSBlackBackground = val; break;
                case 2: ppt.PPTLBBlackBackground = val; break;
                case 3: ppt.PPTRBBlackBackground = val; break;
            }
        }

        private int GetPositionOffset(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSButtonPosition;
                case 1: return ppt.PPTRSButtonPosition;
                case 2: return ppt.PPTLBButtonPosition;
                case 3: return ppt.PPTRBButtonPosition;
                default: return 0;
            }
        }

        private void SetPositionOffset(int index, PowerPointSettings ppt, int val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSButtonPosition = val; break;
                case 1: ppt.PPTRSButtonPosition = val; break;
                case 2: ppt.PPTLBButtonPosition = val; break;
                case 3: ppt.PPTRBButtonPosition = val; break;
            }
        }

        private double GetPositionOpacity(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSButtonOpacity;
                case 1: return ppt.PPTRSButtonOpacity;
                case 2: return ppt.PPTLBButtonOpacity;
                case 3: return ppt.PPTRBButtonOpacity;
                default: return 0.5;
            }
        }

        private void SetPositionOpacity(int index, PowerPointSettings ppt, double val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSButtonOpacity = val; break;
                case 1: ppt.PPTRSButtonOpacity = val; break;
                case 2: ppt.PPTLBButtonOpacity = val; break;
                case 3: ppt.PPTRBButtonOpacity = val; break;
            }
        }

        private bool GetUseGlobalSettings(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSUseGlobalSettings;
                case 1: return ppt.PPTRSUseGlobalSettings;
                case 2: return ppt.PPTLBUseGlobalSettings;
                case 3: return ppt.PPTRBUseGlobalSettings;
                default: return true;
            }
        }

        private void SetUseGlobalSettings(int index, PowerPointSettings ppt, bool val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSUseGlobalSettings = val; break;
                case 1: ppt.PPTRSUseGlobalSettings = val; break;
                case 2: ppt.PPTLBUseGlobalSettings = val; break;
                case 3: ppt.PPTRBUseGlobalSettings = val; break;
            }
        }

        private bool IsPositionDisplayEnabled(int index, PowerPointSettings ppt)
        {
            string str = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (str.Length < 4) str = "2222";
            int displayIndex = MapComboIndexToDisplayOptionIndex(index);
            return str[displayIndex] == '2';
        }

        private double GetPositionScale(int index, PowerPointSettings ppt)
        {
            switch (index)
            {
                case 0: return ppt.PPTLSButtonScale;
                case 1: return ppt.PPTRSButtonScale;
                case 2: return ppt.PPTLBButtonScale;
                case 3: return ppt.PPTRBButtonScale;
                default: return 1.0;
            }
        }

        private void SetPositionScale(int index, PowerPointSettings ppt, double val)
        {
            switch (index)
            {
                case 0: ppt.PPTLSButtonScale = val; break;
                case 1: ppt.PPTRSButtonScale = val; break;
                case 2: ppt.PPTLBButtonScale = val; break;
                case 3: ppt.PPTRBButtonScale = val; break;
            }
        }

        #region Inline Preview

        /// <summary>
        /// 更新左侧 16:9 预览中 4 个翻页按钮的状态（可见性、缩放、偏移、透明度、页码、主题）。
        /// </summary>
        private void UpdateInlinePreview()
        {
            if (PreviewCanvas == null || PreviewLS == null) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;

            // 有效值：位置 i 若 UseGlobalSettings=true，则采用全局字段值，否则采用位置自身字段值
            double lsScale = ppt.PPTLSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLSButtonScale;
            double rsScale = ppt.PPTRSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRSButtonScale;
            double lbScale = ppt.PPTLBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLBButtonScale;
            double rbScale = ppt.PPTRBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRBButtonScale;

            double lsOpacity = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLSButtonOpacity;
            double rsOpacity = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRSButtonOpacity;
            double lbOpacity = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLBButtonOpacity;
            double rbOpacity = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRBButtonOpacity;

            bool lsShowPage = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTLSShowPageNumber;
            bool rsShowPage = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTRSShowPageNumber;
            bool lbShowPage = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTLBShowPageNumber;
            bool rbShowPage = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTRBShowPageNumber;

            bool lsBlackBg = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTLSBlackBackground;
            bool rsBlackBg = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTRSBlackBackground;
            bool lbBlackBg = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTLBBlackBackground;
            bool rbBlackBg = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTRBBlackBackground;

            // 1. Set scale for all 4 bars
            PreviewLS.SetBarScale(lsScale);
            PreviewRS.SetBarScale(rsScale);
            PreviewLB.SetBarScale(lbScale);
            PreviewRB.SetBarScale(rbScale);

            // 有效偏移：位置 i 若 UseGlobalSettings=true，则采用全局字段值，否则采用位置自身字段值。
            // 侧边按钮只应用垂直偏移，底部按钮只应用水平偏移（与运行时 PPTUIManager 折算一致）。
            int lsOffset = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTLSButtonPosition;
            int rsOffset = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTRSButtonPosition;
            int lbOffset = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTLBButtonPosition;
            int rbOffset = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTRBButtonPosition;

            // 2. Set margins — 基础边距按预览宽度比例缩放，位置偏移按 1920 设计宽度折算
            double viewScale = (PreviewCanvas.ActualWidth > 0) ? PreviewCanvas.ActualWidth / 1600.0 : 1.0;
            double offsetScale = (PreviewCanvas.ActualWidth > 0) ? PreviewCanvas.ActualWidth / PreviewDesignWidth : 0.0;

            PreviewLSBorder.Margin = new Thickness(6 * viewScale, 0, 0, lsOffset * 2 * offsetScale);
            PreviewRSBorder.Margin = new Thickness(0, 0, 6 * viewScale, rsOffset * 2 * offsetScale);
            PreviewLBBorder.Margin = new Thickness(6 * viewScale + lbOffset * offsetScale, 0, 0, 6 * viewScale);
            PreviewRBBorder.Margin = new Thickness(0, 0, 6 * viewScale + rbOffset * offsetScale, 6 * viewScale);

            // 3. Set enabled/disabled visibility (UseGlobalSettings 的位由 PPTGlobalButtonEnabled 决定)
            string displayOption = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (displayOption.Length < 4) displayOption = "2222";
            char[] c = displayOption.ToCharArray();
            // LeftBottom = [0], RightBottom = [1], LeftSide = [2], RightSide = [3]
            if (ppt.PPTLBUseGlobalSettings) c[0] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTRBUseGlobalSettings) c[1] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTLSUseGlobalSettings) c[2] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTRSUseGlobalSettings) c[3] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            PreviewLBBorder.Visibility = c[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
            PreviewRBBorder.Visibility = c[1] == '2' ? Visibility.Visible : Visibility.Collapsed;
            PreviewLSBorder.Visibility = c[2] == '2' ? Visibility.Visible : Visibility.Collapsed;
            PreviewRSBorder.Visibility = c[3] == '2' ? Visibility.Visible : Visibility.Collapsed;

            // 4. Set page button visibility (Show Page Number)
            PreviewLS.SetPageButtonVisibility(lsShowPage ? Visibility.Visible : Visibility.Collapsed);
            PreviewRS.SetPageButtonVisibility(rsShowPage ? Visibility.Visible : Visibility.Collapsed);
            PreviewLB.SetPageButtonVisibility(lbShowPage ? Visibility.Visible : Visibility.Collapsed);
            PreviewRB.SetPageButtonVisibility(rbShowPage ? Visibility.Visible : Visibility.Collapsed);

            // 5. Set opacity
            PreviewLS.SetBarOpacity(lsOpacity);
            PreviewRS.SetBarOpacity(rsOpacity);
            PreviewLB.SetBarOpacity(lbOpacity);
            PreviewRB.SetBarOpacity(rbOpacity);

            // 6. Set theme (Black Background)
            PreviewLS.ApplyTheme(lsBlackBg);
            PreviewRS.ApplyTheme(rsBlackBg);
            PreviewLB.ApplyTheme(lbBlackBg);
            PreviewRB.ApplyTheme(rbBlackBg);

            // 7. Apply shadow (Issue #569)
            PreviewLS.ApplyShadowSettings(ppt);
            PreviewRS.ApplyShadowSettings(ppt);
            PreviewLB.ApplyShadowSettings(ppt);
            PreviewRB.ApplyShadowSettings(ppt);
        }

        private void PreviewLS_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => BeginMouseDrag(NavDirection.LeftSide, sender, e);

        private void PreviewRS_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => BeginMouseDrag(NavDirection.RightSide, sender, e);

        private void PreviewLB_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => BeginMouseDrag(NavDirection.LeftBottom, sender, e);

        private void PreviewRB_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => BeginMouseDrag(NavDirection.RightBottom, sender, e);

        private void BeginMouseDrag(NavDirection direction, object sender, MouseButtonEventArgs e)
        {
            if (!BeginPreviewDrag(direction, e.GetPosition(PreviewCanvas))) return;
            _dragMouseCapture = (UIElement)sender;
            _dragMouseCapture.CaptureMouse();
            e.Handled = true;
        }

        private void PreviewButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragDirection == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndPreviewDrag();
                return;
            }

            ProcessPreviewDrag(e.GetPosition(PreviewCanvas));
            e.Handled = true;
        }

        private void PreviewButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragDirection == null) return;
            var direction = _dragDirection.Value;
            bool wasDragging = _isDragging;
            EndPreviewDrag();

            // 未发生拖动时保留原有点击切换标签的行为（从全局页点击则切到对应位置页）
            if (!wasDragging && IsGlobalTabSelected)
                TabControlPositionSelect.SelectedIndex = GetDirectionIndex(direction) + 1;

            e.Handled = true;
        }

        private void PreviewButton_TouchDown(object sender, TouchEventArgs e)
        {
            if (!BeginPreviewDrag(GetDirectionFromSender(sender), e.GetTouchPoint(PreviewCanvas).Position)) return;
            _dragTouchCapture = (UIElement)sender;
            _dragTouchDevice = e.TouchDevice;
            _dragTouchCapture.CaptureTouch(e.TouchDevice);
            e.Handled = true;
        }

        private void PreviewButton_TouchMove(object sender, TouchEventArgs e)
        {
            if (_dragDirection == null || e.TouchDevice != _dragTouchDevice) return;
            ProcessPreviewDrag(e.GetTouchPoint(PreviewCanvas).Position);
            e.Handled = true;
        }

        private void PreviewButton_TouchUp(object sender, TouchEventArgs e)
        {
            if (_dragDirection == null || e.TouchDevice != _dragTouchDevice) return;
            var direction = _dragDirection.Value;
            bool wasDragging = _isDragging;
            EndPreviewDrag();

            if (!wasDragging && IsGlobalTabSelected)
                TabControlPositionSelect.SelectedIndex = GetDirectionIndex(direction) + 1;

            e.Handled = true;
        }

        /// <summary>
        /// 开始拖动：需要写入哪个偏移字段由该位置的「使用全局设置」决定；
        /// 若当前在全局页但目标位置使用独立设置，则切到该位置页以正确显示滑块。
        /// </summary>
        private bool BeginPreviewDrag(NavDirection direction, Point position)
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            int index = GetDirectionIndex(direction);

            if (!IsGlobalTabSelected && TabControlPositionSelect.SelectedIndex != index + 1)
            {
                TabControlPositionSelect.SelectedIndex = index + 1;
            }

            bool useGlobal = GetUseGlobalSettings(index, ppt);
            if (IsGlobalTabSelected && !useGlobal)
            {
                TabControlPositionSelect.SelectedIndex = index + 1;
            }

            _dragDirection = direction;
            _dragStartPoint = position;
            _dragStartOffset = useGlobal
                ? (IsSideDirection(direction) ? ppt.PPTGlobalSideButtonPosition : ppt.PPTGlobalBottomButtonPosition)
                : GetPositionOffset(index, ppt);
            _dragWritesGlobal = useGlobal;
            _isDragging = false;
            return true;
        }

        private void ProcessPreviewDrag(Point position)
        {
            if (_dragDirection == null) return;

            double dx = position.X - _dragStartPoint.X;
            double dy = _dragStartPoint.Y - position.Y;

            if (!_isDragging)
            {
                if (Math.Abs(dx) < DragStartThreshold && Math.Abs(dy) < DragStartThreshold) return;
                _isDragging = true;
            }

            double offsetScale = PreviewCanvas.ActualWidth > 0
                ? PreviewCanvas.ActualWidth / PreviewDesignWidth
                : 0.0;
            if (offsetScale <= 0) return;

            int offset;
            switch (_dragDirection.Value)
            {
                case NavDirection.LeftSide:
                case NavDirection.RightSide:
                    // 侧边按钮只取垂直位移；运行时的 Margin 变化量是偏移量的 2 倍
                    offset = _dragStartOffset + (int)Math.Round(dy / offsetScale / 2.0);
                    break;
                case NavDirection.LeftBottom:
                    offset = _dragStartOffset + (int)Math.Round(dx / offsetScale);
                    break;
                case NavDirection.RightBottom:
                    // 右下按钮的偏移增加表示向左移动
                    offset = _dragStartOffset + (int)Math.Round(-dx / offsetScale);
                    break;
                default:
                    return;
            }

            ApplyDraggedOffset(_dragDirection.Value, NormalizeDraggedOffset(_dragDirection.Value, offset));
        }

        private void ApplyDraggedOffset(NavDirection direction, int offset)
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            int index = GetDirectionIndex(direction);
            bool isSide = IsSideDirection(direction);

            if (_dragWritesGlobal)
            {
                if (isSide) ppt.PPTGlobalSideButtonPosition = offset;
                else ppt.PPTGlobalBottomButtonPosition = offset;
            }
            else
            {
                SetPositionOffset(index, ppt, offset);
            }

            // 同步滑块显示，但跳过滑块自身的保存/刷新逻辑，避免递归
            _isSyncingDrag = true;
            try
            {
                if (isSide) SliderOffset.Value = offset;
                else SliderOffsetBottom.Value = offset;
            }
            finally
            {
                _isSyncingDrag = false;
            }

            _positionSettingsDirty = true;

            if (_dragWritesGlobal) SettingsActionHub.OnPPTGlobalSettingsChanged();
            else SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreviews();
        }

        private void EndPreviewDrag()
        {
            _dragDirection = null;
            _isDragging = false;

            if (_dragMouseCapture != null)
            {
                if (Mouse.Captured == _dragMouseCapture) _dragMouseCapture.ReleaseMouseCapture();
                _dragMouseCapture = null;
            }

            if (_dragTouchCapture != null && _dragTouchDevice != null)
            {
                _dragTouchCapture.ReleaseTouchCapture(_dragTouchDevice);
                _dragTouchCapture = null;
                _dragTouchDevice = null;
            }
        }

        private static int NormalizeDraggedOffset(NavDirection direction, int offset)
        {
            if (Math.Abs(offset) <= SnapToZeroThreshold) return 0;
            return IsSideDirection(direction)
                ? Math.Clamp(offset, SideOffsetMin, SideOffsetMax)
                : Math.Clamp(offset, BottomOffsetMin, BottomOffsetMax);
        }

        private static int GetDirectionIndex(NavDirection direction)
        {
            switch (direction)
            {
                case NavDirection.LeftSide: return 0;
                case NavDirection.RightSide: return 1;
                case NavDirection.LeftBottom: return 2;
                case NavDirection.RightBottom: return 3;
                default: return 0;
            }
        }

        private static bool IsSideDirection(NavDirection direction)
            => direction == NavDirection.LeftSide || direction == NavDirection.RightSide;

        private NavDirection GetDirectionFromSender(object sender)
        {
            if (ReferenceEquals(sender, PreviewRSBorder)) return NavDirection.RightSide;
            if (ReferenceEquals(sender, PreviewLBBorder)) return NavDirection.LeftBottom;
            if (ReferenceEquals(sender, PreviewRBBorder)) return NavDirection.RightBottom;
            return NavDirection.LeftSide;
        }

        #endregion

        private void ToggleSwitchPositionEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalButtonEnabled = ToggleSwitchPositionEnabled.IsOn;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            int displayIndex = MapComboIndexToDisplayOptionIndex(selectedIndex);

            string str = ppt.PPTButtonsDisplayOption.ToString("D4");
            char[] c = str.ToCharArray();
            c[displayIndex] = ToggleSwitchPositionEnabled.IsOn ? '2' : '1';

            ppt.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();

            // Notify other managers and preview
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            UpdatePreviews();
        }

        private void ToggleSwitchShowPageNumber_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalShowPageNumber = ToggleSwitchShowPageNumber.IsOn;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            bool isOn = ToggleSwitchShowPageNumber.IsOn;

            SetPositionShowPageNumber(selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();

            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreviews();
        }

        private void ToggleSwitchBlackBackground_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalBlackBackground = ToggleSwitchBlackBackground.IsOn;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            bool isOn = ToggleSwitchBlackBackground.IsOn;

            SetPositionBlackBackground(selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();

            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreviews();
        }

        private void ToggleSwitchUseGlobalSettings_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();

            SetUseGlobalSettings(selectedIndex, ppt, ToggleSwitchUseGlobalSettings.IsOn);
            SettingsManager.SaveSettingsToFile();

            // Reload UI to apply IsEnabled state and effective values
            bool wasLoaded = _isLoaded;
            _isLoaded = false;
            LoadPositionSettings();
            _isLoaded = wasLoaded;

            // Notify runtime + preview (effective values may have changed)
            SettingsActionHub.OnPPTGlobalSettingsChanged();
            UpdatePreviews();
        }

        private void SliderOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");
            if (_isSyncingDrag) return;
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int offsetVal = (int)SliderOffset.Value;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalSideButtonPosition = offsetVal;
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            SetPositionOffset(selectedIndex, ppt, offsetVal);
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreviews();

            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void ButtonResetOffset_Click(object sender, RoutedEventArgs e)
        {
            SliderOffset.Value = 0;
        }

        private void SliderOffsetBottom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOffsetBottom, TextBlockOffsetBottomValue, "{0:F0}");
            if (_isSyncingDrag) return;
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int offsetVal = (int)SliderOffsetBottom.Value;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalBottomButtonPosition = offsetVal;
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            SetPositionOffset(selectedIndex, ppt, offsetVal);
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreviews();

            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void ButtonResetOffsetBottom_Click(object sender, RoutedEventArgs e)
        {
            SliderOffsetBottom.Value = 0;
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            double roundedValue = Math.Round(SliderOpacity.Value, 1);

            SliderOpacity.ValueChanged -= SliderOpacity_ValueChanged;
            SliderOpacity.Value = roundedValue;
            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;

            if (IsGlobalTabSelected)
            {
                ppt.PPTGlobalButtonOpacity = roundedValue;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTGlobalSettingsChanged();
                UpdatePreviews();
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            SetPositionOpacity(selectedIndex, ppt, roundedValue);

            string buttonKey = "";
            switch (selectedIndex)
            {
                case 0: buttonKey = "LS"; break;
                case 1: buttonKey = "RS"; break;
                case 2: buttonKey = "LB"; break;
                case 3: buttonKey = "RB"; break;
            }

            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged(buttonKey, roundedValue);
            UpdatePreviews();
        }

        private void ButtonResetOpacity_Click(object sender, RoutedEventArgs e)
        {
            SliderOpacity.Value = 0.5;
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderScale, TextBlockScaleValue, "{0:F2}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            double roundedValue = Math.Round(SliderScale.Value, 2);

            SliderScale.ValueChanged -= SliderScale_ValueChanged;
            SliderScale.Value = roundedValue;
            SliderScale.ValueChanged += SliderScale_ValueChanged;

            if (IsGlobalTabSelected)
            {
                ppt.PPTNavBarScale = roundedValue;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTNavBarScaleChanged(roundedValue);
                UpdatePreviews();
                return;
            }

            int selectedIndex = GetSelectedPositionIndex();
            SetPositionScale(selectedIndex, ppt, roundedValue);
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTGlobalSettingsChanged();
            UpdatePreviews();
        }
    }
}
