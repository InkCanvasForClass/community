using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IThemeService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.ThemeHelper"/>。
    /// </summary>
    internal sealed class ThemeService : IThemeService
    {
        public bool IsSystemThemeLight()
        {
            try { return Ink_Canvas.Helpers.ThemeHelper.IsSystemThemeLight(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ThemeService.IsSystemThemeLight failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return true;
            }
        }

        public PluginTheme GetEffectiveTheme()
        {
            try
            {
                var settings = MainWindow.Settings;
                var theme = settings == null
                    ? Ink_Canvas.Helpers.ThemeHelper.IsSystemThemeLight() ? iNKORE.UI.WPF.Modern.ElementTheme.Light : iNKORE.UI.WPF.Modern.ElementTheme.Dark
                    : Ink_Canvas.Helpers.ThemeHelper.GetEffectiveTheme(settings);
                return theme == iNKORE.UI.WPF.Modern.ElementTheme.Dark ? PluginTheme.Dark : PluginTheme.Light;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ThemeService.GetEffectiveTheme failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return PluginTheme.Light;
            }
        }

        public void ApplyThemeToElement(FrameworkElement element)
        {
            try
            {
                var settings = MainWindow.Settings;
                if (settings != null) Ink_Canvas.Helpers.ThemeHelper.ApplyTheme(element, settings);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ThemeService.ApplyThemeToElement failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }
    }
}
