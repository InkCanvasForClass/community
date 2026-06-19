using Ink_Canvas.IPC;
using InkCanvasPptAgent.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    public sealed class PptAgentLinkManager : IPPTLinkManager
    {
        private readonly object _stateLock = new object();
        private PptAgentPipeClient _client;
        private PptState _lastState = new PptState();
        private bool _lastConnected;
        private bool _lastIsRunning;
        private int _lastSlideIndex;
        private string _lastPresentationName;
        private bool _disposed;

        public event Action<object> SlideShowBegin;
        public event Action<object> SlideShowNextSlide;
        public event Action<object> SlideShowEnd;
        public event Action<object> PresentationOpen;
        public event Action<object> PresentationClose;
        public event Action<bool> PPTConnectionChanged;
        public event Action<bool> SlideShowStateChanged;

        public bool IsConnected => _client?.IsConnected == true;
        public bool IsInSlideShow => GetStateSnapshot().IsRunning;
        public bool IsSupportWPS { get; set; }
        public bool SkipAnimationsWhenNavigating { get; set; }
        public int SlidesCount => GetStateSnapshot().TotalSlides;
        public object PPTApplication => null;

        public PptState CurrentState => GetStateSnapshot();

        public void StartMonitoring()
        {
            if (_disposed) return;
            if (_client != null) return;

            _client = new PptAgentPipeClient();
            _client.ConnectionChanged += OnConnectionChanged;
            _client.StateReceived += state => HandleState(state, null);
            _client.EventReceived += (eventName, state) => HandleState(state, eventName);
            _client.Start();

            LogHelper.WriteLogToFile("PPT Agent 联动监控已启动", LogHelper.LogType.Event);
        }

        public void StopMonitoring(bool isShutdown = false)
        {
            var client = _client;
            if (client == null) return;

            client.ConnectionChanged -= OnConnectionChanged;
            client.Dispose();
            _client = null;
            OnConnectionChanged(false);
            LogHelper.WriteLogToFile("PPT Agent 联动监控已停止", LogHelper.LogType.Event);
        }

        public void ReloadConnection()
        {
            StopMonitoring();
            StartMonitoring();
        }

        public bool TryStartSlideShow() => Send(PptCommands.StartSlideShow);
        public bool TryEndSlideShow() => Send(PptCommands.EndSlideShow);
        public bool TryNavigateNext() => Send(PptCommands.Next);
        public bool TryNavigatePrevious() => Send(PptCommands.Previous);

        public bool TryNavigateToSlide(int slideNumber)
        {
            if (slideNumber <= 0) return false;
            return Send(PptCommands.GotoSlide, new GotoSlideRequest { SlideNumber = slideNumber });
        }

        public int GetCurrentSlideNumber() => GetStateSnapshot().SlideIndex;

        public string GetPresentationName() => GetStateSnapshot().PresentationName ?? string.Empty;

        public bool TryShowSlideNavigation() => Send(PptCommands.ShowSlideNavigation);

        public bool TryUnhideHiddenSlides() => Send(PptCommands.UnhideHiddenSlides);

        public bool TryDisableAutoPlayTimings() => Send(PptCommands.DisableAutoPlayTimings);

        public object GetCurrentActivePresentation() => null;

        public List<PptSlideThumbnail> ExportSlideThumbnails(int width, int height)
        {
            var response = _client?.SendRequest<ExportSlideThumbnailsResponse>(
                PptCommands.ExportSlideThumbnails,
                new ExportSlideThumbnailsRequest { Width = width, Height = height });

            if (response?.Slides == null)
                return new List<PptSlideThumbnail>();

            return response.Slides
                .Where(s => s != null && s.PngBytes != null)
                .Select(s => new PptSlideThumbnail { SlideNumber = s.SlideNumber, PngBytes = s.PngBytes })
                .ToList();
        }

        private bool Send(string command, object data = null)
        {
            var result = _client?.SendCommand(command, data) == true;
            if (!result)
                LogHelper.WriteLogToFile($"PPT Agent 命令发送失败: {command}", LogHelper.LogType.Warning);
            return result;
        }

        private void OnConnectionChanged(bool connected)
        {
            lock (_stateLock)
            {
                if (_lastConnected == connected) return;
                _lastConnected = connected;
                if (!connected)
                {
                    _lastState = new PptState();
                    _lastIsRunning = false;
                    _lastSlideIndex = 0;
                    _lastPresentationName = null;
                }
            }

            PPTConnectionChanged?.Invoke(connected);
            if (!connected)
                SlideShowStateChanged?.Invoke(false);
        }

        private void HandleState(PptState state, string eventName)
        {
            if (state == null) return;

            bool raiseConnection = false;
            bool raiseShowStateChanged = false;
            bool newShowState = false;
            bool raisePresentationOpen = false;
            bool raisePresentationClose = false;
            bool raiseSlideShowBegin = false;
            bool raiseSlideShowNext = false;
            bool raiseSlideShowEnd = false;

            lock (_stateLock)
            {
                var oldRunning = _lastIsRunning;
                var oldSlide = _lastSlideIndex;
                var oldPresentation = _lastPresentationName;

                _lastState = state;
                _lastIsRunning = state.IsRunning;
                _lastSlideIndex = state.SlideIndex;
                _lastPresentationName = state.PresentationName;

                if (!_lastConnected)
                {
                    _lastConnected = true;
                    raiseConnection = true;
                }

                if (!string.IsNullOrEmpty(eventName))
                {
                    raisePresentationOpen = eventName == PptEvents.PresentationOpen;
                    raisePresentationClose = eventName == PptEvents.PresentationClose;
                    raiseSlideShowBegin = eventName == PptEvents.SlideShowBegin;
                    raiseSlideShowNext = eventName == PptEvents.SlideShowNextSlide;
                    raiseSlideShowEnd = eventName == PptEvents.SlideShowEnd;
                }
                else
                {
                    var hasPresentation = !string.IsNullOrEmpty(state.PresentationName);
                    var hadPresentation = !string.IsNullOrEmpty(oldPresentation);
                    raisePresentationOpen = hasPresentation && (!hadPresentation || !string.Equals(oldPresentation, state.PresentationName, StringComparison.OrdinalIgnoreCase));
                    raisePresentationClose = !hasPresentation && hadPresentation;
                    raiseSlideShowBegin = !oldRunning && state.IsRunning;
                    raiseSlideShowNext = oldRunning && state.IsRunning && oldSlide > 0 && state.SlideIndex > 0 && oldSlide != state.SlideIndex;
                    raiseSlideShowEnd = oldRunning && !state.IsRunning;
                }

                if (oldRunning != state.IsRunning || raiseSlideShowBegin || raiseSlideShowEnd)
                {
                    raiseShowStateChanged = true;
                    newShowState = state.IsRunning;
                }
            }

            if (raiseConnection) PPTConnectionChanged?.Invoke(true);
            if (raisePresentationOpen) PresentationOpen?.Invoke(state);
            if (raisePresentationClose) PresentationClose?.Invoke(state);
            if (raiseShowStateChanged) SlideShowStateChanged?.Invoke(newShowState);
            if (raiseSlideShowBegin) SlideShowBegin?.Invoke(state);
            if (raiseSlideShowNext) SlideShowNextSlide?.Invoke(state);
            if (raiseSlideShowEnd) SlideShowEnd?.Invoke(state);
        }

        private PptState GetStateSnapshot()
        {
            lock (_stateLock)
            {
                return new PptState
                {
                    SlideIndex = _lastState.SlideIndex,
                    TotalSlides = _lastState.TotalSlides,
                    IsRunning = _lastState.IsRunning,
                    PresentationName = _lastState.PresentationName,
                    PresentationFullName = _lastState.PresentationFullName,
                    HasHiddenSlides = _lastState.HasHiddenSlides,
                    HasAutoPlayTimings = _lastState.HasAutoPlayTimings
                };
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopMonitoring();
        }
    }
}
