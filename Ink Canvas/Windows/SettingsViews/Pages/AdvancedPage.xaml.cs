using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AdvancedPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public AdvancedPage()
        {
            InitializeComponent();
            Loaded += AdvancedPage_Loaded;
        }

        private void AdvancedPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Advanced != null)
                {
                    CardIsSpecialScreen.IsOn = settings.Advanced.IsSpecialScreen;
                    CardIsEnableUriScheme.IsOn = settings.Advanced.IsEnableUriScheme;
                    CardIsQuadIR.IsOn = settings.Advanced.IsQuadIR;
                    CardEraserBindTouchMultiplier.IsOn = settings.Advanced.EraserBindTouchMultiplier;
                    CardIsEnableFullScreenHelper.IsOn = settings.Advanced.IsEnableFullScreenHelper;
                    CardIsEnableAvoidFullScreenHelper.IsOn = settings.Advanced.IsEnableAvoidFullScreenHelper;
                    CardIsEnableEdgeGestureUtil.IsOn = settings.Advanced.IsEnableEdgeGestureUtil;
                    CardIsEnableForceFullScreen.IsOn = settings.Advanced.IsEnableForceFullScreen;
                    CardIsEnableDPIChangeDetection.IsOn = settings.Advanced.IsEnableDPIChangeDetection;
                    CardIsEnableResolutionChangeDetection.IsOn = settings.Advanced.IsEnableResolutionChangeDetection;
                    CardIsLogEnabled.IsOn = settings.Advanced.IsLogEnabled;
                    CardIsSaveLogByDate.IsOn = settings.Advanced.IsSaveLogByDate;
                    CardIsSecondConfirmWhenShutdownApp.IsOn = settings.Advanced.IsSecondConfirmWhenShutdownApp;
                    CardIsAutoBackupBeforeUpdate.IsOn = settings.Advanced.IsAutoBackupBeforeUpdate;
                    CardIsAutoBackupEnabled.IsOn = settings.Advanced.IsAutoBackupEnabled;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载高级设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ToggleSwitchIsSpecialScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSpecialScreen", CardIsSpecialScreen.IsOn);
        }

        private void ToggleSwitchIsEnableUriScheme_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableUriScheme", CardIsEnableUriScheme.IsOn);
        }

        private void ToggleSwitchIsQuadIR_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsQuadIR", CardIsQuadIR.IsOn);
        }

        private void ToggleSwitchEraserBindTouchMultiplier_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEraserBindTouchMultiplier", CardEraserBindTouchMultiplier.IsOn);
        }

        private void ToggleSwitchIsEnableFullScreenHelper_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableFullScreenHelper", CardIsEnableFullScreenHelper.IsOn);
        }

        private void ToggleSwitchIsEnableAvoidFullScreenHelper_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableAvoidFullScreenHelper", CardIsEnableAvoidFullScreenHelper.IsOn);
        }

        private void ToggleSwitchIsEnableEdgeGestureUtil_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableEdgeGestureUtil", CardIsEnableEdgeGestureUtil.IsOn);
        }

        private void ToggleSwitchIsEnableForceFullScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableForceFullScreen", CardIsEnableForceFullScreen.IsOn);
        }

        private void ToggleSwitchIsEnableDPIChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableDPIChangeDetection", CardIsEnableDPIChangeDetection.IsOn);
        }

        private void ToggleSwitchIsEnableResolutionChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableResolutionChangeDetection", CardIsEnableResolutionChangeDetection.IsOn);
        }

        private void ToggleSwitchIsLogEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsLogEnabled", CardIsLogEnabled.IsOn);
        }

        private void ToggleSwitchIsSaveLogByDate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSaveLogByDate", CardIsSaveLogByDate.IsOn);
        }

        private void ToggleSwitchIsSecondConfirmWhenShutdownApp_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSecondConfimeWhenShutdownApp", CardIsSecondConfirmWhenShutdownApp.IsOn);
        }

        private void ToggleSwitchIsAutoBackupBeforeUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoBackupBeforeUpdate", CardIsAutoBackupBeforeUpdate.IsOn);
        }

        private void ToggleSwitchIsAutoBackupEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoBackupEnabled", CardIsAutoBackupEnabled.IsOn);
        }
    }
}
