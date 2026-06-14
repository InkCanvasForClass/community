using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Xml.Linq;

namespace Ink_Canvas.Helpers
{
    public static class LocalizationHelper
    {
        private static readonly string[] EmbeddedOnlyCultures = { "en-US", "zh-ME" };
        private static readonly Dictionary<(string className, string cultureName), Dictionary<string, string>> _embeddedCache = new();
        private static readonly Dictionary<string, ResourceManager> _originalResourceManagers = new();

        public static CultureInfo CurrentCulture
        {
            get => Thread.CurrentThread.CurrentUICulture;
            set
            {
                if (value == null) return;
                Thread.CurrentThread.CurrentUICulture = value;
                Thread.CurrentThread.CurrentCulture = value;
                Strings.Culture = value;
                SetAllResourceCultures(value);
                SyncCommonResources();
            }
        }

        private static ResourceManager _originalModernStringsRM;
        private static bool _modernStringsPatched;

        internal static void SyncCommonResources()
        {
            try
            {
                var onText = CommonStrings.Common_On;
                var offText = CommonStrings.Common_Off;

                if (System.Windows.Application.Current?.Resources != null)
                {
                    System.Windows.Application.Current.Resources["Common_On"] = onText;
                    System.Windows.Application.Current.Resources["Common_Off"] = offText;
                }

                // 替换 iNKORE.UI.WPF.Modern.Strings 的 ResourceManager，让 ToggleSwitch
                // 构造函数中的 SetCurrentValue 直接拿到正确的本地化文本
                PatchModernStrings(onText, offText);

                // 延迟更新所有已加载的 ToggleSwitch
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAllToggleSwitches(onText, offText);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch { }
        }

        /// <summary>
        /// 反射替换 iNKORE.UI.WPF.Modern.Strings 的 ResourceManager，
        /// 注入 ToggleSwitchOn/ToggleSwitchOff 的本地化翻译。
        /// </summary>
        private static void PatchModernStrings(string onText, string offText)
        {
            try
            {
                var stringsType = typeof(iNKORE.UI.WPF.Modern.ThemeManager).Assembly
                    .GetType("iNKORE.UI.WPF.Modern.Strings");
                if (stringsType == null) return;

                var resourceManField = stringsType.GetField("resourceMan",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (resourceManField == null) return;

                // 触发 ResourceManager 的初始化（如果尚未初始化）
                var rmProp = stringsType.GetProperty("ResourceManager",
                    BindingFlags.Public | BindingFlags.Static);
                rmProp?.GetValue(null);

                var current = (ResourceManager)resourceManField.GetValue(null);

                if (!_modernStringsPatched)
                {
                    _originalModernStringsRM = current;
                    _modernStringsPatched = true;
                }

                var toggleKeys = new Dictionary<string, string>
                {
                    { "ToggleSwitchOn", onText },
                    { "ToggleSwitchOff", offText },
                };

                var patched = new ToggleSwitchResourceManager(
                    _originalModernStringsRM ?? current, toggleKeys);
                resourceManField.SetValue(null, patched);
            }
            catch { }
        }

        /// <summary>
        /// 自定义 ResourceManager，拦截 ToggleSwitchOn/ToggleSwitchOff 的 GetString 调用，
        /// 返回本地化文本，其余转发给原始 ResourceManager。
        /// </summary>
        private class ToggleSwitchResourceManager : ResourceManager
        {
            private readonly ResourceManager _fallback;
            private readonly Dictionary<string, string> _overrides;

            public ToggleSwitchResourceManager(ResourceManager fallback, Dictionary<string, string> overrides)
            {
                _fallback = fallback;
                _overrides = overrides;
            }

            public override string GetString(string name)
            {
                if (_overrides.TryGetValue(name, out var value))
                    return value;
                return _fallback.GetString(name);
            }

            public override string GetString(string name, CultureInfo culture)
            {
                if (_overrides.TryGetValue(name, out var value))
                    return value;
                return _fallback.GetString(name, culture);
            }

            public override object GetObject(string name) => _fallback.GetObject(name);
            public override object GetObject(string name, CultureInfo culture) => _fallback.GetObject(name, culture);
            public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
                => _fallback.GetResourceSet(culture, createIfNotExists, tryParents);
        }

        /// <summary>
        /// 遍历所有窗口的逻辑树，更新已创建的 ToggleSwitch。
        /// 保留 XAML 中设置了 OnContent="" / OffContent="" 的开关不变。
        /// </summary>
        private static void UpdateAllToggleSwitches(string onText, string offText)
        {
            try
            {
                if (System.Windows.Application.Current == null) return;
                foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
                {
                    UpdateToggleSwitchesInLogicalTree(window, onText, offText);
                }
            }
            catch { }
        }

        private static void UpdateToggleSwitchesInLogicalTree(DependencyObject parent, string onText, string offText)
        {
            if (parent == null) return;
            try
            {
                if (parent is ToggleSwitch ts)
                {
                    UpdateSingleToggleSwitch(ts, onText, offText);
                }
                var children = System.Windows.LogicalTreeHelper.GetChildren(parent);
                foreach (var child in children)
                {
                    if (child is DependencyObject depChild)
                    {
                        UpdateToggleSwitchesInLogicalTree(depChild, onText, offText);
                    }
                }
            }
            catch { }
        }

        private static void UpdateSingleToggleSwitch(ToggleSwitch ts, string onText, string offText)
        {
            try
            {
                var onLocal = ts.ReadLocalValue(ToggleSwitch.OnContentProperty);
                var offLocal = ts.ReadLocalValue(ToggleSwitch.OffContentProperty);

                // 保留 XAML 中显式设为 "" 的
                if (!(onLocal is string onStr && onStr == ""))
                {
                    ts.ClearValue(ToggleSwitch.OnContentProperty);
                    ts.SetCurrentValue(ToggleSwitch.OnContentProperty, onText);
                }

                if (!(offLocal is string offStr && offStr == ""))
                {
                    ts.ClearValue(ToggleSwitch.OffContentProperty);
                    ts.SetCurrentValue(ToggleSwitch.OffContentProperty, offText);
                }
            }
            catch { }
        }

        /// <summary>
        /// 为指定窗口中的 ToggleSwitch 绑定本地化文本。
        /// 用于设置窗口等后打开的窗口。
        /// </summary>
        internal static void BindToggleSwitchesInWindow(System.Windows.Window window)
        {
            try
            {
                UpdateToggleSwitchesInLogicalTree(window,
                    CommonStrings.Common_On, CommonStrings.Common_Off);
            }
            catch { }
        }

        public static bool TrySetCulture(string cultureName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cultureName))
                {
                    CurrentCulture = CultureInfo.InstalledUICulture;
                    return true;
                }
                if (IsCustomCulture(cultureName))
                {
                    var culture = CreateCustomCulture(cultureName);
                    CurrentCulture = culture;
                    return true;
                }
                var stdCulture = CultureInfo.GetCultureInfo(cultureName);
                CurrentCulture = stdCulture;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetString(string key)
        {
            return Strings.GetString(key);
        }

        private static bool IsCustomCulture(string name)
        {
            foreach (var cn in EmbeddedOnlyCultures)
                if (string.Equals(cn, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static CultureInfo CreateCustomCulture(string name)
        {
            try
            {
                return new CultureInfo(name);
            }
            catch { }

            try
            {
                var parent = name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
                var clone = (CultureInfo)CultureInfo.GetCultureInfo(parent).Clone();
                var dataField = typeof(CultureInfo).GetField("_cultureData",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (dataField != null)
                {
                    var data = dataField.GetValue(clone);
                    var nameField = data.GetType().GetField("_sName",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    nameField?.SetValue(data, name);
                }
                var directNameField = typeof(CultureInfo).GetField("_name",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                directNameField?.SetValue(clone, name);
                return clone;
            }
            catch { }

            return CultureInfo.GetCultureInfo("zh-CN");
        }

        private static void SetAllResourceCultures(CultureInfo culture)
        {
            var cultureName = culture.Name;
            var isEmbeddedOnly = IsEmbeddedOnlyCulture(cultureName);
            var asm = Assembly.GetExecutingAssembly();

            foreach (var type in asm.GetTypes())
            {
                if (type.Namespace != "Ink_Canvas.Properties" || !type.Name.EndsWith("Strings"))
                    continue;

                var prop = type.GetProperty("Culture", BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.CanWrite)
                    prop.SetValue(null, culture);

                if (isEmbeddedOnly)
                    InstallEmbeddedResourceManager(type, asm, cultureName);
                else
                    RestoreOriginalResourceManager(type);
            }
        }

        private static bool IsEmbeddedOnlyCulture(string name)
        {
            foreach (var cn in EmbeddedOnlyCultures)
                if (string.Equals(cn, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static void InstallEmbeddedResourceManager(Type type, Assembly asm, string cultureName)
        {
            var resourceManField = type.GetField("_resourceMan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (resourceManField == null) return;

            var resourceManProp = type.GetProperty("ResourceManager",
                BindingFlags.Public | BindingFlags.Static);
            if (resourceManProp != null)
                resourceManProp.GetValue(null);

            var current = (ResourceManager)resourceManField.GetValue(null);

            if (!_originalResourceManagers.ContainsKey(type.Name))
            {
                if (current != null && !(current is EmbeddedResourceManager))
                    _originalResourceManagers[type.Name] = current;
            }

            var embeddedStrings = LoadEmbeddedResource(asm, type.Name, cultureName);
            var original = _originalResourceManagers.TryGetValue(type.Name, out var orig) ? orig : current;
            var customManager = new EmbeddedResourceManager(original, cultureName, embeddedStrings);
            resourceManField.SetValue(null, customManager);
        }

        private static void RestoreOriginalResourceManager(Type type)
        {
            var resourceManField = type.GetField("_resourceMan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (resourceManField == null) return;

            if (_originalResourceManagers.TryGetValue(type.Name, out var original))
            {
                resourceManField.SetValue(null, original);
                _originalResourceManagers.Remove(type.Name);
                return;
            }

            var current = resourceManField.GetValue(null) as ResourceManager;
            if (current is EmbeddedResourceManager emb)
            {
                var fallbackField = typeof(EmbeddedResourceManager).GetField("_fallback",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (fallbackField?.GetValue(emb) is ResourceManager fallback)
                    resourceManField.SetValue(null, fallback);
            }
        }

        private static Dictionary<string, string> LoadEmbeddedResource(Assembly asm, string className, string cultureName)
        {
            var cacheKey = (className, cultureName.ToLowerInvariant());
            if (_embeddedCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<string, string>();

            var resName = $"Ink_Canvas.Properties.{className}.{cultureName}.resources";
            try
            {
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using var reader = new ResourceReader(stream);
                    foreach (DictionaryEntry entry in reader)
                    {
                        if (entry.Key is string key && entry.Value is string value)
                            result[key] = value;
                    }
                }
            }
            catch { }

            if (result.Count > 0)
            {
                _embeddedCache[cacheKey] = result;
                return result;
            }

            var resxName = $"Ink_Canvas.Properties.{className}.{cultureName}.resx";
            try
            {
                using var stream = asm.GetManifestResourceStream(resxName);
                if (stream != null)
                {
                    ParseResx(stream, result);
                }
            }
            catch { }

            if (result.Count > 0)
            {
                _embeddedCache[cacheKey] = result;
                return result;
            }

            try
            {
                var exeDir = AppContext.BaseDirectory;
                var resxPath = Path.Combine(exeDir, "Properties", $"{className}.{cultureName}.resx");
                if (!File.Exists(resxPath))
                    resxPath = Path.Combine(exeDir, $"{className}.{cultureName}.resx");
                if (File.Exists(resxPath))
                {
                    using var fs = File.OpenRead(resxPath);
                    ParseResx(fs, result);
                }
            }
            catch { }

            _embeddedCache[cacheKey] = result;
            return result;
        }

        private static void ParseResx(Stream stream, Dictionary<string, string> result)
        {
            var doc = XDocument.Load(stream);
            var ns = doc.Root.Name.Namespace;
            foreach (var data in doc.Root.Elements(ns + "data"))
            {
                var nameAttr = data.Attribute("name")?.Value;
                var valueElem = data.Element(ns + "value")?.Value;
                if (nameAttr != null && valueElem != null)
                    result[nameAttr] = valueElem;
            }
        }

        private class EmbeddedResourceManager : ResourceManager
        {
            private readonly ResourceManager _fallback;
            private readonly string _cultureName;
            private readonly Dictionary<string, string> _strings;

            public EmbeddedResourceManager(ResourceManager fallback, string cultureName, Dictionary<string, string> strings)
            {
                _fallback = fallback;
                _cultureName = cultureName;
                _strings = strings;
            }

            public override string GetString(string name, CultureInfo culture)
            {
                if (culture != null && string.Equals(culture.Name, _cultureName, StringComparison.OrdinalIgnoreCase))
                {
                    if (_strings.TryGetValue(name, out var value))
                        return value;
                }
                return _fallback.GetString(name, culture);
            }

            public override string GetString(string name)
            {
                if (_strings.TryGetValue(name, out var value))
                    return value;
                return _fallback.GetString(name);
            }

            public override object GetObject(string name, CultureInfo culture)
            {
                return _fallback.GetObject(name, culture);
            }

            public override object GetObject(string name)
            {
                return _fallback.GetObject(name);
            }

            public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
            {
                return _fallback.GetResourceSet(culture, createIfNotExists, tryParents);
            }
        }
    }
}
