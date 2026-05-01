using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    public static class ThemeHelper
    {
        public static bool IsSystemThemeLight()
        {
            try
            {
                var registryKey = Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (themeKey != null)
                {
                    var value = themeKey.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        bool result = (int)value == 1;
                        themeKey.Close();
                        return result;
                    }
                    themeKey.Close();
                }
            }
            catch
            {
            }
            return true;
        }

        public static bool IsSystemThemeLightLegacy()
        {
            try
            {
                var registryKey = Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (themeKey != null)
                {
                    int keyValue = (int)themeKey.GetValue("SystemUsesLightTheme");
                    themeKey.Close();
                    return keyValue == 1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            return false;
        }

        public static ElementTheme GetEffectiveTheme(Settings settings)
        {
            if (settings.Appearance.Theme == 0)
                return ElementTheme.Light;
            if (settings.Appearance.Theme == 1)
                return ElementTheme.Dark;

            return IsSystemThemeLight() ? ElementTheme.Light : ElementTheme.Dark;
        }

        public static void ApplyTheme(FrameworkElement element, Settings settings)
        {
            if (element == null || settings == null) return;
            try
            {
                ThemeManager.SetRequestedTheme(element, GetEffectiveTheme(settings));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void ApplyTheme(FrameworkElement element, Settings settings, Action<string> onThemeApplied)
        {
            if (element == null || settings == null) return;
            try
            {
                var theme = GetEffectiveTheme(settings);
                ThemeManager.SetRequestedTheme(element, theme);
                onThemeApplied?.Invoke(theme == ElementTheme.Dark ? "Dark" : "Light");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
