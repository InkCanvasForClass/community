using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AppearancePage : Page
    {
        public static event Action<double> OnBottomOffsetChanged;

        private bool _isLoaded = false;
        private bool _isApplyingLanguageFromSettings = false;

        public AppearancePage()
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

            ComboBoxTheme.SelectedIndex = settings.Appearance.Theme;
            SelectComboBoxItemByTag(ComboBoxWindowBackdrop, settings.Appearance.WindowBackdrop);

            _isApplyingLanguageFromSettings = true;
            try
            {
                var lang = settings.Appearance.Language ?? string.Empty;
                int langIndex = string.IsNullOrWhiteSpace(lang) ? 0 :
                    string.Equals(lang, "zh-CN", StringComparison.OrdinalIgnoreCase) ? 1 :
                    string.Equals(lang, "en-US", StringComparison.OrdinalIgnoreCase) ? 2 :
                    string.Equals(lang, "zh-ME", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
                ComboBoxLanguage.SelectedIndex = langIndex;
            }
            finally
            {
                _isApplyingLanguageFromSettings = false;
            }

            CardEnableQuickPanel.IsOn = settings.Appearance.IsShowQuickPanel;
            QuickPanelBottomOffsetSlider.Value = settings.Appearance.QuickPanelBottomOffset;
            ComboBoxUnFoldBtnImg.SelectedIndex = settings.Appearance.UnFoldButtonImageType;
            CardAllowDragSidePanel.IsOn = settings.Appearance.AllowDragSidePanel;
            QuickPanelOpacitySlider.Value = settings.Appearance.QuickPanelOpacity;
            CardAutoCollapseQuickPanel.IsOn = settings.Appearance.IsAutoCollapseQuickPanel;
            AutoCollapseDelaySlider.Value = settings.Appearance.AutoCollapseQuickPanelDelay;
        }

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null) return;

            var selectedItem = comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

            comboBox.SelectedItem = selectedItem;
        }

        private static string GetSelectedComboBoxTag(ComboBox comboBox, string fallback)
        {
            return (comboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
        }

        #region Theme & Language

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.Theme = ComboBoxTheme.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnThemeChanged(ComboBoxTheme.SelectedIndex);
            }
            catch (Exception ex) { Debug.WriteLine($"切换主题时出错: {ex.Message}"); }
        }

        private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _isApplyingLanguageFromSettings) return;
            try
            {
                var index = ComboBoxLanguage.SelectedIndex;
                string language = index switch
                {
                    1 => "zh-CN",
                    2 => "en-US",
                    3 => "zh-ME",
                    _ => string.Empty
                };
                SettingsManager.Settings.Appearance.Language = language;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnLanguageChanged(language);
            }
            catch (Exception ex) { Debug.WriteLine($"切换界面语言时出错: {ex.Message}"); }
        }

        private void ComboBoxWindowBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                var backdrop = GetSelectedComboBoxTag(ComboBoxWindowBackdrop, "None");
                SettingsManager.Settings.Appearance.WindowBackdrop = backdrop;
                SettingsManager.SaveSettingsToFile();

                if (Window.GetWindow(this) is SettingsWindow settingsWindow)
                {
                    settingsWindow.ApplyWindowBackdrop(backdrop);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"切换窗口背景样式时出错: {ex.Message}"); }
        }

        #endregion

        #region Display Options

        private void ToggleSwitchEnableQuickPanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsShowQuickPanel = CardEnableQuickPanel.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAllowDragSidePanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.AllowDragSidePanel = CardAllowDragSidePanel.IsOn;
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
            SettingsManager.Settings.Appearance.IsAutoCollapseQuickPanel = CardAutoCollapseQuickPanel.IsOn;
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

        #endregion
    }
}
