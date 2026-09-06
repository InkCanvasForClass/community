using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Ink.WinRT;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private enum LogicalInkTool
        {
            Cursor,
            Pen,
            PointEraser,
            StrokeEraser,
            Select,
            Shape,
            BoardRoam
        }

        private WetInkOverlayWindow _winRTInkOverlay;
        private WinRTInkHost _winRTInkHost;
        private WinRTInkInputGate _winRTInkInputGate;
        private Ink.WinRT.ChromeInputForwarder _chromeInputForwarder;
        private Ink.WinRT.WpfRenderFrameFence _winRTInkFrameFence;

        // Cached on the UI thread at pipeline start: the chrome-forwarding callbacks run on
        // the ink input thread and must not touch WPF objects (WindowInteropHelper throws).
        private IntPtr _winRTInkMainHwnd;

        private bool _winRTInkStarted;
        private bool _winRTInkDisabled;
        private bool _winRTInkDeviceFailureNotified;

        private WinRTInkConfig _winRTInkConfig;
        private readonly Queue<Action> _winRTInkPendingDry = new Queue<Action>();
        private bool _winRTInkDryInProgress;
        private bool _winRTInkDryEndQueued;
        private long _winRTInkDryId;

        private EventHandler _winRTInkLocationChangedHandler;
        private DependencyPropertyChangedEventHandler _winRTInkIsVisibleChangedHandler;
        private PropertyDataChangedEventHandler _winRTInkAttributesChangedHandler;

        internal bool IsWinRTInkPipelineAvailable =>
            _winRTInkStarted && !_winRTInkDisabled;

        internal void SyncWinRTInkPipelineWithLogicalTool()
        {
            if (inkCanvas == null)
                return;
            if (Settings?.Canvas?.UseWinRTInk == true
                && ResolveLogicalInkTool() == LogicalInkTool.Pen)
            {
                if (_winRTInkStarted && !_winRTInkDisabled)
                {
                    // Already running: tool switches (color / pen type / width) must refresh
                    // the presenter's drawing attributes, not just re-run the start guard.
                    UpdateWinRTInkStyle();
                }
                else
                {
                    TryStartWinRTInkPipeline();
                }
            }
            else
                ShutdownWinRTInkPipeline();
        }

        internal void TryStartWinRTInkPipeline()
        {
            if (Settings?.Canvas?.UseWinRTInk != true)
                return;
            if (inkCanvas == null)
                return;
            if (ResolveLogicalInkTool() != LogicalInkTool.Pen)
                return;
            if (_winRTInkStarted || _winRTInkDisabled)
                return;

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;
                _winRTInkMainHwnd = hwnd;

                _winRTInkFrameFence = new Ink.WinRT.WpfRenderFrameFence(Dispatcher);
                _chromeInputForwarder = new Ink.WinRT.ChromeInputForwarder();
                _winRTInkInputGate = new WinRTInkInputGate(
                    classifyPointer: ClassifyWinRTInkPointer,
                    onChromePointerDown: ForwardWinRTInkChromePointerDown,
                    onChromePointerMove: ForwardWinRTInkChromePointerMove,
                    onChromePointerRelease: ForwardWinRTInkChromePointerUp,
                    onStrokeEnded: OnWinRTInkStrokeCanceled,
                    onStrokeCanceled: OnWinRTInkStrokeCanceled);

                _winRTInkOverlay = new WetInkOverlayWindow(hwnd, IsCanvasPoint);
                _winRTInkHost = new WinRTInkHost(_winRTInkOverlay, _winRTInkInputGate);
                _winRTInkHost.OnDryAvailable = OnWinRTInkDryAvailable;
                _winRTInkHost.OnDryFailed = ex => DisableWinRTInkAfterFailure(ex, notify: true);

                var config = BuildWinRTInkConfig();
                _winRTInkConfig = config;
                _winRTInkHost.Start(hwnd, config);

                WireWinRTInkGeometryListeners();
                _winRTInkStarted = true;

                // Color / width / highlighter changes mutate inkCanvas.DefaultDrawingAttributes
                // in place; push each change to the presenter so wet ink stays in sync.
                if (_winRTInkAttributesChangedHandler == null)
                {
                    _winRTInkAttributesChangedHandler = (_, __) => UpdateWinRTInkStyle();
                    inkCanvas.DefaultDrawingAttributes.AttributeChanged += _winRTInkAttributesChangedHandler;
                }

                PushWinRTInkGateSnapshots();
                EnsureWinRTInkPhysicalEditingMode();

                RefreshWinRTInkOverlayVisibility();
                LogHelper.WriteLogToFile(
                    "[WinRTInk] InkDesktopHost + system wet ink pipeline started.",
                    LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] Failed to start: {ex}",
                    LogHelper.LogType.Error);
                ShutdownWinRTInkPipeline();
                DisableWinRTInkAfterFailure(ex, notify: true);
            }
        }

        private void ShutdownWinRTInkPipeline()
        {
            if (!_winRTInkStarted && _winRTInkHost == null)
                return;

            UnwireWinRTInkGeometryListeners();

            if (_winRTInkAttributesChangedHandler != null)
            {
                try { inkCanvas.DefaultDrawingAttributes.AttributeChanged -= _winRTInkAttributesChangedHandler; }
                catch { /* best-effort */ }
                _winRTInkAttributesChangedHandler = null;
            }

            try { _winRTInkFrameFence?.CancelAll(); }
            catch { /* best-effort */ }
            try { _winRTInkFrameFence?.Dispose(); }
            catch { /* best-effort */ }
            _winRTInkFrameFence = null;

            try { _winRTInkHost?.CancelActiveStrokes(); }
            catch { /* best-effort */ }
            try { _winRTInkHost?.Dispose(); }
            catch { /* best-effort */ }
            _winRTInkHost = null;

            try { _winRTInkOverlay?.Dispose(); }
            catch { /* best-effort */ }
            _winRTInkOverlay = null;

            _winRTInkInputGate = null;
            _chromeInputForwarder = null;
            _winRTInkConfig = null;
            _winRTInkPendingDry.Clear();
            _winRTInkDryInProgress = false;
            _winRTInkDryEndQueued = false;
            _winRTInkStarted = false;

            if (ResolveLogicalInkTool() == LogicalInkTool.Pen
                && inkCanvas?.EditingMode == InkCanvasEditingMode.None)
            {
                try { inkCanvas.EditingMode = InkCanvasEditingMode.Ink; }
                catch { /* best-effort fallback to WPF ink */ }
            }
        }

        private void WireWinRTInkGeometryListeners()
        {
            if (_winRTInkLocationChangedHandler == null)
            {
                _winRTInkLocationChangedHandler = (_, __) => UpdateWinRTInkTarget();
                LocationChanged += _winRTInkLocationChangedHandler;
            }

            if (_winRTInkIsVisibleChangedHandler == null)
            {
                _winRTInkIsVisibleChangedHandler = (_, __) => UpdateWinRTInkTarget();
                IsVisibleChanged += _winRTInkIsVisibleChangedHandler;
            }

            StateChanged -= WinRTInk_StateChanged;
            StateChanged += WinRTInk_StateChanged;
        }

        private void UnwireWinRTInkGeometryListeners()
        {
            if (_winRTInkLocationChangedHandler != null)
            {
                LocationChanged -= _winRTInkLocationChangedHandler;
                _winRTInkLocationChangedHandler = null;
            }

            if (_winRTInkIsVisibleChangedHandler != null)
            {
                IsVisibleChanged -= _winRTInkIsVisibleChangedHandler;
                _winRTInkIsVisibleChangedHandler = null;
            }

            StateChanged -= WinRTInk_StateChanged;
        }

        private void WinRTInk_StateChanged(object sender, EventArgs e)
        {
            UpdateWinRTInkTarget();
        }

        private void UpdateWinRTInkTarget()
        {
            if (!_winRTInkStarted || _winRTInkHost == null || _winRTInkDisabled)
                return;

            try
            {
                var config = BuildWinRTInkConfig();
                _winRTInkConfig = config;
                _winRTInkHost.SetSize(config.WidthPx, config.HeightPx);
                _winRTInkHost.UpdateDrawingAttributes(config.ToInkDrawingAttributes());
                _winRTInkHost.SetInputEnabled(IsVisible && WindowState != WindowState.Minimized);

                var bounds = ScreenBoundsFromConfig(config);
                _winRTInkOverlay.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);

                PushWinRTInkGateSnapshots();
                RefreshWinRTInkOverlayVisibility();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] UpdateTarget failed: {ex}",
                    LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// Re-pushes the current drawing style (color / width / pen type / pressure) to the
        /// presenter without touching geometry. Called on DefaultDrawingAttributes changes
        /// and tool-mode switches; the dry-ink converter reads the same refreshed config,
        /// so wet and dry ink stay consistent.
        /// </summary>
        private void UpdateWinRTInkStyle()
        {
            if (!_winRTInkStarted || _winRTInkHost == null || _winRTInkDisabled)
                return;

            try
            {
                var config = BuildWinRTInkConfig();
                _winRTInkConfig = config;
                _winRTInkHost.UpdateDrawingAttributes(config.ToInkDrawingAttributes());
                PushWinRTInkGateSnapshots();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] UpdateStyle failed: {ex}",
                    LogHelper.LogType.Error);
            }
        }

        private (int X, int Y, int Width, int Height) ScreenBoundsFromConfig(WinRTInkConfig config)
        {
            try
            {
                var topLeft = PointToScreen(new Point(0, 0));
                return (
                    (int)Math.Round(topLeft.X),
                    (int)Math.Round(topLeft.Y),
                    Math.Max(1, (int)Math.Round(config.WidthPx)),
                    Math.Max(1, (int)Math.Round(config.HeightPx)));
            }
            catch
            {
                return ((int)Math.Round(Left), (int)Math.Round(Top), 1, 1);
            }
        }

        private WinRTInkConfig BuildWinRTInkConfig()
        {
            var dpi = GetWinRTDpiScales();
            var style = CaptureWinRTStrokeStyleSnapshot();

            var widthDip = Math.Max(1.0, ActualWidth);
            var heightDip = Math.Max(1.0, ActualHeight);
            var widthPx = (float)(widthDip * dpi.X);
            var heightPx = (float)(heightDip * dpi.Y);

            var origin = InkCanvasOriginInWindow();

            return new WinRTInkConfig(
                widthPx,
                heightPx,
                style,
                dpi.X,
                dpi.Y,
                origin);
        }

        private Point InkCanvasOriginInWindow()
        {
            if (inkCanvas == null)
                return new Point(0, 0);
            try
            {
                return inkCanvas.TransformToAncestor(this).Transform(new Point(0, 0));
            }
            catch
            {
                return new Point(0, 0);
            }
        }

        private WinRTStrokeStyleSnapshot CaptureWinRTStrokeStyleSnapshot()
        {
            var attrs = inkCanvas.DefaultDrawingAttributes;
            var color = attrs.Color;

            var tipRectangle = attrs.StylusTip == StylusTip.Rectangle
                               || attrs.IsHighlighter
                               || penType == 1;
            var useVelocity = ShouldUseRealtimeVelocityBrushTip()
                              && penType != 1
                              && drawingShapeMode == 0
                              && !isPalmEraserActive;

            return new WinRTStrokeStyleSnapshot(
                color,
                Math.Max(0.1, attrs.Width),
                Math.Max(0.1, attrs.Height),
                isHighlighter: attrs.IsHighlighter || penType == 1,
                ignorePressure: Settings.Canvas.DisablePressure,
                tipRectangle,
                useVelocityBrushTip: useVelocity,
                isLaser: penType == 2);
        }

        private void PushWinRTInkGateSnapshots()
        {
            if (_winRTInkInputGate == null)
                return;

            _winRTInkInputGate.CanvasInputEnabled =
                IsEnabled && IsVisible && inkCanvas != null && !IsPluginCanvasToolActive;
            _winRTInkInputGate.PageFrozen = IsCurrentPageFrozen;
            _winRTInkInputGate.MultiTouchWriting = currentMode == 0
                ? Settings.Gesture.IsEnableMultiTouchMode || isInMultiTouchMode
                : Settings.Gesture.IsEnableMultiTouchModeBoard || isInMultiTouchMode;
            _winRTInkInputGate.TwoFingerGestureAllowed = ResolveTwoFingerGestureAllowed();
            _winRTInkInputGate.PalmEraserEnabled = Settings.Canvas.EnablePalmEraser;

            var palm = BuildPalmEraserPolicy();
            _winRTInkInputGate.PalmEraserThresholdDip = palm.Enabled
                ? PalmThresholdDip(palm)
                : double.MaxValue;
        }

        private static double PalmThresholdDip(Ink.PalmEraserPolicy policy)
        {
            // Approximation of PalmEraserCalculator's threshold: bounds * thresholdFactor *
            // sensitivity. Exact recognition is evaluated per-contact in the gate.
            return policy.BoundsWidthDip * policy.ThresholdFactor * policy.SensitivityMultiplier;
        }

        private void EnsureWinRTInkPhysicalEditingMode()
        {
            if (inkCanvas == null)
                return;

            var tool = ResolveLogicalInkTool();
            if (tool != LogicalInkTool.Pen)
                return;
            if (!IsWinRTInkPipelineAvailable)
                return;

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
        }

        private LogicalInkTool ResolveLogicalInkTool()
        {
            if (IsBoardRoamingMode)
                return LogicalInkTool.BoardRoam;
            if (drawingShapeMode != 0
                || string.Equals(_currentToolMode, "shape", StringComparison.OrdinalIgnoreCase))
                return LogicalInkTool.Shape;

            switch (_currentToolMode)
            {
                case "pen":
                case "color":
                    return LogicalInkTool.Pen;
                case "eraser":
                    return LogicalInkTool.PointEraser;
                case "eraserByStrokes":
                    return LogicalInkTool.StrokeEraser;
                case "select":
                    return LogicalInkTool.Select;
                case "roaming":
                    return LogicalInkTool.BoardRoam;
                case "cursor":
                default:
                    return LogicalInkTool.Cursor;
            }
        }

        private bool ResolveTwoFingerGestureAllowed()
        {
            if (_pluginCanvasGestureHandler != null)
                return true;
            if (IsInPPTPresentationMode)
                return Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode
                       && Settings.Gesture.IsEnableTwoFingerGesture;
            return Settings.Gesture.IsEnableTwoFingerGesture;
        }

        private bool IsCanvasPoint(int screenX, int screenY)
        {
            CanvasHitZone zone;
            string detail;
            try
            {
                var windowPoint = PointFromScreen(new Point(screenX, screenY));
                zone = ResolveHitZone(windowPoint.X, windowPoint.Y, out detail);
            }
            catch (Exception ex)
            {
                zone = CanvasHitZone.Outside;
                detail = $"IsCanvasPoint exception: {ex.GetType().Name}: {ex.Message}";
            }

            LogWinRTInkHitTestResult(zone, detail, screenX, screenY);
            return zone == CanvasHitZone.CanvasSurface;
        }

        private CanvasHitZone _lastWinRTInkHitZone = (CanvasHitZone)(-1);
        private string _lastWinRTInkHitDetail;

        /// <summary>
        /// Field diagnostics for the overlay hit-test gate: logs each transition (zone or
        /// classification changed) so unclickable-chrome reports can be traced to the exact
        /// decision — top-level window under the cursor and the WPF element hit.
        /// </summary>
        private void LogWinRTInkHitTestResult(CanvasHitZone zone, string detail, int screenX, int screenY)
        {
            if (zone == _lastWinRTInkHitZone && detail == _lastWinRTInkHitDetail)
                return;
            _lastWinRTInkHitZone = zone;
            _lastWinRTInkHitDetail = detail;
            try
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] NCHITTEST screen=({screenX},{screenY}) zone={zone} {detail ?? ""}",
                    LogHelper.LogType.Event);
            }
            catch { /* never throw from hit-test logging */ }
        }

        private enum CanvasHitZone
        {
            Outside,
            UiChrome,
            SelectionOverlay,
            EraserOverlay,
            CanvasSurface
        }

        private enum TopWindowClass
        {
            Main,
            Overlay,
            Foreign
        }

        private CanvasHitZone ResolveHitZone(double xDip, double yDip, out string detail)
        {
            detail = null;
            if (inkCanvas == null)
            {
                detail = "inkCanvas==null";
                return CanvasHitZone.Outside;
            }

            try
            {
                var windowPoint = new Point(xDip, yDip);
                if (windowPoint.X < 0 || windowPoint.Y < 0
                    || windowPoint.X > ActualWidth || windowPoint.Y > ActualHeight)
                {
                    detail = "outside window bounds";
                    return CanvasHitZone.Outside;
                }

                var topClass = ClassifyTopWindow(windowPoint);
                var hit = InputHitTest(windowPoint) as DependencyObject;
                detail = $"top={topClass} hit={DescribeHit(hit)}";

                if (topClass == TopWindowClass.Foreign)
                {
                    detail += " [foreign-top-window]";
                    return CanvasHitZone.Outside;
                }

                if (hit != null
                    && (IsUnderNamed(hit, "EraserOverlayCanvas")
                        || IsUnderElement(hit, EraserOverlayCanvas)))
                {
                    detail += " [eraser-overlay]";
                    return CanvasHitZone.EraserOverlay;
                }

                if (hit != null)
                {
                    var selectionCover = FindName("GridInkCanvasSelectionCover") as FrameworkElement;
                    if (selectionCover != null
                        && selectionCover.Visibility == Visibility.Visible
                        && (IsUnderNamed(hit, "GridInkCanvasSelectionCover")
                            || IsUnderElement(hit, selectionCover)))
                    {
                        detail += " [selection-overlay]";
                        return CanvasHitZone.SelectionOverlay;
                    }
                }

                if (hit != null && IsUiChromeHit(hit))
                {
                    detail += " [chrome]";
                    return CanvasHitZone.UiChrome;
                }

                if (hit != null && !IsUnderElement(hit, inkCanvas))
                {
                    detail += " [non-canvas-structural]";
                    return CanvasHitZone.UiChrome;
                }

                detail += " [canvas]";
                return CanvasHitZone.CanvasSurface;
            }
            catch (Exception ex)
            {
                detail = $"ResolveHitZone exception: {ex.GetType().Name}: {ex.Message}";
                return CanvasHitZone.CanvasSurface;
            }
        }

        private static string DescribeHit(DependencyObject hit)
        {
            if (hit == null)
                return "null";
            var name = hit is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)
                ? $"#{fe.Name}"
                : "";
            return $"{hit.GetType().Name}{name}";
        }

        /// <summary>
        /// Classifies the top-level window under the cursor: the main window, our own wet-ink
        /// overlay, or a foreign HWND (WPF Popup palettes, combo dropdowns, other apps).
        /// WindowFromPoint is purely geometric — it walks sibling HWNDs top-down and reports
        /// whichever covers the point, so the overlay's own HTTRANSPARENT does not hide it.
        /// </summary>
        private TopWindowClass ClassifyTopWindow(Point windowPoint)
        {
            try
            {
                var screen = PointToScreen(windowPoint);
                var topWindow = WindowFromPoint(new NativeWin32Point(
                    (int)Math.Round(screen.X),
                    (int)Math.Round(screen.Y)));
                if (topWindow == IntPtr.Zero)
                    return TopWindowClass.Main;

                var mainHwnd = new WindowInteropHelper(this).Handle;
                if (topWindow == mainHwnd)
                    return TopWindowClass.Main;

                var overlayHwnd = _winRTInkOverlay?.OverlayHandle ?? IntPtr.Zero;
                if (topWindow == overlayHwnd)
                    return TopWindowClass.Overlay;

                return TopWindowClass.Foreign;
            }
            catch
            {
                return TopWindowClass.Main;
            }
        }

        // ---- Pointer classification & chrome re-dispatch ----------------------------
        //
        // Pointer input that reaches the ink HWND bypasses the overlay WM_NCHITTEST
        // pass-through entirely (empirically confirmed: touch presses over the floating bar
        // produce no WM_NCHITTEST at all), so the authoritative chrome gate runs here, in
        // the CoreInkIndependentInputSource press handler. Every press is classified on the
        // UI thread via a short synchronous marshal; chrome hits are then replayed to the
        // window under the pointer as synthesized mouse messages (the same PostMessage
        // technique the PPT media-control passthrough uses).

        /// <summary>
        /// Ink input thread: classifies one pointer press. Marshals the WPF hit-zone
        /// resolution onto the UI thread (the ink thread must not touch WPF) with a short
        /// timeout; on failure the press is suppressed so no ghost ink lands on chrome.
        /// </summary>
        private Ink.WinRT.PointerGateResult ClassifyWinRTInkPointer(
            global::Windows.UI.Core.PointerEventArgs e)
        {
            try
            {
                var position = e.CurrentPoint.Position;
                if (!TryGetWinRTInkScreenPoint(position, out var screenX, out var screenY))
                {
                    LogHelper.WriteLogToFile(
                        "[WinRTInk] gate classify: overlay not on screen, suppressing press.",
                        LogHelper.LogType.Event);
                    return Ink.WinRT.PointerGateResult.BlockSilently;
                }

                var zone = CanvasHitZone.Outside;
                var classified = false;
                var dispatcher = _winRTInkOverlay?.UiDispatcher;
                if (dispatcher != null)
                {
                    try
                    {
                        dispatcher.Invoke(
                            new Action(() =>
                            {
                                try
                                {
                                    var windowPoint = PointFromScreen(new Point(screenX, screenY));
                                    zone = ResolveHitZone(windowPoint.X, windowPoint.Y, out _);
                                    classified = true;
                                }
                                catch { /* classification failure -> not classified */ }
                            }),
                            DispatcherPriority.Send,
                            CancellationToken.None,
                            TimeSpan.FromMilliseconds(300));
                    }
                    catch { /* dispatcher shutting down -> suppressed */ }
                }

                LogHelper.WriteLogToFile(
                    $"[WinRTInk] gate classify raw=({position.X:0.#},{position.Y:0.#}) screen=({screenX:0},{screenY:0}) zone={zone} classified={classified}",
                    LogHelper.LogType.Event);

                if (!classified)
                    return Ink.WinRT.PointerGateResult.BlockSilently;
                if (zone == CanvasHitZone.CanvasSurface)
                    return Ink.WinRT.PointerGateResult.AllowInk;
                // UiChrome / SelectionOverlay / EraserOverlay / Outside (e.g. an open popup
                // palette): suppress inking and let the forwarder re-dispatch the press.
                return Ink.WinRT.PointerGateResult.BlockAndForward;
            }
            catch
            {
                return Ink.WinRT.PointerGateResult.BlockSilently;
            }
        }

        /// <summary>
        /// Maps an ink-presenter-space position (overlay client, physical px) to screen
        /// coordinates using the overlay's current window rect.
        /// </summary>
        private bool TryGetWinRTInkScreenPoint(
            global::Windows.Foundation.Point position,
            out double screenX,
            out double screenY)
        {
            screenX = 0;
            screenY = 0;
            var overlayHwnd = _winRTInkOverlay?.OverlayHandle ?? IntPtr.Zero;
            if (overlayHwnd == IntPtr.Zero)
                return false;
            if (!GetWindowRect(overlayHwnd, out var rect))
                return false;
            // Parked far off-screen: no live wet ink, so the press cannot be mapped.
            if (rect.Left <= -50000 || rect.Top <= -50000)
                return false;
            screenX = rect.Left + position.X;
            screenY = rect.Top + position.Y;
            return true;
        }

        private void ForwardWinRTInkChromePointerDown(global::Windows.UI.Core.PointerEventArgs e)
        {
            try
            {
                if (!TryGetWinRTInkScreenPoint(e.CurrentPoint.Position, out var screenX, out var screenY))
                    return;
                var target = _chromeInputForwarder?.ForwardDown(
                    screenX,
                    screenY,
                    _winRTInkMainHwnd,
                    _winRTInkOverlay?.OverlayHandle ?? IntPtr.Zero) ?? IntPtr.Zero;
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] chrome forward down screen=({screenX:0},{screenY:0}) target=0x{target.ToString("X")}",
                    LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] chrome forward down failed: {ex.Message}",
                    LogHelper.LogType.Error);
            }
        }

        private void ForwardWinRTInkChromePointerMove(global::Windows.UI.Core.PointerEventArgs e)
        {
            try
            {
                if (!TryGetWinRTInkScreenPoint(e.CurrentPoint.Position, out var screenX, out var screenY))
                    return;
                _chromeInputForwarder?.ForwardMove(screenX, screenY);
            }
            catch { /* forwarding is best-effort */ }
        }

        private void ForwardWinRTInkChromePointerUp(global::Windows.UI.Core.PointerEventArgs e)
        {
            try
            {
                if (!TryGetWinRTInkScreenPoint(e.CurrentPoint.Position, out var screenX, out var screenY))
                    return;
                _chromeInputForwarder?.ForwardUp(screenX, screenY);
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] chrome forward up screen=({screenX:0},{screenY:0})",
                    LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] chrome forward up failed: {ex.Message}",
                    LogHelper.LogType.Error);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeWin32Rect lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWin32Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWin32Point
        {
            public NativeWin32Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativeWin32Point point);

        private bool IsUiChromeHit(DependencyObject hit)
        {
            if (hit == null)
                return false;

            if (IsUnderNamed(hit, "ViewboxFloatingBar")
                || IsUnderNamed(hit, "ViewboxBlackboardLeftSide")
                || IsUnderNamed(hit, "ViewboxBlackboardCenterSide")
                || IsUnderNamed(hit, "ViewboxBlackboardRightSide")
                || IsUnderNamed(hit, "BlackboardLeftSide")
                || IsUnderNamed(hit, "BlackboardCenterSide")
                || IsUnderNamed(hit, "BlackboardRightSide")
                || IsUnderNamed(hit, "BorderInkReplayToolBox")
                || IsUnderNamed(hit, "IdleMiniBar")
                || IsUnderNamed(hit, "EdgeExpandHint")
                || IsUnderNamed(hit, "PPTControlsGrid")
                || IsUnderNamed(hit, "GridPPTControlLeft")
                || IsUnderNamed(hit, "GridPPTControlRight")
                || IsUnderNamed(hit, "LeftBottomPanelForPPTNavigation")
                || IsUnderNamed(hit, "RightBottomPanelForPPTNavigation")
                || IsUnderNamed(hit, "LeftSidePanelForPPTNavigation")
                || IsUnderNamed(hit, "RightSidePanelForPPTNavigation")
                || IsUnderNamed(hit, "PPTQuickPanelContainer"))
            {
                return true;
            }

            var current = hit;
            while (current != null)
            {
                if (current is Button
                    || current is Thumb
                    || current is Slider
                    || current is ToggleButton
                    || current is ScrollBar)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }

            return false;
        }

        private static bool IsUnderNamed(DependencyObject hit, string name)
        {
            var current = hit;
            while (current != null)
            {
                if (current is FrameworkElement fe && string.Equals(fe.Name, name, StringComparison.Ordinal))
                    return true;
                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return false;
        }

        private static bool IsUnderElement(DependencyObject hit, FrameworkElement element)
        {
            var current = hit;
            while (current != null)
            {
                if (ReferenceEquals(current, element))
                    return true;
                current = VisualTreeHelper.GetParent(current)
                          ?? (current as FrameworkElement)?.Parent;
            }
            return false;
        }

        private (double X, double Y) GetWinRTDpiScales()
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var m = source.CompositionTarget.TransformToDevice;
                    return (m.M11 > 0 ? m.M11 : 1.0, m.M22 > 0 ? m.M22 : 1.0);
                }
            }
            catch { /* fall through */ }

            var scale = GetDpiScale();
            return (scale > 0 ? scale : 1.0, scale > 0 ? scale : 1.0);
        }

        private void RefreshWinRTInkOverlayVisibility()
        {
            try
            {
                if (_winRTInkOverlay != null)
                {
                    var onScreen = _winRTInkStarted
                                   && IsVisible
                                   && WindowState != WindowState.Minimized;
                    _winRTInkOverlay.SetOnScreen(onScreen);
                }
            }
            catch { /* best-effort */ }
        }

        internal void CancelActiveWinRTInk()
        {
            try { _winRTInkHost?.CancelActiveStrokes(); }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Ink-thread StrokeEnded canceled by the gate (frozen page / palm eraser / two-finger
        /// gesture / CancelActiveStrokes). The presenter drops the wet stroke; nothing to dry.
        /// Runs on the UI thread after the gate routed it back.
        /// </summary>
        private void OnWinRTInkStrokeCanceled()
        {
            // Nothing queued from the OS side for this stroke. If a BeginDry was already in
            // flight for a previous stroke the serialization queue still owns it.
        }

        /// <summary>
        /// UI thread: a BeginDry batch (one or more wet strokes that just went dry) is ready to
        /// be materialized as WPF strokes. Serialized: the OS presenter raises no new StrokeEnded
        /// until EndDry, so at most one batch is pending at a time here.
        /// </summary>
        private void OnWinRTInkDryAvailable(
            IReadOnlyList<IReadOnlyList<global::Windows.UI.Input.Inking.InkPoint>> pointBatches)
        {
            try
            {
            if (pointBatches == null)
            {
                // Defensive null batch — the presenter still requires a matching EndDry only
                // when BeginDry returned successfully, so a null batch is treated as failure.
                DisableWinRTInkAfterFailure(
                    new InvalidOperationException("WinRT InkSynchronizer returned a null dry batch."),
                    notify: true);
                return;
            }

            if (pointBatches.Count == 0)
            {
                // A successful BeginDry can return no strokes; it still needs exactly one EndDry.
                CompleteWinRTInkDry();
                return;
            }

            var config = _winRTInkConfig;
            if (config == null)
            {
                CompleteWinRTInkDry();
                DisableWinRTInkAfterFailure(
                    new InvalidOperationException("WinRT Ink configuration was unavailable while drying."),
                    notify: true);
                return;
            }

            var pendingDry = new Action(() => MaterializeWinRTInkDry(pointBatches, config));
            _winRTInkPendingDry.Enqueue(pendingDry);
            DrainWinRTInkPendingDry();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] Dry batch enqueue failed: {ex}",
                    LogHelper.LogType.Error);
                CompleteWinRTInkDry();
                DisableWinRTInkAfterFailure(ex, notify: true);
            }
        }

        private void DrainWinRTInkPendingDry()
        {
            if (_winRTInkDryInProgress || _winRTInkPendingDry.Count == 0)
                return;

            var next = _winRTInkPendingDry.Dequeue();
            _winRTInkDryInProgress = true;
            try
            {
                next();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] Dry materialize failed: {ex}",
                    LogHelper.LogType.Error);
                try { CompleteWinRTInkDry(); } catch { /* best-effort */ }
                DisableWinRTInkAfterFailure(ex, notify: true);
            }
        }

        /// <summary>
        /// Materializes one dry batch on the UI thread: convert InkPoints to WPF Stroke, add it
        /// to the canvas (source of truth — TimeMachine/dirty hooks fire first), post-process,
        /// wait a render fence, then EndDry on the ink thread and drain the next pending batch.
        /// </summary>
        private void MaterializeWinRTInkDry(
            IReadOnlyList<IReadOnlyList<global::Windows.UI.Input.Inking.InkPoint>> pointBatches,
            WinRTInkConfig config)
        {
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("书写");
                CompleteWinRTInkDry();
                return;
            }

            var committedStrokes = new List<System.Windows.Ink.Stroke>();
            try
            {
                var strokes = new List<System.Windows.Ink.Stroke>(pointBatches.Count);
                for (var i = 0; i < pointBatches.Count; i++)
                {
                    var stroke = WinRTStrokeConverter.CreateStroke(
                        pointBatches[i],
                        config.Style,
                        config.DpiScaleX,
                        config.DpiScaleY,
                        config.CanvasOriginDip);
                    strokes.Add(stroke);
                }

                // Dry ink is the single source of truth: add first so StrokesChanged / TimeMachine
                // / dirty-page hooks fire before post-processing.
                foreach (var stroke in strokes)
                {
                    inkCanvas.Strokes.Add(stroke);
                    committedStrokes.Add(stroke);
                }

                // Keep the wet ink on the overlay until several WPF composition frames paint the
                // dry stroke, then retire it (EndDry removes the wet ink). Mirrors the previous
                // native pipeline's wet→dry handoff to avoid a dry-missing/wet-gone flash.
                foreach (var stroke in strokes)
                    ProcessCommittedStroke(stroke);

                inkCanvas.InvalidateVisual();

                var dryId = ++_winRTInkDryId;
                _winRTInkFrameFence?.Arm(dryId, () =>
                {
                    try
                    {
                        CompleteWinRTInkDry();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile(
                            $"[WinRTInk] Fence callback failed: {ex}",
                            LogHelper.LogType.Error);
                        DisableWinRTInkAfterFailure(ex, notify: true);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[WinRTInk] Dry commit failed: {ex}",
                    LogHelper.LogType.Error);
                foreach (var stroke in committedStrokes)
                {
                    try
                    {
                        if (inkCanvas.Strokes.Contains(stroke))
                            inkCanvas.Strokes.Remove(stroke);
                    }
                    catch { /* best-effort rollback */ }
                }
                CompleteWinRTInkDry();
                DisableWinRTInkAfterFailure(ex, notify: true);
            }
        }

        /// <summary>
        /// Ends the current dry batch on the ink thread, then drains the next pending batch.
        /// Must be called exactly once per BeginDry.
        /// </summary>
        private void CompleteWinRTInkDry()
        {
            if (_winRTInkDryEndQueued)
                return;
            _winRTInkDryEndQueued = true;

            var host = _winRTInkHost;
            if (host != null)
            {
                try
                {
                    host.QueueEndDry(() =>
                    {
                        // EndDry's completion runs on the ink thread; all queue state and WPF
                        // operations must return to the UI dispatcher before draining the next batch.
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _winRTInkDryEndQueued = false;
                                _winRTInkDryInProgress = false;
                                DrainWinRTInkPendingDry();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile(
                                    $"[WinRTInk] Dry drain failed: {ex}",
                                    LogHelper.LogType.Error);
                            }
                        }), DispatcherPriority.Background);
                    });
                }
                catch (Exception ex)
                {
                    _winRTInkDryEndQueued = false;
                    _winRTInkDryInProgress = false;
                    LogHelper.WriteLogToFile(
                        $"[WinRTInk] EndDry queue failed: {ex}",
                        LogHelper.LogType.Error);
                }
            }
            else
            {
                _winRTInkDryEndQueued = false;
                _winRTInkDryInProgress = false;
                DrainWinRTInkPendingDry();
            }
        }

        private void DisableWinRTInkAfterFailure(Exception ex, bool notify)
        {
            _winRTInkDisabled = true;
            try
            {
                _winRTInkInputGate.CanvasInputEnabled = false;
                _winRTInkHost?.SetInputEnabled(false);
                _winRTInkOverlay?.SetOnScreen(false);
            }
            catch { /* best-effort */ }
            try { CancelActiveWinRTInk(); }
            catch { /* best-effort */ }

            try
            {
                if (inkCanvas != null
                    && inkCanvas.EditingMode == InkCanvasEditingMode.None
                    && ResolveLogicalInkTool() == LogicalInkTool.Pen)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
            }
            catch { /* best-effort */ }

            if (notify && !_winRTInkDeviceFailureNotified)
            {
                _winRTInkDeviceFailureNotified = true;
                try
                {
                    ShowNotification(Properties.CanvasStrings.Canvas_WetInkRendererFailed);
                }
                catch { /* never throw from failure path */ }
            }

            LogHelper.WriteLogToFile(
                $"[WinRTInk] Freehand disabled after pipeline failure: {ex}",
                LogHelper.LogType.Error);
        }
    }
}
