using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 增强的插件基类 V2，提供对三个专门服务接口的访问
    /// 插件开发者可以根据需要选择性地使用这些服务
    /// </summary>
    public abstract class EnhancedPluginBaseV2 : PluginBase, IEnhancedPlugin
    {
        /// <summary>
        /// 获取服务实例
        /// </summary>
        public IGetService GetService { get; private set; }

        /// <summary>
        /// 窗口服务实例
        /// </summary>
        public IWindowService WindowService { get; private set; }

        /// <summary>
        /// 操作服务实例
        /// </summary>
        public IActionService ActionService { get; private set; }

        /// <summary>
        /// 插件服务实例（兼容性）
        /// </summary>
        public IPluginService PluginService { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        protected EnhancedPluginBaseV2()
        {
            // 初始化所有服务实例
            PluginService = PluginServiceManager.Instance;
            GetService = PluginServiceManager.Instance;
            WindowService = PluginServiceManager.Instance;
            ActionService = PluginServiceManager.Instance;
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

        #region 便捷方法

        /// <summary>
        /// 显示通知消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="type">消息类型</param>
        protected void ShowNotification(string message, NotificationType type = NotificationType.Info)
        {
            WindowService.ShowNotification(message, type);
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <returns>用户选择结果</returns>
        protected bool ShowConfirmDialog(string message, string title = "确认")
        {
            return WindowService.ShowConfirmDialog(message, title);
        }

        /// <summary>
        /// 显示输入对话框
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="title">标题</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>用户输入内容</returns>
        protected string ShowInputDialog(string message, string title = "输入", string defaultValue = "")
        {
            return WindowService.ShowInputDialog(message, title, defaultValue);
        }

        /// <summary>
        /// 获取系统设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>设置值</returns>
        protected T GetSetting<T>(string key, T defaultValue = default(T))
        {
            return GetService.GetSetting(key, defaultValue);
        }

        /// <summary>
        /// 设置系统设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="key">设置键</param>
        /// <param name="value">设置值</param>
        protected void SetSetting<T>(string key, T value)
        {
            ActionService.SetSetting(key, value);
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        protected void SaveSettings()
        {
            ActionService.SaveSettings();
        }

        /// <summary>
        /// 清除当前画布
        /// </summary>
        protected void ClearCanvas()
        {
            ActionService.ClearCanvas();
        }

        /// <summary>
        /// 撤销操作
        /// </summary>
        protected void Undo()
        {
            ActionService.Undo();
        }

        /// <summary>
        /// 重做操作
        /// </summary>
        protected void Redo()
        {
            ActionService.Redo();
        }

        /// <summary>
        /// 检查是否可以撤销
        /// </summary>
        protected bool CanUndo => GetService.CanUndo;

        /// <summary>
        /// 检查是否可以重做
        /// </summary>
        protected bool CanRedo => GetService.CanRedo;

        /// <summary>
        /// 获取当前绘制模式
        /// </summary>
        protected int CurrentDrawingMode => GetService.CurrentDrawingMode;

        /// <summary>
        /// 设置绘制模式
        /// </summary>
        /// <param name="mode">绘制模式</param>
        protected void SetDrawingMode(int mode)
        {
            ActionService.SetDrawingMode(mode);
        }

        /// <summary>
        /// 注册事件处理器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理器</param>
        protected void RegisterEventHandler(string eventName, System.EventHandler handler)
        {
            ActionService.RegisterEventHandler(eventName, handler);
        }

        /// <summary>
        /// 注销事件处理器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理器</param>
        protected void UnregisterEventHandler(string eventName, System.EventHandler handler)
        {
            ActionService.UnregisterEventHandler(eventName, handler);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        protected void TriggerEvent(string eventName, object sender, System.EventArgs args)
        {
            ActionService.TriggerEvent(eventName, sender, args);
        }

        #endregion
    }
} 