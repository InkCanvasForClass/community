using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
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
        private bool _isLoaded = false;
        private bool _isApplyingLanguageFromSettings = false;

        public AppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
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
    }
}
