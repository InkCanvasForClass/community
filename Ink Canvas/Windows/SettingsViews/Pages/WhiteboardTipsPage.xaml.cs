using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ContentDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class WhiteboardTipsPage : Page
    {
        private bool _isLoaded = false;
        private List<TipsScheme> _allSchemes = new List<TipsScheme>();
        private int _lastValidInterval = 60;

        public WhiteboardTipsPage()
        {
            InitializeComponent();
            ComboBoxRotationInterval.LostFocus += ComboBoxRotationInterval_LostFocus;
            LoadSettings();
            _isLoaded = true;
        }

        #region Load & Update

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            CardEnableChickenSoupInWhiteboardMode.IsOn = settings.Appearance.EnableChickenSoupInWhiteboardMode;

            var position = settings.Appearance.ChickenSoupPosition;
            foreach (ComboBoxItem item in ComboBoxChickenSoupPosition.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == position)
                {
                    ComboBoxChickenSoupPosition.SelectedItem = item;
                    break;
                }
            }

            CardEnableAutoRotation.IsOn = settings.Appearance.EnableChickenSoupAutoRotation;
            _lastValidInterval = settings.Appearance.ChickenSoupAutoRotationInterval;
            ComboBoxRotationInterval.Text = _lastValidInterval.ToString();

            CardRotationInterval.Visibility = CardEnableAutoRotation.IsOn ? Visibility.Visible : Visibility.Collapsed;

            UpdateChildControlsEnabled();

            _allSchemes = new List<TipsScheme>();
            var presets = ChickenSoup.GetPresetSchemes();
            foreach (var preset in presets)
            {
                preset.IsEnabled = settings.Appearance.EnabledPresetTipsSources?.Contains(preset.PresetId) ?? false;
                _allSchemes.Add(preset);
            }

            if (settings.Appearance.CustomTipsSchemes != null)
            {
                _allSchemes.AddRange(settings.Appearance.CustomTipsSchemes);
            }

            ListViewSchemes.ItemsSource = _allSchemes;
        }

        private void UpdateChildControlsEnabled()
        {
            bool master = CardEnableChickenSoupInWhiteboardMode.IsOn;
            bool autoRotation = CardEnableAutoRotation.IsOn;

            CardQuotePosition.IsEnabled = master;
            CardEnableAutoRotation.IsEnabled = master;
            CardRotationInterval.Visibility = (master && autoRotation) ? Visibility.Visible : Visibility.Collapsed;

            ListViewSchemes.IsEnabled = master;
            BtnImport.IsEnabled = master;
            BtnCreate.IsEnabled = master;

            // Edit/Export/Delete buttons are now per-item in the ListView
        }


        private void RefreshSchemesList()
        {
            ListViewSchemes.ItemsSource = null;
            ListViewSchemes.ItemsSource = _allSchemes;
        }

        #endregion

        #region Section 1: Global Settings

        private void ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableChickenSoupInWhiteboardMode = CardEnableChickenSoupInWhiteboardMode.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateChildControlsEnabled();
            SettingsActionHub.OnChickenSoupInWhiteboardChanged(CardEnableChickenSoupInWhiteboardMode.IsOn, true);
        }

        private void ComboBoxChickenSoupPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var item = ComboBoxChickenSoupPosition.SelectedItem as ComboBoxItem;
            if (item?.Tag == null) return;
            SettingsManager.Settings.Appearance.ChickenSoupPosition = item.Tag.ToString();
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupPositionChanged();
        }

        private void ToggleSwitchEnableAutoRotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableChickenSoupAutoRotation = CardEnableAutoRotation.IsOn;
            SettingsManager.SaveSettingsToFile();
            CardRotationInterval.Visibility = CardEnableAutoRotation.IsOn ? Visibility.Visible : Visibility.Collapsed;
            SettingsActionHub.OnChickenSoupAutoRotationChanged();
        }

        private void ComboBoxRotationInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ValidateAndSaveInterval();
        }

        private void ComboBoxRotationInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            ValidateAndSaveInterval();
        }

        private void ValidateAndSaveInterval()
        {
            if (!int.TryParse(ComboBoxRotationInterval.Text, out int val) || val < 5)
            {
                ComboBoxRotationInterval.Text = _lastValidInterval.ToString();
                return;
            }
            if (val == _lastValidInterval) return;
            _lastValidInterval = val;
            SettingsManager.Settings.Appearance.ChickenSoupAutoRotationInterval = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupAutoRotationChanged();
        }

        #endregion

        #region Section 2: Schemes

        private void ListViewSchemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SchemeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var scheme = (sender as CheckBox)?.DataContext as TipsScheme;
            if (scheme == null) return;

            if (scheme.IsPreset)
            {
                if (SettingsManager.Settings.Appearance.EnabledPresetTipsSources == null)
                    SettingsManager.Settings.Appearance.EnabledPresetTipsSources = new List<string>();

                var sources = SettingsManager.Settings.Appearance.EnabledPresetTipsSources;
                if (scheme.IsEnabled)
                {
                    if (!sources.Contains(scheme.PresetId))
                        sources.Add(scheme.PresetId);
                }
                else
                {
                    sources.Remove(scheme.PresetId);
                }
            }

            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupSchemesChanged();
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            await ShowSchemeDialogAsync(ThemeStrings.Theme_Tips_NewDialogTitle, "", "", false, (name, content) =>
            {
                var scheme = new TipsScheme { Name = name, Content = content, IsPreset = false, IsEnabled = true };
                _allSchemes.Add(scheme);
                if (SettingsManager.Settings.Appearance.CustomTipsSchemes == null)
                    SettingsManager.Settings.Appearance.CustomTipsSchemes = new List<TipsScheme>();
                SettingsManager.Settings.Appearance.CustomTipsSchemes.Add(scheme);
                RefreshSchemesList();
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnChickenSoupSchemesChanged();
            });
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            await ShowSchemeDialogAsync(ThemeStrings.Theme_Tips_ImportDialogTitle, "", "", true, (name, content) =>
            {
                var scheme = new TipsScheme { Name = name, Content = content, IsPreset = false, IsEnabled = true };
                _allSchemes.Add(scheme);
                if (SettingsManager.Settings.Appearance.CustomTipsSchemes == null)
                    SettingsManager.Settings.Appearance.CustomTipsSchemes = new List<TipsScheme>();
                SettingsManager.Settings.Appearance.CustomTipsSchemes.Add(scheme);
                RefreshSchemesList();
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnChickenSoupSchemesChanged();
            });
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var scheme = (sender as Button)?.DataContext as TipsScheme;
            if (scheme == null || scheme.IsPreset) return;

            await ShowSchemeDialogAsync(ThemeStrings.Theme_Tips_EditDialogTitle, scheme.Name, scheme.Content, false, (name, content) =>
            {
                scheme.Name = name;
                scheme.Content = content;
                RefreshSchemesList();
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnChickenSoupSchemesChanged();
            });
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var scheme = (sender as Button)?.DataContext as TipsScheme;
            if (scheme == null) return;

            var dialog = new SaveFileDialog
            {
                FileName = scheme.Name + ".txt",
                Filter = "Text files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                string content;
                if (scheme.IsPreset)
                {
                    var tips = ChickenSoup.GetTipsFromPreset(scheme.PresetId);
                    content = tips != null ? string.Join(Environment.NewLine, tips) : string.Empty;
                }
                else
                {
                    content = scheme.Content ?? string.Empty;
                }
                File.WriteAllText(dialog.FileName, content);
            }
        }

        private async void BtnDeleteScheme_Click(object sender, RoutedEventArgs e)
        {
            var scheme = (sender as Button)?.DataContext as TipsScheme;
            if (scheme == null || scheme.IsPreset) return;

            var dialog = new ContentDialog
            {
                Title = "删除方案",
                Content = "确定删除这个方案吗？",
                PrimaryButtonText = "删除",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _allSchemes.Remove(scheme);
                if (SettingsManager.Settings.Appearance.CustomTipsSchemes != null)
                {
                    SettingsManager.Settings.Appearance.CustomTipsSchemes.Remove(scheme);
                }
                RefreshSchemesList();
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnChickenSoupSchemesChanged();
            }
        }

        private async Task ShowSchemeDialogAsync(string title, string initialName, string initialContent,
            bool showImportButton, Action<string, string> onSave)
        {
            var nameBox = new TextBox { MinWidth = 360 };
            if (!string.IsNullOrEmpty(initialName))
                nameBox.Text = initialName;

            var contentBox = new TextBox
            {
                MinWidth = 360,
                AcceptsReturn = true,
                Height = 150,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            if (!string.IsNullOrEmpty(initialContent))
                contentBox.Text = initialContent;

            var panel = new StackPanel { MinWidth = 360 };

            if (showImportButton)
            {
                var importButton = new Button { Content = ThemeStrings.Theme_Tips_SelectTxtFile, Margin = new Thickness(0, 0, 0, 8) };
                importButton.Click += (fs, fe) =>
                {
                    var ofd = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" };
                    if (ofd.ShowDialog() == true)
                    {
                        nameBox.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                        try { contentBox.Text = File.ReadAllText(ofd.FileName); }
                        catch { /* ignore read errors */ }
                    }
                };
                panel.Children.Add(importButton);
            }

            panel.Children.Add(new TextBlock { Text = ThemeStrings.Theme_Tips_SchemeName, Margin = new Thickness(0, 0, 0, 4) });
            panel.Children.Add(nameBox);
            panel.Children.Add(new TextBlock { Text = ThemeStrings.Theme_Tips_SchemeContent, Margin = new Thickness(0, 8, 0, 4) });
            panel.Children.Add(contentBox);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = FloatingBarStrings.Tools_Save,
                CloseButtonText = CommonStrings.Common_Cancel,
                Owner = Window.GetWindow(this),
                DefaultButton = ContentDialogButton.Primary
            };

            dialog.PrimaryButtonClick += (s, args) =>
            {
                var name = nameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    args.Cancel = true;
                    MessageBox.Show(ThemeStrings.Theme_Tips_NameRequired, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var content = contentBox.Text ?? string.Empty;
                content = content.Replace("\"", "\\\"");
                onSave(name, content);
            };

            dialog.CloseButtonClick += (s, args) =>
            {
                // Allow close directly, no secondary confirmation
            };

            await dialog.ShowAsync();
        }

        #endregion
    }
}
