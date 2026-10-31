using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
#if ICC_NATIVE_WET_INK
using Ink_Canvas.Ink.Native;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private InkCanvasEditingMode GetPalmLogicalEditingMode()
        {
            var mode = inkCanvas?.EditingMode ?? InkCanvasEditingMode.None;
            return mode == InkCanvasEditingMode.None && (isPalmEraserActive || _changingPalmMode)
                ? palmEraserPreviousEditingMode : mode;
        }
        private readonly PalmEraserEngine _palmEngine = new();
        private readonly HashSet<int> _palmSuppressedTouches = new();
        private readonly HashSet<uint> _palmSuppressedPointers = new();
        private TouchDevice _palmTouchDevice;
#if ICC_NATIVE_WET_INK
        private uint? _palmNativePointer;
#endif
        private bool _changingPalmMode;
        private bool _palmPreviousManipulation;
        private bool _palmPreviousStrokeEraser;
        private double _palmPreviousWidth;
        private double _palmHeight;
        private Point? _palmLastPoint;
        private uint? _winRTInkPalmPointer;
        private int _palmRevision;
        private IDisposable _palmRawSubscription;
        private PalmInputMailbox<(RawPalmContact Contact, Rect Pixels)> _palmRawMailbox;
        private Ink.WinRT.WinRTInkInputGate _palmRawGate;
        private readonly Dictionary<(string Device, int Id), (bool Canvas, long Time)> _palmRawRegions = new();

        private void InitializePalmRawInput()
        {
            if (_palmRawSubscription != null) return;
            try { _palmRawSubscription = PalmRawHidSource.Subscribe(OnRawPalmContact); }
            catch (Exception ex) { LogHelper.WriteLogToFile("[PalmHid] Initialization failed: " + ex.Message, LogHelper.LogType.Warning); }
        }

        private void OnRawPalmContact(RawPalmContact sample)
        {
            var key = (sample.Device, sample.Id);
            if (sample.Released) _palmRawRegions.Remove(key);
            if (!CanStartPalmEraser() || !IsVisible || !IsEnabled) return;
            var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
            var environment = TouchCalibrationDisplay.Capture(this, "RawHID");
            var profile = CreatePalmEraserProfile("RawHID");
            var size = PalmEraserEngine.HidDimensions(sample.Width, sample.Height, sample.WidthMaximum,
                sample.HeightMaximum, environment.PixelWidth, environment.PixelHeight, profile.Options.HidScale);
            var physical = new Point(screen.Left + sample.X * screen.Width, screen.Top + sample.Y * screen.Height);
            if (!double.IsFinite(physical.X) || !double.IsFinite(physical.Y)) return;
            if (!sample.Released)
            {
                long now = Environment.TickCount64;
                if (!_palmRawRegions.TryGetValue(key, out var region) || now - region.Time > 300)
                {
                    foreach (var expired in _palmRawRegions.Where(p => now - p.Value.Time > 300).Select(p => p.Key).ToArray())
                        _palmRawRegions.Remove(expired);
                    if (_palmRawRegions.Count >= 128) return;
                    Point windowPoint = PointFromScreen(physical);
                    // Classify the down once; moving samples must not repeatedly walk the
                    // visual tree on a low-powered device, or turn a toolbar tap into erase.
                    region = (ResolveHitZone(windowPoint.X, windowPoint.Y, out _) == CanvasHitZone.CanvasSurface, now);
                }
                _palmRawRegions[key] = (region.Canvas, now);
                if (!region.Canvas) return;
            }
            if (_winRTInkInputGate != null && _winRTInkHost != null && _winRTInkStarted)
            {
                if (_palmRawGate != _winRTInkInputGate)
                {
                    _palmRawMailbox?.Dispose();
                    _palmRawGate = _winRTInkInputGate;
                    var gate = _palmRawGate;
                    var host = _winRTInkHost;
                    _palmRawMailbox = new(action => host.RunOnInkThread(_ => action()),
                        frame => gate.SubmitRawPalm(frame.Contact.Id, frame.Pixels, frame.Contact.Pressure,
                            frame.Contact.Released, frame.Contact.Device), gate.CancelActiveStrokes);
                }
                // Native presenter positions are physical pixels relative to the host HWND.
                Point origin = PointToScreen(new Point());
                var pixels = new Rect(physical.X - origin.X - size.Width / 2,
                    physical.Y - origin.Y - size.Height / 2, size.Width, size.Height);
                _palmRawMailbox.Post((sample, pixels));
                return;
            }
            _palmEngine.Configure(profile);
            if (!_palmEngine.AcceptDevice(sample.Device)) return;
            if (sample.Released)
            {
                _palmEngine.Remove(PalmContactSource.RawHid, sample.Id);
                if (isPalmEraserActive) { _palmLastPoint = null; EndPalmEraser(); }
                return;
            }
            Point local = inkCanvas.PointFromScreen(physical);
            _palmEngine.NotePressure(sample.Pressure);
            _palmEngine.Submit(PalmContactSource.RawHid, sample.Id,
                new Rect(local.X * profile.DpiX - size.Width / 2, local.Y * profile.DpiY - size.Height / 2,
                    size.Width, size.Height), false, sample.Device);
            if (!_palmEngine.TryGetNearestEraser(new Point(local.X * profile.DpiX, local.Y * profile.DpiY), out var eraser))
            {
                if (_palmEngine.ActiveSource == PalmContactSource.RawHid && isPalmEraserActive) EndPalmEraser();
                return;
            }
            // Raw input never captures chrome: require a corresponding canvas contact.
            if (dec.Count == 0 && !_palmSuppressedTouches.Any()) return;
            foreach (int id in dec) { CancelPalmTouchPreview(id); _palmSuppressedTouches.Add(id); }
            var position = new Point(eraser.CenterPx.X / profile.DpiX, eraser.CenterPx.Y / profile.DpiY);
            if (!isPalmEraserActive) BeginPalmEraser(position, eraser.WidthPx / profile.DpiX, eraser.HeightPx / profile.DpiY);
            else
            {
                if (eraser.Revision != _palmRevision) BreakPalmEraserPath();
                MovePalmEraser(position, eraser.WidthPx / profile.DpiX, eraser.HeightPx / profile.DpiY);
            }
            _palmRevision = eraser.Revision;
        }

        private bool CanStartPalmEraser()
        {
            return Settings.Canvas.EnablePalmEraser && !IsCurrentPageFrozen && !IsPluginCanvasToolActive
                && !_isVideoPresenterSpecialMode && !IsBoardRoamingMode
                && !isSingleFingerDragMode && drawingShapeMode == 0
#if ICC_NATIVE_WET_INK
                && ResolveLogicalInkTool() == LogicalInkTool.Pen
#else
                && (_currentToolMode == "pen" || _currentToolMode == "color")
#endif
                && !isInMultiTouchMode
                && !(currentMode == 1 ? Settings.Gesture.IsEnableMultiTouchModeBoard : Settings.Gesture.IsEnableMultiTouchMode);
        }

        private PalmEraserProfile CreatePalmEraserProfile(string source)
        {
            ResolvePalmEraserMetrics(source, out double threshold, out double scale);
            return PalmEraserProfiles.Create(Settings.Advanced.TouchCalibration,
                TouchCalibrationDisplay.Capture(this, Settings.Advanced.TouchCalibration?.Environment?.Source ?? "WPF"), Settings.Advanced.PalmEraser,
                isEraserCircleShape, threshold, scale);
        }

        private void ResolvePalmEraserMetrics(string source, out double threshold, out double scale)
        {
#if ICC_NATIVE_WET_INK
            var policy = BuildPalmRoutePolicy(source);
            threshold = Math.Max(policy.BoundsWidthDip,
                policy.BoundsWidthDip * policy.ThresholdFactor * policy.SensitivityMultiplier);
            scale = policy.EraserSizeFactor * (policy.IsSpecialScreen ? policy.TouchMultiplier : 1);
#else
            var advanced = Settings.Advanced;
            bool nib = Settings.Startup.IsEnableNibMode;
            double sensitivity = Settings.Canvas.PalmEraserSensitivity == 0 ? 3
                : Settings.Canvas.PalmEraserSensitivity == 1 ? 2.5 : 2;
            double factor = nib ? advanced.NibModeBoundsWidthThresholdValue : advanced.FingerModeBoundsWidthThresholdValue;
            threshold = Math.Max(BoundsWidth, BoundsWidth * factor * sensitivity);
            scale = (nib ? advanced.NibModeBoundsWidthEraserSize : advanced.FingerModeBoundsWidthEraserSize)
                * (advanced.IsSpecialScreen ? advanced.TouchMultiplier : 1);
            if (TouchCalibrationPolicy.TryResolve(advanced.TouchCalibration,
                TouchCalibrationDisplay.Capture(this, source), Settings.Canvas.PalmEraserSensitivity,
                out var calibratedThreshold, out var calibratedScale))
            {
                threshold = calibratedThreshold;
                scale = calibratedScale;
            }
#endif
        }

        private void HandleWinRTInkPalm(Ink.WinRT.WinRTInkPalmEvent input)
        {
            if (input.Phase == Ink.WinRT.WinRTInkPalmPhase.End)
            {
                if (_winRTInkPalmPointer.HasValue && (input.PointerId == 0 || _winRTInkPalmPointer == input.PointerId))
                {
                    try
                    {
                        // A release can carry a final point not delivered by PointerMoving.
                        // Cancellation/overflow events have no dimensions and must not erase.
                        if (input.WidthDip > 0 && input.HeightDip > 0 && CanStartPalmEraser())
                            MovePalmEraser(WinRTInkPointToCanvasDip(input.Position.X, input.Position.Y), input.WidthDip, input.HeightDip);
                    }
                    finally { EndPalmEraser(); }
                }
                return;
            }
            if (!CanStartPalmEraser())
            {
                if (_winRTInkPalmPointer.HasValue) EndPalmEraser();
                return;
            }
            var position = WinRTInkPointToCanvasDip(input.Position.X, input.Position.Y);
            if (input.BreakPath && isPalmEraserActive) BreakPalmEraserPath();
            if (!_winRTInkPalmPointer.HasValue && !isPalmEraserActive)
            {
                _winRTInkPalmPointer = input.PointerId;
                CancelStartupFoldVerification();
                ShutdownWinRTInkPauseStraighten();
                BeginPalmEraser(position, input.WidthDip, input.HeightDip);
            }
            else if (_winRTInkPalmPointer == input.PointerId)
                MovePalmEraser(position, input.WidthDip, input.HeightDip);
        }

        private bool TryHandlePalmTouch(TouchEventArgs e, bool down)
        {
            int id = e.TouchDevice.Id;
            if (!CanStartPalmEraser()) { if (isPalmEraserActive) EndPalmEraser(); return false; }
            if (!down && !dec.Contains(id) && !_palmSuppressedTouches.Contains(id)) return false;
            var profile = CreatePalmEraserProfile("WPF");
            _palmEngine.Configure(profile);
            if (down) _palmEngine.Remove(PalmContactSource.Software, id);
            var touch = e.GetTouchPoint(inkCanvas);
            _palmEngine.Submit(PalmContactSource.Software, id,
                new Rect((touch.Position.X - touch.Bounds.Width / 2) * profile.DpiX,
                    (touch.Position.Y - touch.Bounds.Height / 2) * profile.DpiY,
                    touch.Bounds.Width * profile.DpiX, touch.Bounds.Height * profile.DpiY));
            bool erase = _palmEngine.ActiveSource == PalmContactSource.RawHid
                ? _palmEngine.TryGetNearestEraser(new Point(touch.Position.X * profile.DpiX, touch.Position.Y * profile.DpiY), out var frame)
                : _palmEngine.TryGetEraser(id, out frame);
            if (!erase)
            {
                if (_palmTouchDevice == e.TouchDevice) EndPalmEraser();
                if (!isPalmEraserActive && !_palmSuppressedTouches.Contains(id)) return false;
                _palmSuppressedTouches.Add(id);
                e.Handled = true;
                return true;
            }
            var position = new Point(frame.CenterPx.X / profile.DpiX, frame.CenterPx.Y / profile.DpiY);
            double width = frame.WidthPx / profile.DpiX, height = frame.HeightPx / profile.DpiY;
            if (!IsUsablePalmSize(width, height))
                return false;
            // 接触由书写升级为手掌时，丢弃尚未提交的预览，不删除已有干墨。
            foreach (int palmId in _palmEngine.ActiveSource == PalmContactSource.RawHid ? dec.ToArray() : _palmEngine.PalmIds)
            {
                CancelPalmTouchPreview(palmId);
                _palmSuppressedTouches.Add(palmId);
            }
            _palmSuppressedTouches.Add(id);
            if (!isPalmEraserActive)
            {
                _palmTouchDevice = e.TouchDevice;
                BeginPalmEraser(position, width, height);
                inkCanvas.LostTouchCapture += PalmEraser_LostTouchCapture;
                inkCanvas.CaptureTouch(e.TouchDevice);
            }
            else
            {
                if (frame.Revision != _palmRevision) BreakPalmEraserPath();
                MovePalmEraser(position, width, height);
            }
            _palmRevision = frame.Revision;
            e.Handled = true;
            return true;
        }

        private void CancelPalmTouchPreview(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual))
            {
                RealtimeInkFrameScheduler.Cancel(visual);
                RealtimeInkPerformanceMonitor.EndStroke(visual);
            }
            if (VisualCanvasList.TryGetValue(id, out var canvas))
                inkCanvas.Children.Remove(canvas);
            StrokeVisualList.Remove(id);
            VisualCanvasList.Remove(id);
            TouchDownPointsList.Remove(id);
            _activeRealtimeTouchStrokeIds.Remove(id);
            _activeTouchStrokeIds.Remove(id);
            CleanupRealtimeBrushTipState(id);
            CancelPauseStraightenTimer(id);
            EndTouchInkInputIfIdle();
#if ICC_NATIVE_WET_INK
            // WPF fallback 的 pointer namespace 与 WpfPointerInputSource 保持一致。
            uint pointer = 0x80000000 | (unchecked((uint)id) & 0x3FFFFFFF);
            _nativePointerUpdatePump?.DiscardPointer(pointer);
            _nativeInkController?.Cancel(pointer);
            _wetInkWindowHost?.SignalWork();
#endif
        }

        private bool FinishPalmTouch(TouchEventArgs e)
        {
            int id = e.TouchDevice.Id;
            _palmEngine.Remove(PalmContactSource.Software, id);
            bool suppressed = _palmSuppressedTouches.Remove(id);
            if (_palmTouchDevice == e.TouchDevice)
                EndPalmEraser();
            if (!suppressed) return false;
            dec.Remove(id);
            inkCanvas.ReleaseTouchCapture(e.TouchDevice);
            if (dec.Count == 0)
            {
                EndPalmEraser();
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            }
            e.Handled = true;
            return true;
        }

        private void PalmEraser_LostTouchCapture(object sender, TouchEventArgs e)
        {
            var device = e.TouchDevice;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isPalmEraserActive && _palmTouchDevice == device
                    && device.Captured != inkCanvas)
                {
                    dec.Remove(device.Id);
                    _palmEngine.Remove(PalmContactSource.Software, device.Id);
                    EndPalmEraser();
                    ViewboxFloatingBar.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                }
            }), DispatcherPriority.Input);
        }

        private void BeginPalmEraser(Point position, double width, double height)
        {
            if (!IsUsablePalmSize(width, height)) return;
            palmEraserPreviousEditingMode = inkCanvas.EditingMode;
            _palmPreviousWidth = eraserWidth;
            _palmPreviousStrokeEraser = isUsingStrokesEraser;
            _palmPreviousManipulation = inkCanvas.IsManipulationEnabled;
            _changingPalmMode = true;
            try
            {
                // 只使用几何擦除器；不能同时让 InkCanvas 内建点擦再次修改笔画。
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
            finally { _changingPalmMode = false; }
            isPalmEraserActive = true;
            inkCanvas.IsManipulationEnabled = false;
            isUsingStrokesEraser = false;
            eraserWidth = width;
            _palmHeight = height;
            _palmLastPoint = null;
            EnableEraserOverlay();
            if (eraserOverlayCanvas != null) eraserOverlayCanvas.IsHitTestVisible = false;
            EraserOverlay_PointerDown(inkCanvas);
            MovePalmEraser(position, width, height);
        }

        private void MovePalmEraser(Point position, double width, double height)
        {
            if (!IsUsablePalmSize(width, height) || !double.IsFinite(position.X) || !double.IsFinite(position.Y)) return;
            if (IsCurrentPageFrozen)
            {
                EndPalmEraser();
                return;
            }
            if (eraserWidth != width || _palmHeight != height)
            {
                eraserWidth = width;
                _palmHeight = height;
                // 尺寸变化只重建命中器，不拆分同一次手掌擦的撤销记录。
                if (hitTester != null)
                {
                    hitTester.StrokeHit -= EraserGeometry_StrokeHit;
                    hitTester.EndHitTesting();
                }
                hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(CreateGeometryEraserShape());
                hitTester.StrokeHit += EraserGeometry_StrokeHit;
                if (_palmLastPoint.HasValue) hitTester.AddPoint(_palmLastPoint.Value);
                if (eraserFeedback != null)
                {
                    eraserFeedback.Width = width;
                    eraserFeedback.Height = height;
                }
            }
            EraserOverlay_PointerMove(inkCanvas, position);
            _palmLastPoint = position;
        }

        private void BreakPalmEraserPath()
        {
            _palmLastPoint = null;
            if (hitTester != null)
            {
                hitTester.StrokeHit -= EraserGeometry_StrokeHit;
                hitTester.EndHitTesting();
            }
            hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(CreateGeometryEraserShape());
            hitTester.StrokeHit += EraserGeometry_StrokeHit;
        }

        private void EndPalmEraser(bool restoreMode = true)
        {
            if (!isPalmEraserActive) return;
            isPalmEraserActive = false;
            inkCanvas.LostTouchCapture -= PalmEraser_LostTouchCapture;
            if (_palmTouchDevice != null)
            {
                dec.Remove(_palmTouchDevice.Id);
                _palmSuppressedTouches.Remove(_palmTouchDevice.Id);
                if (_palmTouchDevice.Captured == inkCanvas) inkCanvas.ReleaseTouchCapture(_palmTouchDevice);
            }
            _palmTouchDevice = null;
#if ICC_NATIVE_WET_INK
            _palmNativePointer = null;
#endif
            _winRTInkPalmPointer = null;
            _palmLastPoint = null;
            try { DisableEraserOverlay(); }
            finally
            {
                eraserWidth = _palmPreviousWidth;
                isUsingStrokesEraser = _palmPreviousStrokeEraser;
                inkCanvas.IsManipulationEnabled = _palmPreviousManipulation;
                _changingPalmMode = true;
                try
                {
                    if (restoreMode && inkCanvas.EditingMode == InkCanvasEditingMode.None && !IsCurrentPageFrozen)
                        inkCanvas.EditingMode = palmEraserPreviousEditingMode;
                }
                finally { _changingPalmMode = false; }
                if (ViewboxFloatingBar != null) ViewboxFloatingBar.IsHitTestVisible = true;
                if (BlackboardUIGridForInkReplay != null) BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                if (eraserOverlayCanvas != null) eraserOverlayCanvas.IsHitTestVisible = false;
                SetCursorBasedOnEditingMode(inkCanvas);
            }
        }

        private static bool IsUsablePalmSize(double width, double height) =>
            double.IsFinite(width) && double.IsFinite(height) && width >= 0.003 && height >= 0.003
            && width <= 100000 && height <= 100000;
        internal void ResetTouchCalibrationInput()
        {
            ResetPalmEraser();
            _winRTInkInputGate?.CancelActiveStrokes();
            PushWinRTInkGateSnapshots();
        }

        private void ResetPalmEraser()
        {
            EndPalmEraser();
            _palmEngine.Clear();
            _palmSuppressedTouches.Clear();
            _palmSuppressedPointers.Clear();
            _palmRawRegions.Clear();
        }

#if ICC_NATIVE_WET_INK
        private bool TryHandleNativePalm(NativePointerInputBatch batch, NativePointerFacts facts, bool canStart)
        {
            if (batch.InputKind != NativeInkInputKind.Touch) return false;
            // WPF fallback 由 PreviewTouch 管理，不能把同一帧再次送入滤波窗。
            if (batch.IsWpfFallback)
            {
                bool suppressed = isPalmEraserActive
                    || _palmSuppressedTouches.Contains((int)(batch.PointerId & 0x3FFFFFFF));
                if (suppressed) CancelNativePalmInk(batch.PointerId);
                return suppressed;
            }
            if (isPalmEraserActive && !canStart)
            {
                EndPalmEraser();
                _palmSuppressedPointers.Remove(batch.PointerId);
                CancelNativePalmInk(batch.PointerId);
                return false;
            }
            if (!canStart || !CanStartPalmEraser() || HasCanceledFlag(facts)) return false;
            var profile = CreatePalmEraserProfile("Pointer");
            _palmEngine.Configure(profile);
            Point position = TranslatePoint(new Point(facts.XDip, facts.YDip), inkCanvas);
            _palmEngine.Submit(PalmContactSource.Software, unchecked((int)batch.PointerId),
                new Rect((position.X - facts.ContactWidthDip / 2) * profile.DpiX,
                    (position.Y - facts.ContactHeightDip / 2) * profile.DpiY,
                    facts.ContactWidthDip * profile.DpiX, facts.ContactHeightDip * profile.DpiY));
            if (!_palmEngine.TryGetEraser(unchecked((int)batch.PointerId), out var frame))
            {
                if (_palmNativePointer == batch.PointerId) EndPalmEraser();
                return _palmSuppressedPointers.Contains(batch.PointerId);
            }
            CancelNativePalmInk(batch.PointerId);
            _palmNativePointer = batch.PointerId;
            _palmSuppressedPointers.Add(batch.PointerId);
            position = new Point(frame.CenterPx.X / profile.DpiX, frame.CenterPx.Y / profile.DpiY);
            if (!isPalmEraserActive) BeginPalmEraser(position, frame.WidthPx / profile.DpiX, frame.HeightPx / profile.DpiY);
            else MovePalmEraser(position, frame.WidthPx / profile.DpiX, frame.HeightPx / profile.DpiY);
            return true;
        }

        private void CancelNativePalmInk(uint pointerId)
        {
            _nativePointerUpdatePump?.DiscardPointer(pointerId);
            _nativeInkController?.Cancel(pointerId);
            StopNativePauseStraightenTimer(pointerId);
            _wetInkWindowHost?.SignalWork();
        }

        private bool FinishNativePalm(NativePointerInputBatch batch)
        {
            if (batch.IsWpfFallback || batch.InputKind != NativeInkInputKind.Touch) return false;
            _palmEngine.Remove(PalmContactSource.Software, unchecked((int)batch.PointerId));
            bool suppressed = _palmSuppressedPointers.Remove(batch.PointerId);
            if (suppressed) CancelNativePalmInk(batch.PointerId);
            if (_palmNativePointer == batch.PointerId) EndPalmEraser();
            return suppressed;
        }
#endif
    }
}
