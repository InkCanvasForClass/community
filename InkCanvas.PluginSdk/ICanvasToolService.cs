using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    public enum CanvasPointerDeviceKind
    {
        Mouse = 0,
        Touch = 1,
        Pen = 2
    }

    public enum CanvasPointerAction
    {
        Down = 0,
        Move = 1,
        Up = 2,
        Cancel = 3,
        Wheel = 4
    }

    /// <summary>
    /// 与宿主内部输入类型解耦的画布指针事件。
    /// </summary>
    public sealed class CanvasPointerEventArgs : EventArgs
    {
        public CanvasPointerDeviceKind DeviceKind { get; set; }
        public CanvasPointerAction Action { get; set; }
        public int PointerId { get; set; }
        public Point Position { get; set; }
        public float Pressure { get; set; } = 0.5f;
        public MouseButtonState LeftButton { get; set; }
        public MouseButtonState RightButton { get; set; }
        public bool IsPrimary { get; set; }
        public int WheelDelta { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public bool Handled { get; set; }
    }

    /// <summary>
    /// 与宿主内部键盘路由解耦的画布工具按键事件。
    /// </summary>
    public sealed class CanvasKeyEventArgs : EventArgs
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public bool Handled { get; set; }
    }

    /// <summary>
    /// 插件工具的独占输入会话。释放后宿主恢复进入工具前的普通墨迹模式。
    /// </summary>
    public interface ICanvasToolSession : IDisposable
    {
        string PluginId { get; }
        string ToolId { get; }
        bool IsActive { get; }
        event EventHandler<CanvasPointerEventArgs> Pointer;
        event EventHandler<CanvasKeyEventArgs> KeyDown;
        bool CapturePointer(int pointerId);
        void ReleasePointer(int pointerId);
    }

    public interface ICanvasToolService
    {
        /// <summary>
        /// 尝试激活插件工具。同一时间只允许一个插件工具拥有画布输入。
        /// 冻结页面拒绝激活可修改画布的插件工具。
        /// </summary>
        bool TryActivateTool(string pluginId, string toolId, out ICanvasToolSession session);

        /// <summary>结束插件当前拥有的工具会话；插件卸载时宿主会调用。</summary>
        void DeactivateTools(string pluginId);
    }
}
