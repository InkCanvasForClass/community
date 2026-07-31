using Ink_Canvas.Helpers;
using Ink_Canvas.Mathematics.Models;
using Ink_Canvas.Mathematics.Persistence;
using Ink_Canvas.Mathematics.Rendering;
using Ink_Canvas.Mathematics.Services;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private enum MathInsertMode
        {
            None,
            Point,
            Segment,
            Line,
            Ray,
            Circle,
            Label,
            CoordinatePlane,
            Angle,
            Select,
            Delete,
            HorizontalConstraint,
            VerticalConstraint,
            EqualLengthConstraint,
            CollinearConstraint,
            PointOnLineConstraint,
            PointOnCircleConstraint,
            ParallelConstraint,
            PerpendicularConstraint,
            Function,
            EditFunction,
            Cube,
            Cuboid,
            Prism,
            Pyramid,
            Cylinder,
            Cone,
            Sphere,
            RotateSolid,
            Triangle
        }

        private MathInsertMode _mathInsertMode;
        private MathSnapResult? _mathDragStart;
        private Point? _mathLastDragPoint;
        private MathObject _selectedMathObject;
        private string _mathDragBeforeJson;
        private readonly List<MathSnapResult> _pendingMathPoints = new List<MathSnapResult>();
        private readonly List<MathObject> _pendingConstraintObjects = new List<MathObject>();
        private FunctionObject _functionBeingEdited;
        private string _functionEditBeforeJson;
        private TouchDevice _mathTouchDevice;
        private readonly MathStrokeRenderer _mathStrokeRenderer = new MathStrokeRenderer();
        private bool _mathTemporarilyEnabledFocus;
        private bool _mathRotatingSelection;
        private bool _mathScalingHorizontally;
        private bool _mathScalingVertically;
        private Rect _mathSelectionRect = Rect.Empty;
        private MathPoint? _mathPreviewEnd;
        private SolidType? _pendingSolidType;
        private double? _solidRotationRawX;
        private double? _solidRotationRawY;

        private void InitializeMathCanvas()
        {
            BoardMathInsertPopupContent.CoordinatePlaneButtonControl.Click += (_, _) => InsertCoordinatePlaneAtCanvasCenter();
            BoardMathInsertPopupContent.PointButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Point);
            BoardMathInsertPopupContent.SegmentButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Segment);
            BoardMathInsertPopupContent.TriangleButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Triangle);
            BoardMathInsertPopupContent.LineButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Line);
            BoardMathInsertPopupContent.RayButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Ray);
            BoardMathInsertPopupContent.CircleButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Circle);
            BoardMathInsertPopupContent.LabelButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Label);
            BoardMathInsertPopupContent.AngleButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Angle);
            BoardMathInsertPopupContent.SelectButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Select);
            BoardMathInsertPopupContent.DeleteButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.Delete);
            BoardMathInsertPopupContent.HorizontalButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.HorizontalConstraint);
            BoardMathInsertPopupContent.VerticalButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.VerticalConstraint);
            BoardMathInsertPopupContent.ParallelButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.ParallelConstraint);
            BoardMathInsertPopupContent.PerpendicularButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.PerpendicularConstraint);
            BoardMathInsertPopupContent.EqualLengthButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.EqualLengthConstraint);
            BoardMathInsertPopupContent.CollinearButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.CollinearConstraint);
            BoardMathInsertPopupContent.PointOnLineButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.PointOnLineConstraint);
            BoardMathInsertPopupContent.PointOnCircleButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.PointOnCircleConstraint);
            BoardMathInsertPopupContent.FunctionButtonControl.Click += (_, _) => BeginFunctionInsertOrApplyEdit();
            BoardMathInsertPopupContent.EditFunctionButtonControl.Click += (_, _) =>
            {
                CancelFunctionEdit();
                ActivateMathInsertMode(MathInsertMode.EditFunction);
            };
            BoardMathInsertPopupContent.CubeButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Cube);
            BoardMathInsertPopupContent.CuboidButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Cuboid);
            BoardMathInsertPopupContent.PrismButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Prism);
            BoardMathInsertPopupContent.PyramidButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Pyramid);
            BoardMathInsertPopupContent.CylinderButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Cylinder);
            BoardMathInsertPopupContent.ConeButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Cone);
            BoardMathInsertPopupContent.SphereButtonControl.Click += (_, _) => BeginSolidInsert(SolidType.Sphere);
            BoardMathInsertPopupContent.SolidInsertConfirmButtonControl.Click += (_, _) => ConfirmSolidInsert();
            BoardMathInsertPopupContent.SolidInsertCancelButtonControl.Click += (_, _) => CancelSolidInsert();
            BoardMathInsertPopupContent.RotateSolidButtonControl.Click += (_, _) => ActivateMathInsertMode(MathInsertMode.RotateSolid);
            BoardMathInsertPopupContent.CloseButtonControl.Click += (_, _) =>
            {
                BoardMathInsertPopup.IsOpen = false;
                CancelFunctionEdit();
            };
            BoardMathInsertPopup.Closed += (_, _) =>
            {
                CancelSolidInsert();
                EndMathTextInput();
                CancelFunctionEdit();
                UpdateMathToolbarVisual();
            };
            MathObjectEditButton.Content = Strings.GetString("Math_EditObject") ?? "编辑";
            MathObjectMeasureButton.Content = Strings.GetString("Math_MeasureObject") ?? "测量";
            MathObjectCircumsphereButton.Content = Strings.GetString("Math_Circumsphere") ?? "外接球";
            MathObjectInsphereButton.Content = Strings.GetString("Math_Insphere") ?? "内切球";
            MathObjectCircumcircleButton.Content = Strings.GetString("Math_Circumcircle") ?? "外接圆";
            MathObjectIncircleButton.Content = Strings.GetString("Math_Incircle") ?? "内切圆";
            MathObjectResetViewButton.Content = Strings.GetString("Math_ResetView") ?? "重置视图";
            MathObjectDeleteButton.Content = Strings.GetString("Math_Delete") ?? "删除";
            MathObjectEditButton.Click += (_, _) => EditSelectedMathObject();
            MathObjectMeasureButton.Click += (_, _) => MeasureSelectedMathObject();
            MathObjectCircumsphereButton.Click += (_, _) => AddSelectedSolidSphere(false);
            MathObjectInsphereButton.Click += (_, _) => AddSelectedSolidSphere(true);
            MathObjectCircumcircleButton.Click += (_, _) => AddSelectedTriangleCircle(TriangleCircleKind.Circumcircle);
            MathObjectIncircleButton.Click += (_, _) => AddSelectedTriangleCircle(TriangleCircleKind.Incircle);
            MathObjectResetViewButton.Click += (_, _) => ResetSelectedMathObjectView();
            MathObjectDeleteButton.Click += (_, _) => DeleteSelectedMathObject();
            InkCanvasGridForInkReplay.PreviewMouseLeftButtonDown += MathCanvas_MouseLeftButtonDown;
            InkCanvasGridForInkReplay.PreviewMouseMove += MathCanvas_MouseMove;
            InkCanvasGridForInkReplay.PreviewMouseLeftButtonUp += MathCanvas_MouseLeftButtonUp;
            InkCanvasGridForInkReplay.PreviewMouseWheel += MathCanvas_MouseWheel;
            InkCanvasGridForInkReplay.PreviewTouchDown += MathCanvas_PreviewTouchDown;
            InkCanvasGridForInkReplay.PreviewTouchMove += MathCanvas_PreviewTouchMove;
            InkCanvasGridForInkReplay.PreviewTouchUp += MathCanvas_PreviewTouchUp;
            PreviewKeyDown += MathCanvas_PreviewKeyDown;
            InkCanvasGridForInkReplay.PreviewMouseRightButtonDown += (_, e) =>
            {
                if (_mathInsertMode == MathInsertMode.None) return;
                CancelMathInsertMode();
                e.Handled = true;
            };
            ApplyMathSettings();
        }

        private void MathCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_mathInsertMode == MathInsertMode.None) return;
            if (e.Key == Key.Escape)
            {
                CancelMathInsertMode();
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Back) return;
            if (_pendingMathPoints.Count > 0)
                _pendingMathPoints.RemoveAt(_pendingMathPoints.Count - 1);
            else if (_pendingConstraintObjects.Count > 0)
                _pendingConstraintObjects.RemoveAt(_pendingConstraintObjects.Count - 1);
            else
                return;
            UpdateMathToolStatus();
            e.Handled = true;
        }

        private void OpenMathInsertPopup()
        {
            if (!Settings.Canvas.EnableMathCanvas)
            {
                ShowNotification(Strings.GetString("Math_Disabled") ?? "请先启用数学画布");
                return;
            }
            if (currentMode != 1)
            {
                ShowNotification(Strings.GetString("Math_BoardModeOnly") ?? "数学工具仅在白板模式可用");
                return;
            }
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;

            HideSubPanelsImmediately();
            BoardMathInsertPopup.IsOpen = true;
            _popupManager?.BringToFront(BoardMathInsertPopup);
            BeginMathTextInput();
            UpdateMathToolbarVisual();
        }

        private void BeginMathTextInput()
        {
            EnableMathPopupInteraction();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                BoardMathInsertPopupContent.FunctionExpressionInput.Focus();
                Keyboard.Focus(BoardMathInsertPopupContent.FunctionExpressionInput);
            }), DispatcherPriority.Input);
        }

        private void BeginMathObjectActionsInput()
        {
            EnableMathPopupInteraction();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                var button = MathObjectEditButton.IsEnabled
                    ? MathObjectEditButton
                    : MathObjectMeasureButton;
                button.Focus();
                Keyboard.Focus(button);
            }), DispatcherPriority.Input);
        }

        private void EnableMathPopupInteraction()
        {
            if (!Settings.Advanced.IsNoFocusMode || _mathTemporarilyEnabledFocus) return;
            _mathTemporarilyEnabledFocus = true;
            WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = true;
            ApplyNoFocusMode();
        }

        private void EndMathTextInput()
        {
            if (!_mathTemporarilyEnabledFocus) return;
            _mathTemporarilyEnabledFocus = false;
            WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = false;
            ApplyNoFocusMode();
        }

        private void ActivateMathInsertMode(MathInsertMode mode)
        {
            if (mode != MathInsertMode.EditFunction)
                CancelFunctionEdit();
            BoardMathInsertPopup.IsOpen = false;
            _mathInsertMode = mode;
            _mathDragStart = null;
            _mathLastDragPoint = null;
            _selectedMathObject = null;
            _mathDragBeforeJson = null;
            _pendingMathPoints.Clear();
            _pendingConstraintObjects.Clear();
            EndSolidRotation();
            MathCanvas.IsHitTestVisible = true;
            MathCanvas.Cursor = Cursors.Cross;
            InkCanvasGridForInkReplay.Cursor = Cursors.Cross;
            UpdateMathToolStatus();
            UpdateMathToolbarVisual();
            ClearMathPreview();
        }

        private void CancelMathInsertMode()
        {
            _mathInsertMode = MathInsertMode.None;
            _mathDragStart = null;
            _mathLastDragPoint = null;
            _selectedMathObject = null;
            _mathDragBeforeJson = null;
            _pendingMathPoints.Clear();
            _pendingConstraintObjects.Clear();
            if (InkCanvasGridForInkReplay.IsMouseCaptured)
                InkCanvasGridForInkReplay.ReleaseMouseCapture();
            InkCanvasGridForInkReplay.ReleaseAllTouchCaptures();
            _mathTouchDevice = null;
            _mathRotatingSelection = false;
            _mathScalingHorizontally = false;
            _mathScalingVertically = false;
            _mathPreviewEnd = null;
            EndSolidRotation();
            MathCanvas.IsHitTestVisible = false;
            MathCanvas.Cursor = Cursors.Arrow;
            InkCanvasGridForInkReplay.Cursor = Cursors.Arrow;
            MathToolStatusBorder.Visibility = Visibility.Collapsed;
            UpdateMathToolbarVisual();
            ClearMathPreview();
            ClearMathSelection();
        }

        private void MathCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup))
                return;
            if (!HandleMathPointerDown(e.GetPosition(InkCanvasGridForInkReplay))) return;

            if (NeedsMathPointerCapture())
                InkCanvasGridForInkReplay.CaptureMouse();
            e.Handled = true;
        }

        private bool HandleMathPointerDown(Point position)
        {
            if (_mathInsertMode == MathInsertMode.None) return false;
            if (IsPointerInsideOpenMathActionsPopup(position)) return true;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math"))
            {
                CancelMathInsertMode();
                return true;
            }

            if (_mathInsertMode == MathInsertMode.Select)
            {
                if (_selectedMathObject is SolidObject or FunctionObject &&
                    IsNearSelectionHandle(position, true))
                {
                    _mathRotatingSelection = true;
                    _mathDragBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                    _mathLastDragPoint = position;
                    if (_selectedMathObject is SolidObject selectedSolid)
                        BeginSolidRotation(selectedSolid);
                    CloseMathObjectActionsPopup();
                    return true;
                }
                if (_selectedMathObject is SolidObject or FunctionObject or CoordinatePlaneObject or CircleObject &&
                    IsNearSelectionScaleHandle(position, false))
                {
                    _mathScalingHorizontally = true;
                    _mathDragBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                    _mathLastDragPoint = position;
                    CloseMathObjectActionsPopup();
                    return true;
                }
                if (_selectedMathObject is SolidObject or FunctionObject or CoordinatePlaneObject or CircleObject &&
                    IsNearSelectionScaleHandle(position, true))
                {
                    _mathScalingVertically = true;
                    _mathDragBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                    _mathLastDragPoint = position;
                    CloseMathObjectActionsPopup();
                    return true;
                }
                var hitMathObject = MathGeometryService.HitTest(
                    MathCanvas.Scene,
                    ToMathPoint(position),
                    12);
                if (hitMathObject == null)
                {
                    ClearMathSelection();
                    CancelMathInsertMode();
                    PenIcon_Click(null, null);
                    return true;
                }

                _selectedMathObject = hitMathObject;
                if (_selectedMathObject.IsLocked)
                {
                    ClearMathSelection();
                    return true;
                }
                _mathDragBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                _mathLastDragPoint = position;
                UpdateMathSelectionOverlay();
            }
            else if (_mathInsertMode == MathInsertMode.Delete)
            {
                var mathObject = MathGeometryService.HitTest(
                    MathCanvas.Scene,
                    ToMathPoint(position),
                    12);
                if (mathObject != null)
                {
                    var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                    new MathSceneService(MathCanvas.Scene).Remove(mathObject.Id);
                    CommitMathSceneChange(beforeJson);
                }
                ActivateMathInsertMode(MathInsertMode.Select);
            }
            else if (_mathInsertMode == MathInsertMode.Angle)
            {
                _pendingMathPoints.Add(GetMathInput(position));
                UpdateMathToolStatus();
                if (_pendingMathPoints.Count == 3)
                {
                    var angle = new AngleMeasurementObject
                    {
                        Vertex = _pendingMathPoints[0].Position,
                        First = _pendingMathPoints[1].Position,
                        Second = _pendingMathPoints[2].Position,
                        VertexPointId = _pendingMathPoints[0].PointObjectId,
                        FirstPointId = _pendingMathPoints[1].PointObjectId,
                        SecondPointId = _pendingMathPoints[2].PointObjectId
                    };
                    SolidAttachmentService.TryAttach(
                        angle,
                        _pendingMathPoints[0],
                        _pendingMathPoints[1],
                        _pendingMathPoints[2]);
                    AddMathObject(angle);
                    ActivateMathInsertMode(MathInsertMode.Select);
                }
            }
            else if (_mathInsertMode == MathInsertMode.Triangle)
            {
                _pendingMathPoints.Add(GetMathInput(position));
                UpdateMathToolStatus();
                if (_pendingMathPoints.Count == 3)
                {
                    var triangle = new TriangleObject
                    {
                        First = _pendingMathPoints[0].Position,
                        Second = _pendingMathPoints[1].Position,
                        Third = _pendingMathPoints[2].Position,
                        FirstPointId = _pendingMathPoints[0].PointObjectId,
                        SecondPointId = _pendingMathPoints[1].PointObjectId,
                        ThirdPointId = _pendingMathPoints[2].PointObjectId
                    };
                    SolidAttachmentService.TryAttach(
                        triangle,
                        _pendingMathPoints[0],
                        _pendingMathPoints[1],
                        _pendingMathPoints[2]);
                    AddMathObject(triangle);
                    ActivateMathInsertMode(MathInsertMode.Select);
                }
            }
            else if (_mathInsertMode == MathInsertMode.Function)
            {
                if (TryCreateFunction(position, out var function))
                    AddMathObject(function);
                ActivateMathInsertMode(MathInsertMode.Select);
            }
            else if (_mathInsertMode == MathInsertMode.EditFunction)
            {
                BeginFunctionEdit(position);
            }
            else if (TryGetSolidType(_mathInsertMode, out var solidType))
            {
                AddMathObject(CreateSolid(solidType, position));
                ActivateMathInsertMode(MathInsertMode.Select);
            }
            else if (_mathInsertMode == MathInsertMode.RotateSolid)
            {
                _selectedMathObject = MathGeometryService.HitTest(
                    MathCanvas.Scene,
                    ToMathPoint(position),
                    12) as SolidObject;
                if (_selectedMathObject != null && !_selectedMathObject.IsLocked)
                {
                    _mathDragBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
                    _mathLastDragPoint = position;
                    BeginSolidRotation((SolidObject)_selectedMathObject);
                }
                else
                {
                    ShowNotification(Strings.GetString("Math_SelectSolid") ?? "Math_SelectSolid");
                }
            }
            else if (IsConstraintMode(_mathInsertMode))
            {
                SelectConstraintObject(position);
            }
            else if (_mathInsertMode == MathInsertMode.Point)
            {
                var input = GetMathInput(position);
                var point = new PointObject { Position = input.Position };
                SolidAttachmentService.TryAttach(point, input);
                AddMathObject(point);
                ActivateMathInsertMode(MathInsertMode.Select);
            }
            else if (_mathInsertMode == MathInsertMode.Label)
            {
                var input = GetMathInput(position);
                var label = new TextLabelObject
                {
                    Position = input.Position,
                    Text = GetNextMathLabel()
                };
                SolidAttachmentService.TryAttach(label, input);
                AddMathObject(label);
                ActivateMathInsertMode(MathInsertMode.Select);
            }
            else
            {
                _mathDragStart = GetMathInput(position);
            }

            return true;
        }

        private bool IsPointerInsideOpenMathActionsPopup(Point canvasPoint)
        {
            if (MathObjectActionsPopup?.Visibility != Visibility.Visible ||
                MathObjectActionsPopup.ActualWidth <= 0 ||
                MathObjectActionsPopup.ActualHeight <= 0)
                return false;

            try
            {
                var screenPoint = InkCanvasGridForInkReplay.PointToScreen(canvasPoint);
                var popupPoint = MathObjectActionsPopup.PointFromScreen(screenPoint);
                return new Rect(
                    0,
                    0,
                    MathObjectActionsPopup.ActualWidth,
                    MathObjectActionsPopup.ActualHeight).Contains(popupPoint);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void MathCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (HandleMathPointerMove(
                    e.GetPosition(InkCanvasGridForInkReplay),
                    e.LeftButton == MouseButtonState.Pressed))
                e.Handled = true;
        }

        private bool HandleMathPointerMove(Point current, bool isPressed)
        {
            if (_mathInsertMode == MathInsertMode.Select &&
                _selectedMathObject != null &&
                _mathLastDragPoint.HasValue &&
                isPressed)
            {
                var delta = current - _mathLastDragPoint.Value;
                if (_mathRotatingSelection)
                    RotateMathObject(_selectedMathObject, delta);
                else if (_mathScalingHorizontally || _mathScalingVertically)
                    ScaleMathObject(
                        _selectedMathObject,
                        delta,
                        _mathScalingHorizontally,
                        _mathScalingVertically);
                else
                    MathReferenceService.Translate(
                        MathCanvas.Scene,
                        _selectedMathObject,
                        delta.X,
                        delta.Y);
                _mathLastDragPoint = current;
                RefreshMathScene();
                return true;
            }

            if (_mathInsertMode == MathInsertMode.RotateSolid &&
                _selectedMathObject is SolidObject solid &&
                _mathLastDragPoint.HasValue &&
                isPressed)
            {
                var delta = current - _mathLastDragPoint.Value;
                RotateSolidBy(solid, delta);
                _mathLastDragPoint = current;
                RefreshMathScene();
                return true;
            }

            if (_mathDragStart.HasValue && !isPressed)
            {
                CancelMathInsertMode();
                return true;
            }

            if (_mathDragStart.HasValue && isPressed)
            {
                _mathPreviewEnd = GetMathInput(current).Position;
                RefreshMathConstructionPreview();
            }

            return _mathDragStart.HasValue && isPressed;
        }

        private void MathCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup))
                return;
            if (HandleMathPointerUp(e.GetPosition(InkCanvasGridForInkReplay)))
                e.Handled = true;
        }

        private bool HandleMathPointerUp(Point position)
        {
            if (_mathInsertMode == MathInsertMode.Select && _selectedMathObject != null)
            {
                if (!string.IsNullOrWhiteSpace(_mathDragBeforeJson))
                {
                    if (Settings.Canvas.MathConstraintsEnabled &&
                        !MathConstraintService.TryApplyAll(MathCanvas.Scene, out _))
                    {
                        RestoreMathScene(_mathDragBeforeJson);
                        ShowNotification(Strings.GetString("Math_ConstraintConflict") ?? "Math_ConstraintConflict");
                    }
                    else
                    {
                        CommitMathSceneChange(_mathDragBeforeJson);
                    }
                }

                _mathLastDragPoint = null;
                _mathDragBeforeJson = null;
                _mathRotatingSelection = false;
                _mathScalingHorizontally = false;
                _mathScalingVertically = false;
                EndSolidRotation();
                if (InkCanvasGridForInkReplay.IsMouseCaptured)
                    InkCanvasGridForInkReplay.ReleaseMouseCapture();
                UpdateMathSelectionOverlay();
                OpenMathObjectActionsPopup();
                return true;
            }

            if (_mathInsertMode == MathInsertMode.RotateSolid && _selectedMathObject is SolidObject)
            {
                if (!string.IsNullOrWhiteSpace(_mathDragBeforeJson))
                    CommitMathSceneChange(_mathDragBeforeJson);
                _mathLastDragPoint = null;
                _selectedMathObject = null;
                _mathDragBeforeJson = null;
                EndSolidRotation();
                if (InkCanvasGridForInkReplay.IsMouseCaptured)
                    InkCanvasGridForInkReplay.ReleaseMouseCapture();
                return true;
            }

            if (!_mathDragStart.HasValue) return false;

            var startResult = _mathDragStart.Value;
            var endResult = GetMathInput(position);
            var start = ToPoint(startResult.Position);
            var end = ToPoint(endResult.Position);
            var delta = end - start;
            var distance = delta.Length;
            var objectCountBefore = MathCanvas.Scene.Objects.Count;

            if (_mathInsertMode == MathInsertMode.Segment && distance >= 2)
            {
                var segment = new SegmentObject
                {
                    Start = ToMathPoint(start),
                    End = ToMathPoint(end),
                    StartPointId = startResult.PointObjectId,
                    EndPointId = endResult.PointObjectId
                };
                SolidAttachmentService.TryAttach(segment, startResult, endResult);
                AddMathObject(segment);
            }
            else if (_mathInsertMode == MathInsertMode.Line && distance >= 2)
            {
                var line = new LineObject
                {
                    Start = ToMathPoint(start),
                    End = ToMathPoint(end),
                    StartPointId = startResult.PointObjectId,
                    EndPointId = endResult.PointObjectId
                };
                SolidAttachmentService.TryAttach(line, startResult, endResult);
                AddMathObject(line);
            }
            else if (_mathInsertMode == MathInsertMode.Ray && distance >= 2)
            {
                var ray = new RayObject
                {
                    Start = ToMathPoint(start),
                    Through = ToMathPoint(end),
                    StartPointId = startResult.PointObjectId,
                    ThroughPointId = endResult.PointObjectId
                };
                SolidAttachmentService.TryAttach(ray, startResult, endResult);
                AddMathObject(ray);
            }
            else if (_mathInsertMode == MathInsertMode.Circle && distance >= 2)
            {
                var circle = new CircleObject
                {
                    Center = ToMathPoint(start),
                    Radius = distance,
                    CenterPointId = startResult.PointObjectId,
                    RadiusPointId = endResult.PointObjectId
                };
                SolidAttachmentService.TryAttach(circle, startResult, endResult);
                AddMathObject(circle);
            }
            else if (_mathInsertMode == MathInsertMode.CoordinatePlane)
            {
                AddMathObject(MathPlacementService.CreateCoordinatePlane(
                    ToMathPoint(start),
                    ToMathPoint(end),
                    Settings.Canvas.MathGridSpacing,
                    Settings.Canvas.MathShowGrid,
                    Settings.Canvas.MathShowAxes));
            }

            if (MathCanvas.Scene.Objects.Count > objectCountBefore)
                ActivateMathInsertMode(MathInsertMode.Select);
            else
            {
                _mathDragStart = null;
                _mathPreviewEnd = null;
                ClearMathPreview();
                ShowNotification(Strings.GetString("Math_ConstructionTooSmall") ?? "请拖动更长距离");
            }
            return true;
        }

        private void MathCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (_mathInsertMode == MathInsertMode.None) return;
            if (IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup))
                return;
            if (_mathTouchDevice != null && !ReferenceEquals(_mathTouchDevice, e.TouchDevice))
            {
                e.Handled = true;
                return;
            }

            var touchDevice = e.TouchDevice;
            if (!HandleMathPointerDown(e.GetTouchPoint(InkCanvasGridForInkReplay).Position)) return;

            _mathTouchDevice = touchDevice;
            if (NeedsMathPointerCapture())
                touchDevice.Capture(InkCanvasGridForInkReplay);
            e.Handled = true;
        }

        private void MathCanvas_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup))
                return;
            if (!ReferenceEquals(_mathTouchDevice, e.TouchDevice)) return;

            HandleMathPointerMove(
                e.GetTouchPoint(InkCanvasGridForInkReplay).Position,
                true);
            e.Handled = true;
        }

        private void MathCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            if (IsDescendantOf(e.OriginalSource as DependencyObject, MathObjectActionsPopup))
                return;
            if (!ReferenceEquals(_mathTouchDevice, e.TouchDevice)) return;

            HandleMathPointerUp(e.GetTouchPoint(InkCanvasGridForInkReplay).Position);
            e.TouchDevice.Capture(null);
            _mathTouchDevice = null;
            e.Handled = true;
        }

        private bool NeedsMathPointerCapture()
        {
            return _mathDragStart.HasValue ||
                   (_selectedMathObject != null && _mathLastDragPoint.HasValue);
        }

        private void AddMathObject(MathObject mathObject)
        {
            AddMathObjects(new[] { mathObject });
        }

        private void AddMathObjects(IReadOnlyList<MathObject> mathObjects)
        {
            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            var service = new MathSceneService(MathCanvas.Scene);
            for (var i = 0; i < mathObjects.Count; i++)
            {
                var mathObject = mathObjects[i];
                mathObject.StrokeWidth = Settings.Canvas.MathDefaultStrokeWidth;
                mathObject.StrokeColor = GetMathStrokeColor();
                service.Add(mathObject);
            }
            CommitMathSceneChange(beforeJson);
            LogHelper.WriteLogToFile(
                $"Math objects inserted: added={mathObjects.Count}, total={MathCanvas.Scene.Objects.Count}",
                LogHelper.LogType.Event);
        }

        private void InsertCoordinatePlaneAtCanvasCenter()
        {
            BoardMathInsertPopup.IsOpen = false;
            var center = GetMathCanvasCenter();
            AddMathObject(MathPlacementService.CreateCoordinatePlane(
                ToMathPoint(center),
                ToMathPoint(center),
                Settings.Canvas.MathGridSpacing,
                Settings.Canvas.MathShowGrid,
                Settings.Canvas.MathShowAxes));
            ActivateMathInsertMode(MathInsertMode.Select);
        }

        private void InsertSolidAtCanvasCenter(SolidType solidType)
        {
            if (!TryReadSolidDimensions(solidType, out var first, out var second, out var third))
            {
                ShowNotification(Strings.GetString("Math_SolidDimensionsInvalid") ?? "请输入有效的立体尺寸");
                return;
            }
            BoardMathInsertPopup.IsOpen = false;
            AddMathObject(CreateSolid(solidType, GetMathCanvasCenter(), first, second, third));
            ActivateMathInsertMode(MathInsertMode.Select);
        }

        private void BeginSolidInsert(SolidType solidType)
        {
            _pendingSolidType = solidType;
            BoardMathInsertPopupContent.ShowSolidDimensions(solidType);
            EnableMathPopupInteraction();
        }

        private void ConfirmSolidInsert()
        {
            if (_pendingSolidType.HasValue)
                InsertSolidAtCanvasCenter(_pendingSolidType.Value);
        }

        private void CancelSolidInsert()
        {
            _pendingSolidType = null;
            BoardMathInsertPopupContent.HideSolidDimensions();
        }

        private bool TryReadSolidDimensions(
            SolidType solidType,
            out double first,
            out double second,
            out double third)
        {
            first = 0;
            second = 1;
            third = 1;
            if (!TryReadSolidDimension(BoardMathInsertPopupContent.SolidLengthInput.Text, out first))
                return false;
            if (solidType is SolidType.Cube or SolidType.Sphere)
                return true;
            if (!TryReadSolidDimension(BoardMathInsertPopupContent.SolidWidthInput.Text, out second))
                return false;
            if (solidType is SolidType.Cylinder or SolidType.Cone)
                return true;
            return TryReadSolidDimension(BoardMathInsertPopupContent.SolidHeightInput.Text, out third);
        }

        private static bool TryReadSolidDimension(string value, out double result)
        {
            return TryParseFiniteDouble(value, out result) && result > 0 && result <= 1000;
        }

        private Point GetMathCanvasCenter()
        {
            var width = MathCanvas.ActualWidth;
            var height = MathCanvas.ActualHeight;
            if (width <= 0) width = InkCanvasGridForInkReplay.ActualWidth;
            if (height <= 0) height = InkCanvasGridForInkReplay.ActualHeight;
            return new Point(
                width > 0 ? width / 2 : 960,
                height > 0 ? height / 2 : 540);
        }

        private string GetNextMathLabel()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < MathCanvas.Scene.Objects.Count; i++)
            {
                if (MathCanvas.Scene.Objects[i] is TextLabelObject label &&
                    !string.IsNullOrWhiteSpace(label.Text))
                    used.Add(label.Text.Trim());
            }
            for (var i = 0; i < 26; i++)
            {
                var candidate = ((char)('A' + i)).ToString();
                if (!used.Contains(candidate)) return candidate;
            }
            return $"A{used.Count + 1}";
        }

        private bool HasCoordinatePlaneAt(Point position)
        {
            return FindCoordinatePlaneAt(position) != null;
        }

        private CoordinatePlaneObject FindCoordinatePlaneAt(Point position)
        {
            for (var i = 0; i < MathCanvas.Scene.Objects.Count; i++)
            {
                if (MathCanvas.Scene.Objects[i] is not CoordinatePlaneObject coordinatePlane)
                    continue;
                if (Math.Abs(position.X - coordinatePlane.Center.X) <= coordinatePlane.Width / 2 &&
                    Math.Abs(position.Y - coordinatePlane.Center.Y) <= coordinatePlane.Height / 2)
                    return coordinatePlane;
            }
            return null;
        }

        private string GetMathStrokeColor()
        {
            if (GridBackgroundCover.Visibility == Visibility.Visible &&
                GridBackgroundCover.Background is SolidColorBrush background)
            {
                return MathAppearanceService.GetContrastingStrokeColor(
                    background.Color.R,
                    background.Color.G,
                    background.Color.B);
            }

            if (CustomBackgroundColor.HasValue)
            {
                var color = CustomBackgroundColor.Value;
                return MathAppearanceService.GetContrastingStrokeColor(
                    color.R,
                    color.G,
                    color.B);
            }

            return Settings.Canvas.UsingWhiteboard
                ? MathAppearanceService.DarkStrokeColor
                : MathAppearanceService.LightStrokeColor;
        }

        private void BeginFunctionInsertOrApplyEdit()
        {
            if (_functionBeingEdited == null)
            {
                if (!TryReadFunctionInputs(out _, out _, out _))
                {
                    ShowNotification(Strings.GetString("Math_FunctionInvalid") ?? "Math_FunctionInvalid");
                    return;
                }

                var center = GetMathCanvasCenter();
                if (!TryCreateFunction(center, out var function)) return;

                var objects = new List<MathObject>();
                var coordinatePlane = FindCoordinatePlaneAt(center);
                if (coordinatePlane == null)
                {
                    coordinatePlane = MathPlacementService.CreateCoordinatePlane(
                        new MathPoint(center.X - 360, center.Y - 240),
                        new MathPoint(center.X + 360, center.Y + 240),
                        Settings.Canvas.MathGridSpacing,
                        Settings.Canvas.MathShowGrid,
                        Settings.Canvas.MathShowAxes);
                    objects.Add(coordinatePlane);
                }
                function.CoordinatePlaneId = coordinatePlane.Id;
                objects.Add(function);
                BoardMathInsertPopup.IsOpen = false;
                AddMathObjects(objects);
                ActivateMathInsertMode(MathInsertMode.Select);
                return;
            }

            if (!TryReadFunctionInputs(out var expression, out var domainMin, out var domainMax))
            {
                ShowNotification(Strings.GetString("Math_FunctionInvalid") ?? "Math_FunctionInvalid");
                return;
            }

            var previousExpression = _functionBeingEdited.Expression;
            var previousMin = _functionBeingEdited.DomainMin;
            var previousMax = _functionBeingEdited.DomainMax;
            try
            {
                _functionBeingEdited.Expression = expression;
                _functionBeingEdited.DomainMin = domainMin;
                _functionBeingEdited.DomainMax = domainMax;
                new MathSceneService(MathCanvas.Scene).Validate(_functionBeingEdited);
                CommitMathSceneChange(_functionEditBeforeJson);
                ShowNotification(Strings.GetString("Math_FunctionUpdated") ?? "Math_FunctionUpdated");
                BoardMathInsertPopup.IsOpen = false;
                CancelFunctionEdit();
            }
            catch (ArgumentException)
            {
                _functionBeingEdited.Expression = previousExpression;
                _functionBeingEdited.DomainMin = previousMin;
                _functionBeingEdited.DomainMax = previousMax;
                ShowNotification(Strings.GetString("Math_FunctionInvalid") ?? "Math_FunctionInvalid");
            }
        }

        private void BeginFunctionEdit(Point position)
        {
            var function = MathGeometryService.HitTest(
                MathCanvas.Scene,
                ToMathPoint(position),
                12) as FunctionObject;
            if (function == null)
            {
                ShowNotification(Strings.GetString("Math_SelectFunction") ?? "Math_SelectFunction");
                return;
            }

            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            ActivateMathInsertMode(MathInsertMode.Select);
            _functionBeingEdited = function;
            _functionEditBeforeJson = beforeJson;
            BoardMathInsertPopupContent.FunctionExpressionInput.Text = function.Expression;
            BoardMathInsertPopupContent.FunctionDomainMinInput.Text =
                function.DomainMin.ToString(CultureInfo.InvariantCulture);
            BoardMathInsertPopupContent.FunctionDomainMaxInput.Text =
                function.DomainMax.ToString(CultureInfo.InvariantCulture);
            BoardMathInsertPopup.IsOpen = true;
            _popupManager?.BringToFront(BoardMathInsertPopup);
            BeginMathTextInput();
        }

        private bool TryCreateFunction(Point origin, out FunctionObject function)
        {
            function = null;
            if (!TryReadFunctionInputs(out var expression, out var domainMin, out var domainMax))
            {
                ShowNotification(Strings.GetString("Math_FunctionInvalid") ?? "Math_FunctionInvalid");
                return false;
            }

            function = new FunctionObject
            {
                Expression = expression,
                DomainMin = domainMin,
                DomainMax = domainMax,
                Origin = ToMathPoint(origin),
                PixelsPerUnit = Settings.Canvas.MathFunctionPixelsPerUnit,
                SampleQuality = Settings.Canvas.MathFunctionSampleQuality,
                ShowZeros = Settings.Canvas.MathFunctionShowMarkers,
                ShowExtrema = Settings.Canvas.MathFunctionShowMarkers,
                ShowIntersections = Settings.Canvas.MathFunctionShowMarkers
            };
            try
            {
                new MathSceneService(MathCanvas.Scene).Validate(function);
                return true;
            }
            catch (ArgumentException)
            {
                function = null;
                ShowNotification(Strings.GetString("Math_FunctionInvalid") ?? "Math_FunctionInvalid");
                return false;
            }
        }

        private bool TryReadFunctionInputs(
            out string expression,
            out double domainMin,
            out double domainMax)
        {
            expression = BoardMathInsertPopupContent.FunctionExpressionInput.Text?.Trim();
            domainMin = 0;
            domainMax = 0;
            var minimumText = BoardMathInsertPopupContent.FunctionDomainMinInput.Text;
            var maximumText = BoardMathInsertPopupContent.FunctionDomainMaxInput.Text;
            return !string.IsNullOrWhiteSpace(expression) &&
                   TryParseFiniteDouble(minimumText, out domainMin) &&
                   TryParseFiniteDouble(maximumText, out domainMax) &&
                   domainMin < domainMax &&
                   TryParseFunction(expression);
        }

        private static bool TryParseFunction(string expression)
        {
            try
            {
                MathExpressionParser.Parse(expression);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryParseFiniteDouble(string value, out double result)
        {
            return (double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out result) ||
                    double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out result)) &&
                   double.IsFinite(result);
        }

        private void CancelFunctionEdit()
        {
            _functionBeingEdited = null;
            _functionEditBeforeJson = null;
        }

        private void MathCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_mathInsertMode != MathInsertMode.Select) return;
            var mathObject = MathGeometryService.HitTest(
                MathCanvas.Scene,
                ToMathPoint(e.GetPosition(InkCanvasGridForInkReplay)),
                12);
            if (mathObject is not FunctionObject &&
                mathObject is not SolidObject &&
                mathObject is not CoordinatePlaneObject)
                return;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math"))
            {
                e.Handled = true;
                return;
            }

            var reopenActions = MathObjectActionsPopup.Visibility == Visibility.Visible &&
                                _selectedMathObject?.Id == mathObject.Id;
            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            var rotate = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (mathObject is FunctionObject function)
            {
                if (rotate)
                    function.RotationDegrees = NormalizeMathAngle(
                        function.RotationDegrees + (e.Delta > 0 ? 5 : -5));
                else
                    function.PixelsPerUnit = Math.Max(5, Math.Min(200, function.PixelsPerUnit * factor));
            }
            else if (mathObject is SolidObject solid)
            {
                if (rotate)
                    solid.RotationZ = SolidRotationSnapService.SnapToRightAngle(
                        solid.RotationZ + (e.Delta > 0 ? 5 : -5));
                else
                    solid.Scale = Math.Max(5, Math.Min(200, solid.Scale * factor));
            }
            else if (mathObject is CoordinatePlaneObject coordinatePlane)
            {
                coordinatePlane.Width = Math.Max(80, Math.Min(4000, coordinatePlane.Width * factor));
                coordinatePlane.Height = Math.Max(80, Math.Min(4000, coordinatePlane.Height * factor));
                coordinatePlane.GridSpacing = Math.Max(8, Math.Min(200, coordinatePlane.GridSpacing * factor));
            }
            CommitMathSceneChange(beforeJson);
            if (reopenActions) OpenMathObjectActionsPopup();
            e.Handled = true;
        }

        private static double NormalizeMathAngle(double angle)
        {
            angle %= 360;
            return angle < 0 ? angle + 360 : angle;
        }

        private SolidObject CreateSolid(
            SolidType solidType,
            Point center,
            double first = 3,
            double second = 3,
            double third = 3)
        {
            var solid = new SolidObject
            {
                SolidType = solidType,
                Center = ToMathPoint(center),
                ViewMode = GetMathSolidDefaultViewMode(),
                ProjectionMode = SolidProjectionMode.Orthographic,
                ShowHiddenEdges = Settings.Canvas.MathSolidShowHiddenEdges,
                ShowLabels = Settings.Canvas.MathSolidShowLabels,
                ShowAxes = false,
                RenderQuality = Settings.Canvas.MathSolidRenderQuality,
                Scale = Settings.Canvas.MathSolidScale
            };

            switch (solidType)
            {
                case SolidType.Cube:
                    solid.Width = first;
                    solid.Height = first;
                    solid.Depth = first;
                    solid.Radius = first / 2;
                    break;
                case SolidType.Cuboid:
                    solid.Width = first;
                    solid.Depth = second;
                    solid.Height = third;
                    solid.Radius = Math.Max(first, Math.Max(second, third)) / 2;
                    break;
                case SolidType.Prism:
                    solid.Width = first;
                    solid.Height = second;
                    solid.Depth = third;
                    solid.Radius = Math.Max(first, Math.Max(second, third)) / 2;
                    break;
                case SolidType.Pyramid:
                    solid.Width = first;
                    solid.Depth = second;
                    solid.Height = third;
                    solid.Radius = Math.Max(first, Math.Max(second, third)) / 2;
                    break;
                case SolidType.Cylinder:
                case SolidType.Cone:
                    solid.Radius = first;
                    solid.Height = second;
                    solid.Width = first * 2;
                    solid.Depth = first * 2;
                    break;
                case SolidType.Sphere:
                    solid.Radius = first;
                    solid.Width = first * 2;
                    solid.Height = first * 2;
                    solid.Depth = first * 2;
                    break;
            }

            return solid;
        }

        private static SolidViewMode GetMathSolidDefaultViewMode()
        {
            return Settings.Canvas.MathSolidViewMode == (int)SolidViewMode.Front
                ? SolidViewMode.Front
                : SolidViewMode.Projection;
        }

        private static bool TryGetSolidType(MathInsertMode mode, out SolidType solidType)
        {
            solidType = mode switch
            {
                MathInsertMode.Cube => SolidType.Cube,
                MathInsertMode.Cuboid => SolidType.Cuboid,
                MathInsertMode.Prism => SolidType.Prism,
                MathInsertMode.Pyramid => SolidType.Pyramid,
                MathInsertMode.Cylinder => SolidType.Cylinder,
                MathInsertMode.Cone => SolidType.Cone,
                MathInsertMode.Sphere => SolidType.Sphere,
                _ => default
            };
            return mode >= MathInsertMode.Cube && mode <= MathInsertMode.Sphere;
        }

        private void SelectConstraintObject(Point position)
        {
            if (!Settings.Canvas.MathConstraintsEnabled)
            {
                ShowNotification(Strings.GetString("Math_ConstraintsDisabled") ?? "Math_ConstraintsDisabled");
                CancelMathInsertMode();
                return;
            }

            var mathObject = MathGeometryService.HitTest(
                MathCanvas.Scene,
                ToMathPoint(position),
                12);
            if (mathObject == null ||
                !IsValidConstraintSelection(_mathInsertMode, _pendingConstraintObjects.Count, mathObject))
            {
                ShowNotification(Strings.GetString("Math_ConstraintInvalid") ?? "Math_ConstraintInvalid");
                return;
            }

            _pendingConstraintObjects.Add(mathObject);
            UpdateMathToolStatus();
            var expectedCount = GetConstraintObjectCount(_mathInsertMode);
            if (_pendingConstraintObjects.Count < expectedCount) return;

            var constraint = new MathConstraint
            {
                Type = ToConstraintType(_mathInsertMode)
            };
            for (var i = 0; i < _pendingConstraintObjects.Count; i++)
                constraint.ObjectIds.Add(_pendingConstraintObjects[i].Id);

            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            try
            {
                MathConstraintService.Add(MathCanvas.Scene, constraint);
                if (!MathConstraintService.TryApplyAll(MathCanvas.Scene, out _))
                {
                    RestoreMathScene(beforeJson);
                    ShowNotification(Strings.GetString("Math_ConstraintConflict") ?? "Math_ConstraintConflict");
                }
                else
                {
                    CommitMathSceneChange(beforeJson);
                    ShowNotification(Strings.GetString("Math_ConstraintAdded") ?? "Math_ConstraintAdded");
                }
            }
            catch (Exception)
            {
                RestoreMathScene(beforeJson);
                ShowNotification(Strings.GetString("Math_ConstraintInvalid") ?? "Math_ConstraintInvalid");
            }

            ActivateMathInsertMode(MathInsertMode.Select);
        }

        private void RestoreMathScene(string json)
        {
            ClearMathSelection();
            MathCanvas.Scene = MathSceneSerializer.Deserialize(json).Scene;
            RefreshMathScene();
        }

        private static bool IsConstraintMode(MathInsertMode mode)
        {
            return mode >= MathInsertMode.HorizontalConstraint &&
                   mode <= MathInsertMode.PerpendicularConstraint;
        }

        private static bool IsMathConstructionMode(MathInsertMode mode)
        {
            return mode is MathInsertMode.Point or MathInsertMode.Segment or
                MathInsertMode.Line or MathInsertMode.Ray or MathInsertMode.Circle or
                MathInsertMode.Angle or MathInsertMode.Triangle or MathInsertMode.Label or MathInsertMode.CoordinatePlane;
        }

        private static int GetConstraintObjectCount(MathInsertMode mode)
        {
            return mode switch
            {
                MathInsertMode.HorizontalConstraint => 1,
                MathInsertMode.VerticalConstraint => 1,
                MathInsertMode.EqualLengthConstraint => 2,
                MathInsertMode.CollinearConstraint => 3,
                MathInsertMode.PointOnLineConstraint => 2,
                MathInsertMode.PointOnCircleConstraint => 2,
                MathInsertMode.ParallelConstraint => 2,
                MathInsertMode.PerpendicularConstraint => 2,
                _ => 0
            };
        }

        private static bool IsValidConstraintSelection(
            MathInsertMode mode,
            int selectionIndex,
            MathObject mathObject)
        {
            return mode switch
            {
                MathInsertMode.HorizontalConstraint => mathObject is SegmentObject,
                MathInsertMode.VerticalConstraint => mathObject is SegmentObject,
                MathInsertMode.EqualLengthConstraint => mathObject is SegmentObject,
                MathInsertMode.CollinearConstraint => mathObject is PointObject,
                MathInsertMode.PointOnLineConstraint => selectionIndex == 0
                    ? mathObject is PointObject
                    : mathObject is LineObject,
                MathInsertMode.PointOnCircleConstraint => selectionIndex == 0
                    ? mathObject is PointObject
                    : mathObject is CircleObject,
                MathInsertMode.ParallelConstraint => mathObject is SegmentObject,
                MathInsertMode.PerpendicularConstraint => mathObject is SegmentObject,
                _ => false
            };
        }

        private static MathConstraintType ToConstraintType(MathInsertMode mode)
        {
            return mode switch
            {
                MathInsertMode.HorizontalConstraint => MathConstraintType.Horizontal,
                MathInsertMode.VerticalConstraint => MathConstraintType.Vertical,
                MathInsertMode.EqualLengthConstraint => MathConstraintType.EqualLength,
                MathInsertMode.CollinearConstraint => MathConstraintType.Collinear,
                MathInsertMode.PointOnLineConstraint => MathConstraintType.PointOnLine,
                MathInsertMode.PointOnCircleConstraint => MathConstraintType.PointOnCircle,
                MathInsertMode.ParallelConstraint => MathConstraintType.Parallel,
                MathInsertMode.PerpendicularConstraint => MathConstraintType.Perpendicular,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }

        private void CommitMathSceneChange(string beforeJson)
        {
            MathReferenceService.Synchronize(MathCanvas.Scene);
            var afterJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            if (string.Equals(beforeJson, afterJson, StringComparison.Ordinal)) return;

            timeMachine.CommitMathSceneHistory(beforeJson, afterJson);
            RefreshMathScene();
        }

        private bool HasMathObjectsOnCurrentPage()
        {
            return MathCanvas?.Scene?.Objects?.Count > 0;
        }

        private void ClearMathSceneForUserClear()
        {
            if (!HasMathObjectsOnCurrentPage()) return;

            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            CancelMathInsertMode();
            MathCanvas.Scene = new MathScene();
            CommitMathSceneChange(beforeJson);
        }

        private void RefreshMathScene()
        {
            if (MathInkPresenter == null) return;
            var shouldShow = Settings.Canvas.EnableMathCanvas && currentMode == 1;
            MathInkPresenter.Visibility = shouldShow
                ? Visibility.Visible
                : Visibility.Collapsed;
            MathAnnotationOverlay.Visibility = MathInkPresenter.Visibility;
            MathPreviewPresenter.Visibility = MathInkPresenter.Visibility;
            MathInteractionOverlay.Visibility = MathInkPresenter.Visibility;
            try
            {
                MathReferenceService.Synchronize(MathCanvas.Scene);
                var strokes = _mathStrokeRenderer.Render(
                    MathCanvas.Scene,
                    Settings.Canvas.MathShowMeasurements);
                MathInkPresenter.Strokes = strokes;
                RefreshMathAnnotations();
                UpdateMathSelectionOverlay();
                LogHelper.WriteLogToFile(
                    $"Math presenter refreshed: objects={MathCanvas.Scene.Objects.Count}, " +
                    $"strokes={strokes.Count}, visible={MathInkPresenter.Visibility}, " +
                    $"size={MathInkPresenter.ActualWidth:F0}x{MathInkPresenter.ActualHeight:F0}, " +
                    $"host={InkCanvasGridForInkReplay.ActualWidth:F0}x{InkCanvasGridForInkReplay.ActualHeight:F0}, " +
                    $"mode={currentMode}, enabled={Settings.Canvas.EnableMathCanvas}",
                    LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"Math scene stroke rendering failed: {ex}",
                    LogHelper.LogType.Error);
            }
        }

        internal void ApplyMathSettings()
        {
            var enabled = Settings.Canvas.EnableMathCanvas;
            MathCanvas.Visibility = Visibility.Collapsed;
            MathInkPresenter.Visibility = enabled && currentMode == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            MathAnnotationOverlay.Visibility = MathInkPresenter.Visibility;
            MathPreviewPresenter.Visibility = MathInkPresenter.Visibility;
            MathInteractionOverlay.Visibility = MathInkPresenter.Visibility;
            MathCanvas.ShowMeasurements = Settings.Canvas.MathShowMeasurements;
            if (enabled && currentMode == 1)
            {
                ApplyMathSceneContrast();
                RefreshMathScene();
            }
            if (!enabled || currentMode != 1)
            {
                BoardMathInsertPopup.IsOpen = false;
                CancelMathInsertMode();
            }

            if (FindView("board.math") is FrameworkElement mathButton)
                mathButton.IsEnabled = enabled;
            UpdateMathToolbarVisual();
        }

        private void UpdateMathToolbarVisual()
        {
            if (FindView("board.math") is not Controls.BoardToolbarButton mathButton)
                return;

            var isActive = Settings.Canvas.EnableMathCanvas && currentMode == 1 &&
                           (_mathInsertMode != MathInsertMode.None || BoardMathInsertPopup.IsOpen);
            var accent = Application.Current.TryFindResource("FloatingBarAccentBrush") as Brush
                         ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
            var foreground = Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush
                             ?? Brushes.White;
            mathButton.Background = isActive ? accent : Brushes.Transparent;
            mathButton.IconGeometryDrawing.Brush = isActive ? Brushes.White : foreground;
            mathButton.Foreground = isActive ? Brushes.White : foreground;
        }

        private void ApplyMathSceneContrast()
        {
            var strokeColor = GetMathStrokeColor();
            var changed = false;
            for (var i = 0; i < MathCanvas.Scene.Objects.Count; i++)
            {
                var mathObject = MathCanvas.Scene.Objects[i];
                if (!string.Equals(mathObject.StrokeColor, "#FF000000", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mathObject.StrokeColor, MathAppearanceService.DarkStrokeColor, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mathObject.StrokeColor, MathAppearanceService.LightStrokeColor, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(mathObject.StrokeColor, strokeColor, StringComparison.OrdinalIgnoreCase))
                    continue;

                mathObject.StrokeColor = strokeColor;
                changed = true;
            }

            if (changed)
                LogHelper.WriteLogToFile("Math scene contrast updated.", LogHelper.LogType.Trace);
        }

        private static MathPoint ToMathPoint(Point point)
        {
            return new MathPoint(point.X, point.Y);
        }

        private static Point ToPoint(MathPoint point)
        {
            return new Point(point.X, point.Y);
        }

        private void RefreshMathAnnotations()
        {
            if (MathAnnotationOverlay == null) return;
            MathAnnotationOverlay.Children.Clear();
            var occupied = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var objectIndex = 0; objectIndex < MathCanvas.Scene.Objects.Count; objectIndex++)
            {
                if (MathCanvas.Scene.Objects[objectIndex] is SolidObject solid)
                {
                    AddMathSolidAnnotation(solid);
                    continue;
                }
                if (MathCanvas.Scene.Objects[objectIndex] is not FunctionObject function || !function.IsVisible)
                    continue;
                var sample = FunctionSamplingService.Sample(function);
                if (function.ShowZeros)
                {
                    for (var i = 0; i < sample.Zeros.Count; i++)
                        AddMathFunctionAnnotation(function, sample.Zeros[i], "Math_FunctionZero", occupied);
                }
                if (function.ShowExtrema)
                {
                    for (var i = 0; i < sample.Extrema.Count; i++)
                        AddMathFunctionAnnotation(function, sample.Extrema[i], "Math_FunctionExtremum", occupied);
                }
                if (!function.ShowIntersections) continue;
                for (var otherIndex = objectIndex + 1; otherIndex < MathCanvas.Scene.Objects.Count; otherIndex++)
                {
                    if (MathCanvas.Scene.Objects[otherIndex] is not FunctionObject other ||
                        !other.IsVisible ||
                        !other.ShowIntersections ||
                        !FunctionAnalysisService.ShareCoordinateFrame(function, other))
                        continue;
                    var intersections = FunctionAnalysisService.FindIntersections(function, other);
                    for (var i = 0; i < intersections.Count; i++)
                        AddMathFunctionAnnotation(function, intersections[i], "Math_FunctionIntersection", occupied);
                }
            }
        }

        private void AddMathSolidAnnotation(SolidObject solid)
        {
            if (!solid.IsVisible || !solid.ShowLabels) return;
            var projection = SolidProjectionService.Project(solid);
            if (projection.Points.Count == 0) return;
            var left = projection.Points[0].X;
            var top = projection.Points[0].Y;
            for (var i = 1; i < projection.Points.Count; i++)
            {
                left = Math.Min(left, projection.Points[i].X);
                top = Math.Min(top, projection.Points[i].Y);
            }
            AddMathOverlayText(
                $"V={SolidMeasurementService.Volume(solid):0.##}  S={SolidMeasurementService.SurfaceArea(solid):0.##}",
                left,
                top - 25);
        }

        private void AddMathFunctionAnnotation(
            FunctionObject function,
            MathPoint coordinate,
            string kindKey,
            IDictionary<string, int> occupied)
        {
            var screenPoint = new MathPoint(
                function.Origin.X + coordinate.X * function.PixelsPerUnit,
                function.Origin.Y - coordinate.Y * function.PixelsPerUnit);
            screenPoint = RotateMathPoint(screenPoint, function.Origin, function.RotationDegrees);
            var slot = $"{Math.Round(screenPoint.X / 8)}:{Math.Round(screenPoint.Y / 8)}";
            occupied.TryGetValue(slot, out var slotIndex);
            occupied[slot] = slotIndex + 1;
            var offset = slotIndex * 24;
            AddMathOverlayText(
                $"({coordinate.X:0.##}, {coordinate.Y:0.##})",
                screenPoint.X + 7,
                screenPoint.Y - 25 + offset);
        }

        private void AddMathOverlayText(string value, double left, double top)
        {
            var text = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(204, 32, 32, 32)),
                Padding = new Thickness(5, 2, 5, 2),
                FontSize = 13
            };
            System.Windows.Controls.Canvas.SetLeft(text, left);
            System.Windows.Controls.Canvas.SetTop(text, top);
            MathAnnotationOverlay.Children.Add(text);
        }

        private static MathPoint RotateMathPoint(MathPoint point, MathPoint center, double degrees)
        {
            if (Math.Abs(degrees) <= double.Epsilon) return point;
            var radians = degrees * Math.PI / 180;
            var deltaX = point.X - center.X;
            var deltaY = point.Y - center.Y;
            return new MathPoint(
                center.X + deltaX * Math.Cos(radians) - deltaY * Math.Sin(radians),
                center.Y + deltaX * Math.Sin(radians) + deltaY * Math.Cos(radians));
        }

        private void UpdateMathToolStatus()
        {
            var key = _mathInsertMode switch
            {
                MathInsertMode.Select => "Math_StatusSelect",
                MathInsertMode.Point => "Math_StatusPoint",
                MathInsertMode.Segment => "Math_StatusSegment",
                MathInsertMode.Line => "Math_StatusLine",
                MathInsertMode.Ray => "Math_StatusRay",
                MathInsertMode.Circle => "Math_StatusCircle",
                MathInsertMode.Angle => "Math_StatusAngle",
                MathInsertMode.Triangle => "Math_StatusTriangle",
                MathInsertMode.RotateSolid => "Math_StatusRotate",
                _ when IsConstraintMode(_mathInsertMode) => "Math_StatusConstraint",
                _ => "Math_StatusPlace"
            };
            var text = Strings.GetString(key) ?? key;
            if (Settings.Canvas.MathSnapEnabled && IsMathConstructionMode(_mathInsertMode))
                text += "  ·  " + (Strings.GetString("Math_SnapHint") ?? "靠近端点、中点或交点时自动吸附");
            if ((_mathInsertMode is MathInsertMode.Angle or MathInsertMode.Triangle) &&
                _pendingMathPoints.Count > 0)
                text += $" ({_pendingMathPoints.Count + 1}/3)";
            if (IsConstraintMode(_mathInsertMode) && _pendingConstraintObjects.Count > 0)
                text += $" ({_pendingConstraintObjects.Count + 1}/{GetConstraintObjectCount(_mathInsertMode)})";
            MathToolStatusText.Text = text;
            MathToolStatusBorder.Visibility = _mathInsertMode == MathInsertMode.None
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void RefreshMathConstructionPreview()
        {
            if (!_mathDragStart.HasValue || !_mathPreviewEnd.HasValue) return;
            var start = _mathDragStart.Value.Position;
            var end = _mathPreviewEnd.Value;
            var distance = MathMeasurementService.Distance(start, end);
            MathObject preview = _mathInsertMode switch
            {
                MathInsertMode.Segment => new SegmentObject { Start = start, End = end },
                MathInsertMode.Line => new LineObject { Start = start, End = end },
                MathInsertMode.Ray => new RayObject { Start = start, Through = end },
                MathInsertMode.Circle => new CircleObject { Center = start, Radius = Math.Max(1, distance) },
                MathInsertMode.CoordinatePlane => MathPlacementService.CreateCoordinatePlane(
                    start, end, Settings.Canvas.MathGridSpacing,
                    Settings.Canvas.MathShowGrid, Settings.Canvas.MathShowAxes),
                _ => null
            };
            if (preview == null) return;
            preview.StrokeColor = GetMathStrokeColor();
            preview.StrokeWidth = Settings.Canvas.MathDefaultStrokeWidth;
            var scene = new MathScene();
            scene.Objects.Add(preview);
            MathPreviewPresenter.Strokes = _mathStrokeRenderer.Render(scene, true);
        }

        private void ClearMathPreview()
        {
            if (MathPreviewPresenter != null)
                MathPreviewPresenter.Strokes = new StrokeCollection();
            if (MathSnapIndicator != null)
            {
                MathSnapIndicator.Visibility = Visibility.Collapsed;
                MathSnapIndicatorText.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMathSnapFeedback(MathSnapResult result)
        {
            var point = ToPoint(result.Position);
            System.Windows.Controls.Canvas.SetLeft(MathSnapIndicator, point.X - MathSnapIndicator.Width / 2);
            System.Windows.Controls.Canvas.SetTop(MathSnapIndicator, point.Y - MathSnapIndicator.Height / 2);
            MathSnapIndicator.Visibility = Visibility.Visible;
            MathSnapIndicatorText.Text = result.PointObjectId.HasValue
                ? (Strings.GetString("Math_SnapPoint") ?? "吸附到点")
                : (Strings.GetString("Math_SnapGeometry") ?? "吸附到几何位置");
            System.Windows.Controls.Canvas.SetLeft(MathSnapIndicatorText, point.X + 12);
            System.Windows.Controls.Canvas.SetTop(MathSnapIndicatorText, point.Y + 10);
            MathSnapIndicatorText.Visibility = Visibility.Visible;
        }

        private void HideMathSnapFeedback()
        {
            MathSnapIndicator.Visibility = Visibility.Collapsed;
            MathSnapIndicatorText.Visibility = Visibility.Collapsed;
        }

        private void UpdateMathSelectionOverlay()
        {
            if (_selectedMathObject == null || MathInkPresenter == null)
            {
                ClearMathSelection();
                return;
            }
            MathObject currentObject = null;
            for (var i = 0; i < MathCanvas.Scene.Objects.Count; i++)
            {
                if (MathCanvas.Scene.Objects[i].Id == _selectedMathObject.Id)
                {
                    currentObject = MathCanvas.Scene.Objects[i];
                    break;
                }
            }
            if (currentObject == null)
            {
                ClearMathSelection();
                return;
            }
            _selectedMathObject = currentObject;
            var id = _selectedMathObject.Id.ToString("D");
            var bounds = Rect.Empty;
            foreach (var stroke in MathInkPresenter.Strokes)
            {
                if (!stroke.ContainsPropertyData(MathStrokeRenderer.MathObjectIdProperty) ||
                    !string.Equals(stroke.GetPropertyData(MathStrokeRenderer.MathObjectIdProperty) as string, id, StringComparison.Ordinal))
                    continue;
                bounds.Union(stroke.GetBounds());
            }
            if (bounds.IsEmpty) return;
            var width = InkCanvasGridForInkReplay.ActualWidth;
            var height = InkCanvasGridForInkReplay.ActualHeight;
            if (width > 0 && height > 0)
                bounds.Intersect(new Rect(0, 0, width, height));
            if (bounds.IsEmpty) return;
            bounds.Inflate(8, 8);
            _mathSelectionRect = bounds;
            System.Windows.Controls.Canvas.SetLeft(MathSelectionBounds, bounds.Left);
            System.Windows.Controls.Canvas.SetTop(MathSelectionBounds, bounds.Top);
            MathSelectionBounds.Width = bounds.Width;
            MathSelectionBounds.Height = bounds.Height;
            MathSelectionBounds.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(MathSelectionScaleHandle, bounds.Right - 7);
            System.Windows.Controls.Canvas.SetTop(MathSelectionScaleHandle, bounds.Top + bounds.Height / 2 - 7);
            System.Windows.Controls.Canvas.SetLeft(MathSelectionVerticalScaleHandle, bounds.Left + bounds.Width / 2 - 7);
            System.Windows.Controls.Canvas.SetTop(MathSelectionVerticalScaleHandle, bounds.Bottom - 7);
            var canScale = _selectedMathObject is SolidObject or FunctionObject or
                CoordinatePlaneObject or CircleObject;
            MathSelectionScaleHandle.Visibility = canScale ? Visibility.Visible : Visibility.Collapsed;
            MathSelectionVerticalScaleHandle.Visibility = canScale ? Visibility.Visible : Visibility.Collapsed;
            var canRotate = _selectedMathObject is SolidObject or FunctionObject;
            System.Windows.Controls.Canvas.SetLeft(MathSelectionRotateHandle, bounds.Right + 12);
            System.Windows.Controls.Canvas.SetTop(MathSelectionRotateHandle, bounds.Top - 20);
            MathSelectionRotateHandle.Visibility = canRotate ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearMathSelection()
        {
            _selectedMathObject = null;
            _mathSelectionRect = Rect.Empty;
            MathSelectionBounds.Visibility = Visibility.Collapsed;
            MathSelectionRotateHandle.Visibility = Visibility.Collapsed;
            MathSelectionScaleHandle.Visibility = Visibility.Collapsed;
            MathSelectionVerticalScaleHandle.Visibility = Visibility.Collapsed;
            CloseMathObjectActionsPopup();
        }

        private bool IsNearSelectionHandle(Point point, bool rotate)
        {
            if (_mathSelectionRect.IsEmpty) return false;
            var center = rotate
                ? new Point(_mathSelectionRect.Right + 20, _mathSelectionRect.Top - 12)
                : new Point(_mathSelectionRect.Right, _mathSelectionRect.Top + _mathSelectionRect.Height / 2);
            return (point - center).Length <= 18;
        }

        private bool IsNearSelectionScaleHandle(Point point, bool vertical)
        {
            if (_mathSelectionRect.IsEmpty) return false;
            var center = vertical
                ? new Point(_mathSelectionRect.Left + _mathSelectionRect.Width / 2, _mathSelectionRect.Bottom)
                : new Point(_mathSelectionRect.Right, _mathSelectionRect.Top + _mathSelectionRect.Height / 2);
            return (point - center).Length <= 18;
        }

        private void RotateMathObject(MathObject mathObject, Vector delta)
        {
            if (mathObject is SolidObject solid)
            {
                RotateSolidBy(solid, delta);
            }
            else if (mathObject is FunctionObject function)
            {
                function.RotationDegrees = NormalizeMathAngle(function.RotationDegrees + delta.X * 0.5);
            }
        }

        private void BeginSolidRotation(SolidObject solid)
        {
            _solidRotationRawX = solid.RotationX;
            _solidRotationRawY = solid.RotationY;
        }

        private void RotateSolidBy(SolidObject solid, Vector delta)
        {
            _solidRotationRawX ??= solid.RotationX;
            _solidRotationRawY ??= solid.RotationY;
            _solidRotationRawX += delta.Y * 0.5;
            _solidRotationRawY += delta.X * 0.5;
            solid.RotationX = SolidRotationSnapService.SnapToRightAngle(_solidRotationRawX.Value);
            solid.RotationY = SolidRotationSnapService.SnapToRightAngle(_solidRotationRawY.Value);
        }

        private void EndSolidRotation()
        {
            _solidRotationRawX = null;
            _solidRotationRawY = null;
        }

        private void ScaleMathObject(
            MathObject mathObject,
            Vector delta,
            bool horizontal,
            bool vertical)
        {
            var horizontalFactor = Math.Max(0.8, Math.Min(1.2, 1 + delta.X / 100));
            var verticalFactor = Math.Max(0.8, Math.Min(1.2, 1 + delta.Y / 100));
            var factor = horizontal ? horizontalFactor : verticalFactor;
            switch (mathObject)
            {
                case SolidObject solid:
                    MathGeometryService.StretchSolid(
                        solid,
                        horizontal ? horizontalFactor : 1,
                        vertical ? verticalFactor : 1);
                    break;
                case FunctionObject function:
                    function.PixelsPerUnit = Math.Max(5, Math.Min(200, function.PixelsPerUnit * factor));
                    break;
                case CoordinatePlaneObject plane:
                    if (horizontal) plane.Width = Math.Max(80, plane.Width * horizontalFactor);
                    if (vertical) plane.Height = Math.Max(80, plane.Height * verticalFactor);
                    break;
                case CircleObject circle:
                    var circleFactor = Math.Max(2, circle.Radius * factor) / circle.Radius;
                    if (circle.RadiusPointId.HasValue)
                    {
                        for (var i = 0; i < MathCanvas.Scene.Objects.Count; i++)
                        {
                            if (MathCanvas.Scene.Objects[i] is not PointObject radiusPoint ||
                                radiusPoint.Id != circle.RadiusPointId.Value ||
                                radiusPoint.IsLocked)
                                continue;
                            radiusPoint.Position = new MathPoint(
                                circle.Center.X + (radiusPoint.Position.X - circle.Center.X) * circleFactor,
                                circle.Center.Y + (radiusPoint.Position.Y - circle.Center.Y) * circleFactor);
                            MathReferenceService.Synchronize(MathCanvas.Scene);
                            return;
                        }
                    }
                    else
                    {
                        circle.Radius = Math.Max(2, circle.Radius * factor);
                    }
                    break;
            }
        }

        private void OpenMathObjectActionsPopup()
        {
            if (_selectedMathObject == null || _mathSelectionRect.IsEmpty) return;
            MathObjectEditButton.IsEnabled = _selectedMathObject is FunctionObject;
            MathObjectCircumsphereButton.IsEnabled = _selectedMathObject is SolidObject solid &&
                SolidSphereConstructionService.TryCreateCircumsphere(solid, out _);
            MathObjectInsphereButton.IsEnabled = _selectedMathObject is SolidObject insphereSolid &&
                SolidSphereConstructionService.TryCreateInsphere(insphereSolid, out _);
            MathObjectCircumcircleButton.IsEnabled = _selectedMathObject is TriangleObject triangle &&
                TriangleCircleConstructionService.TryCreate(triangle, TriangleCircleKind.Circumcircle, out _);
            MathObjectIncircleButton.IsEnabled = _selectedMathObject is TriangleObject incircleTriangle &&
                TriangleCircleConstructionService.TryCreate(incircleTriangle, TriangleCircleKind.Incircle, out _);
            MathObjectResetViewButton.IsEnabled = _selectedMathObject is SolidObject or FunctionObject;
            System.Windows.Controls.Canvas.SetLeft(MathObjectActionsPopup, _mathSelectionRect.Left);
            System.Windows.Controls.Canvas.SetTop(MathObjectActionsPopup, Math.Max(0, _mathSelectionRect.Top - 46));
            EnableMathPopupInteraction();
            MathObjectActionsPopup.Visibility = Visibility.Visible;
            BeginMathObjectActionsInput();
        }

        private void CloseMathObjectActionsPopup()
        {
            if (MathObjectActionsPopup.Visibility != Visibility.Visible) return;
            MathObjectActionsPopup.Visibility = Visibility.Collapsed;
            EndMathTextInput();
        }

        private void AddSelectedSolidSphere(bool inscribed)
        {
            if (_selectedMathObject is not SolidObject solid) return;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;
            SolidObject sphere;
            var created = inscribed
                ? SolidSphereConstructionService.TryCreateInsphere(solid, out sphere)
                : SolidSphereConstructionService.TryCreateCircumsphere(solid, out sphere);
            if (!created)
            {
                ShowNotification(Strings.GetString("Math_ConstructionUnavailable") ?? "当前几何体不满足此构造的严格条件");
                return;
            }
            AddMathObject(sphere);
            OpenMathObjectActionsPopup();
        }

        private void AddSelectedTriangleCircle(TriangleCircleKind kind)
        {
            if (_selectedMathObject is not TriangleObject triangle) return;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;
            if (!TriangleCircleConstructionService.TryCreate(triangle, kind, out var circle))
            {
                ShowNotification(Strings.GetString("Math_ConstructionUnavailable") ?? "当前几何体不满足此构造的严格条件");
                return;
            }
            AddMathObject(circle);
            OpenMathObjectActionsPopup();
        }

        private void EditSelectedMathObject()
        {
            if (_selectedMathObject is not FunctionObject function) return;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;
            CloseMathObjectActionsPopup();
            _functionBeingEdited = function;
            _functionEditBeforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            BoardMathInsertPopupContent.FunctionExpressionInput.Text = function.Expression;
            BoardMathInsertPopupContent.FunctionDomainMinInput.Text = function.DomainMin.ToString(CultureInfo.InvariantCulture);
            BoardMathInsertPopupContent.FunctionDomainMaxInput.Text = function.DomainMax.ToString(CultureInfo.InvariantCulture);
            BoardMathInsertPopup.IsOpen = true;
            _popupManager?.BringToFront(BoardMathInsertPopup);
            BeginMathTextInput();
        }

        private void MeasureSelectedMathObject()
        {
            if (_selectedMathObject == null) return;
            var value = _selectedMathObject switch
            {
                PointObject point => $"({point.Position.X:0.##}, {point.Position.Y:0.##})",
                SegmentObject segment => $"L={MathMeasurementService.Distance(segment.Start, segment.End):0.##}",
                CircleObject circle => $"r={circle.Radius:0.##}",
                AngleMeasurementObject angle =>
                    $"{MathMeasurementService.AngleDegrees(angle.First, angle.Vertex, angle.Second):0.##}°",
                SolidObject solid =>
                    $"V={SolidMeasurementService.Volume(solid):0.##}, S={SolidMeasurementService.SurfaceArea(solid):0.##}",
                FunctionObject function => FormatFunctionAnalysis(function),
                _ => Strings.GetString("Math_MeasurementUnavailable") ?? "—"
            };
            var format = Strings.GetString("Math_MeasurementResult") ?? "Measurement: {0}";
            ShowNotification(string.Format(CultureInfo.CurrentCulture, format, value));
        }

        private string FormatFunctionAnalysis(FunctionObject function)
        {
            var analysis = FunctionAnalysisService.Analyze(function);
            var none = Strings.GetString("Math_None") ?? "—";
            var intercept = analysis.YAxisIntercept.HasValue
                ? FormatFunctionCoordinate(analysis.YAxisIntercept.Value)
                : none;
            var template = Strings.GetString("Math_FunctionAnalysis") ??
                           "y={0}; zeros: {1}; extrema: {2}; y-intercept: {3}; monotonic: {4}";
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                function.Expression,
                FormatFunctionCoordinates(analysis.Zeros, none),
                FormatFunctionCoordinates(analysis.Extrema, none),
                intercept,
                FormatFunctionIntervals(analysis.MonotonicIntervals, none));
        }

        private static string FormatFunctionCoordinate(MathPoint value)
        {
            return $"({value.X:0.##}, {value.Y:0.##})";
        }

        private static string FormatFunctionCoordinates(
            IReadOnlyList<MathPoint> values,
            string emptyValue)
        {
            if (values == null || values.Count == 0) return emptyValue;
            var count = Math.Min(values.Count, 4);
            var result = string.Empty;
            for (var i = 0; i < count; i++)
            {
                if (i > 0) result += ", ";
                result += FormatFunctionCoordinate(values[i]);
            }
            return values.Count > count ? result + ", …" : result;
        }

        private static string FormatFunctionIntervals(
            IReadOnlyList<FunctionMonotonicInterval> values,
            string emptyValue)
        {
            if (values == null || values.Count == 0) return emptyValue;
            var count = Math.Min(values.Count, 3);
            var result = string.Empty;
            for (var i = 0; i < count; i++)
            {
                if (i > 0) result += ", ";
                var trend = Strings.GetString(values[i].Monotonicity == FunctionMonotonicity.Increasing
                    ? "Math_Increasing"
                    : "Math_Decreasing") ?? values[i].Monotonicity.ToString();
                var template = Strings.GetString("Math_FunctionInterval") ?? "{0}[{1:0.##}, {2:0.##}]";
                result += string.Format(
                    CultureInfo.CurrentCulture,
                    template,
                    trend,
                    values[i].Start,
                    values[i].End);
            }
            return values.Count > count ? result + ", …" : result;
        }

        private void ResetSelectedMathObjectView()
        {
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;
            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            if (_selectedMathObject is SolidObject solid)
            {
                solid.RotationX = 0;
                solid.RotationY = 0;
                solid.RotationZ = 0;
                solid.Scale = Settings.Canvas.MathSolidScale;
                solid.HorizontalScale = 1;
                solid.VerticalScale = 1;
                solid.ViewMode = GetMathSolidDefaultViewMode();
                solid.ProjectionMode = SolidProjectionMode.Orthographic;
                solid.ShowAxes = false;
            }
            else if (_selectedMathObject is FunctionObject function)
            {
                function.RotationDegrees = 0;
                function.PixelsPerUnit = Settings.Canvas.MathFunctionPixelsPerUnit;
            }
            CommitMathSceneChange(beforeJson);
            UpdateMathSelectionOverlay();
        }

        private void DeleteSelectedMathObject()
        {
            if (_selectedMathObject == null) return;
            if (TryBlockFrozenPageMutation(Strings.GetString("Board_Math") ?? "Board_Math")) return;
            var beforeJson = MathSceneSerializer.Serialize(MathCanvas.Scene);
            new MathSceneService(MathCanvas.Scene).Remove(_selectedMathObject.Id);
            ClearMathSelection();
            CommitMathSceneChange(beforeJson);
        }

        private MathSnapResult GetMathInput(Point point)
        {
            var input = ToMathPoint(point);
            if (Settings.Canvas.MathSnapEnabled &&
                MathSnapService.TrySnap(
                    MathCanvas.Scene,
                    input,
                    Settings.Canvas.MathSnapTolerance,
                    out MathSnapResult snapped))
            {
                ShowMathSnapFeedback(snapped);
                return snapped;
            }

            HideMathSnapFeedback();
            return new MathSnapResult(input, null);
        }

        private string GetMathSceneJsonForPage(int pageNumber)
        {
            if (currentMode == 0 || pageNumber == CurrentWhiteboardIndex)
                return MathSceneSerializer.Serialize(MathCanvas.Scene);

            var sceneJson = MathSceneSerializer.Serialize(new MathScene());
            if (pageNumber < 0 || pageNumber >= TimeMachineHistories.Length)
                return sceneJson;

            var history = TimeMachineHistories[pageNumber];
            if (history == null) return sceneJson;

            for (var i = 0; i < history.Length; i++)
            {
                var item = history[i];
                if (item.CommitType != TimeMachineHistoryType.MathSceneChange) continue;
                sceneJson = item.StrokeHasBeenCleared
                    ? item.MathSceneBeforeJson
                    : item.MathSceneAfterJson;
            }

            return sceneJson;
        }

        private void SaveMathSceneSidecar(string contentFilePath, int pageNumber)
        {
            SaveMathSceneFile(Path.ChangeExtension(contentFilePath, ".math.json"), pageNumber);
        }

        private void SaveMathSceneFile(string mathFilePath, int pageNumber)
        {
            var result = MathSceneSerializer.Deserialize(GetMathSceneJsonForPage(pageNumber));
            LogMathSceneIssues(result, $"保存第 {pageNumber} 页");
            MathSceneFileStore.Save(mathFilePath, result.Scene);
        }

        private bool HasMathSceneForPage(int pageNumber)
        {
            return MathSceneSerializer.Deserialize(GetMathSceneJsonForPage(pageNumber)).Scene.Objects.Count > 0;
        }

        private void LoadMathSceneSidecar(string contentFilePath)
        {
            var mathFilePath = Path.ChangeExtension(contentFilePath, ".math.json");
            if (!File.Exists(mathFilePath))
            {
                MathCanvas.Scene = new MathScene();
                RefreshMathScene();
                return;
            }

            var result = MathSceneFileStore.Load(mathFilePath);
            LogMathSceneIssues(result, $"打开 {Path.GetFileName(mathFilePath)}");
            var beforeJson = MathSceneSerializer.Serialize(new MathScene());
            MathCanvas.Scene = result.Scene;
            RefreshMathScene();
            if (result.Scene.Objects.Count > 0)
                timeMachine.CommitMathSceneHistory(beforeJson, MathSceneSerializer.Serialize(result.Scene));
        }

        private TimeMachineHistory CreateMathSceneHistoryFromFile(string mathFilePath)
        {
            var result = MathSceneFileStore.Load(mathFilePath);
            LogMathSceneIssues(result, $"打开 {Path.GetFileName(mathFilePath)}");
            if (result.Scene.Objects.Count == 0) return null;

            return new TimeMachineHistory(
                MathSceneSerializer.Serialize(new MathScene()),
                MathSceneSerializer.Serialize(result.Scene));
        }

        private static void LogMathSceneIssues(MathSceneLoadResult result, string operation)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                LogHelper.WriteLogToFile(
                    $"{operation}数学场景时已隔离异常数据: {result.Issues[i]}",
                    LogHelper.LogType.Warning);
            }
        }
    }
}
