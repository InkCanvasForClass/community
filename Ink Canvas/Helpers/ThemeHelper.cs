using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public static class ThemeHelper
    {
        static ThemeHelper()
        {
            try
            {
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.General)
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            ApplySystemAccentColor();
                        }));
                    }
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Theme] 订阅系统外观偏好变化事件失败，系统深浅色/强调色将不再自动跟随: {ex.Message}", LogHelper.LogType.Info);
            }
        }

        /// <summary>
        /// 获取 Windows 系统的个性化强调色 (System Accent Color)。
        /// </summary>
        public static Color GetSystemAccentColor()
        {
            try
            {
                // 1. 优先读取 DWM AccentColor (ABGR 格式: 0xAABBGGRR)
                using (var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (dwmKey != null)
                    {
                        var val = dwmKey.GetValue("AccentColor");
                        if (val is int abgr)
                        {
                            byte a = (byte)((abgr >> 24) & 0xFF);
                            byte b = (byte)((abgr >> 16) & 0xFF);
                            byte g = (byte)((abgr >> 8) & 0xFF);
                            byte r = (byte)(abgr & 0xFF);
                            if (a == 0) a = 255;
                            return Color.FromArgb(a, r, g, b);
                        }

                        var colVal = dwmKey.GetValue("ColorizationColor");
                        if (colVal is int argb)
                        {
                            byte a = (byte)((argb >> 24) & 0xFF);
                            byte r = (byte)((argb >> 16) & 0xFF);
                            byte g = (byte)((argb >> 8) & 0xFF);
                            byte b = (byte)(argb & 0xFF);
                            if (a == 0) a = 255;
                            return Color.FromArgb(a, r, g, b);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Theme] 读取 DWM 强调色注册表值失败，改用下一级回退来源: {ex.Message}", LogHelper.LogType.Info);
            }

            try
            {
                // 2. 尝试读取 Explorer Accent
                using (var expKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    if (expKey != null)
                    {
                        var val = expKey.GetValue("AccentColorMenu");
                        if (val is int abgr)
                        {
                            byte a = (byte)((abgr >> 24) & 0xFF);
                            byte b = (byte)((abgr >> 16) & 0xFF);
                            byte g = (byte)((abgr >> 8) & 0xFF);
                            byte r = (byte)(abgr & 0xFF);
                            if (a == 0) a = 255;
                            return Color.FromArgb(a, r, g, b);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Theme] 读取资源管理器 Accent 注册表值失败，改用窗口玻璃色作为最终来源: {ex.Message}", LogHelper.LogType.Info);
            }

            try
            {
                return SystemParameters.WindowGlassColor;
            }
            catch
            {
                return Color.FromRgb(0, 120, 215); // Fallback Fluent Blue
            }
        }

        /// <summary>
        /// 将 Windows 系统强调色应用到 ModernWPF 全局主题管理器。
        /// 这将使所有 AccentButtonStyle（强调按钮、弹窗确认按钮等）自动使用系统强调色。
        /// </summary>
        public static void ApplySystemAccentColor()
        {
            try
            {
                var accentColor = GetSystemAccentColor();
                ThemeManager.Current.AccentColor = accentColor;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用系统强调色失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Theme] 读取系统深浅色注册表值(AppsUseLightTheme)失败，按浅色处理: {ex.Message}", LogHelper.LogType.Info);
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
                ApplySystemAccentColor();
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
                ApplySystemAccentColor();
                onThemeApplied?.Invoke(theme == ElementTheme.Dark ? "Dark" : "Light");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
