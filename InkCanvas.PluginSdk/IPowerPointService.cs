using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// PowerPoint 控制服务，供插件操控 PPT 联动。
    /// </summary>
    public interface IPowerPointService
    {
        /// <summary>
        /// PPT 是否正在放映。
        /// </summary>
        bool IsSlideshowActive { get; }

        /// <summary>
        /// 当前幻灯片页码（从 1 开始），未放映时返回 0。
        /// </summary>
        int CurrentSlide { get; }

        /// <summary>
        /// 幻灯片总数，未打开时返回 0。
        /// </summary>
        int TotalSlides { get; }

        /// <summary>
        /// 当前 PPT 文件名（不含路径），未打开时返回 null。
        /// </summary>
        string CurrentFileName { get; }

        /// <summary>
        /// 跳转到指定页。
        /// </summary>
        void GoToSlide(int slideNumber);

        /// <summary>
        /// 下一页。
        /// </summary>
        void NextSlide();

        /// <summary>
        /// 上一页。
        /// </summary>
        void PreviousSlide();

        /// <summary>
        /// 开始放映。
        /// </summary>
        void StartSlideshow();

        /// <summary>
        /// 结束放映。
        /// </summary>
        void StopSlideshow();

        /// <summary>
        /// 翻页事件（页码）。
        /// </summary>
        event Action<int> SlideChanged;

        /// <summary>
        /// 放映开始事件。
        /// </summary>
        event Action SlideshowStarted;

        /// <summary>
        /// 放映结束事件。
        /// </summary>
        event Action SlideshowEnded;
    }
}
