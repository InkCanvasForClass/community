using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public static class GlobalFontHelper
    {
        public const string DefaultUIFontFamily = "Segoe UI";
        public const string MonoUIFontFamily = "Consolas";
        public const string MonoUIFontFamilyChain = "Consolas, Cascadia Mono, Courier New";
        public const string EmbeddedWenKaiFontFamily = "pack://application:,,,/Resources/Fonts/#LXGW WenKai TC";

        public static readonly FontFamily MonoFontFamily = new FontFamily(MonoUIFontFamily);
        public static readonly FontFamily MonoFontFamilyChain = new FontFamily(MonoUIFontFamilyChain);

        public static FontFamily CurrentUIFontFamily { get; private set; } = new FontFamily(DefaultUIFontFamily);

        private static bool _windowHookRegistered;

        public static string CustomFontsDirectory =>
            Path.Combine(!string.IsNullOrEmpty(App.RootPath)
                ? App.RootPath
                : AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "Fonts");

        public static IReadOnlyList<KeyValuePair<string, string>> PresetFonts { get; } = new[]
        {
            new KeyValuePair<string, string>("Microsoft YaHei UI", "Microsoft YaHei UI"),
            new KeyValuePair<string, string>("等线 DengXian", "DengXian"),
            new KeyValuePair<string, string>("霞鹜文楷 LXGW WenKai TC", EmbeddedWenKaiFontFamily),
            new KeyValuePair<string, string>("宋体 SimSun", "SimSun"),
            new KeyValuePair<string, string>("楷体 KaiTi", "KaiTi"),
        };

        public static bool IsImportedEntry(string setting) =>
            !string.IsNullOrWhiteSpace(setting) && setting.Contains('|');

        public static string GetImportedDisplayName(string entry)
        {
            var parts = (entry ?? string.Empty).Split('|');
            return parts.Length == 2 ? parts[1] : entry;
        }

        public static FontFamily Resolve(string setting)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(setting))
                    return new FontFamily(DefaultUIFontFamily);

                if (IsImportedEntry(setting))
                {
                    var parts = setting.Split('|');
                    var path = Path.Combine(CustomFontsDirectory, parts[0]);
                    if (File.Exists(path))
                    {
                        var dir = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
                        return new FontFamily(new Uri(dir, UriKind.Absolute), "./" + parts[0] + "#" + parts[1]);
                    }
                }
                else
                {
                    return new FontFamily(setting);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"解析界面字体失败: {setting}, {ex.Message}", LogHelper.LogType.Warning);
            }
            return new FontFamily(DefaultUIFontFamily);
        }

        public static void Apply(string setting)
        {
            var fontFamily = Resolve(setting);
            CurrentUIFontFamily = fontFamily;
            var app = Application.Current;
            if (app == null) return;
            app.Resources["GlobalUIFontFamily"] = fontFamily;
            app.Resources["ContentControlThemeFontFamily"] = fontFamily;

            if (!_windowHookRegistered)
            {
                _windowHookRegistered = true;
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler((sender, _) =>
                    {
                        if (sender is Window window) window.FontFamily = CurrentUIFontFamily;
                    }));
            }
            foreach (Window window in app.Windows)
                window.FontFamily = fontFamily;
        }

        public static string ImportFont(string sourcePath)
        {
            try
            {
                ProcessProtectionManager.WithWriteAccess(CustomFontsDirectory,
                    () => Directory.CreateDirectory(CustomFontsDirectory));

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(CustomFontsDirectory, fileName);

                if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!File.Exists(destPath) || !FilesEqual(sourcePath, destPath))
                    {
                        if (File.Exists(destPath))
                        {
                            var name = Path.GetFileNameWithoutExtension(fileName);
                            var ext = Path.GetExtension(fileName);
                            for (var i = 1; i < 100; i++)
                            {
                                var candidate = $"{name}_{i}{ext}";
                                var candidatePath = Path.Combine(CustomFontsDirectory, candidate);
                                if (!File.Exists(candidatePath) || FilesEqual(sourcePath, candidatePath))
                                {
                                    fileName = candidate;
                                    destPath = candidatePath;
                                    break;
                                }
                            }
                        }
                        if (!File.Exists(destPath))
                        {
                            var src = sourcePath;
                            var dst = destPath;
                            ProcessProtectionManager.WithWriteAccess(dst, () => File.Copy(src, dst, true));
                        }
                    }
                }

                var dir = Path.GetDirectoryName(destPath) + Path.DirectorySeparatorChar;
                var first = Fonts.GetFontFamilies(new Uri(dir, UriKind.Absolute), "./" + fileName).FirstOrDefault();
                if (first == null)
                {
                    LogHelper.WriteLogToFile($"导入字体失败，未在文件中解析到字体族: {sourcePath}", LogHelper.LogType.Warning);
                    return null;
                }

                var familyName = PickLocalizedFamilyName(first);
                if (string.IsNullOrWhiteSpace(familyName))
                    familyName = first.Source.Split('#').Last();
                if (string.IsNullOrWhiteSpace(familyName))
                {
                    LogHelper.WriteLogToFile($"导入字体失败，无法确定字体族名: {sourcePath}", LogHelper.LogType.Warning);
                    return null;
                }

                return fileName + "|" + familyName;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导入字体失败: {sourcePath}, {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static bool RemoveImportedFont(string entry)
        {
            if (!IsImportedEntry(entry)) return false;
            try
            {
                var path = Path.Combine(CustomFontsDirectory, entry.Split('|')[0]);
                if (File.Exists(path))
                    ProcessProtectionManager.WithWriteAccess(path, () => File.Delete(path));
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除导入字体失败: {entry}, {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        private static string PickLocalizedFamilyName(FontFamily fontFamily)
        {
            var names = fontFamily.FamilyNames;
            if (names == null || names.Count == 0) return null;

            var current = CultureInfo.CurrentUICulture;
            foreach (var lang in new[] { current?.Name, current?.TwoLetterISOLanguageName, "zh-CN", "zh", "en-US", "en" })
            {
                if (string.IsNullOrEmpty(lang)) continue;
                foreach (var pair in names)
                {
                    if (string.Equals(pair.Key.IetfLanguageTag, lang, StringComparison.OrdinalIgnoreCase))
                        return pair.Value;
                }
            }
            foreach (var pair in names) return pair.Value;
            return null;
        }

        private static bool FilesEqual(string pathA, string pathB)
        {
            try
            {
                var infoA = new FileInfo(pathA);
                var infoB = new FileInfo(pathB);
                if (infoA.Length != infoB.Length) return false;

                using var streamA = File.OpenRead(pathA);
                using var streamB = File.OpenRead(pathB);
                var bufferA = new byte[8192];
                var bufferB = new byte[8192];
                int read;
                while ((read = streamA.Read(bufferA, 0, bufferA.Length)) > 0)
                {
                    if (streamB.Read(bufferB, 0, bufferB.Length) != read) return false;
                    for (var i = 0; i < read; i++)
                        if (bufferA[i] != bufferB[i]) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
