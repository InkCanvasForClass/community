using Ink_Canvas.Properties;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarAppearancePage : Page
    {
        private bool _isLoaded = false;

        public ToolbarAppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            if (settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
                ViewboxFloatingBarScaleTransformValueSlider.Value = settings.Appearance.ViewboxFloatingBarScaleTransformValue;

            ViewboxFloatingBarOpacityValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityValue;
            ViewboxFloatingBarOpacityInPPTValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            UpdateFloatingBarActualScaleText();
        }

        private void UpdateFloatingBarActualScaleText()
        {
            if (ViewboxFloatingBarScaleTransformValueSlider == null || ViewboxFloatingBarActualScaleText == null) return;
            double val = ViewboxFloatingBarScaleTransformValueSlider.Value;
            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            ViewboxFloatingBarActualScaleText.Text = $"{actualScale:F2}x";
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarScaleTransformValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();

            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            UpdateFloatingBarActualScaleText();

            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxFloatingBarScaleTransform.ScaleX = actualScale;
                mw.ViewboxFloatingBarScaleTransform.ScaleY = actualScale;
                if (mw.IsInPptPresentationMode)
                    mw.ViewboxFloatingBarMarginAnimation(60);
                else
                    mw.ViewboxFloatingBarMarginAnimation(100, true);
            }
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityValue = val;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxFloatingBar.Opacity = val;
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityInPPTValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = val;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 2)
            {
                mw.ViewboxFloatingBar.Opacity = val;
            }
        }
    }
}
