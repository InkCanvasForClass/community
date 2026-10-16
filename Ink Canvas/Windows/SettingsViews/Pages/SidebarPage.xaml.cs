using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class SidebarPage : Page
    {
        public static event Action<double> OnBottomOffsetChanged;

        private bool _isLoaded = false;

        public SidebarPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        public static void NotifyBottomOffsetChanged(double val)
        {
            OnBottomOffsetChanged?.Invoke(val);
        }

        private void HandleBottomOffsetChanged(double val)
        {
            if (QuickPanelBottomOffsetSlider != null)
            {
                QuickPanelBottomOffsetSlider.Value = val;
                UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            OnBottomOffsetChanged += HandleBottomOffsetChanged;
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
            UpdateSliderText(QuickPanelOpacitySlider, QuickPanelOpacityText, "{0:P0}");
            UpdateSliderText(AutoCollapseDelaySlider, AutoCollapseDelayText, "{0:F1}s");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            OnBottomOffsetChanged -= HandleBottomOffsetChanged;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            ToggleSwitchEnableQuickPanel.IsOn = settings.Appearance.IsShowQuickPanel;
            QuickPanelBottomOffsetSlider.Value = settings.Appearance.QuickPanelBottomOffset;
            ComboBoxUnFoldBtnImg.SelectedIndex = settings.Appearance.UnFoldButtonImageType;
            ToggleSwitchAllowDragSidePanel.IsOn = settings.Appearance.AllowDragSidePanel;
            QuickPanelOpacitySlider.Value = settings.Appearance.QuickPanelOpacity;
            ToggleSwitchAutoCollapseQuickPanel.IsOn = settings.Appearance.IsAutoCollapseQuickPanel;
            AutoCollapseDelaySlider.Value = settings.Appearance.AutoCollapseQuickPanelDelay;
        }

        private void ToggleSwitchEnableQuickPanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsShowQuickPanel = ToggleSwitchEnableQuickPanel.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAllowDragSidePanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.AllowDragSidePanel = ToggleSwitchAllowDragSidePanel.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void QuickPanelOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(QuickPanelOpacitySlider, QuickPanelOpacityText, "{0:P0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.QuickPanelOpacity = QuickPanelOpacitySlider.Value;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnQuickPanelOpacityChanged(QuickPanelOpacitySlider.Value);
        }

        private void ToggleSwitchAutoCollapseQuickPanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsAutoCollapseQuickPanel = ToggleSwitchAutoCollapseQuickPanel.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoCollapseQuickPanelChanged();
        }

        private void AutoCollapseDelaySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(AutoCollapseDelaySlider, AutoCollapseDelayText, "{0:F1}s");
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.AutoCollapseQuickPanelDelay = AutoCollapseDelaySlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void QuickPanelBottomOffsetSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
            if (!_isLoaded) return;
            var val = Math.Round(QuickPanelBottomOffsetSlider.Value);
            if (QuickPanelBottomOffsetSlider.Value != val)
            {
                QuickPanelBottomOffsetSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.QuickPanelBottomOffset = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnQuickPanelBottomOffsetChanged(val);
        }

        private void ComboBoxUnFoldBtnImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.UnFoldButtonImageType = ComboBoxUnFoldBtnImg.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnUnFoldButtonImageTypeChanged(ComboBoxUnFoldBtnImg.SelectedIndex);
        }
    }
}
