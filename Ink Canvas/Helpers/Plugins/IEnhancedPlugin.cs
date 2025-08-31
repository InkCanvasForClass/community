using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 增强的插件接口，提供对插件服务的访问
    /// </summary>
    public interface IEnhancedPlugin : IPlugin
    {
        /// <summary>
        /// 获取插件服务实例
        /// </summary>
        IPluginService PluginService { get; }

        /// <summary>
        /// 插件启动时调用，在Initialize之后
        /// </summary>
        void OnStartup();

        /// <summary>
        /// 插件关闭时调用，在Cleanup之前
        /// </summary>
        void OnShutdown();

        /// <summary>
        /// 获取插件的菜单项
        /// </summary>
        /// <returns>菜单项集合</returns>
        MenuItem[] GetMenuItems();

        /// <summary>
        /// 获取插件的工具栏按钮
        /// </summary>
        /// <returns>工具栏按钮集合</returns>
        Button[] GetToolbarButtons();

        /// <summary>
        /// 获取插件的状态栏信息
        /// </summary>
        /// <returns>状态栏信息</returns>
        string GetStatusBarInfo();

        /// <summary>
        /// 插件配置变更时调用
        /// </summary>
        void OnConfigurationChanged();
    }
} 