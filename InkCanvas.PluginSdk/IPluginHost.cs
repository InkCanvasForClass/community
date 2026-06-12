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
        T GetService<T>() where T : class;
        void RegisterService<T>(T service) where T : class;
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
        public Func<FrameworkElement> ViewFactory { get; set; }
        public Action<FrameworkElement, Orientation> ApplyOrientation { get; set; }
        public Action<FrameworkElement, Dictionary<string, object>> ApplySettings { get; set; }
        public List<PluginToolbarSettingInfo> CustomSettings { get; set; } = new List<PluginToolbarSettingInfo>();
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
