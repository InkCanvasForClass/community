using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    internal class PowerPointService : IPowerPointService
    {
        private readonly MainWindow _mainWindow;

        public PowerPointService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            if (_mainWindow != null)
            {
                _mainWindow.PluginSlideChanged += OnSlideChanged;
                _mainWindow.PluginSlideShowStateChanged += OnSlideShowStateChanged;
            }
        }

        private void OnSlideShowStateChanged(bool isActive)
        {
            if (isActive) OnSlideshowStarted();
            else OnSlideshowEnded();
        }

        public bool IsSlideshowActive
        {
            get
            {
                try { return _mainWindow?.PPTManager?.IsInSlideShow ?? false; }
                catch { return false; }
            }
        }

        public int CurrentSlide
        {
            get
            {
                try { return _mainWindow?.PPTManager?.GetCurrentSlideNumber() ?? 0; }
                catch { return 0; }
            }
        }

        public int TotalSlides
        {
            get
            {
                try { return _mainWindow?.PPTManager?.SlidesCount ?? 0; }
                catch { return 0; }
            }
        }

        public string CurrentFileName
        {
            get
            {
                try { return _mainWindow?.PPTManager?.GetPresentationName(); }
                catch { return null; }
            }
        }

        public bool IsConnected
        {
            get
            {
                try { return _mainWindow?.PPTManager?.IsConnected ?? false; }
                catch { return false; }
            }
        }

        public string GetPresentationPath()
        {
            try { return _mainWindow?.PPTManager?.GetPresentationPath(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PowerPointService.GetPresentationPath failed: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        public IReadOnlyList<PluginSlideThumbnail> ExportSlideThumbnails(int width, int height)
        {
            try
            {
                var list = _mainWindow?.PPTManager?.ExportSlideThumbnails(width, height);
                if (list == null || list.Count == 0) return Array.Empty<PluginSlideThumbnail>();
                return list.Select(t => new PluginSlideThumbnail
                {
                    SlideNumber = t.SlideNumber,
                    PngBytes = t.PngBytes,
                }).ToList();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PowerPointService.ExportSlideThumbnails failed: {ex.Message}", LogHelper.LogType.Warning);
                return Array.Empty<PluginSlideThumbnail>();
            }
        }

        public bool TryShowSlideNavigation()
        {
            try { return _mainWindow?.PPTManager?.TryShowSlideNavigation() ?? false; }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PowerPointService.TryShowSlideNavigation failed: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public event Action<int> SlideChanged;
        public event Action SlideshowStarted;
        public event Action SlideshowEnded;

        public void GoToSlide(int slideNumber)
        {
            try { _mainWindow?.PPTManager?.TryNavigateToSlide(slideNumber); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PowerPointService.GoToSlide failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void NextSlide()
        {
            try { _mainWindow?.PPTManager?.TryNavigateNext(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PowerPointService.NextSlide failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void PreviousSlide()
        {
            try { _mainWindow?.PPTManager?.TryNavigatePrevious(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PowerPointService.PreviousSlide failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void StartSlideshow()
        {
            try { _mainWindow?.PPTManager?.TryStartSlideShow(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PowerPointService.StartSlideshow failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void StopSlideshow()
        {
            try { _mainWindow?.PPTManager?.TryEndSlideShow(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"PowerPointService.StopSlideshow failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        internal void OnSlideChanged(int slide) => SlideChanged?.Invoke(slide);
        internal void OnSlideshowStarted() => SlideshowStarted?.Invoke();
        internal void OnSlideshowEnded() => SlideshowEnded?.Invoke();
    }
}
