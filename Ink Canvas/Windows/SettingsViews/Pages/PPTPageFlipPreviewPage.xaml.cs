using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using NavigationViewPaneDisplayMode = iNKORE.UI.WPF.Modern.Controls.NavigationViewPaneDisplayMode;
using NavigationView = iNKORE.UI.WPF.Modern.Controls.NavigationView;
using NavigationViewItem = iNKORE.UI.WPF.Modern.Controls.NavigationViewItem;
using NavigationViewSelectionChangedEventArgs = iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs;
using SlideNavigationTransitionInfo = iNKORE.UI.WPF.Modern.Media.Animation.SlideNavigationTransitionInfo;
using SlideNavigationTransitionEffect = iNKORE.UI.WPF.Modern.Media.Animation.SlideNavigationTransitionEffect;

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

        public PPTPageFlipPreviewPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
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
                _originalMainWindowExStyle = NativeWindowHelper.GetWindowLong(mwHwnd, NativeWindowHelper.GWL_EXSTYLE);
                NativeWindowHelper.SetWindowLong(mwHwnd, NativeWindowHelper.GWL_EXSTYLE, _originalMainWindowExStyle | NativeWindowHelper.WS_EX_NOACTIVATE);

                // Block/intercept preview input events to disable standard clicks on MainWindow without grey-out and without click-through
                mw.PreviewMouseDown += BlockPreviewInput;
                mw.PreviewMouseUp += BlockPreviewInput;
                mw.PreviewTouchDown += BlockPreviewInput;
                mw.PreviewStylusDown += BlockPreviewInput;
            }

            // Create and show preview window
            var previewWin = new PPTPageFlipPreviewWindow();
            previewWin.Show();
            
            // Re-activate settings window so it remains in front
            settingsWindow?.Activate();

            _isLoaded = false;
            TabControlPositionSelect.SelectedIndex = 0; // Trigger load for Left Side (LS)
            
            var ppt = SettingsManager.Settings.PowerPointSettings;
            SliderScale.Value = ppt.PPTNavBarScale;
            UpdateSliderText(SliderScale, TextBlockScaleValue, "{0:F2}");

            _isLoaded = true;
            
            LoadSelectedPositionSettings();
            previewWin.UpdatePreview();
            
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
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
                NativeWindowHelper.SetWindowLong(mwHwnd, NativeWindowHelper.GWL_EXSTYLE, _originalMainWindowExStyle);

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
        }

        private void SettingsWindow_Closed(object sender, EventArgs e)
        {
            ClosePreviewWindow();
        }

        private void BlockPreviewInput(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ClosePreviewWindow()
        {
            if (PPTPageFlipPreviewWindow.ActiveInstance != null)
            {
                try
                {
                    PPTPageFlipPreviewWindow.ActiveInstance.Close();
                }
                catch { }
            }
        }

        private void TabControlPositionSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != TabControlPositionSelect) return;
            if (!_isLoaded) return;
            LoadSelectedPositionSettings();
        }

        private int GetSelectedPositionIndex()
        {
            if (TabControlPositionSelect == null) return 0;
            return TabControlPositionSelect.SelectedIndex;
        }

        private void LoadSelectedPositionSettings()
        {
            if (TabControlPositionSelect == null) return;

            bool wasLoaded = _isLoaded;
            _isLoaded = false;
            LoadPositionSettings();
            _isLoaded = wasLoaded;
        }

        private void LoadPositionSettings()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            string posName = GetPositionName(selectedIndex);
            CardPositionEnabled.Header = "启用" + posName + "按钮";

            // 1. Position enabled ToggleSwitch
            string displayOptionStr = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (displayOptionStr.Length < 4) displayOptionStr = "2222";
            int displayIndex = MapComboIndexToDisplayOptionIndex(selectedIndex);
            ToggleSwitchPositionEnabled.IsOn = displayOptionStr[displayIndex] == '2';

            // 2. Show Page Number ToggleSwitch
            ToggleSwitchShowPageNumber.IsOn = GetPositionShowPageNumber(selectedIndex, ppt);

            // 3. Black Background ToggleSwitch
            ToggleSwitchBlackBackground.IsOn = GetPositionBlackBackground(selectedIndex, ppt);

            // 4. Offset Slider (Adjust range dynamically: Side = -500 to 500, Bottom = -100 to 500)
            if (selectedIndex == 0 || selectedIndex == 1) // Side (左侧 / 右侧)
            {
                SliderOffset.Minimum = -500;
            }
            else // Bottom (左下 / 右下)
            {
                SliderOffset.Minimum = -100;
            }
            SliderOffset.Value = GetPositionOffset(selectedIndex, ppt);
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");

            // 5. Opacity Slider
            SliderOpacity.Value = GetPositionOpacity(selectedIndex, ppt);
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");
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

        private void ToggleSwitchPositionEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            int displayIndex = MapComboIndexToDisplayOptionIndex(selectedIndex);

            string str = ppt.PPTButtonsDisplayOption.ToString("D4");
            char[] c = str.ToCharArray();
            c[displayIndex] = ToggleSwitchPositionEnabled.IsOn ? '2' : '1';

            ppt.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();

            // Notify other managers and preview window
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void ToggleSwitchShowPageNumber_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            bool isOn = ToggleSwitchShowPageNumber.IsOn;

            SetPositionShowPageNumber(selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();

            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void ToggleSwitchBlackBackground_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            bool isOn = ToggleSwitchBlackBackground.IsOn;

            SetPositionBlackBackground(selectedIndex, ppt, isOn);
            SettingsManager.SaveSettingsToFile();

            // Trigger UI and preview refresh
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }

        private void SliderOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOffset, TextBlockOffsetValue, "{0:F0}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            int offsetVal = (int)SliderOffset.Value;

            SetPositionOffset(selectedIndex, ppt, offsetVal);
            SettingsActionHub.OnPPTButtonPositionChanged();
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();

            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void ButtonResetOffset_Click(object sender, RoutedEventArgs e)
        {
            SliderOffset.Value = 0;
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(SliderOpacity, TextBlockOpacityValue, "{0:P0}");
            if (!_isLoaded) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            int selectedIndex = GetSelectedPositionIndex();
            double roundedValue = Math.Round(SliderOpacity.Value, 1);

            SliderOpacity.ValueChanged -= SliderOpacity_ValueChanged;
            SliderOpacity.Value = roundedValue;
            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;

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
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
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
            
            ppt.PPTNavBarScale = roundedValue;
            SettingsManager.SaveSettingsToFile();
            
            SettingsActionHub.OnPPTNavBarScaleChanged(roundedValue);
            PPTPageFlipPreviewWindow.ActiveInstance?.UpdatePreview();
        }
    }
}
