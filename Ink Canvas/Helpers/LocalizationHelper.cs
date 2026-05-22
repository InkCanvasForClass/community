using Ink_Canvas.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    public static class LocalizationHelper
    {
        private static readonly string[] CustomCultureNames = { "zh-ME" };
        private static readonly Dictionary<string, Dictionary<string, string>> _zhMeCache = new();
        private static readonly Dictionary<string, ResourceManager> _originalResourceManagers = new();
        private static bool _zhMeActive;

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
            }
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
            foreach (var cn in CustomCultureNames)
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
                var clone = (CultureInfo)CultureInfo.GetCultureInfo("zh-CN").Clone();
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
            var isZhMe = culture.Name.StartsWith("zh-ME", StringComparison.OrdinalIgnoreCase);
            var asm = Assembly.GetExecutingAssembly();

            foreach (var type in asm.GetTypes())
            {
                if (type.Namespace != "Ink_Canvas.Properties" || !type.Name.EndsWith("Strings"))
                    continue;

                var prop = type.GetProperty("Culture", BindingFlags.Public | BindingFlags.Static);
                if (prop != null && prop.CanWrite)
                    prop.SetValue(null, culture);

                if (isZhMe)
                    InstallZhMeResourceManager(type, asm);
                else
                    RestoreOriginalResourceManager(type);
            }

            _zhMeActive = isZhMe;
        }

        private static void InstallZhMeResourceManager(Type type, Assembly asm)
        {
            var resourceManField = type.GetField("_resourceMan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (resourceManField == null) return;

            var current = (ResourceManager)resourceManField.GetValue(null);

            if (_originalResourceManagers.ContainsKey(type.Name))
                return;

            if (current != null)
                _originalResourceManagers[type.Name] = current;

            var zhMeStrings = LoadZhMeResource(asm, type.Name);
            var customManager = new ZhMeResourceManager(current, zhMeStrings);
            resourceManField.SetValue(null, customManager);
        }

        private static void RestoreOriginalResourceManager(Type type)
        {
            if (!_originalResourceManagers.TryGetValue(type.Name, out var original))
                return;

            var resourceManField = type.GetField("_resourceMan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (resourceManField == null) return;

            resourceManField.SetValue(null, original);
            _originalResourceManagers.Remove(type.Name);
        }

        private static Dictionary<string, string> LoadZhMeResource(Assembly asm, string className)
        {
            if (_zhMeCache.TryGetValue(className, out var cached))
                return cached;

            var result = new Dictionary<string, string>();
            var resourceName = $"Ink_Canvas.Properties.{className}.zh-ME.resources";

            try
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
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

            _zhMeCache[className] = result;
            return result;
        }

        private class ZhMeResourceManager : ResourceManager
        {
            private readonly ResourceManager _fallback;
            private readonly Dictionary<string, string> _zhMeStrings;

            public ZhMeResourceManager(ResourceManager fallback, Dictionary<string, string> zhMeStrings)
            {
                _fallback = fallback;
                _zhMeStrings = zhMeStrings;
            }

            public override string GetString(string name, CultureInfo culture)
            {
                if (culture != null && culture.Name.StartsWith("zh-ME", StringComparison.OrdinalIgnoreCase))
                {
                    if (_zhMeStrings.TryGetValue(name, out var value))
                        return value;
                }
                return _fallback.GetString(name, culture);
            }

            public override string GetString(string name)
            {
                if (_zhMeStrings.TryGetValue(name, out var value))
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
