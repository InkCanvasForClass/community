using Ink_Canvas.Ink;
using Ink_Canvas.Ink.Native;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Dictionary<int, PalmEraserContactTracker> _palmContacts = new();
        private readonly HashSet<int> _palmSuppressedTouches = new();
        private readonly HashSet<uint> _palmSuppressedPointers = new();
        private PalmEraserContactTracker _palmContact;
        private TouchDevice _palmTouchDevice;
        private uint? _palmNativePointer;
        private bool _changingPalmMode;
        private bool _palmPreviousManipulation;
        private bool _palmPreviousStrokeEraser;
        private double _palmPreviousWidth;
        private double _palmHeight;
        private Point? _palmLastPoint;

        private bool CanStartPalmEraser()
        {
            return Settings.Canvas.EnablePalmEraser && !IsCurrentPageFrozen
                && !_isVideoPresenterSpecialMode && !IsBoardRoamingMode
                && !isSingleFingerDragMode && drawingShapeMode == 0
                && ResolveLogicalInkTool() == LogicalInkTool.Pen
                && !isInMultiTouchMode
                && !(currentMode == 1 ? Settings.Gesture.IsEnableMultiTouchModeBoard : Settings.Gesture.IsEnableMultiTouchMode);
        }

        private bool UpdatePalmContact(PalmEraserContactTracker contact, double width, double height,
            out double eraserW, out double eraserH)
        {
            var policy = BuildPalmRoutePolicy();
            double threshold = Math.Max(policy.BoundsWidthDip,
                policy.BoundsWidthDip * policy.ThresholdFactor * policy.SensitivityMultiplier);
            double scale = policy.EraserSizeFactor * (policy.IsSpecialScreen ? policy.TouchMultiplier : 1);
            return contact.Update(width, height, threshold, scale, isEraserCircleShape, out eraserW, out eraserH);
        }

        private bool TryHandlePalmTouch(TouchEventArgs e, bool down)
        {
            int id = e.TouchDevice.Id;
            if (isPalmEraserActive)
            {
                if (!CanStartPalmEraser())
                    EndPalmEraser();
                else if (_palmTouchDevice == e.TouchDevice)
                {
                    var point = e.GetTouchPoint(inkCanvas);
                    if (UpdatePalmContact(_palmContact, point.Bounds.Width, point.Bounds.Height, out var w, out var h))
                        MovePalmEraser(point.Position, w, h);
                }
                _palmSuppressedTouches.Add(id);
                e.Handled = true;
                return true;
            }
            if (_palmSuppressedTouches.Contains(id))
            {
                e.Handled = true;
                return true;
            }
            if (!CanStartPalmEraser() || (!down && !dec.Contains(id))
                || (_pluginCanvasGestureHandler != null && dec.Count > 1)
                || (!down && dec.Count > 1))
                return false;

            if (!_palmContacts.TryGetValue(id, out var contact))
            {
                contact = new PalmEraserContactTracker();
                _palmContacts[id] = contact;
            }
            var touch = e.GetTouchPoint(inkCanvas);
            if (!UpdatePalmContact(contact, touch.Bounds.Width, touch.Bounds.Height, out var width, out var height))
                return false;

            // 接触由书写升级为手掌时，丢弃尚未提交的预览，不删除已有干墨。
            CancelPalmTouchPreview(id);
            _palmTouchDevice = e.TouchDevice;
            _palmContact = contact;
            _palmSuppressedTouches.Add(id);
            BeginPalmEraser(touch.Position, width, height);
            inkCanvas.LostTouchCapture += PalmEraser_LostTouchCapture;
            inkCanvas.CaptureTouch(e.TouchDevice);
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
            // WPF fallback 的 pointer namespace 与 WpfPointerInputSource 保持一致。
            uint pointer = 0x80000000 | (unchecked((uint)id) & 0x3FFFFFFF);
            _nativePointerUpdatePump?.DiscardPointer(pointer);
            _nativeInkController?.Cancel(pointer);
            _wetInkWindowHost?.SignalWork();
        }

        private bool FinishPalmTouch(TouchEventArgs e)
        {
            int id = e.TouchDevice.Id;
            _palmContacts.Remove(id);
            bool suppressed = _palmSuppressedTouches.Remove(id);
            if (_palmTouchDevice == e.TouchDevice)
                EndPalmEraser();
            if (!suppressed) return false;
            dec.Remove(id);
            inkCanvas.ReleaseTouchCapture(e.TouchDevice);
            if (dec.Count == 0)
            {
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            }
            e.Handled = true;
            return true;
        }

        private void PalmEraser_LostTouchCapture(object sender, TouchEventArgs e)
        {
            var contact = _palmContact;
            var device = e.TouchDevice;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isPalmEraserActive && _palmContact == contact && _palmTouchDevice == device
                    && device.Captured != inkCanvas)
                {
                    dec.Remove(device.Id);
                    _palmContacts.Remove(device.Id);
                    EndPalmEraser();
                    ViewboxFloatingBar.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                }
            }), DispatcherPriority.Input);
        }

        private void BeginPalmEraser(Point position, double width, double height)
        {
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

        private void EndPalmEraser(bool restoreMode = true)
        {
            if (!isPalmEraserActive) return;
            isPalmEraserActive = false;
            inkCanvas.LostTouchCapture -= PalmEraser_LostTouchCapture;
            _palmTouchDevice = null;
            _palmNativePointer = null;
            _palmContact = null;
            _palmLastPoint = null;
            DisableEraserOverlay();
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
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private void ResetPalmEraser()
        {
            EndPalmEraser();
            _palmContacts.Clear();
            _palmSuppressedTouches.Clear();
            _palmSuppressedPointers.Clear();
        }

        private bool TryHandleNativePalm(NativePointerInputBatch batch, NativePointerFacts facts, bool canStart)
        {
            if (batch.InputKind != NativeInkInputKind.Touch) return false;
            // WPF fallback 由 PreviewTouch 管理，不能把同一帧再次送入滤波窗。
            if (batch.IsWpfFallback)
                return isPalmEraserActive || _palmSuppressedTouches.Contains((int)(batch.PointerId & 0x3FFFFFFF));
            if (isPalmEraserActive)
            {
                _palmSuppressedPointers.Add(batch.PointerId);
                if (!CanStartPalmEraser() || HasCanceledFlag(facts))
                    EndPalmEraser();
                else if (_palmNativePointer == batch.PointerId
                    && UpdatePalmContact(_palmContact, facts.ContactWidthDip, facts.ContactHeightDip, out var w, out var h))
                    MovePalmEraser(TranslatePoint(new Point(facts.XDip, facts.YDip), inkCanvas), w, h);
                return true;
            }
            if (_palmSuppressedPointers.Contains(batch.PointerId)) return true;
            if (!canStart || !CanStartPalmEraser() || HasCanceledFlag(facts)) return false;
            var contact = new PalmEraserContactTracker();
            if (!UpdatePalmContact(contact, facts.ContactWidthDip, facts.ContactHeightDip, out var width, out var height))
                return false;
            _nativePointerUpdatePump?.DiscardPointer(batch.PointerId);
            _nativeInkController?.Cancel(batch.PointerId);
            _wetInkWindowHost?.SignalWork();
            _palmContact = contact;
            _palmNativePointer = batch.PointerId;
            _palmSuppressedPointers.Add(batch.PointerId);
            BeginPalmEraser(TranslatePoint(new Point(facts.XDip, facts.YDip), inkCanvas), width, height);
            return true;
        }

        private bool FinishNativePalm(NativePointerInputBatch batch)
        {
            if (batch.IsWpfFallback || batch.InputKind != NativeInkInputKind.Touch) return false;
            bool suppressed = _palmSuppressedPointers.Remove(batch.PointerId);
            if (_palmNativePointer == batch.PointerId) EndPalmEraser();
            return suppressed;
        }
    }
}
