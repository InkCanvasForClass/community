using System;
using System.IO;
using System.Threading;
using InkCanvasPptAgent.Contracts;
using Newtonsoft.Json;

namespace PptAgent.PowerPointAddIn.Core
{
    public sealed class PptController
    {
        private readonly Microsoft.Office.Interop.PowerPoint.Application _application;
        private SynchronizationContext _syncContext;

        public PptController(Microsoft.Office.Interop.PowerPoint.Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            CaptureSyncContext();
        }

        public PptState GetState()
        {
            var state = new PptState();

            try
            {
                if (_application.Presentations.Count > 0)
                {
                    var pres = _application.ActivePresentation;
                    state.PresentationName = pres.Name;
                    try { state.PresentationFullName = pres.FullName; } catch { }
                    state.TotalSlides = pres.Slides.Count;
                    state.HasHiddenSlides = HasHiddenSlides(pres);
                    state.HasAutoPlayTimings = HasAutoPlayTimings(pres);
                }
            }
            catch
            {
            }

            try
            {
                if (_application.SlideShowWindows.Count > 0)
                {
                    state.IsRunning = true;
                    state.SlideIndex = _application.SlideShowWindows[1].View.CurrentShowPosition;
                }
            }
            catch
            {
            }

            return state;
        }

        public bool Next()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Next();
                return true;
            });
        }

        public bool Previous()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Previous();
                return true;
            });
        }

        public bool GotoSlide(int slideNumber)
        {
            return Run(() =>
            {
                if (slideNumber <= 0) return false;
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.GotoSlide(slideNumber);
                return true;
            });
        }

        public bool StartSlideShow()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                _application.ActivePresentation.SlideShowSettings.Run();
                return true;
            });
        }

        public bool EndSlideShow()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Exit();
                return true;
            });
        }

        public bool ShowSlideNavigation()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                try
                {
                    dynamic nav = _application.SlideShowWindows[1].SlideNavigation;
                    if (nav == null) return false;
                    nav.Visible = true;
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public bool DisableAutoPlayTimings()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                _application.ActivePresentation.SlideShowSettings.AdvanceMode = Microsoft.Office.Interop.PowerPoint.PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                return true;
            });
        }

        public bool UnhideHiddenSlides()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                foreach (Microsoft.Office.Interop.PowerPoint.Slide slide in _application.ActivePresentation.Slides)
                {
                    if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue)
                        slide.SlideShowTransition.Hidden = Microsoft.Office.Core.MsoTriState.msoFalse;
                }
                return true;
            });
        }

        public void EnsureOnMainThread(Action action)
        {
            if (_syncContext != null)
                _syncContext.Post(_ => action.Invoke(), null);
            else
                action.Invoke();
        }

        private void CaptureSyncContext()
        {
            if (SynchronizationContext.Current != null)
                _syncContext = SynchronizationContext.Current;
        }

        private bool Run(Func<bool> action)
        {
            if (_syncContext != null)
            {
                bool result = false;
                Exception captured = null;
                _syncContext.Send(_ =>
                {
                    try { result = action(); }
                    catch (Exception ex) { captured = ex; }
                }, null);

                if (captured != null)
                    throw captured;
                return result;
            }

            return action.Invoke();
        }
    }
}
