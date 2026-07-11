using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Plugins
{
    public interface IPluginHost
    {
        void Log(string message);
        void LogError(string message, Exception ex = null);

        /// <summary>
        /// 依赖注入服务集合。插件可在 Initialize 阶段向此集合注册自己的服务。
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// 依赖注入服务提供者。在所有插件 Initialize 完成后可用。
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 从 DI 容器获取服务（兼容旧接口）。
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// 向 DI 容器注册服务（兼容旧接口，仅在 Initialize 阶段有效）。
        /// </summary>
        void RegisterService<T>(T service) where T : class;

        /// <summary>
        /// 向工具栏注册插件组件。
        /// </summary>
        void RegisterToolbarItem(PluginToolbarItemInfo itemInfo);
    }

    /// <summary>
    /// 插件工具栏项信息，用于向主程序注册工具栏组件。
    /// </summary>
    public class PluginToolbarItemInfo
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconGeometry { get; set; }
        public Func<FrameworkElement> ViewFactory { get; set; }
        public Action<FrameworkElement, Orientation> ApplyOrientation { get; set; }
        public Action<FrameworkElement, Dictionary<string, object>> ApplySettings { get; set; }
        public List<PluginToolbarSettingInfo> CustomSettings { get; set; } = new List<PluginToolbarSettingInfo>();

        /// <summary>
        /// 弹窗内容工厂。若提供此属性，点击按钮时将自动打开包含此内容的弹窗菜单。
        /// 返回的 FrameworkElement 将作为 Popup 的 Child 显示。
        /// </summary>
        public Func<FrameworkElement> PopupContentFactory { get; set; }
    }

    /// <summary>
    /// 插件工具栏项的自定义设置描述。
    /// </summary>
    public class PluginToolbarSettingInfo
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public PluginToolbarSettingType Type { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public string DefaultValue { get; set; }
    }

    public enum PluginToolbarSettingType
    {
        ComboBox,
        Slider,
        Toggle
    }
}
