using System;
using System.Collections.Generic;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    public sealed class PPTSlideThumbnail
    {
        public int SlideNumber { get; set; }
        public byte[] PngBytes { get; set; }
    }

    public interface IPPTLinkManager : IDisposable
    {
        event Action<object> SlideShowBegin;
        event Action<object> SlideShowNextSlide;
        event Action<object> SlideShowEnd;
        event Action<object> PresentationOpen;
        /// <summary>
        /// 仅在用户真正打开一个演示文稿时触发；ICC 启动/重连时补发的 <see cref="PresentationOpen"/> 不会触发此事件。
        /// 用于「打开演示文稿时自动开始放映」等只应对真实打开操作生效的逻辑。
        /// </summary>
        event Action<object> PresentationOpenedExternally;
        event Action<object> PresentationClose;
        event Action<bool> PPTConnectionChanged;
        event Action<bool> SlideShowStateChanged;

        bool IsConnected { get; }
        bool IsInSlideShow { get; }
        bool IsSupportWPS { get; set; }
        bool SkipAnimationsWhenNavigating { get; set; }
        int SlidesCount { get; }

        object PPTApplication { get; }

        /// <summary>
        /// 开始监视与 PowerPoint 的连接以及幻灯片放映相关状态，并在状态变化时触发对应事件。
        /// </summary>
        void StartMonitoring();
        void StopMonitoring(bool isShutdown = false);

        /// <summary>
        /// 重新加载或重建与 PowerPoint 的连接。
        /// </summary>
        /// <remarks>
        /// 调用后实现应刷新内部连接与状态，必要时重建与 PowerPoint 的会话；此操作可能导致 IsConnected 变化并触发 PPTConnectionChanged 或其他相关事件（例如 SlideShowStateChanged）。
        /// </remarks>
        void ReloadConnection();

        /// <summary>
        /// 尝试启动当前演示文稿的放映模式。
        /// </summary>
        /// <returns><c>true</c> 如果放映已成功启动，<c>false</c> 否则。</returns>
        bool TryStartSlideShow();
        /// <summary>
        /// 尝试按完整路径启动指定演示文稿的放映模式。
        /// </summary>
        /// <param name="presentationFullName">要放映的演示文稿完整路径；为空时退化为 <see cref="TryStartSlideShow()"/>。</param>
        /// <remarks>
        /// WPS 打开新文稿时 <c>PresentationOpen</c> 事件可能先于其对象模型更新触发，
        /// 此时"当前演示文稿"仍指向旧文稿。按路径查找目标文稿可避免放映错误的文稿；
        /// 目标文稿尚未出现在集合中时应返回 <c>false</c>，由调用方稍后重试。
        /// </remarks>
        /// <returns><c>true</c> 如果放映已成功启动，<c>false</c> 否则。</returns>
        bool TryStartSlideShow(string presentationFullName);
        /// <summary>
        /// 尝试结束当前正在进行的幻灯片放映。
        /// </summary>
        /// <returns><c>true</c> 如果放映已成功结束，<c>false</c> 否则。</returns>
        bool TryEndSlideShow();

        // 导航控制
        bool TryNavigateToSlide(int slideNumber);
        bool TryNavigateNext();
        bool TryNavigatePrevious();

        // 查询
        int GetCurrentSlideNumber();
        string GetPresentationName();
        string GetPresentationPath();
        bool TryShowSlideNavigation();
        object GetCurrentActivePresentation();
        List<PPTSlideThumbnail> ExportSlideThumbnails(
            int width,
            int height,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default);
    }
}

