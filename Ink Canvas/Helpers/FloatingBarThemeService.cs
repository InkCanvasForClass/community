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

        private const string DefaultThemeId = "luotianyi";
        private const string LegacyDefaultThemeId = "default";
        private const string BuiltInThemeUri = "/InkCanvasForClass;component/Resources/FloatingBarThemes/LuoTianyi/Theme.xaml";
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
                Name = "洛天依 · 苍青音律",
                Description = "以洛天依的苍青、青绿色与粉色为灵感的浮动栏主题",
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
            dictionary["FloatingBarBackgroundBrush"] = new LinearGradientBrush(
                Color.FromArgb(0xF2, 0x0C, 0x52, 0x66),
                Color.FromArgb(0xF2, 0x3B, 0x61, 0x7D), 45);
            dictionary["FloatingBarForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF3, 0xFF, 0xFF));
            dictionary["FloatingBarBorderBrush"] = new SolidColorBrush(Color.FromArgb(0xB3, 0xA6, 0xF4, 0xF2));
            dictionary["FloatingBarAccentBrush"] = new SolidColorBrush(Color.FromRgb(0x73, 0xE0, 0xD4));
            dictionary["FloatingBarButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x3D, 0x73, 0xE0, 0xD4));
            dictionary["FloatingBarButtonPressedBrush"] = new SolidColorBrush(Color.FromArgb(0x70, 0x73, 0xE0, 0xD4));
            dictionary["FloatingBarPopupBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, 0x0B, 0x3D, 0x4A));
            dictionary["FloatingBarPopupInnerBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, 0x1C, 0x59, 0x66));
            dictionary["FloatingBarPopupInnerBorderBrush"] = new SolidColorBrush(Color.FromArgb(0xB3, 0x73, 0xE0, 0xD4));
            dictionary["FloatingBarPopupTitleForegroundBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xBF, 0xFF, 0xF8));
            dictionary["FloatingBarPopupCloseBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x78, 0x91));
            dictionary["FloatingBarPopupHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x44, 0x73, 0xE0, 0xD4));
            dictionary["FloatingBarTitleBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 0x2B, 0x9A, 0xA0));
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
