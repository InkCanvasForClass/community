using System;
using Microsoft.Office.Interop.PowerPoint;

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

        public int SlidesCount => _inner.SlidesCount;

        public object PPTApplication => _inner.PPTApplication;
        #endregion

        #region 生命周期管理
        public void StartMonitoring() => _inner.StartMonitoring();

        public void StopMonitoring() => _inner.StopMonitoring();

        public void ReloadConnection()
        {
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

