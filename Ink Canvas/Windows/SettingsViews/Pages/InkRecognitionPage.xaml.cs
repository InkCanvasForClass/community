using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class InkRecognitionPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public InkRecognitionPage()
        {
            InitializeComponent();
            Loaded += InkRecognitionPage_Loaded;
        }

        private void InkRecognitionPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(AutoStraightenLineThresholdSlider, AutoStraightenLineThresholdText, "{0:0}");
            UpdateSliderText(LineStraightenSensitivitySlider, LineStraightenSensitivityText, "{0:F2}");
            UpdateSliderText(PauseStraightenDelaySlider, PauseStraightenDelayText, "{0:0} ms");
            UpdateSliderText(LineEndpointSnappingThresholdSlider, LineEndpointSnappingThresholdText, "{0:0}");
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

                if (settings.InkToShape != null)
                {
                    CardEnableInkToShape.IsOn = settings.InkToShape.IsInkToShapeEnabled;
                    int eng = settings.InkToShape.ShapeRecognitionEngine;
                    if (eng < 0) eng = 0;
                    if (eng > 2) eng = 2;
                    ComboBoxShapeRecognitionEngine.SelectedIndex = eng;
                    CardEnableWinRtHandwritingStrokeBeautify.IsOn = settings.InkToShape.EnableWinRtHandwritingStrokeBeautify;
                    SelectHandwritingFontByValue(settings.InkToShape.HandwritingCorrectionFontFamily);
                    CardEnableInkToShapeNoFakePressureRectangle.IsOn = settings.InkToShape.IsInkToShapeNoFakePressureRectangle;
                    CardEnableInkToShapeNoFakePressureTriangle.IsOn = settings.InkToShape.IsInkToShapeNoFakePressureTriangle;
                    ToggleCheckboxEnableInkToShapeTriangle.IsChecked = settings.InkToShape.IsInkToShapeTriangle;
                    ToggleCheckboxEnableInkToShapeRectangle.IsChecked = settings.InkToShape.IsInkToShapeRectangle;
                    ToggleCheckboxEnableInkToShapeRounded.IsChecked = settings.InkToShape.IsInkToShapeRounded;
                    LineStraightenSensitivitySlider.Value = settings.InkToShape.LineStraightenSensitivity;
                }

                if (settings.Canvas != null)
                {
                    ToggleSwitchAutoStraightenLine.IsOn = settings.Canvas.AutoStraightenLine;
                    AutoStraightenLineThresholdSlider.Value = settings.Canvas.AutoStraightenLineThreshold;
                    ToggleSwitchHighPrecisionLineStraighten.IsOn = settings.Canvas.HighPrecisionLineStraighten;
                    ToggleSwitchPauseStraightenLine.IsOn = settings.Canvas.PauseStraightenLine;
                    PauseStraightenDelaySlider.Value = settings.Canvas.PauseStraightenDelay;
                    ToggleSwitchLineEndpointSnapping.IsOn = settings.Canvas.LineEndpointSnapping;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载墨迹纠正设置时出错: {ex.Message}");
            }

            _isLoaded = true;

            ExpanderAutoStraightenLine.IsExpanded = ToggleSwitchAutoStraightenLine.IsOn;
            ExpanderLineEndpointSnapping.IsExpanded = ToggleSwitchLineEndpointSnapping.IsOn;
        }

        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeEnabled = CardEnableInkToShape.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw != null)
            {
                if (mw.FloatingBarToggleSwitchEnableInkToShape != null)
                    mw.FloatingBarToggleSwitchEnableInkToShape.IsOn = CardEnableInkToShape.IsOn;
                if (mw.BoardToggleSwitchEnableInkToShape != null)
                    mw.BoardToggleSwitchEnableInkToShape.IsOn = CardEnableInkToShape.IsOn;
            }
        }

        private void ComboBoxShapeRecognitionEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxShapeRecognitionEngine == null) return;
            int idx = ComboBoxShapeRecognitionEngine.SelectedIndex;
            if (idx < 0) idx = 0;
            if (idx > 2) idx = 2;
            SettingsManager.Settings.InkToShape.ShapeRecognitionEngine = idx;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableWinRtHandwritingStrokeBeautify_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.EnableWinRtHandwritingStrokeBeautify = CardEnableWinRtHandwritingStrokeBeautify.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void SelectHandwritingFontByValue(string value)
        {
            if (ComboBoxHandwritingCorrectionFont == null) return;
            value = (value ?? string.Empty).Trim();
            int matchIndex = -1;
            for (int i = 0; i < ComboBoxHandwritingCorrectionFont.Items.Count; i++)
            {
                var item = ComboBoxHandwritingCorrectionFont.Items[i] as ComboBoxItem;
                var tag = item?.Tag as string;
                if (!string.IsNullOrEmpty(tag) && string.Equals(tag, value, StringComparison.OrdinalIgnoreCase))
                {
                    matchIndex = i;
                    break;
                }
            }
            ComboBoxHandwritingCorrectionFont.SelectedIndex = matchIndex >= 0 ? matchIndex : 0;
        }

        private void ComboBoxHandwritingCorrectionFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxHandwritingCorrectionFont == null) return;
            var item = ComboBoxHandwritingCorrectionFont.SelectedItem as ComboBoxItem;
            var tag = item?.Tag as string;
            if (string.IsNullOrWhiteSpace(tag)) return;
            SettingsManager.Settings.InkToShape.HandwritingCorrectionFontFamily = tag;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableInkToShapeNoFakePressureRectangle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeNoFakePressureRectangle = CardEnableInkToShapeNoFakePressureRectangle.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableInkToShapeNoFakePressureTriangle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeNoFakePressureTriangle = CardEnableInkToShapeNoFakePressureTriangle.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeTriangle_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeTriangle = (bool)ToggleCheckboxEnableInkToShapeTriangle.IsChecked;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeRectangle_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeRectangle = (bool)ToggleCheckboxEnableInkToShapeRectangle.IsChecked;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeRounded_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.InkToShape.IsInkToShapeRounded = (bool)ToggleCheckboxEnableInkToShapeRounded.IsChecked;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoStraightenLine_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.AutoStraightenLine = ToggleSwitchAutoStraightenLine.IsOn;
            ExpanderAutoStraightenLine.IsExpanded = ToggleSwitchAutoStraightenLine.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void AutoStraightenLineThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(AutoStraightenLineThresholdSlider, AutoStraightenLineThresholdText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.AutoStraightenLineThreshold = (int)e.NewValue;
            SettingsManager.SaveSettingsToFile();
        }

        private void LineStraightenSensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(LineStraightenSensitivitySlider, LineStraightenSensitivityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(LineStraightenSensitivitySlider.Value, 2);
            LineStraightenSensitivitySlider.Value = val;
            SettingsManager.Settings.InkToShape.LineStraightenSensitivity = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchHighPrecisionLineStraighten_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.HighPrecisionLineStraighten = ToggleSwitchHighPrecisionLineStraighten.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchPauseStraightenLine_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.PauseStraightenLine = ToggleSwitchPauseStraightenLine.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void PauseStraightenDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(PauseStraightenDelaySlider, PauseStraightenDelayText, "{0:0} ms");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.PauseStraightenDelay = (int)PauseStraightenDelaySlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchLineEndpointSnapping_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.LineEndpointSnapping = ToggleSwitchLineEndpointSnapping.IsOn;
            ExpanderLineEndpointSnapping.IsExpanded = ToggleSwitchLineEndpointSnapping.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void LineEndpointSnappingThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(LineEndpointSnappingThresholdSlider, LineEndpointSnappingThresholdText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.LineEndpointSnappingThreshold = (int)e.NewValue;
            SettingsManager.SaveSettingsToFile();
        }
    }
}
