using System.Collections.Generic;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 屏幕信息服务：供插件读取系统显示器信息。
    /// </summary>
    public interface IScreenInfoService
    {
        /// <summary>所有显示器的列表。</summary>
        IReadOnlyList<PluginScreenInfo> GetAllScreens();

        /// <summary>主显示器。</summary>
        PluginScreenInfo GetPrimaryScreen();

        /// <summary>是否存在多显示器。</summary>
        bool HasMultipleScreens();
    }

    /// <summary>
    /// 显示器信息（只读）。
    /// </summary>
    public sealed class PluginScreenInfo
    {
        /// <summary>显示器完整边界（设备无关像素，相对虚拟屏幕原点）。</summary>
        public Rect Bounds { get; set; }

        /// <summary>显示器工作区（扣除任务栏等，设备无关像素）。</summary>
        public Rect WorkingArea { get; set; }

        /// <summary>显示器设备名（如 "\\.\DISPLAY1"）。</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>是否为主显示器。</summary>
        public bool IsPrimary { get; set; }
    }
}
