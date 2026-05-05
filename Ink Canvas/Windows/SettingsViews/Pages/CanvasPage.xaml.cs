using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class CanvasPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public CanvasPage()
        {
            InitializeComponent();
            Loaded += CanvasPage_Loaded;
        }

        private void CanvasPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(InkFadeTimeSlider, InkFadeTimeText, "{0:0}ms");
            UpdateSliderText(BrushAutoRestoreWidthSlider, BrushAutoRestoreWidthText, "{0:F2}");
            UpdateSliderText(BrushAutoRestoreAlphaSlider, BrushAutoRestoreAlphaText, "{0:0}");
            UpdateSliderText(EraserAutoSwitchBackDelaySlider, EraserAutoSwitchBackDelayText, "{0:0}秒");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Canvas != null)
                {
                    CardShowCursor.IsOn = settings.Canvas.IsShowCursor;
                    CardEnablePressureTouchMode.IsOn = settings.Canvas.EnablePressureTouchMode;
                    CardDisablePressure.IsOn = settings.Canvas.DisablePressure;
                    ComboBoxEraserSize.SelectedIndex = settings.Canvas.EraserSize;
                    CardHideStrokeWhenSelecting.IsOn = settings.Canvas.HideStrokeWhenSelecting;
                    CardClearCanvasAndClearTimeMachine.IsOn = settings.Canvas.ClearCanvasAndClearTimeMachine;
                    CardClearCanvasAlsoClearImages.IsOn = settings.Canvas.ClearCanvasAlsoClearImages;
                    CardCompressPicturesUploaded.IsOn = settings.Canvas.IsCompressPicturesUploaded;
                    CardLaunchSeewoVideoShowcaseForWhiteboardBooth.IsOn = settings.Canvas.LaunchSeewoVideoShowcaseForWhiteboardBooth;
                    ComboBoxHyperbolaAsymptoteOption.SelectedIndex = (int)settings.Canvas.HyperbolaAsymptoteOption;
                    CardShowCircleCenter.IsOn = settings.Canvas.ShowCircleCenter;
                    int curveMode = 0;
                    if (settings.Canvas.UseAdvancedBezierSmoothing) curveMode = 2;
                    else if (settings.Canvas.FitToCurve) curveMode = 1;
                    ComboBoxCurveSmoothingMode.SelectedIndex = curveMode;
                    ToggleSwitchEnableInkFade.IsOn = settings.Canvas.EnableInkFade;
                    InkFadeTimeSlider.Value = settings.Canvas.InkFadeTime;
                    CardHideInkFadeControlInPenMenu.IsOn = settings.Canvas.HideInkFadeControlInPenMenu;
                    ToggleSwitchBrushAutoRestore.IsOn = settings.Canvas.EnableBrushAutoRestore;
                    BrushAutoRestoreTimesTextBox.Text = settings.Canvas.BrushAutoRestoreTimes ?? string.Empty;
                    LoadBrushAutoRestoreColor(settings.Canvas.BrushAutoRestoreColor);
                    BrushAutoRestoreWidthSlider.Value = settings.Canvas.BrushAutoRestoreWidth > 0 ? settings.Canvas.BrushAutoRestoreWidth : 5;
                    BrushAutoRestoreAlphaSlider.Value = settings.Canvas.BrushAutoRestoreAlpha;
                    ToggleSwitchEnableEraserAutoSwitchBack.IsOn = settings.Canvas.EnableEraserAutoSwitchBack;
                    EraserAutoSwitchBackDelaySlider.Value = settings.Canvas.EraserAutoSwitchBackDelaySeconds;
                }

                if (settings.Gesture != null)
                {
                    CardEnableTwoFingerRotationOnSelection.IsOn = settings.Gesture.IsEnableTwoFingerRotationOnSelection;
                }

                if (settings.Canvas != null)
                {
                    ToggleSwitchEnablePalmEraser.IsOn = settings.Canvas.EnablePalmEraser;
                    ComboBoxPalmEraserSensitivity.SelectedIndex = settings.Canvas.PalmEraserSensitivity;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载画板设置时出错: {ex.Message}");
            }

            _isLoaded = true;

            ExpanderEnableInkFade.IsExpanded = ToggleSwitchEnableInkFade.IsOn;
            ExpanderBrushAutoRestore.IsExpanded = ToggleSwitchBrushAutoRestore.IsOn;
            ExpanderEnableEraserAutoSwitchBack.IsExpanded = ToggleSwitchEnableEraserAutoSwitchBack.IsOn;
            ExpanderEnablePalmEraser.IsExpanded = ToggleSwitchEnablePalmEraser.IsOn;
        }

        private void LoadBrushAutoRestoreColor(string hex)
        {
            try
            {
                foreach (var item in ComboBoxBrushAutoRestoreColor.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Tag != null &&
                        string.Equals(cbi.Tag.ToString(), hex, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboBoxBrushAutoRestoreColor.SelectedItem = cbi;
                        return;
                    }
                }
                ComboBoxBrushAutoRestoreColor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载画笔恢复颜色时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchShowCursor_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.IsShowCursor = CardShowCursor.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePressureTouchMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnablePressureTouchMode = CardEnablePressureTouchMode.IsOn;
            if (SettingsManager.Settings.Canvas.EnablePressureTouchMode && SettingsManager.Settings.Canvas.DisablePressure)
            {
                SettingsManager.Settings.Canvas.DisablePressure = false;
                CardDisablePressure.IsOn = false;
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw != null && mw.inkCanvas != null)
                    mw.inkCanvas.DefaultDrawingAttributes.IgnorePressure = false;
            }
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchDisablePressure_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.DisablePressure = CardDisablePressure.IsOn;
            if (SettingsManager.Settings.Canvas.DisablePressure && SettingsManager.Settings.Canvas.EnablePressureTouchMode)
            {
                SettingsManager.Settings.Canvas.EnablePressureTouchMode = false;
                CardEnablePressureTouchMode.IsOn = false;
            }
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null && mw.inkCanvas != null)
                mw.inkCanvas.DefaultDrawingAttributes.IgnorePressure = CardDisablePressure.IsOn;
        }

        private void ComboBoxEraserSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EraserSize = ComboBoxEraserSize.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                if (mw.ComboBoxEraserSizeFloatingBar != null)
                    mw.ComboBoxEraserSizeFloatingBar.SelectedIndex = ComboBoxEraserSize.SelectedIndex;
                if (mw.BoardComboBoxEraserSize != null)
                    mw.BoardComboBoxEraserSize.SelectedIndex = ComboBoxEraserSize.SelectedIndex;
            }
        }

        private void ToggleSwitchHideStrokeWhenSelecting_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.HideStrokeWhenSelecting = CardHideStrokeWhenSelecting.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchClearCanvasAndClearTimeMachine_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.ClearCanvasAndClearTimeMachine = CardClearCanvasAndClearTimeMachine.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchClearCanvasAlsoClearImages_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.ClearCanvasAlsoClearImages = CardClearCanvasAlsoClearImages.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchCompressPicturesUploaded_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.IsCompressPicturesUploaded = CardCompressPicturesUploaded.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchLaunchSeewoVideoShowcaseForWhiteboardBooth_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.LaunchSeewoVideoShowcaseForWhiteboardBooth = CardLaunchSeewoVideoShowcaseForWhiteboardBooth.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxHyperbolaAsymptoteOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.HyperbolaAsymptoteOption = (OptionalOperation)ComboBoxHyperbolaAsymptoteOption.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchShowCircleCenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.ShowCircleCenter = CardShowCircleCenter.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxCurveSmoothingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var item = ComboBoxCurveSmoothingMode?.SelectedItem as ComboBoxItem;
            if (item == null) return;
            var tag = item.Tag?.ToString() ?? "0";
            switch (tag)
            {
                case "1":
                    SettingsManager.Settings.Canvas.FitToCurve = true;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = false;
                    break;
                case "2":
                    SettingsManager.Settings.Canvas.FitToCurve = false;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = true;
                    break;
                default:
                    SettingsManager.Settings.Canvas.FitToCurve = false;
                    SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing = false;
                    break;
            }
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null && mw.inkCanvas != null)
            {
                if (SettingsManager.Settings.Canvas.UseAdvancedBezierSmoothing)
                    mw.inkCanvas.DefaultDrawingAttributes.FitToCurve = false;
                else
                    mw.inkCanvas.DefaultDrawingAttributes.FitToCurve = SettingsManager.Settings.Canvas.FitToCurve;
            }
        }

        private void ToggleSwitchEnableInkFade_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnableInkFade = ToggleSwitchEnableInkFade.IsOn;
            ExpanderEnableInkFade.IsExpanded = ToggleSwitchEnableInkFade.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                mw.UpdateInkFadeManager(ToggleSwitchEnableInkFade.IsOn, SettingsManager.Settings.Canvas.InkFadeTime);
                if (mw.ToggleSwitchInkFadeInPanel != null)
                    mw.ToggleSwitchInkFadeInPanel.IsOn = ToggleSwitchEnableInkFade.IsOn;
                if (mw.ToggleSwitchInkFadeInPanel2 != null)
                    mw.ToggleSwitchInkFadeInPanel2.IsOn = ToggleSwitchEnableInkFade.IsOn;
            }
        }

        private void InkFadeTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(InkFadeTimeSlider, InkFadeTimeText, "{0:0}ms");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.InkFadeTime = (int)e.NewValue;
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null && SettingsManager.Settings.Canvas.EnableInkFade)
            {
                mw.UpdateInkFadeManager(true, (int)e.NewValue);
            }
        }

        private void ToggleSwitchHideInkFadeControlInPenMenu_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.HideInkFadeControlInPenMenu = CardHideInkFadeControlInPenMenu.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchBrushAutoRestore_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnableBrushAutoRestore = ToggleSwitchBrushAutoRestore.IsOn;
            ExpanderBrushAutoRestore.IsExpanded = ToggleSwitchBrushAutoRestore.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void BrushAutoRestoreTimesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.BrushAutoRestoreTimes = BrushAutoRestoreTimesTextBox.Text ?? string.Empty;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxBrushAutoRestoreColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxBrushAutoRestoreColor.SelectedItem is ComboBoxItem item)
            {
                string hex = item.Tag as string ?? string.Empty;
                SettingsManager.Settings.Canvas.BrushAutoRestoreColor = hex;
                SettingsManager.SaveSettingsToFile();
            }
        }

        private void BrushAutoRestoreWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(BrushAutoRestoreWidthSlider, BrushAutoRestoreWidthText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = BrushAutoRestoreWidthSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Canvas.BrushAutoRestoreWidth = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void BrushAutoRestoreAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(BrushAutoRestoreAlphaSlider, BrushAutoRestoreAlphaText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.BrushAutoRestoreAlpha = (int)e.NewValue;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableEraserAutoSwitchBack_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnableEraserAutoSwitchBack = ToggleSwitchEnableEraserAutoSwitchBack.IsOn;
            ExpanderEnableEraserAutoSwitchBack.IsExpanded = ToggleSwitchEnableEraserAutoSwitchBack.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void EraserAutoSwitchBackDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(EraserAutoSwitchBackDelaySlider, EraserAutoSwitchBackDelayText, "{0:0}秒");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EraserAutoSwitchBackDelaySeconds = (int)e.NewValue;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerRotationOnSelection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Gesture.IsEnableTwoFingerRotationOnSelection = CardEnableTwoFingerRotationOnSelection.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePalmEraser_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.EnablePalmEraser = ToggleSwitchEnablePalmEraser.IsOn;
            ExpanderEnablePalmEraser.IsExpanded = ToggleSwitchEnablePalmEraser.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxPalmEraserSensitivity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.PalmEraserSensitivity = ComboBoxPalmEraserSensitivity.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }
    }
}
