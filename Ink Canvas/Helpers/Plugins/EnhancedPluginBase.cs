using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 增强的插件基类，提供对插件服务的访问和基本实现
    /// </summary>
    public abstract class EnhancedPluginBase : PluginBase, IEnhancedPlugin
    {
        /// <summary>
        /// 插件服务实例
        /// </summary>
        public IPluginService PluginService { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        protected EnhancedPluginBase()
        {
            PluginService = PluginServiceManager.Instance;
        }

        /// <summary>
        /// 插件启动时调用，在Initialize之后
        /// </summary>
        public virtual void OnStartup()
        {
            LogHelper.WriteLogToFile($"插件 {Name} 已启动");
        }

        /// <summary>
        /// 插件关闭时调用，在Cleanup之前
        /// </summary>
        public virtual void OnShutdown()
        {
            LogHelper.WriteLogToFile($"插件 {Name} 正在关闭");
        }

        /// <summary>
        /// 获取插件的菜单项
        /// </summary>
        /// <returns>菜单项集合</returns>
        public virtual MenuItem[] GetMenuItems()
        {
            return new MenuItem[0];
        }

        /// <summary>
        /// 获取插件的工具栏按钮
        /// </summary>
        /// <returns>工具栏按钮集合</returns>
        public virtual Button[] GetToolbarButtons()
        {
            return new Button[0];
        }

        /// <summary>
        /// 获取插件的状态栏信息
        /// </summary>
        /// <returns>状态栏信息</returns>
        public virtual string GetStatusBarInfo()
        {
            return $"{Name} v{Version} - {(IsEnabled ? "已启用" : "已禁用")}";
        }

        /// <summary>
        /// 插件配置变更时调用
        /// </summary>
        public virtual void OnConfigurationChanged()
        {
            LogHelper.WriteLogToFile($"插件 {Name} 配置已变更");
        }

        /// <summary>
        /// 重写初始化方法，调用OnStartup
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            OnStartup();
        }

        /// <summary>
        /// 重写清理方法，调用OnShutdown
        /// </summary>
        public override void Cleanup()
        {
            OnShutdown();
            base.Cleanup();
        }
    }
}