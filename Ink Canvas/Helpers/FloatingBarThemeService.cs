using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 加载浮动工具栏的本地 XAML 主题。
    /// Each theme is a folder containing manifest.json and Theme.xaml.
    /// </summary>
    public sealed class FloatingBarThemeService
    {
        public sealed class ThemeInfo : System.ComponentModel.INotifyPropertyChanged
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Path { get; set; }
            public bool IsBuiltIn { get; set; }

            [JsonIgnore]
            public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

            private bool _isApplied;
            [JsonIgnore]
            public bool IsApplied
            {
                get => _isApplied;
                set
                {
                    if (_isApplied == value) return;
                    _isApplied = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsApplied)));
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        }

        private const string DefaultThemeId = "default";
        private readonly MainWindow _mainWindow;
        private ResourceDictionary _themeDictionary;

        public ObservableCollection<ThemeInfo> Themes { get; } = new ObservableCollection<ThemeInfo>();

        public bool DeleteTheme(string themeId)
        {
            var theme = Themes.FirstOrDefault(x => string.Equals(x.Id, themeId, StringComparison.OrdinalIgnoreCase));
            if (theme == null || theme.IsBuiltIn) return false;
            try
            {
                // Resolve the actual theme folder to delete using the loaded ThemeInfo.Path when available.
                var themesRoot = Path.Combine(App.RootPath, "FloatingBarThemes");
                var themesRootFull = Path.GetFullPath(themesRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                string targetDir;
                if (!string.IsNullOrWhiteSpace(theme.Path))
                {
                    // theme.Path is expected to be the folder path where the manifest was read from
                    targetDir = Path.GetFullPath(theme.Path);
                }
                else
                {
                    // fallback to folder named by id under themes root
                    targetDir = Path.GetFullPath(Path.Combine(themesRoot, themeId));
                }

                // Safety check: ensure targetDir is inside themesRoot to prevent deleting outside the themes folder
                var targetDirNormalized = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!targetDirNormalized.StartsWith(themesRootFull, StringComparison.OrdinalIgnoreCase))
                {
                    // refuse to delete if path escapes the themes root
                    LogHelper.WriteLogToFile($"拒绝删除浮动栏主题（路径不在 FloatingBarThemes 下）: {targetDir}", LogHelper.LogType.Warning);
                }
                else
                {
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, true);
                    }
                }
                // if deleted theme was applied, revert to default
                if (string.Equals(MainWindow.Settings?.Appearance?.FloatingBarThemeId, themeId, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyTheme(DefaultThemeId);
                }
                LoadThemes();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除浮动栏主题失败: {themeId}, {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

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
                IsBuiltIn = true,
                IsApplied = string.Equals(MainWindow.Settings?.Appearance?.FloatingBarThemeId ?? DefaultThemeId, DefaultThemeId, StringComparison.OrdinalIgnoreCase)
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
                    manifest.IsApplied = string.Equals(MainWindow.Settings?.Appearance?.FloatingBarThemeId ?? DefaultThemeId, manifest.Id, StringComparison.OrdinalIgnoreCase);
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
                // update IsApplied flags so UI updates without needing full reload
                foreach (var t in Themes)
                {
                    t.IsApplied = string.Equals(t.Id, theme.Id, StringComparison.OrdinalIgnoreCase);
                }
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
