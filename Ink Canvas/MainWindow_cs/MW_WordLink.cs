using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Interop;
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
        private void DisposeWordLecture()
        {
            _wordDisposing = true;
            _wordLecture?.Dispose(); _wordLecture = null;
            _wordToolbarPlacement?.Dispose(); _wordToolbarPlacement = null;
            _wordFocusToolbarPresentation?.Dispose(); _wordFocusToolbarPresentation = null;
            Main_Grid.SizeChanged -= WordFocusSurfaceSizeChanged;
            _wordTouches.Clear();
        }
    }
}
