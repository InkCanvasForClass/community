using H.NotifyIcon;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AppearancePage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public AppearancePage()
        {
            InitializeComponent();
            Loaded += AppearancePage_Loaded;
        }

        private void AppearancePage_Loaded(object sender, RoutedEventArgs e)
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
                if (settings.Appearance != null)
                {
                    ComboBoxTheme.SelectedIndex = settings.Appearance.Theme;
                    ComboBoxLanguage.SelectedIndex = int.TryParse(settings.Appearance.Language, out int langIdx) ? langIdx : 0;
                    CardEnableSplashScreen.IsOn = settings.Appearance.EnableSplashScreen;
                    ComboBoxSplashScreenStyle.SelectedIndex = settings.Appearance.SplashScreenStyle;
                    ComboBoxFloatingBarImg.SelectedIndex = settings.Appearance.FloatingBarImg;
                    ViewboxFloatingBarScaleTransformValueSlider.Value = settings.Appearance.ViewboxFloatingBarScaleTransformValue;
                    ViewboxFloatingBarOpacityValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityValue;
                    ViewboxFloatingBarOpacityInPPTValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;
                    CardEnableDisPlayNibModeToggle.IsOn = settings.Appearance.IsEnableDisPlayNibModeToggler;
                    CardEnableViewboxBlackBoardScaleTransform.IsOn = settings.Appearance.EnableViewboxBlackBoardScaleTransform;
                    CardEnableTimeDisplayInWhiteboardMode.IsOn = settings.Appearance.EnableTimeDisplayInWhiteboardMode;
                    CardEnableChickenSoupInWhiteboardMode.IsOn = settings.Appearance.EnableChickenSoupInWhiteboardMode;
                    ComboBoxChickenSoupSource.SelectedIndex = settings.Appearance.ChickenSoupSource;
                    CardEnableQuickPanel.IsOn = settings.Appearance.IsShowQuickPanel;
                    ComboBoxUnFoldBtnImg.SelectedIndex = settings.Appearance.UnFoldButtonImageType;
                    CardUseLegacyFloatingBarUI.IsOn = settings.Appearance.UseLegacyFloatingBarUI;
                    ToggleSwitchShowShapeButton.IsOn = settings.Appearance.IsShowShapeButton;
                    ToggleSwitchShowUndoButton.IsOn = settings.Appearance.IsShowUndoButton;
                    ToggleSwitchShowRedoButton.IsOn = settings.Appearance.IsShowRedoButton;
                    ToggleSwitchShowClearButton.IsOn = settings.Appearance.IsShowClearButton;
                    ToggleSwitchShowWhiteboardButton.IsOn = settings.Appearance.IsShowWhiteboardButton;
                    ToggleSwitchShowHideButton.IsOn = settings.Appearance.IsShowHideButton;
                    ToggleSwitchShowLassoSelectButton.IsOn = settings.Appearance.IsShowLassoSelectButton;
                    ToggleSwitchShowClearAndMouseButton.IsOn = settings.Appearance.IsShowClearAndMouseButton;
                    ToggleSwitchShowQuickColorPalette.IsOn = settings.Appearance.IsShowQuickColorPalette;
                    ComboBoxQuickColorPaletteDisplayMode.SelectedIndex = settings.Appearance.QuickColorPaletteDisplayMode;
                    ComboBoxEraserDisplayOption.SelectedIndex = settings.Appearance.EraserDisplayOption;
                    CardEnableTrayIcon.IsOn = settings.Appearance.EnableTrayIcon;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载外观设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxTheme", (sender as ComboBox)?.SelectedItem);
        }

        private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.Language = ComboBoxLanguage.SelectedIndex.ToString();
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置语言时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchEnableSplashScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.EnableSplashScreen = CardEnableSplashScreen.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置启动画面时出错: {ex.Message}");
            }
        }

        private void ComboBoxSplashScreenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.SplashScreenStyle = ComboBoxSplashScreenStyle.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置启动画面样式时出错: {ex.Message}");
            }
        }

        private void ComboBoxFloatingBarImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxFloatingBarImg", (sender as ComboBox)?.SelectedItem);
        }

        private void ButtonAddCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            MainWindowSettingsHelper.InvokeMainWindowMethod("ButtonAddCustomIcon_Click", sender, e);
        }

        private void ButtonManageCustomIcons_Click(object sender, RoutedEventArgs e)
        {
            MainWindowSettingsHelper.InvokeMainWindowMethod("ButtonManageCustomIcons_Click", sender, e);
        }

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarScaleTransformValueSlider", e.NewValue);
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarOpacityValueSlider", e.NewValue);
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarOpacityInPPTValueSlider", e.NewValue);
        }

        private void ToggleSwitchEnableDisPlayNibModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableDisPlayNibModeToggle", CardEnableDisPlayNibModeToggle.IsOn);
        }

        private void ToggleSwitchEnableViewboxBlackBoardScaleTransform_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableViewboxBlackBoardScaleTransform", CardEnableViewboxBlackBoardScaleTransform.IsOn);
        }

        private void ToggleSwitchEnableTimeDisplayInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTimeDisplayInWhiteboardMode", CardEnableTimeDisplayInWhiteboardMode.IsOn);
        }

        private void ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableChickenSoupInWhiteboardMode", CardEnableChickenSoupInWhiteboardMode.IsOn);
        }

        private void ComboBoxChickenSoupSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxChickenSoupSource", (sender as ComboBox)?.SelectedItem);
        }

        private void ToggleSwitchEnableQuickPanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowQuickPanel = CardEnableQuickPanel.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置快捷面板时出错: {ex.Message}");
            }
        }

        private void ComboBoxUnFoldBtnImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxUnFoldBtnImg", (sender as ComboBox)?.SelectedItem);
        }

        private void ToggleSwitchUseLegacyFloatingBarUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("CheckBoxUseLegacyFloatingBarUI", CardUseLegacyFloatingBarUI.IsOn);
        }

        private void ToggleSwitchShowShapeButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowShapeButton = ToggleSwitchShowShapeButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置形状按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowUndoButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowUndoButton = ToggleSwitchShowUndoButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置撤销按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowRedoButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowRedoButton = ToggleSwitchShowRedoButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置重做按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowClearButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowClearButton = ToggleSwitchShowClearButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置清空按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowWhiteboardButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowWhiteboardButton = ToggleSwitchShowWhiteboardButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置白板按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowHideButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowHideButton = ToggleSwitchShowHideButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置隐藏按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowLassoSelectButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowLassoSelectButton = ToggleSwitchShowLassoSelectButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置套索按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowClearAndMouseButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowClearAndMouseButton = ToggleSwitchShowClearAndMouseButton.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置清并鼠按钮时出错: {ex.Message}"); }
        }

        private void ToggleSwitchShowQuickColorPalette_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.IsShowQuickColorPalette = ToggleSwitchShowQuickColorPalette.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置快捷调色盘时出错: {ex.Message}"); }
        }

        private void ComboBoxQuickColorPaletteDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.QuickColorPaletteDisplayMode = ComboBoxQuickColorPaletteDisplayMode.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex) { Debug.WriteLine($"设置调色盘模式时出错: {ex.Message}"); }
        }

        private void ComboBoxEraserDisplayOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.EraserDisplayOption = ComboBoxEraserDisplayOption.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("UpdateFloatingBarButtonsVisibility");
            }
            catch (Exception ex) { Debug.WriteLine($"设置橡皮擦显示时出错: {ex.Message}"); }
        }

        private void ToggleSwitchEnableTrayIcon_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggledWithThemeCheck("ToggleSwitchEnableTrayIcon", CardEnableTrayIcon.IsOn);
        }
    }
}
