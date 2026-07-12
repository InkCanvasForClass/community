using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 主程序窗口概览的插件安全视图。插件只能读取窗口元数据，不能操作目标窗口。
    /// </summary>
    public sealed class PluginWindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string ProcessPath { get; set; } = "";
        public bool IsVisible { get; set; }
        public bool IsMinimized { get; set; }
        public uint ProcessId { get; set; }
    }

    /// <summary>
    /// 提供主程序窗口读取模型的只读插件接口。
    /// </summary>
    public interface IWindowOverviewService
    {
        IReadOnlyList<PluginWindowInfo> Windows { get; }
        PluginWindowInfo ForegroundWindow { get; }
        void Refresh();
        event Action WindowsChanged;
    }
}
