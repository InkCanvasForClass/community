using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 事件服务，供插件订阅主程序事件。
    /// </summary>
    public interface IEventService
    {
        /// <summary>白板模式切换时触发（true=进入白板，false=退出白板）</summary>
        event Action<bool> WhiteboardModeChanged;

        /// <summary>画笔模式切换时触发（true=画笔模式，false=鼠标模式）</summary>
        event Action<bool> PenModeChanged;

        /// <summary>PPT 翻页时触发（当前页码）</summary>
        event Action<int> SlideChanged;

        /// <summary>PPT 开始放映时触发</summary>
        event Action SlideShowStarted;

        /// <summary>PPT 结束放映时触发</summary>
        event Action SlideShowEnded;

        /// <summary>窗口置顶状态变化时触发</summary>
        event Action<bool> TopMostChanged;

        /// <summary>应用即将退出时触发</summary>
        event Action AppExiting;
    }
}
