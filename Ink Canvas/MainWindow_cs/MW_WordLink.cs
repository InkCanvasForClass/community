using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Linq;
using Ink_Canvas.Ink.WinRT;
using Ink_Canvas.WordLink;
using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private WordLectureController _wordLecture;
        private WordToolbarPlacement _wordToolbarPlacement;
        private WordFocusToolbarPresentation _wordFocusToolbarPresentation;
        private Point _wordFocusSavedDesktopPoint;
        private bool _wordFocusSavedDragged;
        private FrameworkElement _wordFocusExitFallback;
        private Controls.ToolbarImageButton _wordFocusExitView;
        private string _wordFocusExitLabel;
        private Rect? _wordFocusSavedOverlayBounds;
        private bool IsWordFocusToolbarActive => _wordFocusToolbarPresentation?.IsActive == true;
        private readonly HashSet<int> _wordTouches = new();
        private TimeMachine _wordDesktopHistory;
        private bool _wordOwnsCanvas, _wordDisposing;
        private bool _wordDesktopUndo, _wordDesktopRedo;
        private readonly WordWindowInputRegion _wordInputRegion = new();
        private DispatcherCoalescedAction _wordInputRegionUpdate;
        private readonly WordBoundaryToolState _wordBoundaryTool = new();
        private System.Windows.Threading.DispatcherTimer _wordBoundaryTimer;
        private bool _wordAutoToolChanging;
        private bool _wordBoundaryToolbarPositionQueued;
        private DrawingAttributes _wordBoundaryAttributes;
        private int _wordBoundaryPenType;
        private bool _wordBoundaryInkFade;

        private void InitializeWordLecture()
        {
            if (_wordLecture != null) return;
            RightBottomWordLectureControls.Mirror(WordLectureControls);
            _wordToolbarPlacement = new WordToolbarPlacement(WordNavigationOverlay, WordLectureControls, RightBottomWordLectureControls);
            _wordFocusToolbarPresentation = new WordFocusToolbarPresentation(ViewboxFloatingBar, GetWordFocusToolbarBounds,
                () => Settings.WordLink?.FocusToolbarOpacity ?? .5, CancelWordFocusToolbarAnimation,
                () => !isFloatingBarFolded, FloatingbarUIForInkReplay);
            Main_Grid.SizeChanged += WordFocusSurfaceSizeChanged;
            _wordLecture = new WordLectureController(inkCanvas, WordLectureControls, Path.Combine(App.RootPath, "WordAnnotations"),
                () => currentMode == 0 && !IsCurrentPageFrozen && !IsInPPTPresentationMode && !_isVideoPresenterSpecialMode && !IsPluginCanvasToolActive,
                () => _wordTouches.Count > 0
                    || (Mouse.LeftButton == MouseButtonState.Pressed && inkCanvas.IsMouseCaptureWithin)
                    || (Stylus.CurrentStylusDevice?.InAir == false && inkCanvas.IsStylusCaptureWithin),
                owned =>
                {
                    _wordOwnsCanvas = owned;
                    _inkFadeManager?.ClearAllFadingStrokes();
                    if (owned)
                    {
                        AddedStroke = ReplacedStroke = null;
                        _wordDesktopHistory = timeMachine;
                        _wordDesktopUndo = IsUndoEnabled; _wordDesktopRedo = IsRedoEnabled;
                        timeMachine = new TimeMachine();
                        IsUndoEnabled = IsRedoEnabled = false;
                    }
                    else
                    {
                        AddedStroke = ReplacedStroke = null;
                        timeMachine = _wordDesktopHistory ?? timeMachine;
                        _wordDesktopHistory = null;
                        IsUndoEnabled = _wordDesktopUndo; IsRedoEnabled = _wordDesktopRedo;
                    }
                    if (!_wordDisposing) SyncWinRTInkPipelineWithLogicalTool();
                },
                action =>
                {
                    var previous = _currentCommitType;
                    _currentCommitType = CommitReason.CodeInput;
                    try { action(); } finally { _currentCommitType = previous; }
                },
                (undo, redo) =>
                {
                    if (_wordOwnsCanvas) timeMachine = new TimeMachine();
                    IsUndoEnabled = undo; IsRedoEnabled = redo;
                }, clearTransient: () => _inkFadeManager?.ClearAllFadingStrokes(), annotationEnabled: () => IsAnnotating,
                pausePresentation: paused =>
                {
                    if (paused) _inkFadeManager?.ClearAllFadingStrokes();
                    inkCanvas.PausePresentation(paused);
                }, previewTransform: inkCanvas.SetPresentationTransform, placeWindow: _wordToolbarPlacement.Update,
                focusChanged: UpdateWordFocusToolbar);
            _wordInputRegionUpdate = new DispatcherCoalescedAction(Dispatcher, TimeSpan.FromMilliseconds(32), UpdateWordInputRegion);
            _wordBoundaryTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
            { Interval = TimeSpan.FromMilliseconds(40) };
            _wordBoundaryTimer.Tick += WordBoundaryTick;
            _wordLecture.InputRegionChanged += UpdateWordInputRegion;
            Main_Grid.LayoutUpdated += WordInputLayoutUpdated;
            inkCanvas.EditingModeChanged += WordInputToolChanged;
            AddHandler(PreviewTouchDownEvent, new EventHandler<TouchEventArgs>((_, e) => _wordTouches.Add(e.TouchDevice.Id)), true);
            AddHandler(PreviewTouchUpEvent, new EventHandler<TouchEventArgs>((_, e) =>
            { _wordTouches.Remove(e.TouchDevice.Id); _wordLecture?.NotifyInkInputCompleted(); }), true);
            AddHandler(LostTouchCaptureEvent, new EventHandler<TouchEventArgs>((_, e) =>
            { _wordTouches.Remove(e.TouchDevice.Id); _wordLecture?.NotifyInkInputCompleted(); }), true);
            AddHandler(PreviewMouseUpEvent, new MouseButtonEventHandler((_, _) => _wordLecture?.NotifyInkInputCompleted()), true);
            AddHandler(PreviewStylusUpEvent, new StylusEventHandler((_, _) => _wordLecture?.NotifyInkInputCompleted()), true);
            Deactivated += (_, __) => _wordTouches.Clear();
            ApplyWordLinkSettings();
        }

        internal void ApplyWordLinkSettings()
        {
            var settings = Settings.WordLink ??= new WordLinkSettings();
            settings.Normalize();
            WordLectureControls.ApplySettings(settings, Settings.PowerPointSettings, false);
            RightBottomWordLectureControls.ApplySettings(settings, Settings.PowerPointSettings, true);
            _wordToolbarPlacement?.Configure(settings.FollowWordWindow, settings.HorizontalInset, settings.BottomInset);
            _wordLecture?.Configure(settings.Enabled, settings.ShowNavigation, settings.AutoEnterFocusMode);
            _wordFocusToolbarPresentation?.Refresh();
            UpdateWordInputRegion();
        }

        private void WordInputLayoutUpdated(object sender, EventArgs e)
        {
            if (_wordInputRegion.IsApplied || _wordOwnsCanvas) _wordInputRegionUpdate?.Request();
        }

        private void WordInputToolChanged(object sender, RoutedEventArgs e) => UpdateWordInputRegion();

        private void UpdateWordInputRegion()
        {
            if (_wordDisposing) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            bool targetAlive = WordWindowInputRegion.IsAlive(_wordLecture?.WindowHandle ?? 0);
            bool scoped = Settings.WordLink?.Enabled == true && targetAlive && currentMode == 0
                && !IsInPPTPresentationMode && !_isVideoPresenterSpecialMode && !IsPluginCanvasToolActive;
            try
            {
                if (!scoped)
                {
                    _wordBoundaryTimer?.Stop();
                    _wordBoundaryTool.UserSelectedTool();
                    bool wordClosed = !targetAlive && _wordInputRegion.IsApplied
                        && Settings.WordLink?.Enabled == true && currentMode == 0
                        && !IsInPPTPresentationMode && !_isVideoPresenterSpecialMode && !IsPluginCanvasToolActive;
                    _wordInputRegion.Restore();
                    if (wordClosed && IsAnnotating) CursorIcon_Click(null, null);
                    return;
                }
                if (_wordBoundaryTimer?.IsEnabled == false) _wordBoundaryTimer.Start();
                if (!GetWindowRect(hwnd, out var window)) return;
                var regions = new List<Rect>();
                // The region also bounds rendering: keep existing Word ink visible in
                // cursor/scroll mode. The normal tool's hit-test policy controls input there.
                if (_wordOwnsCanvas && _wordLecture.AnnotationViewport is { } viewport)
                    regions.Add(new Rect(viewport.X - window.Left, viewport.Y - window.Top, viewport.Width, viewport.Height));
                // Popups have their own HWND. Only actual main-window controls belong
                // in this union; full-window structural containers must stay excluded.
                var names = CanvasChromeGeometry.Names.Where(name => name != "GridInkCanvasSelectionCover")
                    .Concat(new[] { "LeftSidePanel", "RightSidePanel", "LeftUnFoldButtonQuickPanel", "RightUnFoldButtonQuickPanel",
                        "BorderStrokeSelectionControl", "BorderImageSelectionControl", "BorderPdfPageSidebar",
                        "QuickDrawFloatingButton", "PPTTimeCapsuleContainer" }).Distinct();
                foreach (string name in names)
                    if (FindName(name) is FrameworkElement element && CanvasChromeGeometry.TryGetBounds(element, this, out var bounds))
                    {
                        Point topLeft = PointToScreen(bounds.TopLeft), bottomRight = PointToScreen(bounds.BottomRight);
                        regions.Add(new Rect(topLeft.X - window.Left, topLeft.Y - window.Top,
                            bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));
                    }
                _wordInputRegion.Update(hwnd, regions);
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[WordLink] Input region update failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        private void WordBoundaryTick(object sender, EventArgs e)
        {
            if (_wordDisposing || !_wordInputRegion.IsApplied) return;
            if (currentMode != 0 || Settings.WordLink?.Enabled != true || IsInPPTPresentationMode
                || _isVideoPresenterSpecialMode || IsPluginCanvasToolActive) return;
            if (_wordTouches.Count > 0 || Mouse.LeftButton == MouseButtonState.Pressed
                || Mouse.RightButton == MouseButtonState.Pressed || Stylus.CurrentStylusDevice?.InAir == false) return;
            try
            {
                var cursor = System.Windows.Forms.Cursor.Position;
                var screen = new Point(cursor.X, cursor.Y);
                var hwnd = new WindowInteropHelper(this).Handle;
                var top = WindowFromPoint(new NativeWin32Point(cursor.X, cursor.Y));
                if (top != IntPtr.Zero && top != hwnd)
                {
                    global::Windows.Win32.PInvoke.GetWindowThreadProcessId(new global::Windows.Win32.Foundation.HWND(top), out uint process);
                    if (process == Environment.ProcessId) return; // Popup palette or another ICCCE window.
                }
                Point local = PointFromScreen(screen);
                bool chrome = ShouldHandleWindowChromeHitTest(local)
                    || ContainsPoint(WordLectureControls, local) || ContainsPoint(RightBottomWordLectureControls, local);
                var viewport = _wordLecture?.AnnotationViewport;
                bool inside = viewport != null && new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height).Contains(screen);
                var change = _wordBoundaryTool.Update(inside, chrome, false, _currentToolMode, drawingShapeMode);
                if (change == null) return;
                _wordAutoToolChanging = true;
                try
                {
                    if (change.Value.Mode == "cursor")
                    {
                        _wordBoundaryAttributes = drawingAttributes.Clone();
                        _wordBoundaryPenType = penType;
                        _wordBoundaryInkFade = Settings.Canvas.EnableInkFade;
                    }
                    drawingShapeMode = 0;
                    if (change.Value.Mode == "cursor") CursorIcon_Click(null, null);
                    else
                    {
                        suppressNextPenPaletteOpen = true;
                        switch (change.Value.Mode)
                        {
                            case "eraser": EraserIcon_Click(null, null); break;
                            case "eraserByStrokes": EraserIconByStrokes_Click(null, null); break;
                            case "select": SymbolIconSelect_MouseUp(null, null); break;
                            default: PenIcon_Click(null, null); break;
                        }
                        drawingShapeMode = change.Value.Shape;
                        if (_wordBoundaryAttributes is { } saved)
                        {
                            // PenIcon_Click may normalize highlighter/laser attributes.
                            // Auto-return must keep the exact tool the user was using.
                            penType = _wordBoundaryPenType;
                            drawingAttributes.Color = saved.Color;
                            drawingAttributes.Width = saved.Width; drawingAttributes.Height = saved.Height;
                            drawingAttributes.IsHighlighter = saved.IsHighlighter;
                            drawingAttributes.StylusTip = saved.StylusTip;
                            drawingAttributes.StylusTipTransform = saved.StylusTipTransform;
                            drawingAttributes.IgnorePressure = saved.IgnorePressure;
                            drawingAttributes.FitToCurve = saved.FitToCurve;
                            Settings.Canvas.EnableInkFade = _wordBoundaryInkFade;
                            if (_inkFadeManager != null) _inkFadeManager.IsEnabled = _wordBoundaryInkFade;
                            UpdateBoardPenIconColor();
                        }
                    }
                }
                finally { _wordAutoToolChanging = false; }
                QueueWordBoundaryToolbarPosition();
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[WordLink] Boundary tool switch failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        private void QueueWordBoundaryToolbarPosition()
        {
            if (_wordBoundaryToolbarPositionQueued || _wordDisposing) return;
            _wordBoundaryToolbarPositionQueued = true;
            // Visibility changes alter toolbar width. Loaded runs after the pending
            // layout/render pass; coalesce rapid tool changes and position the latest UI.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _wordBoundaryToolbarPositionQueued = false;
                if (_wordDisposing || isFloatingBarFolded || currentMode != 0
                    || Settings.WordLink?.Enabled != true || !_wordInputRegion.IsApplied) return;
                if (!_userHasDraggedFloatingBar) pointDesktop = new Point(-1, -1);
                ViewboxFloatingBarMarginAnimation(100, true, skipAnimation: true);
            }));
        }

        private void UpdateWordFocusToolbar(bool focus)
        {
            if (focus == IsWordFocusToolbarActive) return;
            if (focus)
            {
                _wordFocusSavedDesktopPoint = pointDesktop;
                _wordFocusSavedDragged = _userHasDraggedFloatingBar;
            }
            else RestoreWordFocusOverlayBounds();
            _wordFocusToolbarPresentation?.Update(focus);
            if (focus) ExpandWordFocusOverlayBounds();
            if (!focus)
            {
                pointDesktop = _wordFocusSavedDesktopPoint;
                _userHasDraggedFloatingBar = _wordFocusSavedDragged;
                if (!IsAnnotating) RestoreFullScreenOnExitAnnotationMode();
            }
            pos = new Point(ViewboxFloatingBar.Margin.Left, ViewboxFloatingBar.Margin.Top);
            ApplyFloatingBarMenuOpacity();
            UpdateToolbarComponentVisibility();
        }
        private void ExpandWordFocusOverlayBounds()
        {
            if (!AvoidFullScreenHelper.GetIsAvoidFullScreenEnabled(this)) return;
            var handle = new WindowInteropHelper(this).Handle;
            if (!GetWindowRect(handle, out var previous)) return;
            _wordFocusSavedOverlayBounds = new Rect(previous.Left, previous.Top, previous.Right - previous.Left, previous.Bottom - previous.Top);
            // Like PPT, allow the overlay to reach the screen edge while Word
            // hides the taskbar. Temporarily release only this window's hook.
            AvoidFullScreenHelper.StopAvoidFullScreen(this);
            var screen = System.Windows.Forms.Screen.FromHandle(new IntPtr(_wordLecture?.WindowHandle ?? 0)).Bounds;
            MoveWindow(handle, screen.Left, screen.Top, screen.Width, screen.Height, true);
            _wordFocusToolbarPresentation.Refresh();
        }
        private void RestoreWordFocusOverlayBounds()
        {
            if (_wordFocusSavedOverlayBounds is not { } previous) return;
            _wordFocusSavedOverlayBounds = null;
            if (currentMode == 0 && !IsInPPTPresentationMode && !_isVideoPresenterSpecialMode && !IsAnnotating)
                MoveWindow(new WindowInteropHelper(this).Handle, (int)previous.X, (int)previous.Y, (int)previous.Width, (int)previous.Height, true);
            if (Settings.Advanced.IsEnableAvoidFullScreenHelper) AvoidFullScreenHelper.StartAvoidFullScreen(this);
        }
        private Rect GetWordFocusToolbarBounds()
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new IntPtr(_wordLecture?.WindowHandle ?? 0)).Bounds;
            var bounds = new Rect(Main_Grid.PointFromScreen(new Point(screen.Left, screen.Top)),
                Main_Grid.PointFromScreen(new Point(screen.Right, screen.Bottom)));
            bounds.Intersect(new Rect(Main_Grid.RenderSize));
            return bounds;
        }
        private void CancelWordFocusToolbarAnimation()
        {
            ++_floatingBarPositionVersion;
            ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
            isViewboxFloatingBarMarginAnimationRunning = false;
        }
        private void WordFocusSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
            => _wordFocusToolbarPresentation?.Refresh();

        private void SuspendWordLecture() => _wordLecture?.Suspend();
        internal bool SaveWordBeforeRestart()
        {
            if (!Dispatcher.CheckAccess()) return Dispatcher.Invoke(SaveWordBeforeRestart);
            try
            {
                if (_wordLecture?.FlushForRestart() != false) return true;
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[WordLink] Restart flush failed: {ex.GetType().Name}", LogHelper.LogType.Error); }
            ShowNotification(Properties.WordStrings.SaveFailed);
            return false;
        }
        private void DisposeWordLecture()
        {
            if (_wordBoundaryTimer != null)
            {
                _wordBoundaryTimer.Stop(); _wordBoundaryTimer.Tick -= WordBoundaryTick; _wordBoundaryTimer = null;
            }
            Main_Grid.LayoutUpdated -= WordInputLayoutUpdated;
            inkCanvas.EditingModeChanged -= WordInputToolChanged;
            _wordInputRegionUpdate?.Dispose(); _wordInputRegionUpdate = null;
            if (_wordLecture != null) _wordLecture.InputRegionChanged -= UpdateWordInputRegion;
            _wordInputRegion.Restore();
            _wordDisposing = true;
            _wordLecture?.Dispose(); _wordLecture = null;
            _wordToolbarPlacement?.Dispose(); _wordToolbarPlacement = null;
            _wordFocusToolbarPresentation?.Dispose(); _wordFocusToolbarPresentation = null;
            Main_Grid.SizeChanged -= WordFocusSurfaceSizeChanged;
            _wordTouches.Clear();
        }
    }
}
