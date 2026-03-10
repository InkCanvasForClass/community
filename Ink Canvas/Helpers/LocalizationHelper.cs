using Ink_Canvas.Properties;
using System.Globalization;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// i18n 本地化辅助：设置/获取当前 UI 语言，便于后续从配置切换语言。
    /// </summary>
    public static class LocalizationHelper
    {
        /// <summary>
        /// 当前 UI 语言（如 "zh-CN", "en-US"）。未设置时使用系统当前 UI 语言。
        /// </summary>
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

        /// <summary>
        /// 使用指定语言名称设置当前 UI 语言（如 "zh-CN", "en-US"）。
        /// 若名称无效则保持当前语言不变。
        /// </summary>
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

        /// <summary>
        /// 获取本地化字符串。优先使用强类型属性，未知键时用此方法。
        /// </summary>
        public static string GetString(string key)
        {
            return Strings.GetString(key);
        }
    }
}
