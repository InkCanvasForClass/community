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
                app.Exit += (_, _) => CleanupPendingDeletes();
            }
            foreach (Window window in app.Windows)
                window.FontFamily = fontFamily;
        }

        private static readonly List<string> _pendingDeleteFiles = new List<string>();

        public static void RegisterPendingDelete(string entry)
        {
            if (!IsImportedEntry(entry)) return;
            var fileName = entry.Split('|')[0];
            if (!_pendingDeleteFiles.Contains(fileName)) _pendingDeleteFiles.Add(fileName);
        }

        public static void CleanupPendingDeletes()
        {
            foreach (var fileName in _pendingDeleteFiles.ToList())
            {
                try
                {
                    var path = Path.Combine(CustomFontsDirectory, fileName);
                    if (File.Exists(path))
                        ProcessProtectionManager.WithWriteAccess(path, () => File.Delete(path));
                    _pendingDeleteFiles.Remove(fileName);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"延迟删除导入字体失败: {fileName}, {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        public static void CleanupOrphanedFonts(IEnumerable<string> importedEntries)
        {
            try
            {
                if (!Directory.Exists(CustomFontsDirectory)) return;
                var referenced = new HashSet<string>(
                    (importedEntries ?? Enumerable.Empty<string>())
                    .Where(IsImportedEntry)
                    .Select(entry => entry.Split('|')[0]),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(CustomFontsDirectory))
                {
                    if (referenced.Contains(Path.GetFileName(file))) continue;
                    try
                    {
                        var path = file;
                        ProcessProtectionManager.WithWriteAccess(path, () => File.Delete(path));
                        LogHelper.WriteLogToFile($"清理无引用的导入字体文件: {file}", LogHelper.LogType.Trace);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"清理无引用的导入字体文件失败: {file}, {ex.Message}", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"扫描导入字体目录失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public static List<string> ImportFont(string sourcePath)
        {
            try
            {
                ProcessProtectionManager.WithWriteAccess(CustomFontsDirectory,
                    () => Directory.CreateDirectory(CustomFontsDirectory));
                var fileName = Path.GetFileName(sourcePath);
                if (fileName.IndexOf('#') >= 0 || fileName.IndexOf('|') >= 0)
                    fileName = fileName.Replace('#', '_').Replace('|', '_');
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
                var families = Fonts.GetFontFamilies(new Uri(dir, UriKind.Absolute), "./" + fileName);
                var entries = new List<string>();
                foreach (var family in families)
                {
                    var familyName = PickLocalizedFamilyName(family);
                    if (string.IsNullOrWhiteSpace(familyName))
                        familyName = family.Source.Split('#').Last();
                    if (string.IsNullOrWhiteSpace(familyName)) continue;
                    var entry = fileName + "|" + familyName;
                    if (!entries.Contains(entry)) entries.Add(entry);
                }

                if (entries.Count == 0)
                {
                    LogHelper.WriteLogToFile($"导入字体失败，未在文件中解析到字体族: {sourcePath}", LogHelper.LogType.Warning);
                    return null;
                }
                return entries;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"导入字体失败: {sourcePath}, {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        public static bool RemoveImportedFont(string entry, IEnumerable<string> remainingEntries)
        {
            if (!IsImportedEntry(entry)) return true;
            var fileName = entry.Split('|')[0];
            var stillReferenced = (remainingEntries ?? Enumerable.Empty<string>())
                .Where(IsImportedEntry)
                .Any(other => string.Equals(other.Split('|')[0], fileName, StringComparison.OrdinalIgnoreCase));
            if (stillReferenced) return true;
            try
            {
                var path = Path.Combine(CustomFontsDirectory, fileName);
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
