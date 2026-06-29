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
        private int _lastSelectedIndex = -1;

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
            if (TabControlPositionSelect == null || contentFramePositionSelect == null) return;
            
            int selectedIndex = GetSelectedPositionIndex();
            if (selectedIndex < 0) selectedIndex = 0;
            
            var transitionInfo = new SlideNavigationTransitionInfo();
            if (_lastSelectedIndex != -1 && selectedIndex < _lastSelectedIndex)
            {
                transitionInfo.Effect = SlideNavigationTransitionEffect.FromLeft;
            }
            else
            {
                transitionInfo.Effect = SlideNavigationTransitionEffect.FromRight;
            }
            _lastSelectedIndex = selectedIndex;

            contentFramePositionSelect.Navigate(new PPTPageFlipSettingsSubPage(selectedIndex), null, transitionInfo);
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
