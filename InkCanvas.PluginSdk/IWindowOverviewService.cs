using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 主程序窗口概览的插件安全视图。插件只能读取窗口元数据，不能操作目标窗口。
    /// </summary>
    public sealed class PluginWindowInfo
    {
        /// <summary>窗口句柄（HWND）。</summary>
        public IntPtr Handle { get; set; }
        /// <summary>窗口标题。</summary>
        public string Title { get; set; } = "";
        /// <summary>窗口类名。</summary>
        public string ClassName { get; set; } = "";
        /// <summary>所属进程名（不含扩展名）。</summary>
        public string ProcessName { get; set; } = "";
        /// <summary>所属进程的可执行文件路径。</summary>
        public string ProcessPath { get; set; } = "";
        /// <summary>窗口是否可见（当前快照仅包含可见且未最小化的窗口）。</summary>
        public bool IsVisible { get; set; }
        /// <summary>窗口是否最小化（当前快照仅包含可见且未最小化的窗口）。</summary>
        public bool IsMinimized { get; set; }
        /// <summary>所属进程 ID。</summary>
        public uint ProcessId { get; set; }
    }

    /// <summary>
    /// 提供主程序窗口读取模型的只读插件接口。
    /// </summary>
    public interface IWindowOverviewService
    {
        /// <summary>当前窗口快照（只读）。仅包含可见且未最小化的窗口，宿主在窗口列表变化时自动刷新。</summary>
        IReadOnlyList<PluginWindowInfo> Windows { get; }
        /// <summary>当前前台窗口。若前台窗口不在快照列表中（如已最小化、属于其他桌面）则为 null。</summary>
        PluginWindowInfo ForegroundWindow { get; }
        /// <summary>立即重新枚举窗口并触发 <see cref="WindowsChanged"/>。</summary>
        void Refresh();
        /// <summary>窗口快照更新完成后触发。</summary>
        event Action WindowsChanged;
    }
}
