using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 窗口控制服务，供插件操控主窗口状态。
    /// </summary>
    public interface IWindowService
    {
        /// <summary>窗口是否置顶</summary>
        bool IsTopMost { get; }

        /// <summary>窗口是否全屏</summary>
        bool IsFullscreen { get; }

        /// <summary>窗口是否处于收纳（隐藏）状态</summary>
        bool IsCollapsed { get; }

        /// <summary>当前是否处于白板模式</summary>
        bool IsWhiteboardMode { get; }

        /// <summary>
        /// 设置窗口置顶状态。
        /// </summary>
        void SetTopMost(bool topMost);

        /// <summary>
        /// 切换置顶状态。
        /// </summary>
        void ToggleTopMost();

        /// <summary>
        /// 收纳浮动栏（隐藏到屏幕边缘）。
        /// </summary>
        void Collapse();

        /// <summary>
        /// 展开浮动栏。
        /// </summary>
        void Expand();

        /// <summary>
        /// 切换收纳/展开。
        /// </summary>
        void ToggleCollapse();

        /// <summary>
        /// 进入白板模式。
        /// </summary>
        void EnterWhiteboard();

        /// <summary>
        /// 退出白板模式（回到浮动栏）。
        /// </summary>
        void ExitWhiteboard();

        /// <summary>
        /// 窗口置顶状态变化事件。
        /// </summary>
        event Action<bool> TopMostChanged;

        /// <summary>
        /// 收纳状态变化事件。
        /// </summary>
        event Action<bool> CollapseChanged;
    }
}
