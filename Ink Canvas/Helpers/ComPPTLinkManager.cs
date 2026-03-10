using System;

namespace Ink_Canvas.Helpers
{
    public class ComPPTLinkManager : IPPTLinkManager
    {
        private readonly PPTManager _inner;

        public ComPPTLinkManager()
        {
            _inner = new PPTManager();

            _inner.SlideShowBegin += wn => SlideShowBegin?.Invoke(wn);
            _inner.SlideShowNextSlide += wn => SlideShowNextSlide?.Invoke(wn);
            _inner.SlideShowEnd += pres => SlideShowEnd?.Invoke(pres);
            _inner.PresentationOpen += pres => PresentationOpen?.Invoke(pres);
            _inner.PresentationClose += pres => PresentationClose?.Invoke(pres);
            _inner.PPTConnectionChanged += connected => PPTConnectionChanged?.Invoke(connected);
            _inner.SlideShowStateChanged += inSlideShow => SlideShowStateChanged?.Invoke(inSlideShow);
        }

        #region IPPTLinkManager 事件
        public event Action<object> SlideShowBegin;
        public event Action<object> SlideShowNextSlide;
        public event Action<object> SlideShowEnd;
        public event Action<object> PresentationOpen;
        public event Action<object> PresentationClose;
        public event Action<bool> PPTConnectionChanged;
        public event Action<bool> SlideShowStateChanged;
        #endregion

        #region IPPTLinkManager 属性
        public bool IsConnected => _inner.IsConnected;

        public bool IsInSlideShow => _inner.IsInSlideShow;

        public bool IsSupportWPS
        {
            get => _inner.IsSupportWPS;
            set => _inner.IsSupportWPS = value;
        }

        public bool SkipAnimationsWhenNavigating
        {
            get => _inner.SkipAnimationsWhenNavigating;
            set => _inner.SkipAnimationsWhenNavigating = value;
        }

        public int SlidesCount => _inner.SlidesCount;

        public object PPTApplication => _inner.PPTApplication;
        #endregion

        #region 生命周期管理
        /// <summary>
        /// 开始监控本地 PowerPoint 的连接与运行状态，并在状态变化时触发相应事件。
        /// </summary>
        public void StartMonitoring() => _inner.StartMonitoring();

        /// <summary>
        /// 停止对 PowerPoint 的监控，断开当前连接并停止触发相关事件。
        /// </summary>
        public void StopMonitoring() => _inner.StopMonitoring();

        /// <summary>
        /// 强制断开当前 COM PPT 连接并停止对其监控，同时写入事件日志。
        /// </summary>
        /// <remarks>
        /// 会向日志记录一条事件信息并调用内部管理器停止监控；该方法不会重新启动监控或重新初始化内部管理器实例。
        /// </remarks>
        public void ReloadConnection()
        {
            LogHelper.WriteLogToFile("COM PPT 执行热重载：强制断开并重新连接", LogHelper.LogType.Event);
            _inner.StopMonitoring();
        }
        #endregion

        #region 放映控制
        public bool TryStartSlideShow() => _inner.TryStartSlideShow();

        public bool TryEndSlideShow() => _inner.TryEndSlideShow();
        #endregion

        #region 导航控制
        public bool TryNavigateToSlide(int slideNumber) => _inner.TryNavigateToSlide(slideNumber);

        public bool TryNavigateNext() => _inner.TryNavigateNext();

        public bool TryNavigatePrevious() => _inner.TryNavigatePrevious();
        #endregion

        #region 查询
        public int GetCurrentSlideNumber() => _inner.GetCurrentSlideNumber();

        public string GetPresentationName() => _inner.GetPresentationName();

        public bool TryShowSlideNavigation() => _inner.TryShowSlideNavigation();

        public object GetCurrentActivePresentation() => _inner.GetCurrentActivePresentation();
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _inner?.Dispose();
        }
        #endregion
    }
}
