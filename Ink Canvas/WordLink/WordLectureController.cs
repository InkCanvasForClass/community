using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using InkCanvasWordLink;
using Ink_Canvas.Controls;

namespace Ink_Canvas.WordLink
{
    internal sealed class WordLectureController : IDisposable
    {
        private readonly InkCanvas _canvas;
        private readonly WordLectureBar _bar;
        private readonly WordInkProjection _projection;
        private readonly WordAnnotationStore _store;
        private readonly IWordFrameSource _host;
        private readonly Func<long, long> _targetWindow;
        private readonly Func<uint> _inputStamp;
        private readonly DispatcherTimer _timer;
        private readonly WordViewChangeWatcher _viewWatcher;
        private long _lastViewChange;
        private long _viewVersion;
        private bool _discardedViewFrame;
        private bool _viewRefreshQueued;
        private readonly WordRefreshGate _refreshGate = new();
        private bool _scrollPendingView;
        private long _scrollVersion;
        private readonly Action<bool> _pausePresentation;
        private readonly Func<bool> _allowed, _inputActive;
        private readonly Func<bool> _annotationEnabled;
        private readonly bool _automaticPolling;
        private readonly Action<bool> _ownershipChanged;
        private readonly Action<Action> _mutate;
        private readonly Action<bool, bool> _historyChanged;
        private readonly Action _clearTransient;
        private readonly Action<long> _placeWindow;
        private long _placementTarget;
        private readonly Action<bool> _focusChanged;
        private bool _focusMode;
        private bool _autoEnterFocus;
        private bool? _targetFocus;
        private long _focusCommandWindow;
        private string _focusDocumentKey, _focusCommandKey;
        private readonly HashSet<string> _autoFocusAttempted = new();
        internal long WindowHandle => _lastWindow;
        private readonly Dictionary<string, WordInkDocument> _cache = new();
        private readonly CancellationTokenSource _shutdown = new();
        private StrokeCollection _desktop;
        private Geometry _desktopClip;
        private bool _desktopHitTest;
        private bool _desktopAnnotationEnabled;
        private WordInkDocument _document;
        private WordFrame _frame;
        private string _key, _revision;
        private string _sessionKey;
        private long _readRetryAfter;
        private long _lastWindow;
        private bool _busy, _enabled, _layoutPaused, _savingFailed;
        private string _nextRevision;
        private int _targetPage, _navigationTarget, _generation;
        private int _targetZoom;
        private long _zoomWindow;
        private bool _disposed;
        private Task _hostWarmup = Task.CompletedTask;
        private bool _inkCommitQueued;
        private bool _transientPaused;
        private int _transientFailures;
        private long _lastInkInput;
        private long _lastFullViewCheck;
        private long _confirmedViewVersion, _lastConfirmedViewAt;
        private uint _confirmedInputStamp;
        private uint _queryInputStamp;
        private long _nativeMotionUntil;
        private (bool Undo, bool Redo)? _publishedHistory;
        internal bool IsActive => _desktop != null;
        internal bool BlocksInput => IsActive && (_frame == null || _frame.NeedsRefinement || _targetFocus != null || _targetZoom > 0 || _layoutPaused || _savingFailed || _discardedViewFrame || _scrollPendingView || _transientPaused);

        internal WordLectureController(InkCanvas canvas, WordLectureBar bar, string storage,
            Func<bool> allowed, Func<bool> inputActive, Action<bool> ownershipChanged,
            Action<Action> mutate, Action<bool, bool> historyChanged,
            IWordFrameSource host = null, Func<long, long> targetWindow = null, Action clearTransient = null,
            Func<bool> annotationEnabled = null, bool automaticPolling = true, Action<bool> pausePresentation = null,
            Action<Matrix?> previewTransform = null, Func<uint> inputStamp = null, Action<long> placeWindow = null,
            Action<bool> focusChanged = null)
        {
            _canvas = canvas; _bar = bar; _allowed = allowed; _inputActive = inputActive;
            _ownershipChanged = ownershipChanged; _mutate = mutate; _historyChanged = historyChanged;
            _clearTransient = clearTransient ?? (() => { });
            _placeWindow = placeWindow ?? (_ => { });
            _focusChanged = focusChanged ?? (_ => { });
            _annotationEnabled = annotationEnabled ?? (() => _canvas.EditingMode != InkCanvasEditingMode.None);
            _automaticPolling = automaticPolling;
            _pausePresentation = pausePresentation ?? (_ => { });
            _host = host ?? new WordHostClient(); _targetWindow = targetWindow ?? WordWindowSelector.GetTarget;
            _inputStamp = inputStamp ?? WordWindowSelector.InputStamp;
            _store = new WordAnnotationStore(storage); _projection = new WordInkProjection(canvas, previewTransform);
            _canvas.Strokes.StrokesChanged += OnChanged;
            _timer = new DispatcherTimer(DispatcherPriority.Input, canvas.Dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
            _timer.Tick += Tick;
            _viewWatcher = new WordViewChangeWatcher(NotifyViewChanged, NotifyNativeScroll);
            bar.Navigate += Navigate;
            bar.Resume += Resume;
            bar.Zoom += Zoom;
        }

        internal void Configure(bool enabled, bool showNavigation, bool autoEnterFocus = false)
        {
            if (autoEnterFocus != _autoEnterFocus || !enabled) _autoFocusAttempted.Clear();
            _autoEnterFocus = autoEnterFocus;
            _bar.ShowNavigation = showNavigation;
            if (enabled && !_enabled)
            {
                _hostWarmup = WarmHostAsync();
                _refreshGate.Immediate();
                _timer.Interval = TimeSpan.FromMilliseconds(16);
            }
            _enabled = enabled;
            if (enabled) { if (_automaticPolling) _timer.Start(); }
            else { ++_generation; _timer.Stop(); Suspend(); _host.Stop(); }
        }

        private async Task WarmHostAsync()
        {
            try { await _host.WarmAsync(_shutdown.Token); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (Exception ex)
            { Helpers.LogHelper.WriteLogToFile($"[WordLink] Warm-up failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Warning); }
        }

        internal void NotifyViewChanged()
        {
            if (_disposed || !_enabled) return;
            // Window movement must remain responsive even while Office is busy.
            _placeWindow(_placementTarget);
            ++_viewVersion;
            _lastViewChange = Environment.TickCount64;
            if (!_automaticPolling || _busy || _viewRefreshQueued) return;
            // Real Word input must not wait behind the 750ms idle timer. Office
            // notifications alone still cannot create a polling feedback loop.
            if (_inputStamp() != _confirmedInputStamp) _refreshGate.InputChanged(Environment.TickCount64);
            _viewRefreshQueued = true;
            _canvas.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(async () =>
            {
                _viewRefreshQueued = false;
                if (_enabled && !_disposed) await PollAsync();
            }));
        }

        internal void NotifyScrolled()
        {
            if (_disposed || !_enabled) return;
            ++_scrollVersion;
            if (IsActive && !_inputActive() && !_scrollPendingView)
            {
                _scrollPendingView = true;
                _canvas.IsHitTestVisible = false;
                _clearTransient();
                // Keep the existing ink visible until the next confirmed view.
            }
            _refreshGate.Scrolled(Environment.TickCount64);
            NotifyViewChanged();
        }

        internal void NotifyNativeScroll(bool directScroll)
        {
            long now = Environment.TickCount64;
            bool inputChanged = _inputStamp() != _queryInputStamp;
            if (directScroll || inputChanged) _nativeMotionUntil = now + 250;
            // Weak location events can be delayed echoes of our own COM call.
            // Only actual input, direct scrolling, or recently measured motion
            // may extend a gesture; notifications alone cannot keep it alive.
            if (directScroll || inputChanged || now < _nativeMotionUntil) NotifyScrolled();
            else NotifyViewChanged();
        }

        private async void Tick(object sender, EventArgs e)
            => await PollAsync();

        internal async Task PollAsync()
        {
            if (_busy || _disposed || !_enabled) return;
            int delay = _refreshGate.Delay(Environment.TickCount64);
            if (_automaticPolling && delay > 0)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(delay);
                if (!_timer.IsEnabled) _timer.Start();
                return;
            }
            _busy = true;
            _timer.Stop();
            long cycleStarted = Environment.TickCount64;
            bool viewMoved = false;
            bool? cachedGeometry = null;
            bool waitingForDocument = false;
            int generation = _generation;
            try
            {
                if (!_allowed()) { Suspend(); return; }
                if (_inputActive() && !_scrollPendingView && !_transientPaused) return;
                Capture();
                if (_document?.Dirty == true && !_document.Saving && Environment.TickCount64 >= _document.RetryAfter)
                    _ = SaveAsync(_key, _revision, _document);
                long target = _targetWindow(_lastWindow);
                if (target == 0) { Suspend(); return; }
                _placementTarget = target; _placeWindow(target);
                if (_targetZoom > 0 && target != _zoomWindow) _targetZoom = 0;
                if (_targetFocus != null && target != _focusCommandWindow) _targetFocus = null;
                _viewWatcher.Observe(target);
                Enter();
                uint inputStamp = _inputStamp();
                _queryInputStamp = inputStamp;
                // Keep checking window ownership without entering Office while
                // its confirmed view is idle. Native view events wake geometry
                // queries. After new input, a slower fallback detects missed
                // events/edits without continuously waking a completely idle Word.
                if (_automaticPolling && _frame?.WindowHandle == target && !BlocksInput && _targetPage == 0
                    && _viewVersion == _confirmedViewVersion && (_confirmedInputStamp == inputStamp
                        || Environment.TickCount64 - _lastConfirmedViewAt < 5000))
                    return;
                if (Environment.TickCount64 < _readRetryAfter) { _bar.SetState("LoadFailed", "", 0, 0, false); return; }
                var request = new WordRequest { WindowHandle = target, TargetPage = _targetFocus != null ? 0 : _targetPage,
                    ExpectedDocumentKey = _targetFocus != null ? _focusCommandKey : _key,
                    TargetZoomPercentage = _targetFocus != null ? 0 : _targetZoom,
                    TargetFocusMode = _targetFocus,
                    ScrollPreview = _refreshGate.IsScrolling(Environment.TickCount64),
                    VerifyCachedView = !_refreshGate.IsScrolling(Environment.TickCount64) && _frame != null && (_scrollPendingView
                        || Environment.TickCount64 - _lastFullViewCheck < 5000),
                    DeferContentCheck = _targetZoom > 0 || _scrollPendingView || Environment.TickCount64 - _lastViewChange < 700 || _navigationTarget > 0
                        || Environment.TickCount64 - _lastInkInput < 1500 };
                _targetPage = 0;
                long viewVersion = _viewVersion, scrollVersion = _scrollVersion;
                await _hostWarmup;
                if (_disposed || generation != _generation || !_enabled) return;
                var frame = await _host.ExchangeAsync(request, _shutdown.Token);
                waitingForDocument = frame?.Status == "Waiting";
                if (frame?.Status == "Ready") cachedGeometry = frame.CachedGeometry;
                if (_disposed || generation != _generation || !_enabled || !_allowed()) return;
                if (_inputActive() && !_scrollPendingView && !_transientPaused) return;
                // The user may have switched applications during the helper request.
                if (_targetWindow(target) != target) { Suspend(); return; }
                bool scrolling = _refreshGate.IsScrolling(Environment.TickCount64) || scrollVersion != _scrollVersion;
                if (frame?.Status == "Ready" && _targetFocus is { } desiredFocus)
                {
                    if (frame.DocumentKey != request.ExpectedDocumentKey) _targetFocus = null;
                    else if (frame.IsFocusMode != desiredFocus && request.TargetFocusMode != _targetFocus)
                    { _discardedViewFrame = true; _canvas.IsHitTestVisible = false; return; }
                    else _targetFocus = null;
                }
                if (frame?.Status == "Ready" && _targetZoom > 0)
                {
                    if (frame.DocumentKey != _key) _targetZoom = 0;
                    else if (frame.ZoomPercentage != _targetZoom && request.TargetZoomPercentage != _targetZoom)
                    {
                        // A newer click supersedes a reply already in flight.
                        // Keep one absolute target, never a queue of increments.
                        _discardedViewFrame = true; _canvas.IsHitTestVisible = false;
                        return;
                    }
                    // The current request's read-back is authoritative if Word
                    // constrained the value; do not retry an unsupported zoom forever.
                    else _targetZoom = 0;
                }
                if (frame?.Status == "Ready" && _targetPage > 0 && request.TargetPage != _targetPage)
                {
                    _discardedViewFrame = true; _canvas.IsHitTestVisible = false;
                    return;
                }
                if (!scrolling && frame?.Status == "Ready" && !frame.NeedsRefinement && viewVersion != _viewVersion
                    && !_discardedViewFrame)
                {
                    // A view notification can invalidate even a fast reply. Replace it
                    // with one fresh request, without queueing every scroll event.
                    // At most one consecutive discard prevents continuous-motion
                    // starvation if Word itself remains slow.
                    _discardedViewFrame = true; _canvas.IsHitTestVisible = false;
                    return;
                }
                _discardedViewFrame = false;
                if (frame == null || frame.Status != "Ready")
                {
                    if (frame?.Status == "Locating" && scrolling && _frame != null && !_transientPaused)
                    {
                        // A text anchor can leave the viewport between samples.
                        // Retain the last visual during motion rather than flash
                        // an empty canvas; editing still requires a settled frame.
                        _scrollPendingView = true;
                        _canvas.IsHitTestVisible = false;
                        return;
                    }
                    if (frame?.Status is "Busy" or "Timeout" or "Locating")
                    {
                        ++_transientFailures;
                        if (request.TargetPage > 0 && _targetPage == 0 && _navigationTarget == request.TargetPage)
                            _targetPage = request.TargetPage;
                        _transientPaused = true;
                        _canvas.IsHitTestVisible = false;
                        PausePresentation(true);
                        // Keep canonical and projected ink while Word recovers;
                        // never accept drawing against unverified old geometry.
                        _bar.SetState(frame.Status, _frame?.DocumentName ?? "", _frame?.CurrentPage ?? 0,
                            _frame?.PageCount ?? 0, false, _frame?.ZoomPercentage ?? 0);
                        return;
                    }
                    _targetZoom = 0; _zoomWindow = 0;
                    _targetFocus = null;
                    SetFocusMode(false);
                    Capture(); _frame = null;
                    _mutate(() => { _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; _canvas.IsHitTestVisible = false; });
                    _projection.Reset();
                    _bar.SetState(frame?.Status ?? "Unavailable", "", 0, 0, false);
                    return;
                }
                _lastWindow = frame.WindowHandle;
                _focusDocumentKey = frame.DocumentKey;
                SetFocusMode(frame.IsFocusMode);
                if (_navigationTarget == frame.CurrentPage && _targetPage == 0) _navigationTarget = 0;
                if (_key != frame.DocumentKey) _layoutPaused = false;
                if (_key != frame.DocumentKey && _sessionKey == frame.SessionKey && WordFrame.IsHash(_sessionKey)
                    && _revision == frame.Revision && _document != null)
                {
                    // Saving an untitled document / Save As changes its path, not its
                    // live COM document identity. Carry ink forward to the new key.
                    Capture();
                    _cache.Remove(_key + _revision);
                    _key = frame.DocumentKey;
                    _cache[_key + _revision] = _document;
                    _document.Dirty = true; ++_document.Version;
                    _ = SaveAsync(_key, _revision, _document);
                }
                if (_key == frame.DocumentKey && _revision != null && _revision != frame.Revision)
                {
                    Capture(); _layoutPaused = true; _nextRevision = frame.Revision;
                    _mutate(() => { _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; });
                    _projection.Reset(); _frame = null;
                    _bar.SetState("LayoutChanged", frame.DocumentName, frame.CurrentPage, frame.PageCount, true);
                    return;
                }
                if (_layoutPaused)
                {
                    _bar.SetState("LayoutChanged", frame.DocumentName, frame.CurrentPage, frame.PageCount, true);
                    return;
                }
                bool changedDocument = _key != frame.DocumentKey || _revision != frame.Revision;
                if (changedDocument)
                {
                    Capture();
                    _frame = null;
                    _mutate(() => { _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; });
                    _projection.Reset();
                    string cacheKey = frame.DocumentKey + frame.Revision;
                    if (!_cache.TryGetValue(cacheKey, out var document))
                    {
                        if (_cache.Count >= 4)
                        {
                            var clean = _cache.FirstOrDefault(p => !p.Value.Dirty);
                            if (clean.Value == null) { _bar.SetState("SaveFailed", frame.DocumentName, 0, 0, false); return; }
                            _cache.Remove(clean.Key);
                        }
                        _bar.SetState("Loading", frame.DocumentName, frame.CurrentPage, frame.PageCount, false);
                        await _store.WaitPendingAsync();
                        document = await _store.LoadAsync(frame.DocumentKey, frame.Revision);
                        if (_disposed || generation != _generation) return;
                        _cache[cacheKey] = document;
                    }
                    _key = frame.DocumentKey; _revision = frame.Revision; _document = document;
                    _savingFailed = false;
                }
                if (frame.Pages.Any(p => _document.Pages.GetValueOrDefault(p.Number)?.Count > 0
                    && _document.Layout.TryGetValue(p.Number, out var layout) && layout != p.LayoutKey))
                {
                    _layoutPaused = true; _nextRevision = frame.Revision; _frame = null;
                    _mutate(() => { _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; });
                    _projection.Reset();
                    _bar.SetState("LayoutChanged", frame.DocumentName, frame.CurrentPage, frame.PageCount, true);
                    return;
                }
                bool moved = _frame == null || !SameViewport(_frame, frame);
                if (moved && scrolling) _nativeMotionUntil = Environment.TickCount64 + 250;
                viewMoved = moved;
                _frame = frame;
                _sessionKey = frame.SessionKey;
                _confirmedViewVersion = _viewVersion;
                _lastConfirmedViewAt = Environment.TickCount64;
                _confirmedInputStamp = inputStamp;
                if ((!request.ScrollPreview && !request.VerifyCachedView) || changedDocument)
                    _lastFullViewCheck = Environment.TickCount64;
                bool previewed = scrolling && _projection.TryPreview(frame, _document);
                if (!previewed && (moved || _projection.IsPreviewing))
                    _mutate(() => { _clearTransient(); _projection.Show(frame, _document); });
                _scrollPendingView = scrolling;
                if (_transientPaused) { _transientPaused = false; PausePresentation(false); }
                _transientFailures = 0;
                _canvas.IsHitTestVisible = !BlocksInput && _annotationEnabled();
                _bar.SetState(_savingFailed ? "SaveFailed" : "Ready", frame.DocumentName, frame.CurrentPage, frame.PageCount, false, frame.ZoomPercentage);
                PublishHistory();
                if (_autoEnterFocus && !frame.NeedsRefinement && _targetFocus == null && !_inputActive()
                    && !_autoFocusAttempted.Contains(FocusDocumentKey(frame)))
                {
                    if (_autoFocusAttempted.Count >= 128) _autoFocusAttempted.Clear();
                    if (_autoFocusAttempted.Add(FocusDocumentKey(frame)) && !frame.IsFocusMode) RequestFocusMode(true);
                }
            }
            catch (Exception ex)
            {
                _discardedViewFrame = false;
                _readRetryAfter = Environment.TickCount64 + 5000;
                Helpers.LogHelper.WriteLogToFile($"[WordLink] Session paused: {ex.GetType().Name}", Helpers.LogHelper.LogType.Error);
                _frame = null;
                if (IsActive) _mutate(() => { _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; });
                _bar.SetState("LoadFailed", "", 0, 0, false);
            }
            finally
            {
                _busy = false;
                long now = Environment.TickCount64;
                _refreshGate.Complete(now, now - cycleStarted, viewMoved, cachedGeometry);
                if (_discardedViewFrame) _refreshGate.RetryAfter(now, 16);
                else if (_transientPaused) _refreshGate.RetryAfter(now, Math.Min(1500, 150 * Math.Max(1, _transientFailures)));
                else if (_scrollPendingView && !_refreshGate.IsScrolling(now)) _refreshGate.RetryAfter(now, 16);
                else if (_targetPage > 0 || _targetZoom > 0 || _targetFocus != null) _refreshGate.RetryAfter(now, 16);
                else if (_frame?.NeedsRefinement == true) _refreshGate.RetryAfter(now, 16);
                else if (waitingForDocument) _refreshGate.RetryAfter(now, 150);
                _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, _refreshGate.Delay(now)));
                if (_automaticPolling && _enabled && !_disposed) _timer.Start();
            }
        }

        private void Enter()
        {
            if (IsActive) return;
            _publishedHistory = null;
            _desktop = new StrokeCollection(_canvas.Strokes);
            _desktopClip = _canvas.Clip;
            _desktopHitTest = _canvas.IsHitTestVisible;
            _desktopAnnotationEnabled = _annotationEnabled();
            _ownershipChanged(true);
            _mutate(() => { WordInkProjection.ClearSelection(_canvas); _canvas.Strokes.Clear(); _canvas.Clip = Geometry.Empty; _canvas.IsHitTestVisible = false; });
        }

        internal void Suspend()
        {
            SetFocusMode(false);
            _viewWatcher.Dispose();
            _placementTarget = 0; _placeWindow(0);
            _discardedViewFrame = false;
            _refreshGate.Immediate();
            _nativeMotionUntil = 0;
            if (_scrollPendingView || _transientPaused)
            { _scrollPendingView = _transientPaused = false; PausePresentation(false); }
            _transientFailures = 0;
            ++_generation;
            if (!IsActive) { _bar.Visibility = Visibility.Collapsed; return; }
            Capture();
            _projection.Reset();
            bool annotation = _annotationEnabled();
            bool hitTest = annotation == _desktopAnnotationEnabled ? _desktopHitTest : annotation;
            _mutate(() => { WordInkProjection.ClearSelection(_canvas); _canvas.Strokes.Clear(); _canvas.Strokes.Add(_desktop); _canvas.Clip = _desktopClip; _canvas.IsHitTestVisible = hitTest; });
            _desktop = null; _desktopClip = null; _frame = null; _document = null;
            _key = _revision = _sessionKey = null; _layoutPaused = false; _targetPage = _navigationTarget = 0;
            _targetZoom = 0; _zoomWindow = 0;
            _targetFocus = null; _focusCommandWindow = 0;
            _focusDocumentKey = _focusCommandKey = null;
            _bar.Visibility = Visibility.Collapsed;
            _ownershipChanged(false);
            // Release the cached Office window when the user leaves Word or
            // closes its document; a retained RCW must not prolong Word's life.
            _host.Suspend();
        }

        private void SetFocusMode(bool focus)
        {
            if (_focusMode == focus) return;
            _focusMode = focus; _focusChanged(focus);
        }
        private static string FocusDocumentKey(WordFrame frame) => frame.DocumentKey + ":" + frame.WindowHandle;
        internal void RequestFocusMode(bool focus)
        {
            if (!_enabled || _inputActive() || !WordFrame.IsHash(_focusDocumentKey)
                || (_frame == null && (focus || !_focusMode))) return;
            Capture();
            _autoFocusAttempted.Add(_focusDocumentKey + ":" + _lastWindow);
            _targetFocus = focus; _focusCommandWindow = _lastWindow; _focusCommandKey = _focusDocumentKey;
            _readRetryAfter = 0;
            _canvas.IsHitTestVisible = false;
            _clearTransient(); _refreshGate.Immediate(); NotifyViewChanged();
        }

        private void OnChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            if (!IsActive || _projection.Updating) return;
            _projection.Changed();
            NotifyInkInputCompleted();
        }

        internal void NotifyInkInputCompleted()
        {
            if (_disposed || !_enabled || !IsActive || _inkCommitQueued) return;
            _lastInkInput = Environment.TickCount64;
            _inkCommitQueued = true;
            int generation = _generation;
            // Run after StrokeCollected and touch/stylus capture cleanup. This is
            // entirely local: a finished stroke must never wait for Office IPC.
            _canvas.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _inkCommitQueued = false;
                if (_disposed || generation != _generation || !_enabled || !IsActive
                    || _inputActive() || BlocksInput) return;
                try { Capture(); }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"[WordLink] Ink commit failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Error);
                }
            }));
        }

        private void PausePresentation(bool paused)
        {
            try { _pausePresentation(paused); }
            catch (Exception ex)
            { Helpers.LogHelper.WriteLogToFile($"[WordLink] Presentation update failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Warning); }
        }

        private void Capture()
        {
            if (_frame == null || _document == null || !_projection.HasChanges || _projection.Updating) return;
            var pages = _projection.Capture(_frame, _document);
            if (!_projection.EqualsContent(_document.Pages, pages))
            {
                _document.Undo.Push(_document.Pages); Trim(_document.Undo);
                _document.Redo.Clear(); _document.Pages = pages;
                foreach (var page in _frame.Pages) _document.Layout[page.Number] = page.LayoutKey;
                _document.Dirty = true; ++_document.Version;
                _ = SaveAsync(_key, _revision, _document);
            }
            _projection.Accept();
            _mutate(() => _projection.Show(_frame, _document));
            PublishHistory();
        }

        private async Task SaveAsync(string key, string revision, WordInkDocument document)
        {
            // Save As can move the destination while a snapshot is awaiting I/O.
            // The running loop must drain the new destination before marking clean.
            document.SaveKey = key; document.SaveRevision = revision;
            if (document.Saving) return;
            document.Saving = true;
            bool saved = false;
            try
            {
                do
                {
                    // Coalesce rapid strokes without delaying local undo or input.
                    await Task.Delay(150, _shutdown.Token);
                    if (_disposed || document.Superseded) return;
                    int version = document.Version;
                    string saveKey = document.SaveKey, saveRevision = document.SaveRevision;
                    saved = await _store.SaveCanonicalAsync(saveKey, saveRevision, document);
                    if (saved && document.Version == version && document.SaveKey == saveKey && document.SaveRevision == saveRevision)
                        document.Dirty = false;
                } while (saved && document.Dirty && !_disposed);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"[WordLink] Annotation snapshot failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Error);
            }
            finally { document.Saving = false; }
            document.RetryAfter = Environment.TickCount64 + (saved ? 0 : 5000);
            if (_disposed) return;
            if (ReferenceEquals(document, _document)) _savingFailed = !saved;
        }

        internal bool Undo(bool redo)
        {
            if (!IsActive) return false;
            if (_inputActive() || _frame == null || _document == null || _layoutPaused) return true;
            Capture();
            var source = redo ? _document.Redo : _document.Undo;
            var target = redo ? _document.Undo : _document.Redo;
            if (source.Count > 0)
            {
                target.Push(_document.Pages); Trim(target);
                _document.Pages = source.Pop(); _document.Dirty = true; ++_document.Version;
                _mutate(() => _projection.Show(_frame, _document));
                _ = SaveAsync(_key, _revision, _document);
            }
            PublishHistory(); return true;
        }

        private void PublishHistory()
        {
            var state = (_frame != null && _document?.Undo.Count > 0, _frame != null && _document?.Redo.Count > 0);
            if (_publishedHistory == state) return;
            _publishedHistory = state;
            _historyChanged(state.Item1, state.Item2);
        }
        private static void Trim(Stack<Dictionary<int, StrokeCollection>> stack)
        {
            if (stack.Count <= 24) return;
            var keep = stack.Take(24).Reverse().ToArray(); stack.Clear();
            foreach (var entry in keep) stack.Push(entry);
        }
        private void Navigate(int delta)
        {
            if (_frame == null || _inputActive()) return;
            Capture();
            int from = _navigationTarget > 0 ? _navigationTarget : _frame.CurrentPage;
            _targetPage = _navigationTarget = Math.Clamp(from + delta, 1, _frame.PageCount);
            _refreshGate.Immediate();
            NotifyViewChanged();
        }
        private void Zoom(int delta)
        {
            if (_frame == null || _inputActive() || _layoutPaused || _savingFailed || _transientPaused
                || _frame.NeedsRefinement || _frame.ZoomPercentage is < 10 or > 500) return;
            int target = Math.Clamp((_targetZoom > 0 ? _targetZoom : _frame.ZoomPercentage) + delta, 10, 500);
            if (target == (_targetZoom > 0 ? _targetZoom : _frame.ZoomPercentage)) return;
            Capture();
            _targetZoom = target; _zoomWindow = _frame.WindowHandle;
            _canvas.IsHitTestVisible = false;
            _clearTransient();
            _refreshGate.Immediate();
            NotifyViewChanged();
        }
        private void Resume()
        {
            // A fresh revision receives separate storage. Existing annotations remain
            // preserved under their previous revision and are never silently remapped.
            if (_document != null && _nextRevision == _revision)
            {
                _document.Superseded = true;
                string archive = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(_revision + ":layout-archive:" + Guid.NewGuid())));
                _ = _store.SaveAsync(_key, archive, _document);
                var fresh = new WordInkDocument { Dirty = true, Version = 1 };
                _cache[_key + _revision] = fresh;
                _ = SaveAsync(_key, _revision, fresh);
            }
            _layoutPaused = false; _revision = null; _nextRevision = null; _frame = null; _document = null;
        }
        private static bool SameViewport(WordFrame a, WordFrame b) => a.WindowHandle == b.WindowHandle
            && a.Viewport.X == b.Viewport.X && a.Viewport.Y == b.Viewport.Y
            && a.Viewport.Width == b.Viewport.Width && a.Viewport.Height == b.Viewport.Height
            && a.Pages.Count == b.Pages.Count && a.Pages.Zip(b.Pages).All(pair => pair.First.Number == pair.Second.Number
                && Math.Abs(pair.First.Bounds.X - pair.Second.Bounds.X) < .25
                && Math.Abs(pair.First.Bounds.Y - pair.Second.Bounds.Y) < .25
                && Math.Abs(pair.First.Bounds.Width - pair.Second.Bounds.Width) < .25
                && Math.Abs(pair.First.Bounds.Height - pair.Second.Bounds.Height) < .25);

        public void Dispose()
        {
            if (_disposed) return;
            _timer.Stop(); Suspend(); _disposed = true; _shutdown.Cancel(); _host.Dispose();
            // Queue the latest state before blocking for shutdown: an earlier save may
            // still be awaiting its UI continuation and must not hide newer ink.
            foreach (var entry in _cache.Where(p => p.Value.Dirty))
            {
                try { _ = _store.SaveCanonicalAsync(entry.Key[..64], entry.Key[64..], entry.Value); }
                catch (Exception ex) { Helpers.LogHelper.WriteLogToFile($"[WordLink] Final snapshot failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Error); }
            }
            _canvas.Strokes.StrokesChanged -= OnChanged;
            _timer.Tick -= Tick; _bar.Navigate -= Navigate; _bar.Resume -= Resume; _bar.Zoom -= Zoom;
            if (!_store.Flush(TimeSpan.FromSeconds(2)))
                Helpers.LogHelper.WriteLogToFile("[WordLink] Annotation writer did not finish during shutdown.", Helpers.LogHelper.LogType.Warning);
            _shutdown.Dispose();
        }
    }
}
