using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件服务接口，提供对软件内部功能的访问
    /// 继承自三个专门的服务接口：获取服务、窗口服务、操作服务
    /// </summary>
    public interface IPluginService : IGetService, IWindowService, IActionService
    {
        // 这个接口现在继承自三个专门的服务接口
        // 所有方法都在子接口中定义，这里不需要重复定义
    }

    /// <summary>
    /// 通知类型枚举
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 成功
        /// </summary>
        Success,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }
}