using Ink_Canvas.Properties;
using System.Globalization;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    public static class LocalizationHelper
    {
        public static CultureInfo CurrentCulture
        {
            get => Thread.CurrentThread.CurrentUICulture;
            set
            {
                if (value == null) return;
                Thread.CurrentThread.CurrentUICulture = value;
                Strings.Culture = value;
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
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CurrentCulture = culture;
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
    }
}