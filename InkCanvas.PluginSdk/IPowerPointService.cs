using System;
using System.Collections.Generic;

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
        /// 是否已连接到 PowerPoint/WPS。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 当前演示文件的完整路径；未打开时返回 null。
        /// </summary>
        string GetPresentationPath();

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
        /// 导出全部幻灯片缩略图（PNG）。
        /// </summary>
        /// <param name="width">缩略图宽度。</param>
        /// <param name="height">缩略图高度。</param>
        /// <returns>缩略图列表；未连接时返回空列表。</returns>
        IReadOnlyList<PluginSlideThumbnail> ExportSlideThumbnails(int width, int height);

        /// <summary>
        /// 尝试打开 PPT 翻页导航界面。返回是否成功打开。
        /// </summary>
        bool TryShowSlideNavigation();

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

    /// <summary>
    /// 单张幻灯片的缩略图（PNG 字节）。
    /// </summary>
    public sealed class PluginSlideThumbnail
    {
        /// <summary>幻灯片页码（从 1 开始）。</summary>
        public int SlideNumber { get; set; }

        /// <summary>缩略图 PNG 字节。</summary>
        public byte[] PngBytes { get; set; }
    }
}
