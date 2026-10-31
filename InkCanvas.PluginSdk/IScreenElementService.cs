using System;
using System.Collections.Generic;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 只读的 Windows UI Automation 元素快照。
    /// <para>
    /// 快照不包含 AutomationElement/COM 引用，插件只能读取信息，不能通过此接口操作目标窗口。
    /// BoundingRectangle 使用屏幕物理像素坐标，与 IScreenshotService 的截图坐标一致。
    /// </para>
    /// </summary>
    public sealed class PluginScreenElementInfo
    {
        public string Name { get; set; } = "";
        public string AutomationId { get; set; } = "";
        public string ControlType { get; set; } = "";
        public string LocalizedControlType { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string FrameworkId { get; set; } = "";
        public string Value { get; set; } = "";
        public Rect BoundingRectangle { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsOffscreen { get; set; }
        public IntPtr WindowHandle { get; set; }
        public int ProcessId { get; set; }
        public int Depth { get; set; }
        public List<PluginScreenElementInfo> Children { get; set; } = new List<PluginScreenElementInfo>();
    }

    /// <summary>
    /// 命中元素及其有限祖先/子树快照。
    /// </summary>
    public sealed class PluginScreenElementSnapshot
    {
        public PluginScreenElementInfo Element { get; set; }
        public List<PluginScreenElementInfo> Ancestors { get; set; } = new List<PluginScreenElementInfo>();
    }

    /// <summary>
    /// 屏幕 UI Automation 只读服务。
    /// <para>
    /// 宿主会限制树深度和元素数量，插件不得假设所有应用都提供完整 UIA 信息；
    /// 自绘控件、浏览器 canvas 和视频内容应回退到截图/视觉模型。
    /// </para>
    /// </summary>
    public interface IScreenElementService
    {
        /// <summary>
        /// 读取屏幕像素坐标处的元素，并返回命中元素及祖先链。
        /// </summary>
        PluginScreenElementSnapshot GetElementAtPoint(Point screenPixelPoint,
            int maxAncestorDepth = 8, int maxChildDepth = 1, int maxElements = 128);

        /// <summary>
        /// 读取指定窗口的 UIA 根元素和有限子树。传入零句柄时使用当前前台窗口。
        /// </summary>
        PluginScreenElementInfo GetWindowTree(IntPtr windowHandle = default,
            int maxDepth = 3, int maxElements = 256);
    }
}
