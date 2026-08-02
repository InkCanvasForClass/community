using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 主题枚举。
    /// </summary>
    public enum PluginTheme
    {
        /// <summary>浅色。</summary>
        Light = 0,
        /// <summary>深色。</summary>
        Dark = 1,
    }

    /// <summary>
    /// 主题服务：供插件检测系统/宿主当前主题，并把主题应用到自己的控件。
    /// </summary>
    public interface IThemeService
    {
        /// <summary>系统当前是否为浅色主题（注册表 AppsUseLightTheme）。</summary>
        bool IsSystemThemeLight();

        /// <summary>宿主当前生效的主题（按宿主设置：浅色/深色/跟随系统）。</summary>
        PluginTheme GetEffectiveTheme();

        /// <summary>把宿主当前主题应用到指定元素（调用方需持有该元素在可视树中的引用）。</summary>
        void ApplyThemeToElement(FrameworkElement element);
    }
}
