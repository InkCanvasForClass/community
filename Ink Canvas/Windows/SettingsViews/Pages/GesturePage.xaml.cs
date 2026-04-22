using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class GesturePage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public GesturePage()
        {
            InitializeComponent();
            Loaded += GesturePage_Loaded;
        }

        private void GesturePage_Loaded(object sender, RoutedEventArgs e)
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
                if (settings.Gesture != null)
                {
                    CardAutoSwitchTwoFingerGesture.IsOn = settings.Gesture.AutoSwitchTwoFingerGesture;
                    CardEnableTwoFingerRotationOnSelection.IsOn = settings.Gesture.IsEnableTwoFingerRotationOnSelection;
                }
                if (settings.Canvas != null)
                {
                    CardEnablePalmEraser.IsOn = settings.Canvas.EnablePalmEraser;
                    ComboBoxPalmEraserSensitivity.SelectedIndex = settings.Canvas.PalmEraserSensitivity;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载手势设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ToggleSwitchAutoSwitchTwoFingerGesture_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSwitchTwoFingerGesture", CardAutoSwitchTwoFingerGesture.IsOn);
        }

        private void ToggleSwitchEnableTwoFingerRotationOnSelection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTwoFingerRotationOnSelection", CardEnableTwoFingerRotationOnSelection.IsOn);
        }

        private void ToggleSwitchEnablePalmEraser_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePalmEraser", CardEnablePalmEraser.IsOn);
        }

        private void ComboBoxPalmEraserSensitivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Canvas.PalmEraserSensitivity = ComboBoxPalmEraserSensitivity.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置掌擦灵敏度时出错: {ex.Message}");
            }
        }
    }
}
