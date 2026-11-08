using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
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
        private bool _isApplyingUIFontFromSettings = false;

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
                SelectComboBoxItemByTag(ComboBoxLanguage, lang);
            }
            finally
            {
                _isApplyingLanguageFromSettings = false;
            }

            _isApplyingUIFontFromSettings = true;
            try
            {
                LoadUIFontItems();
                SelectComboBoxItemByTag(ComboBoxUIFont, settings.Appearance.GlobalUIFontFamily);
            }
            finally
            {
                _isApplyingUIFontFromSettings = false;
            }
        }

        private void LoadUIFontItems()
        {
            ComboBoxUIFont.Items.Clear();
            AddUIFontItem(ThemeStrings.Theme_UIFont_Default, GlobalFontHelper.DefaultUIFontFamily);
            foreach (var preset in GlobalFontHelper.PresetFonts)
                AddUIFontItem(preset.Key, preset.Value);
            foreach (var entry in (SettingsManager.Settings.Appearance.ImportedUIFonts
                         ?? Enumerable.Empty<string>()))
                AddUIFontItem(GlobalFontHelper.GetImportedDisplayName(entry), entry);
        }

        private void AddUIFontItem(string display, string tag)
        {
            var item = new ComboBoxItem { Content = display, Tag = tag };
            item.FontFamily = GlobalFontHelper.Resolve(tag);
            ComboBoxUIFont.Items.Add(item);
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
                string language = GetSelectedComboBoxTag(ComboBoxLanguage, string.Empty);
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

        private void ComboBoxUIFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnRemoveFont.IsEnabled = GlobalFontHelper.IsImportedEntry(
                (ComboBoxUIFont.SelectedItem as ComboBoxItem)?.Tag?.ToString());
            if (!_isLoaded || _isApplyingUIFontFromSettings) return;
            try
            {
                var font = GetSelectedComboBoxTag(ComboBoxUIFont, GlobalFontHelper.DefaultUIFontFamily);
                SettingsManager.Settings.Appearance.GlobalUIFontFamily = font;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnGlobalUIFontChanged(font);
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"切换界面字体时出错: {ex.Message}", LogHelper.LogType.Error); }
        }

        private void BtnImportFont_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "字体文件 (*.ttf;*.ttc)|*.ttf;*.ttc",
                    Multiselect = false,
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() != true) return;

                var entry = GlobalFontHelper.ImportFont(dialog.FileName);
                if (entry == null)
                {
                    MessageBox.Show(ThemeStrings.Theme_UIFont_ImportFailed, ThemeStrings.Theme_UIFontLabel,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var imported = SettingsManager.Settings.Appearance.ImportedUIFonts ??= new List<string>();
                if (!imported.Contains(entry)) imported.Add(entry);
                SettingsManager.Settings.Appearance.GlobalUIFontFamily = entry;
                SettingsManager.SaveSettingsToFile();

                _isApplyingUIFontFromSettings = true;
                try
                {
                    LoadUIFontItems();
                    SelectComboBoxItemByTag(ComboBoxUIFont, entry);
                }
                finally
                {
                    _isApplyingUIFontFromSettings = false;
                }
                SettingsActionHub.OnGlobalUIFontChanged(entry);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导入字体时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(ThemeStrings.Theme_UIFont_ImportFailed, ThemeStrings.Theme_UIFontLabel,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRemoveFont_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                var entry = (ComboBoxUIFont.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!GlobalFontHelper.IsImportedEntry(entry)) return;

                var imported = SettingsManager.Settings.Appearance.ImportedUIFonts ??= new List<string>();
                imported.Remove(entry);
                GlobalFontHelper.RemoveImportedFont(entry);

                if (string.Equals(SettingsManager.Settings.Appearance.GlobalUIFontFamily, entry,
                        StringComparison.OrdinalIgnoreCase))
                {
                    SettingsManager.Settings.Appearance.GlobalUIFontFamily = GlobalFontHelper.DefaultUIFontFamily;
                    SettingsActionHub.OnGlobalUIFontChanged(GlobalFontHelper.DefaultUIFontFamily);
                }
                SettingsManager.SaveSettingsToFile();

                _isApplyingUIFontFromSettings = true;
                try
                {
                    LoadUIFontItems();
                    SelectComboBoxItemByTag(ComboBoxUIFont, SettingsManager.Settings.Appearance.GlobalUIFontFamily);
                }
                finally
                {
                    _isApplyingUIFontFromSettings = false;
                }
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"移除导入字体时出错: {ex.Message}", LogHelper.LogType.Error); }
        }

        #endregion
    }
}
