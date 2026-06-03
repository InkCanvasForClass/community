using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardAppearancePage : Page
    {
        private bool _isLoaded = false;

        public BoardAppearancePage()
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
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            BoardToolbarLeftOpacitySlider.Value = settings.Appearance.BoardToolbarLeftOpacity;
            BoardToolbarCenterOpacitySlider.Value = settings.Appearance.BoardToolbarCenterOpacity;
            BoardToolbarRightOpacitySlider.Value = settings.Appearance.BoardToolbarRightOpacity;

            ViewboxBlackBoardLeftScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardLeftScaleTransformValue;
            ViewboxBlackBoardCenterScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardScaleTransformValue;
            ViewboxBlackBoardRightScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardRightScaleTransformValue;
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(BoardToolbarLeftOpacitySlider, BoardToolbarLeftOpacityText, "{0:F2}");
            UpdateSliderText(BoardToolbarCenterOpacitySlider, BoardToolbarCenterOpacityText, "{0:F2}");
            UpdateSliderText(BoardToolbarRightOpacitySlider, BoardToolbarRightOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardLeftScaleTransformValueSlider, ViewboxBlackBoardLeftScaleText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardCenterScaleTransformValueSlider, ViewboxBlackBoardCenterScaleText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardRightScaleTransformValueSlider, ViewboxBlackBoardRightScaleText, "{0:F2}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void BoardToolbarLeftOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarLeftOpacitySlider, BoardToolbarLeftOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarLeftOpacitySlider.Value, 2);
            if (BoardToolbarLeftOpacitySlider.Value != val)
            {
                BoardToolbarLeftOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarLeftOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarLeftOpacityChanged(val);
        }

        private void BoardToolbarCenterOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarCenterOpacitySlider, BoardToolbarCenterOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarCenterOpacitySlider.Value, 2);
            if (BoardToolbarCenterOpacitySlider.Value != val)
            {
                BoardToolbarCenterOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarCenterOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarCenterOpacityChanged(val);
        }

        private void BoardToolbarRightOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarRightOpacitySlider, BoardToolbarRightOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarRightOpacitySlider.Value, 2);
            if (BoardToolbarRightOpacitySlider.Value != val)
            {
                BoardToolbarRightOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarRightOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarRightOpacityChanged(val);
        }

        private void ViewboxBlackBoardLeftScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardLeftScaleTransformValueSlider, ViewboxBlackBoardLeftScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardLeftScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardLeftScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardLeftScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardLeftScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardLeftScaleChanged(val);
        }

        private void ViewboxBlackBoardCenterScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardCenterScaleTransformValueSlider, ViewboxBlackBoardCenterScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardCenterScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardCenterScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardCenterScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardScaleChanged(val);
        }

        private void ViewboxBlackBoardRightScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardRightScaleTransformValueSlider, ViewboxBlackBoardRightScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardRightScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardRightScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardRightScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardRightScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardRightScaleChanged(val);
        }
    }
}
