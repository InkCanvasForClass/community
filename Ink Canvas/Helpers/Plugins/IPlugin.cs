using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 定义插件的基本接口
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 插件描述
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// 插件版本
        /// </summary>
        Version Version { get; }
        
        /// <summary>
        /// 插件作者
        /// </summary>
        string Author { get; }
        
        /// <summary>
        /// 是否为内置插件
        /// </summary>
        bool IsBuiltIn { get; }
        
        /// <summary>
        /// 初始化插件
        /// 此方法在插件加载时被调用，用于执行一些初始化工作
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// 启用插件
        /// 此方法在插件被用户或系统启用时调用，激活插件功能
        /// </summary>
        void Enable();
        
        /// <summary>
        /// 禁用插件
        /// 此方法在插件被用户或系统禁用时调用，停用插件功能
        /// </summary>
        void Disable();
        
        /// <summary>
        /// 获取插件设置界面
        /// 此方法返回插件的设置界面控件，用于展示在设置窗口
        /// </summary>
        /// <returns>插件设置界面</returns>
        UserControl GetSettingsView();
        
        /// <summary>
        /// 插件卸载时的清理工作
        /// 此方法在插件被卸载前调用，用于释放资源和执行清理
        /// </summary>
        void Cleanup();
    }
} 