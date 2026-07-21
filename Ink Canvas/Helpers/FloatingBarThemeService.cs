using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Loads ClassIsland-style local XAML themes for the floating toolbar.
    /// Each theme is a folder containing manifest.json and Theme.xaml.
    /// </summary>
    public sealed class FloatingBarThemeService
    {
        public sealed class ThemeInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Path { get; set; }
            public bool IsBuiltIn { get; set; }

            [JsonIgnore]
            public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
        }

        private const string DefaultThemeId = "default";
        private readonly MainWindow _mainWindow;
        private ResourceDictionary _themeDictionary;

        public List<ThemeInfo> Themes { get; } = new List<ThemeInfo>();

        public FloatingBarThemeService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void LoadThemes()
        {
            Themes.Clear();
            Themes.Add(new ThemeInfo
            {
                Id = DefaultThemeId,
                Name = ThemeStrings.Theme_FloatingBarBorderColor_Default,
                Description = ThemeStrings.Theme_FloatingBarBorderColorHint,
                IsBuiltIn = true
            });

            var root = Path.Combine(App.RootPath, "FloatingBarThemes");
            if (!Directory.Exists(root)) return;

            foreach (var directory in Directory.GetDirectories(root))
            {
                var manifestPath = Path.Combine(directory, "manifest.json");
                var themePath = Path.Combine(directory, "Theme.xaml");
                if (!File.Exists(manifestPath) || !File.Exists(themePath)) continue;

                try
                {
                    var manifest = JsonConvert.DeserializeObject<ThemeInfo>(File.ReadAllText(manifestPath));
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id)) continue;
                    manifest.Path = directory;
                    manifest.IsBuiltIn = false;
                    if (Themes.Any(x => string.Equals(x.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    Themes.Add(manifest);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"加载浮动栏主题失败: {manifestPath}, {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        public void ApplySavedTheme()
        {
            var id = MainWindow.Settings?.Appearance?.FloatingBarThemeId;
            ApplyTheme(string.IsNullOrWhiteSpace(id) ? DefaultThemeId : id);
        }

        private ResourceDictionary CreateBuiltInThemeDictionary()
        {
            var dictionary = new ResourceDictionary();
            dictionary["FloatingBarBackgroundBrush"] = Application.Current.TryFindResource("FloatBarBackground") ?? new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x1C, 0x1E));
            dictionary["FloatingBarForegroundBrush"] = Application.Current.TryFindResource("FloatBarForeground") ?? Brushes.White;
            dictionary["FloatingBarBorderBrush"] = Application.Current.TryFindResource("FloatBarBorderBrush") ?? Brushes.White;
            dictionary["FloatingBarAccentBrush"] = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            dictionary["FloatingBarButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x22, 0x25, 0x63, 0xEB));
            dictionary["FloatingBarButtonPressedBrush"] = new SolidColorBrush(Color.FromArgb(0x44, 0x25, 0x63, 0xEB));
            dictionary["FloatingBarPopupBackgroundBrush"] = Application.Current.TryFindResource("ToolsPopupBackground") ?? dictionary["FloatingBarBackgroundBrush"];
            dictionary["FloatingBarPopupInnerBackgroundBrush"] = Application.Current.TryFindResource("ToolsPopupInnerBackground") ?? dictionary["FloatingBarBackgroundBrush"];
            dictionary["FloatingBarPopupInnerBorderBrush"] = Application.Current.TryFindResource("ToolsPopupInnerBorderBrush") ?? dictionary["FloatingBarBorderBrush"];
            dictionary["FloatingBarPopupTitleForegroundBrush"] = Application.Current.TryFindResource("ToolsPopupTitleForeground") ?? dictionary["FloatingBarForegroundBrush"];
            dictionary["FloatingBarPopupCloseBrush"] = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            return dictionary;
        }

        public bool ApplyTheme(string themeId)
        {
            var theme = Themes.FirstOrDefault(x => string.Equals(x.Id, themeId, StringComparison.OrdinalIgnoreCase));
            if (theme == null) theme = Themes.FirstOrDefault(x => x.Id == DefaultThemeId);
            if (theme == null) return false;

            try
            {
                var dictionary = theme.IsBuiltIn
                    ? CreateBuiltInThemeDictionary()
                    : new ResourceDictionary
                    {
                        Source = new Uri(Path.Combine(theme.Path, "Theme.xaml"), UriKind.Absolute)
                    };

                var resources = Application.Current.Resources;
                if (_themeDictionary != null) resources.MergedDictionaries.Remove(_themeDictionary);
                _themeDictionary = dictionary;
                resources.MergedDictionaries.Add(dictionary);

                MainWindow.Settings.Appearance.FloatingBarThemeId = theme.Id;
                SettingsManager.SaveSettingsToFile();
                _mainWindow.ApplyFloatingBarBorderColor();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用浮动栏主题失败: {theme.Id}, {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }
    }
}
