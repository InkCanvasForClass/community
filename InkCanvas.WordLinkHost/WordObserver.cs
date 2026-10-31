using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using InkCanvasWordLink;

namespace InkCanvasWordLinkHost;

/// <summary>Borrow existing Word windows only. Never create an Office application or change its lifetime.</summary>
internal sealed class WordObserver : IDisposable
{
    private readonly Action<string> _trace;
    internal WordObserver(Action<string> trace = null) { _trace = trace; }
    private readonly Dictionary<string, (long Time, string Hash)> _revisions = new();
    private string _lastKey;
    private long _lastDiscovery;
    private double _lastScale;
    private readonly List<WordAnchor> _anchors = new();
    private readonly Dictionary<int, WordPageFrame> _pageLayouts = new();
    private long _metadataAt;
    private int _pageCount;
    private string _revision;
    private int _currentPage;
    private int _lastSelectionPage;
    private int _discoveryCursor;
    private WordFrame _lastReadyFrame;
    private long _lastDocumentIdentity;
    private long _previewFallbackAt;
    private object _boundWindow;
    private object _boundDocumentObject;
    private IntPtr _boundRoot, _boundDocument;
    private uint _boundProcess;
    private object _focusCommandBars;
    private bool _focusMode, _focusCommandSupported = true;
    private Native.Rect _focusViewport;
    private long _focusCheckedAt;
    internal int NativeAttachments { get; private set; }
    internal int PreviewReads { get; private set; }
    internal int DiscoverySamples { get; private set; }
    internal int PageBoundaryReads { get; private set; }
    internal int ContentReads { get; private set; }
    internal int FocusReads { get; private set; }
    internal string LastDiagnostic { get; private set; }

    internal WordFrame Read(WordRequest request)
    {
        var root = new IntPtr(request.WindowHandle);
        if (!Native.IsWindow(root) || Native.IsIconic(root) || !Native.IsWindowVisible(root)) return new();
        Native.GetWindowThreadProcessId(root, out uint pid);
        using var process = Process.GetProcessById((int)pid);
        if (!process.ProcessName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase)) return new();
        IntPtr documentHwnd = Native.FindDocument(root);
        if (documentHwnd == IntPtr.Zero) return new WordFrame { Status = "Waiting" };
        using var scope = new ComScope();
        Guid dispatch = new("00020400-0000-0000-C000-000000000046");
        if (_boundWindow == null || _boundRoot != root || _boundDocument != documentHwnd || _boundProcess != pid)
        {
            ResetConnection();
            _trace?.Invoke("native-attach");
            Marshal.ThrowExceptionForHR(Native.AccessibleObjectFromWindow(documentHwnd, 0xfffffff0, ref dispatch, out object native));
            if (!new OfficeObject(native).Supports(new Guid("00020962-0000-0000-c000-000000000046")))
            {
                Marshal.ReleaseComObject(native);
                return new WordFrame { Status = "Unavailable" };
            }
            _boundWindow = native; _boundRoot = root; _boundDocument = documentHwnd; _boundProcess = pid;
            NativeAttachments++;
        }
        _trace?.Invoke("document");
        // Retain the native window only on this helper's STA. Recreating the
        // automation binding for every poll needlessly re-enters Office setup.
        var window = new OfficeObject(_boundWindow);
        // Keep one owning RCW reference between requests. Releasing the last
        // document reference lets its proxy identity change on the next poll,
        // which falsely invalidates every anchor and document revision cache.
        object currentDocument = window.Get<object>("Document");
        if (ReferenceEquals(currentDocument, _boundDocumentObject)) Marshal.ReleaseComObject(currentDocument);
        else
        {
            if (_boundDocumentObject != null) Marshal.ReleaseComObject(_boundDocumentObject);
            _boundDocumentObject = currentDocument;
        }
        var document = new OfficeObject(_boundDocumentObject);
        IntPtr documentIdentity = Marshal.GetIUnknownForObject(document.Value);
        long identityValue;
        try { identityValue = documentIdentity.ToInt64(); }
        finally { Marshal.Release(documentIdentity); }
        if ((request.ScrollPreview || request.VerifyCachedView) && request.TargetPage == 0 && request.TargetZoomPercentage == 0 && request.TargetFocusMode == null
            && _lastReadyFrame?.NeedsRefinement == false
            && _lastReadyFrame.WindowHandle == request.WindowHandle
            && request.ExpectedDocumentKey == _lastReadyFrame.DocumentKey && identityValue == _lastDocumentIdentity)
        {
            var preview = ReadPreview(scope, window.Value, document.Value, root, documentHwnd, request.VerifyCachedView);
            if (preview != null) return preview;
            // Crossing a page can invalidate every cached character. Discovery
            // is still needed, but never perform a full search on every wheel tick.
            if (Environment.TickCount64 < _previewFallbackAt) return new WordFrame { Status = "Locating" };
            _previewFallbackAt = Environment.TickCount64 + 500;
        }
        var view = scope.Keep(window.Get<object>("View"));
        var panes = scope.Keep(window.Get<object>("Panes"));
        if (view.Get<int>("Type") != 3 || panes.Get<int>("Count") != 1) return new WordFrame { Status = "PrintLayoutRequired" };
        string path = document.Get<string>("Path");
        string name = document.Get<string>("Name");
        string identity = path.Length > 0 ? (document.Get<string>("FullName")).ToUpperInvariant()
            : $"unsaved:{pid}:{process.StartTime.ToUniversalTime().Ticks}:{root.ToInt64()}:{name}";
        string key = Hash(identity);
        IntPtr identityPointer = Marshal.GetIUnknownForObject(document.Value);
        string sessionKey;
        try { sessionKey = Hash($"{pid}:{process.StartTime.ToUniversalTime().Ticks}:{root.ToInt64()}:{identityPointer.ToInt64()}"); }
        finally { Marshal.Release(identityPointer); }
        if (_lastKey != key || (_lastReadyFrame != null && identityValue != _lastDocumentIdentity))
        {
            _anchors.Clear(); _pageLayouts.Clear(); _revisions.Remove(key);
            _metadataAt = 0; _currentPage = _lastSelectionPage = 0; _lastKey = key; _previewFallbackAt = 0;
            _lastReadyFrame = null;
        }
        var content = scope.Keep(document.Get<object>("Content"));
        // Page count, section geometry and whole-document text are expensive in Word.
        // Keep them off the ordinary scroll path and invalidate page metadata only
        // when the document revision actually changes.
        if (_metadataAt == 0 || (!request.DeferContentCheck && Environment.TickCount64 - _metadataAt >= 3000))
        {
            _trace?.Invoke("revision");
            _pageCount = Convert.ToInt32(content.Get<object>("Information", 4));
            string revision = Revision(scope, document, content, key, _pageCount);
            if (_revision != revision) { _anchors.Clear(); _pageLayouts.Clear(); }
            _revision = revision; _metadataAt = Environment.TickCount64;
        }
        int total = _pageCount;
        if (total <= 0) return new WordFrame { Status = "Locating" };
        if (request.TargetFocusMode is { } focusTarget && request.ExpectedDocumentKey == key
            && Native.GetWindowRect(documentHwnd, out var focusViewport)
            && ReadFocusMode(scope, window, view, focusViewport, force: true) != focusTarget)
        {
            if (_focusCommandSupported)
            {
                // ExecuteMso targets the application's active window. A user can
                // switch Word windows while this request is in flight.
                var application = scope.Keep(window.Get<object>("Application"));
                var activeWindow = scope.Keep(application.Get<object>("ActiveWindow"));
                if (activeWindow.Get<int>("Hwnd") != window.Get<int>("Hwnd"))
                    return new WordFrame { Status = "Locating" };
                new OfficeObject(_focusCommandBars).Call("ExecuteMso", "ViewFocusModeView");
            }
            else view.Set("FullScreen", focusTarget);
            _focusCheckedAt = 0;
            // Focus changes layout and can replace the native document surface.
            // Reattach and calibrate on the next request before accepting ink.
            return new WordFrame { Status = "Locating" };
        }
        var zoom = scope.Keep(view.Get<object>("Zoom"));
        int percentage = zoom.Get<int>("Percentage");
        // Absolute targets are safe to repeat after a busy/unknown reply. Verify
        // document identity first: switching documents must not redirect a click.
        if (request.TargetZoomPercentage is >= 10 and <= 500 && request.ExpectedDocumentKey == key
            && percentage != request.TargetZoomPercentage)
        {
            zoom.Set("Percentage", request.TargetZoomPercentage);
            percentage = zoom.Get<int>("Percentage");
            _lastDiscovery = 0;
        }
        object navigationRange = null;
        if (request.TargetPage > 0 && request.ExpectedDocumentKey == key)
        {
            var target = scope.Keep(document.GoTo(Math.Clamp(request.TargetPage, 1, total)));
            window.Call("ScrollIntoView", target.Value, true);
            navigationRange = target.Value;
            _anchors.Clear();
        }
        if (!Native.GetWindowRect(documentHwnd, out var viewport)) return new WordFrame { Status = "Locating" };
        _trace?.Invoke("geometry");
        double scale = Native.GetDpiForWindow(root) / 72.0 * percentage / 100.0;
        bool discover = Environment.TickCount64 - _lastDiscovery >= 2000 || Math.Abs(scale - _lastScale) > .001;
        _lastScale = scale;
        var frame = new WordFrame { DocumentKey = key, DocumentName = name, WindowHandle = root.ToInt64(), PageCount = total,
            SessionKey = sessionKey, Viewport = viewport.ToDto(), Revision = _revision, ZoomPercentage = percentage };
        var candidates = new List<WordAnchor>();
        if (navigationRange != null)
        {
            try
            {
                WordAnchor? navigated = ReadAnchor(window.Value, navigationRange);
                if (navigated.HasValue) candidates.Add(navigated.Value);
            }
            catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
        }
        foreach (var old in _anchors)
        {
            // A previously calibrated character plus the current DPI/zoom gives
            // a scroll origin. Avoid sampling two points at different animation
            // instants; full stationary queries still validate multiple anchors.
            if (request.ScrollPreview && candidates.Any(a => a.Page == old.Page)) continue;
            _trace?.Invoke("cached-anchor");
            if (candidates.Any(a => a.Start == old.Start)) continue;
            try
            {
                var range = scope.Keep(document.Call("Range", old.Start, old.Start + 1));
                int left = 0, top = 0, width = 0, height = 0;
                window.GetPoint(out left, out top, out width, out height, range);
                candidates.Add(old with { ScreenX = left, ScreenY = top });
            }
            catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
        }
        int centerPage = 0;
        double centerX = viewport.Left + (viewport.Right - viewport.Left) / 2.0;
        double centerY = viewport.Top + (viewport.Bottom - viewport.Top) / 2.0;
        var known = new List<(int Number, WordRect Bounds)>();
        foreach (var group in candidates.GroupBy(a => a.Page))
            if (_pageLayouts.TryGetValue(group.Key, out var layout))
            {
                var bounds = WordPageGeometry.Solve(group.ToArray(), layout.WidthPoints, layout.HeightPoints, scale);
                if (bounds != null) known.Add((group.Key, bounds));
            }
        centerPage = known.Where(p => p.Bounds.X <= centerX && centerX <= p.Bounds.X + p.Bounds.Width
            && p.Bounds.Y <= centerY && centerY <= p.Bounds.Y + p.Bounds.Height).Select(p => p.Number).FirstOrDefault();
        bool knownSpread = known.Count > 1 && known.Any(p => known.Any(other => p.Number != other.Number
            && Math.Abs(p.Bounds.Y - other.Bounds.Y) < 10 && p.Bounds.Y <= centerY && centerY <= p.Bounds.Y + p.Bounds.Height));
        // Cached geometry is enough while it covers the centre. Immediately
        // discover newly exposed pages, rather than waiting two seconds; avoid
        // repeating expensive hit tests for ordinary movement within a page.
        if (centerPage == 0 && !knownSpread)
        {
            try
            {
                _trace?.Invoke("center-hit");
                DiscoverySamples++;
                var hit = scope.Keep(window.Call("RangeFromPoint", (int)centerX, (int)centerY));
                if (hit?.IsRange == true)
                {
                    var range = scope.Keep(hit.Get<object>("Duplicate"));
                    WordAnchor? center = ReadAnchor(window.Value, range.Value);
                    if (center.HasValue) { centerPage = center.Value.Page; candidates.Insert(0, center.Value); }
                }
            }
            catch (Exception ex) when (IsUnavailablePoint(ex)) { }
        }
        // Sampling discovers multiple visible pages; cached ranges make steady-state
        // polling cheap. A soft budget bounds work between individual COM calls.
        var budget = Stopwatch.StartNew();
        bool firstPage = _lastReadyFrame == null && candidates.Count > 0 && request.TargetPage == 0;
        bool exposedEdge = known.Count > 0 && (known.Min(p => p.Number) > 1 && known.Min(p => p.Bounds.Y) > viewport.Top + 20
            || known.Max(p => p.Number) < total && known.Max(p => p.Bounds.Y + p.Bounds.Height) < viewport.Bottom - 20);
        if (!firstPage && (known.Count == 0 || candidates.Count == 0 || _anchors.Count == 0
            || exposedEdge || (discover && !request.ScrollPreview) || (centerPage > 0 && !_anchors.Any(a => a.Page == centerPage))))
        {
            _lastDiscovery = Environment.TickCount64;
            var seen = candidates.Select(a => a.Start).ToHashSet();
            // Centre-first coarse sampling, bounded between calls. A single COM
            // call can still stall and remains protected by the process deadline.
            (double X, double Y)[] positions = { (.5, .02), (.5, .98), (.15, .5), (.85, .5),
                (.15, .15), (.85, .85), (.15, .85), (.85, .15) };
            if (exposedEdge)
            {
                _discoveryCursor = 0;
                if (known.Max(p => p.Number) < total && known.Max(p => p.Bounds.Y + p.Bounds.Height) < viewport.Bottom - 20)
                    (positions[0], positions[1]) = (positions[1], positions[0]);
            }
            for (int sample = 0; sample < positions.Length; sample++)
            {
                if (budget.ElapsedMilliseconds >= 60) break;
                var position = positions[_discoveryCursor];
                _discoveryCursor = (_discoveryCursor + 1) % positions.Length;
                try
                {
                    int x = viewport.Left + (int)(position.X * (viewport.Right - viewport.Left));
                    int y = viewport.Top + (int)(position.Y * (viewport.Bottom - viewport.Top));
                    DiscoverySamples++;
                    _trace?.Invoke("edge-hit");
                    var hit = scope.Keep(window.Call("RangeFromPoint", x, y));
                    if (hit?.IsRange != true) continue;
                    var range = scope.Keep(hit.Get<object>("Duplicate"));
                    int start = range.Get<int>("Start");
                    if (!seen.Add(start)) continue;
                    WordAnchor? anchor = ReadAnchor(window.Value, range.Value);
                    if (anchor.HasValue) candidates.Add(anchor.Value);
                }
                catch (Exception ex) when (IsUnavailablePoint(ex)) { }
            }
        }
        candidates = candidates.DistinctBy(a => a.Start).ToList();
        foreach (var group in candidates.GroupBy(a => a.Page).Take(16))
        {
            try
            {
                if (!_pageLayouts.TryGetValue(group.Key, out var layout))
                {
                    _trace?.Invoke("page-layout");
                    var range = scope.Keep(document.Call("Range", group.First().Start, group.First().Start));
                    _trace?.Invoke("page-setup");
                    var setup = scope.Keep(range.Get<object>("PageSetup"));
                    double pageWidth = (double)setup.Get<float>("PageWidth"), pageHeight = (double)setup.Get<float>("PageHeight");
                    // Inconsistent samples cannot produce a usable page. Do not
                    // ask Word to paginate page boundaries for a discarded frame.
                    if (WordPageGeometry.Solve(group.ToArray(), pageWidth, pageHeight, scale) == null) continue;
                    int start = 0;
                    if (_pageLayouts.TryGetValue(group.Key - 1, out var preceding)) start = preceding.End;
                    else if (group.Key > 1)
                    {
                        _trace?.Invoke("page-start");
                        var startRange = scope.Keep(document.GoTo(group.Key));
                        start = startRange.Get<int>("Start");
                    }
                    int end = content.Get<int>("End");
                    if (_pageLayouts.TryGetValue(group.Key + 1, out var following)) end = following.Start;
                    else if (group.Key < total)
                    {
                        _trace?.Invoke("page-end");
                        var nextRange = scope.Keep(document.GoTo(group.Key + 1));
                        end = nextRange.Get<int>("Start");
                    }
                    layout = new WordPageFrame { Number = group.Key, Start = start, End = end,
                        WidthPoints = pageWidth, HeightPoints = pageHeight };
                    _pageLayouts[group.Key] = layout;
                    // The observer cache is bounded even in very long documents.
                    if (_pageLayouts.Count > 64) _pageLayouts.Remove(_pageLayouts.Keys.First(k => k != group.Key));
                    PageBoundaryReads++;
                }
                double width = layout.WidthPoints, height = layout.HeightPoints;
                WordRect bounds = WordPageGeometry.Solve(group.ToArray(), width, height, scale);
                if (bounds == null) continue;
                frame.Pages.Add(new WordPageFrame { Number = group.Key, Start = layout.Start, End = layout.End, WidthPoints = width,
                    HeightPoints = height, Bounds = bounds });
            }
            catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
        }
        WordPageGeometry.RetainVisibleNeighbours(_lastReadyFrame, frame);
        var previousAnchors = _anchors.ToArray();
        _anchors.Clear();
        var reliable = candidates.Where(a => frame.Pages.Count == 0 || frame.Pages.Any(p => p.Number == a.Page
            && Math.Abs(a.ScreenX - p.Bounds.X - a.PageX * p.Bounds.Width / p.WidthPoints) <= 4
            && Math.Abs(a.ScreenY - p.Bounds.Y - a.PageY * p.Bounds.Height / p.HeightPoints) <= 4));
        _anchors.AddRange(reliable.Concat(previousAnchors.Where(a => frame.Pages.Any(p => p.Number == a.Page)))
            .GroupBy(a => a.Page).SelectMany(group =>
            {
                var points = group.DistinctBy(a => a.Start).OrderBy(a => a.Start).ToArray();
                // Keep a bounded spread through the page, not only its edges.
                // Interior text remains usable when both edge samples leave view.
                return points.Length <= 4 ? points : Enumerable.Range(0, 4)
                    .Select(i => points[(int)Math.Round(i * (points.Length - 1) / 3.0)]).ToArray();
            }).Take(64));
        // Periodically rediscover edges, otherwise scrolling can expose another page
        // while an old anchor still remains visible.
        if (frame.Pages.Count == 0)
        {
            LastDiagnostic = $"scale={scale}; viewport={viewport.Left},{viewport.Top},{viewport.Right},{viewport.Bottom}; anchors="
                + string.Join(";", candidates.Select(a => $"p{a.Page}:{a.PageX},{a.PageY}->{a.ScreenX},{a.ScreenY}"));
            frame.Status = "Locating"; return frame;
        }
        frame.Pages = frame.Pages.OrderBy(p => p.Number).ToList();
        int preferredPage = request.TargetPage > 0 && request.ExpectedDocumentKey == key ? request.TargetPage : _currentPage;
        _trace?.Invoke("current-page");
        if (request.TargetPage == 0 && frame.Pages.Count > 1
            && frame.Pages.Any(p => frame.Pages.Any(other => p.Number != other.Number && Math.Abs(p.Bounds.Y - other.Bounds.Y) < 10)))
        {
            // Multiple pages in one row: honour a caret moved into a visible page.
            // A caret left behind on an offscreen page must never select that page.
            try
            {
                var selection = scope.Keep(window.Get<object>("Selection"));
                var selected = scope.Keep(selection.Get<object>("Range"));
                int selectedPage = Convert.ToInt32(selected.Get<object>("Information", 3));
                if (selectedPage != _lastSelectionPage && frame.Pages.Any(p => p.Number == selectedPage)) preferredPage = selectedPage;
                _lastSelectionPage = selectedPage;
            }
            catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
        }
        frame.CurrentPage = _currentPage = WordPageGeometry.SelectCurrentPage(frame.Pages, frame.Viewport, centerPage, preferredPage);
        frame.IsFocusMode = ReadFocusMode(scope, window, view, viewport);
        frame.Status = "Ready";
        frame.NeedsRefinement = firstPage;
        _lastReadyFrame = frame;
        _lastDocumentIdentity = identityValue;
        _trace?.Invoke("ready");
        return frame;
    }

    private WordFrame ReadPreview(ComScope scope, object windowObject, object documentObject, IntPtr root, IntPtr documentHwnd, bool verify)
    {
        _trace?.Invoke("scroll-preview");
        PreviewReads++;
        var window = new OfficeObject(windowObject);
        var document = new OfficeObject(documentObject);
        if (!Native.GetWindowRect(documentHwnd, out var viewport)) return null;
        var view = scope.Keep(window.Get<object>("View"));
        if (view.Get<int>("Type") != 3) return new WordFrame { Status = "PrintLayoutRequired" };
        var zoom = scope.Keep(view.Get<object>("Zoom"));
        int percentage = zoom.Get<int>("Percentage");
        double scale = Native.GetDpiForWindow(root) / 72.0 * percentage / 100.0;
        var frame = new WordFrame { DocumentKey = _lastReadyFrame.DocumentKey, DocumentName = _lastReadyFrame.DocumentName,
            SessionKey = _lastReadyFrame.SessionKey, Revision = _lastReadyFrame.Revision, PageCount = _lastReadyFrame.PageCount,
            WindowHandle = root.ToInt64(), Viewport = viewport.ToDto(), ZoomPercentage = percentage };
        var measuredAnchors = new Dictionary<int, WordAnchor>();
        foreach (var group in _anchors.GroupBy(a => a.Page))
        {
            if (!_pageLayouts.TryGetValue(group.Key, out var layout)) continue;
            var measured = new List<WordAnchor>();
            foreach (var anchor in group.OrderBy(a => Math.Abs(a.ScreenY - (viewport.Top + viewport.Bottom) / 2.0)))
            {
                try
                {
                    var range = scope.Keep(document.Call("Range", anchor.Start, anchor.Start + 1));
                    int x = 0, y = 0, width = 0, height = 0;
                    window.GetPoint(out x, out y, out width, out height, range);
                    measured.Add(anchor with { ScreenX = x, ScreenY = y });
                    measuredAnchors[anchor.Start] = measured[^1];
                    if (!verify) break;
                }
                catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
            }
            var bounds = WordPageGeometry.Solve(measured, layout.WidthPoints, layout.HeightPoints, scale);
            if (bounds != null)
                frame.Pages.Add(new WordPageFrame { Number = layout.Number, Start = layout.Start, End = layout.End,
                    WidthPoints = layout.WidthPoints, HeightPoints = layout.HeightPoints, Bounds = bounds });
        }
        for (int i = 0; i < _anchors.Count; i++)
            if (measuredAnchors.TryGetValue(_anchors[i].Start, out var measured)) _anchors[i] = measured;
        if (frame.Pages.Count == 0) return null;
        WordPageGeometry.RetainVisibleNeighbours(_lastReadyFrame, frame);
        if (WordPageGeometry.NeedsAdjacentPage(frame.Pages, frame.Viewport, frame.PageCount)) return null;
        // If the centre exposes an unknown page, let the bounded full discovery
        // find it. Known text at an edge must not keep the old page active forever.
        double centerY = frame.Viewport.Y + frame.Viewport.Height / 2;
        double centerX = frame.Viewport.X + frame.Viewport.Width / 2;
        var centerRow = frame.Pages.Where(p => p.Bounds.Y <= centerY && centerY <= p.Bounds.Y + p.Bounds.Height).ToArray();
        bool measuredPageGap = frame.Pages.Any(above => above.Bounds.Y + above.Bounds.Height <= centerY
            && above.Bounds.X <= centerX && centerX <= above.Bounds.X + above.Bounds.Width
            && frame.Pages.Any(below => below.Number == above.Number + 1 && below.Bounds.Y >= centerY
                && below.Bounds.X <= centerX && centerX <= below.Bounds.X + below.Bounds.Width));
        if (centerRow.Length == 0 ? !measuredPageGap : centerX < centerRow.Min(p => p.Bounds.X)
            || centerX > centerRow.Max(p => p.Bounds.X + p.Bounds.Width)) return null;
        frame.Pages = frame.Pages.OrderBy(p => p.Number).ToList();
        frame.CurrentPage = WordPageGeometry.SelectCurrentPage(frame.Pages, frame.Viewport, preferredPage: _currentPage);
        frame.IsFocusMode = ReadFocusMode(scope, window, view, viewport);
        frame.Status = "Ready";
        frame.CachedGeometry = true;
        return frame;
    }

    private bool ReadFocusMode(ComScope scope, OfficeObject window, OfficeObject view, Native.Rect viewport, bool force = false)
    {
        bool changed = viewport.Left != _focusViewport.Left || viewport.Top != _focusViewport.Top
            || viewport.Right != _focusViewport.Right || viewport.Bottom != _focusViewport.Bottom;
        // Focus changes the document viewport. Keep these Office UI reads off
        // ordinary scrolling; confirm viewport changes in the same reply so an
        // idle controller cannot suppress a deferred mode check indefinitely.
        if (!force && _focusCheckedAt != 0 && !changed) return _focusMode;
        bool focus = false;
        FocusReads++;
        if (_focusCommandSupported)
        {
            try
            {
                if (_focusCommandBars == null)
                {
                    var application = scope.Keep(window.Get<object>("Application"));
                    _focusCommandBars = application.Get<object>("CommandBars");
                }
                // Microsoft's Office control identifier for Word's Focus toggle.
                focus = Convert.ToBoolean(new OfficeObject(_focusCommandBars).Call("GetPressedMso", "ViewFocusModeView"));
            }
            catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { _focusCommandSupported = false; }
        }
        if (!_focusCommandSupported) focus = view.Get<bool>("FullScreen");
        _focusViewport = viewport; _focusMode = focus; _focusCheckedAt = Environment.TickCount64;
        return focus;
    }

    // A temporarily busy Office server is not a missing/offscreen text point.
    // Stop the request immediately; probing more points only multiplies retries.
    private static bool IsUnavailablePoint(Exception error) => error is COMException com
        ? !OfficeMessageFilter.IsBusy(com.HResult)
        : error is MissingMemberException;

    private WordAnchor? ReadAnchor(object windowObject, object rangeObject)
    {
        var window = new OfficeObject(windowObject);
        var range = new OfficeObject(rangeObject);
        if (range.Get<int>("StoryType") != 1) return null;
        range.Call("Collapse", 1);
        _trace?.Invoke("anchor-information");
        int page = Convert.ToInt32(range.Get<object>("Information", 3)), start = range.Get<int>("Start");
        double px = Convert.ToDouble(range.Get<object>("Information", 5)), py = Convert.ToDouble(range.Get<object>("Information", 6));
        if (px < 0 || py < 0) return null;
        // A zero-length range can return Word's virtual caret column rather
        // than a glyph's screen position. Anchor to one real character instead.
        if (Convert.ToInt32(range.Call("MoveEnd", 1, 1)) != 1) return null;
        int left = 0, top = 0, width = 0, height = 0;
        _trace?.Invoke("anchor-screen-point");
        window.GetPoint(out left, out top, out width, out height, range);
        return new WordAnchor(page, start, px, py, left, top);
    }

    private string Revision(ComScope scope, OfficeObject document, OfficeObject content, string key, int total)
    {
        long now = Environment.TickCount64;
        if (_revisions.TryGetValue(key, out var previous) && now - previous.Time < 1500) return previous.Hash;
        string text = content.Get<string>("Text");
        ContentReads++;
        if (text.Length > 4_000_000) throw new InvalidOperationException("Document exceeds the lecture observer budget.");
        var signature = new StringBuilder(text).Append('|').Append(total);
        var sections = scope.Keep(document.Get<object>("Sections"));
        int count = Math.Min(sections.Get<int>("Count"), 128);
        for (int i = 1; i <= count; i++)
        {
            var section = scope.Keep(sections.Call("Item", i));
            var setup = scope.Keep(section.Get<object>("PageSetup"));
            foreach (object value in new object[] { setup.Get<float>("PageWidth"), setup.Get<float>("PageHeight"), setup.Get<float>("TopMargin"), setup.Get<float>("BottomMargin"),
                setup.Get<float>("LeftMargin"), setup.Get<float>("RightMargin"), setup.Get<float>("HeaderDistance"), setup.Get<float>("FooterDistance") })
                signature.Append('|').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        var font = scope.Keep(content.Get<object>("Font"));
        signature.Append('|').Append(Convert.ToString(font.Get<string>("FontName"), CultureInfo.InvariantCulture))
            .Append('|').Append(Convert.ToString(font.Get<float>("Size"), CultureInfo.InvariantCulture));
        string hash = Hash(signature.ToString());
        if (_revisions.Count > 64) _revisions.Clear();
        _revisions[key] = (now, hash);
        return hash;
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal void ResetConnection()
    {
        if (_focusCommandBars != null)
        {
            try { Marshal.ReleaseComObject(_focusCommandBars); } catch (InvalidComObjectException) { }
            _focusCommandBars = null;
        }
        _focusCheckedAt = 0; _focusMode = false; _focusCommandSupported = true;
        if (_boundDocumentObject != null)
        {
            try { Marshal.ReleaseComObject(_boundDocumentObject); } catch (InvalidComObjectException) { }
            _boundDocumentObject = null;
        }
        if (_boundWindow != null)
        {
            try { Marshal.ReleaseComObject(_boundWindow); } catch (InvalidComObjectException) { }
            _boundWindow = null;
        }
        _boundRoot = _boundDocument = IntPtr.Zero; _boundProcess = 0;
        _lastKey = null; _lastReadyFrame = null; _metadataAt = 0;
        _anchors.Clear(); _pageLayouts.Clear();
    }
    public void Dispose() { ResetConnection(); _revisions.Clear(); }

    private sealed class ComScope : IDisposable
    {
        private readonly List<object> _objects = new();
        internal OfficeObject Keep(object value)
        {
            if (value != null && Marshal.IsComObject(value) && !_objects.Any(o => ReferenceEquals(o, value))) _objects.Add(value);
            return value == null ? null : new OfficeObject(value);
        }
        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                try { Marshal.ReleaseComObject(_objects[i]); } catch (InvalidComObjectException) { }
        }
    }
}
