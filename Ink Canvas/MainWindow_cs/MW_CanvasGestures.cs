using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Dictionary<int, TouchDevice> _canvasGestureContacts = new();
        private readonly HashSet<int> _canvasGestureCanceledContacts = new();
        private bool _canvasGestureOwnsTouchInput;
        private bool _canvasGestureIgnoreManipulation;
        private bool _changingCanvasGestureMode;
        private bool _changingCanvasGestureCapture;
        private bool _isApplyingCanvasGesture;
        private bool _canvasGestureInputEventsAttached;
        private InkCanvasEditingMode? _canvasGestureSavedMode;
        private bool _canvasGestureSavedManipulation;
        private int _canvasGestureMode;
        private int _canvasGesturePageIndex;
        private long _canvasGestureFirstTouchTime;
        private int _canvasGestureFirstContactId = -1;
        private int _canvasGestureSecondContactId = -1;
        private Point _canvasGestureFirstPoint;
        private Point _canvasGestureSecondPoint;
        private Matrix _canvasGestureAppliedMatrix = Matrix.Identity;
        private double _canvasGestureAppliedScale = 1;
        private StrokeCollection _canvasGestureSelectedStrokes;
        private Dictionary<Stroke, StylusPointCollection> _canvasGestureStrokePoints;
        private Dictionary<Stroke, DrawingAttributes> _canvasGestureDrawingAttributes;
        private Dictionary<UIElement, Transform> _canvasGestureElementTransforms;
        private HashSet<Stroke> _canvasGestureShapeStartStrokes;

        private bool IsCanvasGestureManipulationSuppressed
            => !_isVideoPresenterSpecialMode && _pluginCanvasGestureHandler == null
                && (_canvasGestureOwnsTouchInput || _canvasGestureIgnoreManipulation);

        private bool CanUseCanvasTwoFingerGesture()
        {
            if (IsBoardRoamingMode || _isVideoPresenterSpecialMode || _pluginCanvasGestureHandler != null
                || IsCurrentPageFrozen || (ArePPTControlsVisible
                    && !Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode)) return false;

            return currentMode == 1
                ? Settings.Gesture.IsEnableTwoFingerTranslateBoard || Settings.Gesture.IsEnableTwoFingerZoomBoard
                    || Settings.Gesture.IsEnableTwoFingerRotationBoard
                : Settings.Gesture.IsEnableTwoFingerTranslate || Settings.Gesture.IsEnableTwoFingerZoom
                    || Settings.Gesture.IsEnableTwoFingerRotation;
        }

        // Track before the editing-mode and handwriting branches. Raw touch remains available even
        // when handwriting or palm calibration disables WPF Manipulation promotion.
        private bool TrackCanvasGestureTouchDown(TouchEventArgs e)
        {
            _canvasGestureCanceledContacts.Remove(e.TouchDevice.Id);
            if (_canvasGestureContacts.Count == 0) _canvasGestureIgnoreManipulation = false;
            if (!_canvasGestureOwnsTouchInput && !CanUseCanvasTwoFingerGesture()) return false;
            AttachCanvasGestureInputEvents();
            if (_canvasGestureContacts.Count == 0)
            {
                _canvasGestureSavedMode = inkCanvas.EditingMode;
                _canvasGestureSavedManipulation = inkCanvas.IsManipulationEnabled;
                _canvasGestureMode = currentMode;
                _canvasGesturePageIndex = GetCurrentFreezePageIndex();
                _canvasGestureFirstTouchTime = Environment.TickCount64;
                _canvasGestureShapeStartStrokes = drawingShapeMode == 0 ? null : new HashSet<Stroke>(inkCanvas.Strokes);
            }
            _canvasGestureContacts[e.TouchDevice.Id] = e.TouchDevice;
            if (_canvasGestureContacts.Count == 2) RebaseCanvasGesture();
            if (!_canvasGestureOwnsTouchInput) return false;

            inkCanvas.CaptureTouch(e.TouchDevice);
            e.Handled = true;
            return true;
        }

        private bool TryStartCanvasGesture(TouchEventArgs e)
        {
            if (_canvasGestureOwnsTouchInput) return true;
            if (_canvasGestureContacts.Count != 2 || !CanUseCanvasTwoFingerGesture()
                || isPalmEraserActive || _palmGroupErasing
                || _canvasGestureContacts.Keys.Any(id => _palmSuppressedTouches.Contains(id))) return false;

            // Nearby point-only contacts may still be a calibrated palm. Allow the grouping window
            // to decide; separated fingers do not need to wait for that window.
            if (_palmGroupSavedMode.HasValue)
            {
                EvaluatePalmGroup();
                if (isPalmEraserActive || _palmGroupErasing) return false;
                var contacts = _canvasGestureContacts.Values.ToArray();
                var first = contacts[0].GetTouchPoint(inkCanvas).Position;
                var second = contacts[1].GetTouchPoint(inkCanvas).Position;
                var record = Settings.Advanced.TouchCalibration;
                if (record != null && Math.Abs(first.X - second.X) <= record.OutlineWidth * record.GroupTolerance
                    && Math.Abs(first.Y - second.Y) <= record.OutlineHeight * record.GroupTolerance
                    && Environment.TickCount64 - _canvasGestureFirstTouchTime < record.WindowMilliseconds)
                    return false;
            }

            _canvasGestureOwnsTouchInput = true;
            _canvasGestureIgnoreManipulation = true;
            _canvasGestureSelectedStrokes = inkCanvas.GetSelectedStrokes();
            _changingCanvasGestureCapture = true;
            _changingCanvasGestureMode = true;
            try
            {
                CancelPalmGroup(false);
                foreach (int id in StrokeVisualList.Keys.Union(VisualCanvasList.Keys).ToArray())
                    CancelPalmTouchPreview(id);
                AbortAllActiveTouchInputs();
                CancelPauseStraightenTimer(-200001);
                _pauseStraightenInkModeTracking = false;
                isMultiTouchTimerActive = false;
                isTouchDown = false;
                isMouseDown = false;
                _isMouseRealtimeInking = false;
                inkCanvas.ReleaseStylusCapture();
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.IsManipulationEnabled = false;
                if (_canvasGestureShapeStartStrokes != null)
                {
                    var commitType = _currentCommitType;
                    _currentCommitType = CommitReason.CodeInput;
                    try
                    {
                        foreach (var stroke in inkCanvas.Strokes.Where(stroke => !_canvasGestureShapeStartStrokes.Contains(stroke)).ToArray())
                            inkCanvas.Strokes.Remove(stroke);
                    }
                    finally { _currentCommitType = commitType; }
                    isWaitUntilNextTouchDown = true;
                }
                foreach (var contact in _canvasGestureContacts.Values)
                {
                    if (!dec.Contains(contact.Id)) dec.Add(contact.Id);
                    inkCanvas.CaptureTouch(contact);
                }
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;
            }
            finally
            {
                _changingCanvasGestureMode = false;
                _changingCanvasGestureCapture = false;
            }

            var strokes = _canvasGestureSelectedStrokes.Count > 0 ? _canvasGestureSelectedStrokes : inkCanvas.Strokes;
            _canvasGestureStrokePoints = new Dictionary<Stroke, StylusPointCollection>();
            _canvasGestureDrawingAttributes = new Dictionary<Stroke, DrawingAttributes>();
            foreach (var stroke in strokes)
            {
                _canvasGestureStrokePoints[stroke] = stroke.StylusPoints.Clone();
                _canvasGestureDrawingAttributes[stroke] = stroke.DrawingAttributes.Clone();
            }
            _canvasGestureElementTransforms = new Dictionary<UIElement, Transform>();
            if (_canvasGestureSelectedStrokes.Count == 0)
                foreach (UIElement child in inkCanvas.Children)
                    if (child is Image || child is MediaElement || child is CanvasMediaControl)
                        _canvasGestureElementTransforms[child] = child.RenderTransform.CloneCurrentValue();
            RebaseCanvasGesture();
            e.Handled = true;
            return true;
        }

        private void RebaseCanvasGesture()
        {
            _canvasGestureFirstContactId = -1;
            _canvasGestureSecondContactId = -1;
            foreach (int id in _canvasGestureContacts.Keys)
            {
                if (_canvasGestureFirstContactId == -1) _canvasGestureFirstContactId = id;
                else { _canvasGestureSecondContactId = id; break; }
            }
            if (_canvasGestureSecondContactId != -1)
            {
                _canvasGestureFirstPoint = _canvasGestureContacts[_canvasGestureFirstContactId].GetTouchPoint(inkCanvas).Position;
                _canvasGestureSecondPoint = _canvasGestureContacts[_canvasGestureSecondContactId].GetTouchPoint(inkCanvas).Position;
            }
            _canvasGestureAppliedMatrix = Matrix.Identity;
            _canvasGestureAppliedScale = 1;
        }

        private bool TryHandleCanvasGestureTouchMove(TouchEventArgs e)
        {
            if (_canvasGestureCanceledContacts.Contains(e.TouchDevice.Id))
            {
                e.Handled = true;
                return true;
            }
            if (!_canvasGestureContacts.ContainsKey(e.TouchDevice.Id)) return false;
            if (!_canvasGestureOwnsTouchInput)
            {
                // Save the pre-move positions before taking over a pending palm-group pair.
                var first = _canvasGestureFirstPoint;
                var second = _canvasGestureSecondPoint;
                if (!TryStartCanvasGesture(e)) return false;
                _canvasGestureFirstPoint = first;
                _canvasGestureSecondPoint = second;
            }
            e.Handled = true;
            bool changedPage = _canvasGestureMode != currentMode || _canvasGesturePageIndex != GetCurrentFreezePageIndex();
            if (changedPage || !CanUseCanvasTwoFingerGesture())
            {
                ResetCanvasGestureInput(!changedPage && !IsCurrentPageFrozen);
                return true;
            }
            if (_canvasGestureContacts.Count < 2 || (e.TouchDevice.Id != _canvasGestureFirstContactId
                && e.TouchDevice.Id != _canvasGestureSecondContactId)) return true;

            var firstPoint = _canvasGestureContacts[_canvasGestureFirstContactId].GetTouchPoint(inkCanvas).Position;
            var secondPoint = _canvasGestureContacts[_canvasGestureSecondContactId].GetTouchPoint(inkCanvas).Position;
            var initialVector = _canvasGestureSecondPoint - _canvasGestureFirstPoint;
            var vector = secondPoint - firstPoint;
            var initialCenter = _canvasGestureFirstPoint + initialVector / 2;
            var center = firstPoint + vector / 2;
            if (!IsFiniteCanvasGesturePoint(initialCenter) || !IsFiniteCanvasGesturePoint(center)
                || !double.IsFinite(initialVector.Length) || !double.IsFinite(vector.Length)) return true;

            bool board = currentMode == 1;
            bool translate = board ? Settings.Gesture.IsEnableTwoFingerTranslateBoard : Settings.Gesture.IsEnableTwoFingerTranslate;
            bool rotate = board ? Settings.Gesture.IsEnableTwoFingerRotationBoard : Settings.Gesture.IsEnableTwoFingerRotation;
            bool zoom = board ? Settings.Gesture.IsEnableTwoFingerZoomBoard : Settings.Gesture.IsEnableTwoFingerZoom;
            var matrix = Matrix.Identity;
            if (translate) matrix.Translate(center.X - initialCenter.X, center.Y - initialCenter.Y);
            var pivot = matrix.Transform(initialCenter);
            bool hasSpan = initialVector.Length >= 1 && vector.Length >= 1;
            if (rotate && hasSpan) matrix.RotateAt(Vector.AngleBetween(initialVector, vector), pivot.X, pivot.Y);
            double scale = zoom && hasSpan ? vector.Length / initialVector.Length : 1;
            if (!double.IsFinite(scale) || scale <= 0) return true;
            if (zoom) matrix.ScaleAt(scale, scale, pivot.X, pivot.Y);
            if (!_canvasGestureAppliedMatrix.HasInverse || !IsFiniteCanvasGestureMatrix(matrix)) return true;

            // Derive frame deltas from the initial pair, rather than accumulating scale/rotation
            // from separately delivered finger events. This keeps the gesture pivot stable.
            var delta = _canvasGestureAppliedMatrix;
            delta.Invert();
            delta.Append(matrix);
            if (!IsFiniteCanvasGestureMatrix(delta) || delta.IsIdentity) return true;
            _isApplyingCanvasGesture = true;
            try
            {
                ApplyCanvasGestureTransformation(delta, new Vector(scale / _canvasGestureAppliedScale,
                    scale / _canvasGestureAppliedScale), scale != _canvasGestureAppliedScale, _canvasGestureSelectedStrokes);
            }
            finally { _isApplyingCanvasGesture = false; }
            _canvasGestureAppliedMatrix = matrix;
            _canvasGestureAppliedScale = scale;
            return true;
        }

        private static bool IsFiniteCanvasGesturePoint(Point point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y);

        private static bool IsFiniteCanvasGestureMatrix(Matrix matrix)
            => double.IsFinite(matrix.M11) && double.IsFinite(matrix.M12) && double.IsFinite(matrix.M21)
                && double.IsFinite(matrix.M22) && double.IsFinite(matrix.OffsetX) && double.IsFinite(matrix.OffsetY);

        private bool TryHandleCanvasGestureTouchUp(TouchEventArgs e)
        {
            if (_canvasGestureCanceledContacts.Remove(e.TouchDevice.Id))
            {
                e.Handled = true;
                return true;
            }
            if (!_canvasGestureContacts.Remove(e.TouchDevice.Id)) return false;
            if (!_canvasGestureOwnsTouchInput)
            {
                if (_canvasGestureContacts.Count == 0) _canvasGestureSavedMode = null;
                return false;
            }
            dec.Remove(e.TouchDevice.Id);
            _changingCanvasGestureCapture = true;
            try { inkCanvas.ReleaseTouchCapture(e.TouchDevice); }
            finally { _changingCanvasGestureCapture = false; }
            if (_canvasGestureContacts.Count == 0) ResetCanvasGestureInput();
            else if (e.TouchDevice.Id == _canvasGestureFirstContactId || e.TouchDevice.Id == _canvasGestureSecondContactId)
                RebaseCanvasGesture();
            e.Handled = true;
            return true;
        }

        private void AttachCanvasGestureInputEvents()
        {
            if (_canvasGestureInputEventsAttached) return;
            inkCanvas.LostTouchCapture += (sender, e) =>
            {
                if (_changingCanvasGestureCapture || e.TouchDevice.Captured == inkCanvas
                    || !_canvasGestureContacts.ContainsKey(e.TouchDevice.Id)) return;
                if (_canvasGestureOwnsTouchInput) ResetCanvasGestureInput();
                else _canvasGestureContacts.Remove(e.TouchDevice.Id);
            };
            inkCanvas.Unloaded += (sender, e) => ResetCanvasGestureInput(false);
            _canvasGestureInputEventsAttached = true;
        }

        private void ResetCanvasGestureInput(bool restoreMode = true)
        {
            if (_canvasGestureContacts == null) return;
            bool owned = _canvasGestureOwnsTouchInput;
            var savedMode = _canvasGestureSavedMode;
            if (owned)
            {
                try { CommitCanvasGestureHistory(); }
                catch (Exception ex) { LogHelper.WriteLogToFile($"Canvas gesture history commit failed: {ex}", LogHelper.LogType.Warning); }
            }
            _canvasGestureOwnsTouchInput = false;
            _changingCanvasGestureCapture = true;
            _changingCanvasGestureMode = true;
            try
            {
                var contacts = _canvasGestureContacts.Values.ToArray();
                _canvasGestureContacts.Clear();
                foreach (var contact in contacts)
                {
                    _canvasGestureCanceledContacts.Add(contact.Id);
                    dec.Remove(contact.Id);
                    inkCanvas.ReleaseTouchCapture(contact);
                }
                if (owned)
                {
                    inkCanvas.IsManipulationEnabled = _canvasGestureSavedManipulation;
                    if (restoreMode && savedMode.HasValue && inkCanvas.EditingMode == InkCanvasEditingMode.None
                        && !IsCurrentPageFrozen)
                        inkCanvas.EditingMode = savedMode.Value;
                    isTouchDown = false;
                    isMouseDown = false;
                    isMultiTouchTimerActive = false;
                    isWaitUntilNextTouchDown = false;
                    if (ViewboxFloatingBar != null) ViewboxFloatingBar.IsHitTestVisible = true;
                    if (BlackboardUIGridForInkReplay != null) BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                }
            }
            finally
            {
                _changingCanvasGestureCapture = false;
                _changingCanvasGestureMode = false;
                _canvasGestureSavedMode = null;
                _canvasGestureSelectedStrokes = null;
                _canvasGestureStrokePoints = null;
                _canvasGestureDrawingAttributes = null;
                _canvasGestureElementTransforms = null;
                _canvasGestureShapeStartStrokes = null;
                _canvasGestureFirstContactId = -1;
                _canvasGestureSecondContactId = -1;
            }
        }

        private void CommitCanvasGestureHistory()
        {
            if (_canvasGestureStrokePoints == null) return;
            var points = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            var attributes = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
            foreach (var item in _canvasGestureStrokePoints)
            {
                if (!inkCanvas.Strokes.Contains(item.Key)) continue;
                var after = item.Key.StylusPoints.Clone();
                var beforeAttributes = _canvasGestureDrawingAttributes[item.Key];
                var afterAttributes = item.Key.DrawingAttributes.Clone();
                bool attributesChanged = !beforeAttributes.Equals(afterAttributes);
                if (!item.Value.SequenceEqual(after) || attributesChanged)
                {
                    points[item.Key] = Tuple.Create(item.Value, after);
                    StrokeInitialHistory[item.Key] = after;
                }
                if (attributesChanged) attributes[item.Key] = Tuple.Create(beforeAttributes, afterAttributes);
            }
            var elements = new Dictionary<UIElement, Tuple<Transform, Transform>>();
            foreach (var item in _canvasGestureElementTransforms)
                if (inkCanvas.Children.Contains(item.Key) && item.Value.Value != item.Key.RenderTransform.Value)
                    elements[item.Key] = Tuple.Create(item.Value, item.Key.RenderTransform.CloneCurrentValue());
            if (points.Count > 0 || attributes.Count > 0 || elements.Count > 0)
                timeMachine.CommitStrokeManipulationHistory(points, attributes, elements);
        }
    }
}
