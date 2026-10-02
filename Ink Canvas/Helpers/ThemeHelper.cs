using Wpf.Ui.Appearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
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
                ApplicationAccentColorManager.Apply(accentColor, ApplicationThemeManager.GetAppTheme());
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

        /// <summary>
        /// 依据设置计算当前应生效的主题（0=浅色，1=深色，其它=跟随系统）。
        /// WPF-UI 的主题是应用级的，返回 WPF-UI 的 ApplicationTheme。
        /// </summary>
        public static ApplicationTheme GetEffectiveTheme(Settings settings)
        {
            if (settings.Appearance.Theme == 0)
                return ApplicationTheme.Light;
            if (settings.Appearance.Theme == 1)
                return ApplicationTheme.Dark;

            return IsSystemThemeLight() ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }

        /// <summary>
        /// 应用主题。注意：WPF-UI 没有 iNKORE 那样逐元素的 RequestedTheme，主题统一作用于
        /// 整个应用；element 参数仅为保持既有调用签名而保留。
        /// </summary>
        public static void ApplyTheme(FrameworkElement element, Settings settings)
        {
            if (settings == null) return;
            try
            {
                ApplyApplicationTheme(GetEffectiveTheme(settings));
                ApplySystemAccentColor();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void ApplyTheme(FrameworkElement element, Settings settings, Action<string> onThemeApplied)
        {
            if (settings == null) return;
            try
            {
                var theme = GetEffectiveTheme(settings);
                ApplyApplicationTheme(theme);
                ApplySystemAccentColor();
                onThemeApplied?.Invoke(theme == ApplicationTheme.Dark ? "Dark" : "Light");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用应用级主题（带 MainWindow 保护）。
        ///
        /// WPF-UI 的 ApplicationThemeManager.Apply 会对 Application.Current.MainWindow 执行
        /// WindowBackgroundManager.UpdateBackground：内部先调用 WindowBackdrop.RemoveBackdrop，
        /// 而 RestoreContentBackground 会把窗口 Background 涂成 ApplicationBackgroundBrush（不透明）
        /// 并把合成背景重置为 SystemColors.WindowColor；backdrop 非 None 时随后会被 RemoveBackground
        /// 改回透明，None 时则保持不透明——ICC 主窗口是无边框透明窗口，一旦被涂上不透明底色就会出现
        /// 全屏纯色遮挡。这里在切换期间临时摘掉 MainWindow 引用，让主题字典/强调色正常更新但不动主窗口，
        /// 并在切换后对主窗口重新断言透明背景作为兜底。
        /// </summary>
        public static void ApplyApplicationTheme(ApplicationTheme theme, bool updateAccent = false)
        {
            var application = Application.Current;
            var mainWindow = application?.MainWindow;

            try
            {
                if (application != null && mainWindow != null)
                {
                    application.MainWindow = null;
                }

                ApplicationThemeManager.Apply(theme, WindowBackdropType.None, updateAccent: updateAccent);
            }
            finally
            {
                if (application != null && mainWindow != null)
                {
                    application.MainWindow = mainWindow;

                    // 兜底恢复主窗口的透明背景，避免不透明底色残留成全屏遮挡
                    Wpf.Ui.Controls.WindowBackdrop.RemoveBackground(mainWindow);
                }

                // DWM 深色模式是原生窗口属性，不会跟随主题字典更新，
                // 切换完成后统一给已登记的浮窗重写一次。
                WindowBackdropHelper.SyncAllWindowsDarkMode();
            }
        }
    }
}
