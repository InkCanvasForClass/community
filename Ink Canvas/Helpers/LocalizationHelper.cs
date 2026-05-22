using Ink_Canvas.Properties;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    public static class LocalizationHelper
    {
        private static readonly string[] CustomCultureNames = { "zh-ME" };

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
            var asm = Assembly.GetExecutingAssembly();
            foreach (var type in asm.GetTypes())
            {
                if (type.Namespace == "Ink_Canvas.Properties" && type.Name.EndsWith("Strings"))
                {
                    var prop = type.GetProperty("Culture", BindingFlags.Public | BindingFlags.Static);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(null, culture);
                    }
                }
            }
        }
    }
}
