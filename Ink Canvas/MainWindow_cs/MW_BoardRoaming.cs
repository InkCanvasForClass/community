using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private bool _isBoardRoamingPointerDown;
        private Point _boardRoamingLastPoint;
        private Dictionary<Stroke, StylusPointCollection> _boardRoamingStrokeHistory;
        private Rect _boardRoamingWorldBounds;
        private Point _boardRoamingViewportWorldPosition;
        private Rect _boardRoamingViewportInPreview;
        private double _boardRoamingPreviewScale;
        private Point _boardRoamingPreviewOffset;
        private Rect _boardRoamingPreviewMovementBounds;
        private bool _isUpdatingBoardRoamingPopup;
        private bool _boardRoamingPopupEventsAttached;
        private bool _isSyncingBoardRoamingToggles;
        // 漫游接触点（设备 ID -> 最近位置，按落下顺序排列）。
        // Stylus 与 PreviewTouch 共用接触点状态，双指手势直接从触点计算，不叠加 WPF Manipulation。
        private readonly List<KeyValuePair<int, Point>> _boardRoamingContacts = new List<KeyValuePair<int, Point>>();
        private Matrix _boardRoamingContactInputInverse;
        private bool _isBoardRoamingTwoFingerGesture;
        private bool _isBoardRoamingPopupDragActive;
        // 单指拖动挂起：第一指落下后先不拖动，位移超过阈值才激活，
        // 避免放置第二指过程中的手部漂移带动板书产生异常位移。
        private bool _isBoardRoamingSingleFingerPending;
        private Point _boardRoamingPendingStartPoint;
        private const double BoardRoamingSingleFingerActivationThreshold = 10.0;
        private readonly HashSet<int> _boardRoamingContactIds = new HashSet<int>();
        private int _boardRoamingPrimaryContactId = -1;
        private bool _isBoardRoamingMultiTouchSuppressed;
        private Vector _boardRoamingPendingTranslation;
        private Transform _boardRoamingOriginalRenderTransform;
        private Matrix _boardRoamingInputInverse;
        private TranslateTransform _boardRoamingRenderTranslation;
        private bool _isCommittingBoardRoaming;
        private bool _isBoardRoamingPopupDrag;
        private bool _boardRoamingInputEventsAttached;
        private bool _boardRoamingPreviewDirty = true;
        private DispatcherOperation _boardRoamingPreviewRefreshOperation;
        private double _boardRoamingBoothStartX;
        private double _boardRoamingBoothStartY;

        internal void ActivateBoardRoamingMode()
        {
            if (IsBoardRoamingMode)
            {
                if (BoardRoamingPopup != null && BoardRoamingPopup.IsOpen)
                {
                    BoardRoamingPopup.IsOpen = false;
                }
                else
                {
                    ShowBoardRoamingPopup();
                }
                return;
            }

            if (currentMode != 1) return;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation();
                return;
            }

            HideEdgeExpandHint();
            ResetBoardRoamingInput();
            CancelSingleFingerDragMode();
            drawingShapeMode = 0;
            forceEraser = false;
            forcePointEraser = false;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            inkCanvas.Select(new StrokeCollection());

            if (!SetCurrentToolMode(InkCanvasEditingMode.None)) return;

            UpdateCurrentToolMode("roaming");
            _boardRoamingViewportWorldPosition = new Point();
            HideSubPanels("roaming");
            UpdateBoardRoamingButtonState();
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private void ResetBoardRoamingInput()
        {
            var previewIds = new HashSet<int>(StrokeVisualList.Keys);
            previewIds.UnionWith(VisualCanvasList.Keys);
            foreach (var id in previewIds)
                CancelPalmTouchPreview(id);
            AbortAllActiveTouchInputs();
            TouchDownPointsList.Clear();
            isMouseDown = false;
            isTouchDown = false;
            _isMouseRealtimeInking = false;
            _pauseStraightenInkModeTracking = false;
            isMultiTouchTimerActive = false;
            isWaitUntilNextTouchDown = false;
            ResetBoardRoamingContactState();
            ResetTouchStates();
            inkCanvas.ReleaseStylusCapture();
        }

        private bool IsBoardRoamingMode
            => currentMode == 1 && string.Equals(_currentToolMode, "roaming", StringComparison.Ordinal);

        private void UpdateBoardRoamingButtonState()
        {
            if (FindView("board.roaming") is not BoardToolbarButton roamingButton) return;

            var foreground = Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush
                ?? Brushes.White;
            var accent = Application.Current.TryFindResource("FloatingBarAccentBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
            var isSelected = IsBoardRoamingMode;

            roamingButton.Background = isSelected ? accent : Brushes.Transparent;
            roamingButton.IconGeometryDrawing.Brush = isSelected ? Brushes.White : foreground;
            roamingButton.Foreground = isSelected ? Brushes.White : foreground;
        }

        private void BeginBoardRoaming(Point point)
        {
            if (!IsBoardRoamingMode || _isBoardRoamingPointerDown || IsCurrentPageFrozen
                || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;

            var inputTransform = GetBoardRoamingInputTransform(inkCanvas);
            if (!inputTransform.HasInverse) return;
            inputTransform.Invert();
            _boardRoamingInputInverse = inputTransform;
            AttachBoardRoamingInputEvents();
            _isBoardRoamingPointerDown = true;
            _boardRoamingLastPoint = point;
            _boardRoamingPendingTranslation = new Vector();
            if (PluginCanvasViewportTransformChanged == null)
            {
                _boardRoamingStrokeHistory = null;
                _boardRoamingOriginalRenderTransform = inkCanvas.RenderTransform;
                _boardRoamingRenderTranslation = new TranslateTransform();
                var transform = new TransformGroup();
                transform.Children.Add(_boardRoamingRenderTranslation);
                transform.Children.Add(_boardRoamingOriginalRenderTransform ?? Transform.Identity);
                inkCanvas.SetCurrentValue(UIElement.RenderTransformProperty, transform);
            }
            else
            {
                // 插件视口需要每个增量同步，保留实时内容变换，避免重复移动插件子元素。
                _boardRoamingOriginalRenderTransform = null;
                _boardRoamingRenderTranslation = null;
                _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
                foreach (var stroke in inkCanvas.Strokes)
                    _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();
            }
            _boardRoamingBoothStartX = _boothPreviewTranslateX;
            _boardRoamingBoothStartY = _boothPreviewTranslateY;
            inkCanvas.Cursor = Cursors.Hand;
        }

        private void MoveBoardRoaming(Point point)
        {
            if (!_isBoardRoamingPointerDown || !IsBoardRoamingMode
                || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;

            // 使用已应用到视觉树的变换，而非可能尚未完成布局的 RenderTransform 属性。
            point = _boardRoamingInputInverse.Transform(GetBoardRoamingInputTransform(inkCanvas).Transform(point));
            var delta = point - _boardRoamingLastPoint;
            if (delta.X == 0 && delta.Y == 0) return;

            TranslateBoardRoamingContent(delta.X, delta.Y);
            _boardRoamingLastPoint = point;
        }

        private static Matrix GetBoardRoamingInputTransform(Visual visual)
        {
            var matrix = VisualTreeHelper.GetTransform(visual)?.Value ?? Matrix.Identity;
            var offset = VisualTreeHelper.GetOffset(visual);
            matrix.Translate(offset.X, offset.Y);
            return matrix;
        }

        private void EndBoardRoaming()
        {
            if (_isBoardRoamingTwoFingerGesture)
            {
                EndBoardRoamingTwoFingerGesture();
                return;
            }
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CancelBoardRoamingPreviewRefresh();
            _isBoardRoamingPopupDrag = false;
            _isBoardRoamingPopupDragActive = false;
            try
            {
                CommitBoardRoamingHistory();
                CompletePluginCanvasViewportTransform();
                // 提交后内容坐标整体平移，缓存边界也必须同步，保持弹窗中的墨迹位置不变。
                if (IsValidBoardRoamingBounds(_boardRoamingWorldBounds))
                {
                    _boardRoamingWorldBounds.Offset(_boardRoamingPendingTranslation.X, _boardRoamingPendingTranslation.Y);
                    if (!IsValidBoardRoamingBounds(_boardRoamingWorldBounds))
                        _boardRoamingWorldBounds = Rect.Empty;
                }
            }
            finally
            {
                if (_boardRoamingRenderTranslation != null)
                    inkCanvas.SetCurrentValue(UIElement.RenderTransformProperty,
                        _boardRoamingOriginalRenderTransform ?? Transform.Identity);
                _boardRoamingOriginalRenderTransform = null;
                _boardRoamingRenderTranslation = null;
                _boardRoamingPendingTranslation = new Vector();
                _boardRoamingViewportWorldPosition = new Point();
                _boardRoamingStrokeHistory = null;
                _boardRoamingPreviewDirty = true;
                if (ViewboxFloatingBar != null) ViewboxFloatingBar.IsHitTestVisible = true;
                if (BlackboardUIGridForInkReplay != null) BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
                if (BoardRoamingPopup?.IsOpen == true)
                    QueueBoardRoamingPreviewRefresh();
            }
        }

        private void AttachBoardRoamingInputEvents()
        {
            if (_boardRoamingInputEventsAttached) return;

            inkCanvas.LostMouseCapture += (s, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, inkCanvas) && !_isBoardRoamingPopupDrag
                    && _boardRoamingContactIds.Count == 0)
                    EndBoardRoaming();
            };
            inkCanvas.LostStylusCapture += (s, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, inkCanvas) || _isBoardRoamingPopupDrag
                    || IsTouchStylusDevice(e.StylusDevice)) return;
                CancelBoardRoamingContact(e.StylusDevice.Id);
            };
            inkCanvas.LostTouchCapture += (s, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, inkCanvas) && !_isBoardRoamingPopupDrag)
                    CancelBoardRoamingContact(e.TouchDevice.Id);
            };
            inkCanvas.Unloaded += (s, e) =>
            {
                EndBoardRoaming();
                ResetBoardRoamingContactState();
            };
            _boardRoamingInputEventsAttached = true;
        }

        /// <summary>
        /// 强制结束漫游交互（单指拖动/双指手势），用于切换工具等场景。
        /// </summary>
        private void CancelBoardRoamingInteraction()
        {
            // 先记录手势类型，重置触点后仍需提交双指手势历史并结束插件视口变换。
            var wasTwoFingerGesture = _isBoardRoamingTwoFingerGesture;
            ResetBoardRoamingGestureState();
            if (wasTwoFingerGesture)
                EndBoardRoamingTwoFingerGesture();
            else
                EndBoardRoaming();
        }

        private void ResetBoardRoamingContactState() => ResetBoardRoamingGestureState();

        private void ResetBoardRoamingGestureState()
        {
            var hadContacts = _boardRoamingContacts.Count > 0 || _boardRoamingContactIds.Count > 0;
            _boardRoamingContacts.Clear();
            _boardRoamingContactIds.Clear();
            _boardRoamingPrimaryContactId = -1;
            _isBoardRoamingMultiTouchSuppressed = false;
            _isBoardRoamingTwoFingerGesture = false;
            _isBoardRoamingPopupDragActive = false;
            _isBoardRoamingPopupDrag = false;
            _isBoardRoamingSingleFingerPending = false;
            if (hadContacts)
            {
                inkCanvas.ReleaseStylusCapture();
                inkCanvas.ReleaseAllTouchCaptures();
            }
        }

        private void CancelBoardRoamingContact(int contactId)
        {
            if (!_boardRoamingContactIds.Contains(contactId)) return;
            _isBoardRoamingMultiTouchSuppressed = true;
            _boardRoamingPrimaryContactId = -1;
            EndBoardRoaming();
            EndBoardRoamingContact(contactId);
        }

        /// <summary>
        /// 漫游模式下的接触点落下：第 1 指开始单指拖动，第 2 指切换为双指手势。
        /// </summary>
        private void BeginBoardRoamingContact(int contactId, Point point)
        {
            if (!IsBoardRoamingMode || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;
            if (_boardRoamingContacts.FindIndex(c => c.Key == contactId) >= 0) return;
            if (_boardRoamingContacts.Count == 0)
            {
                var transform = GetBoardRoamingInputTransform(inkCanvas);
                if (!IsFinite(transform) || !transform.HasInverse) return;
                transform.Invert();
                _boardRoamingContactInputInverse = transform;
                _boardRoamingPrimaryContactId = contactId;
            }
            point = _boardRoamingContactInputInverse.Transform(GetBoardRoamingInputTransform(inkCanvas).Transform(point));
            _boardRoamingContacts.Add(new KeyValuePair<int, Point>(contactId, point));
            _boardRoamingContactIds.Add(contactId);

            if (_boardRoamingContacts.Count == 1)
            {
                // 先挂起，等位移越过激活阈值再启动单指拖动（见 MoveBoardRoamingContact）。
                _isBoardRoamingSingleFingerPending = true;
                _boardRoamingPendingStartPoint = point;
            }
            else if (_boardRoamingContacts.Count == 2 && !_isBoardRoamingTwoFingerGesture)
            {
                BeginBoardRoamingTwoFingerGesture();
            }
        }

        private void MoveBoardRoamingContact(int contactId, Point point)
        {
            var index = _boardRoamingContacts.FindIndex(c => c.Key == contactId);
            if (index < 0 || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;

            if (!IsBoardRoamingMode)
            {
                CancelBoardRoamingInteraction();
                return;
            }

            var inputPoint = point;
            // 接触点始终使用首指落下时的坐标系，切换显示层平移/双指手势时不跳变。
            point = _boardRoamingContactInputInverse.Transform(GetBoardRoamingInputTransform(inkCanvas).Transform(point));
            var previousPoint = _boardRoamingContacts[index].Value;

            if (_isBoardRoamingTwoFingerGesture && _boardRoamingContacts.Count >= 2)
            {
                var previousFirst = _boardRoamingContacts[0].Value;
                var previousSecond = _boardRoamingContacts[1].Value;
                _boardRoamingContacts[index] = new KeyValuePair<int, Point>(contactId, point);
                ApplyBoardRoamingTwoFingerGesture(previousFirst, previousSecond,
                    _boardRoamingContacts[0].Value, _boardRoamingContacts[1].Value);
                RefreshBoardRoamingPopup(false);
                return;
            }

            _boardRoamingContacts[index] = new KeyValuePair<int, Point>(contactId, point);

            if (_isBoardRoamingTwoFingerGesture)
            {
                // 双指手势进行中但仅剩一指：继续按该指平移（与批注态 Manipulation 一致）。
                var delta = point - previousPoint;
                if (delta.X != 0 || delta.Y != 0)
                {
                    var matrix = Matrix.Identity;
                    matrix.Translate(delta.X, delta.Y);
                    TransformBoardRoamingContent(matrix, delta, 1);
                    RefreshBoardRoamingPopup(false);
                }
                return;
            }

            if (_isBoardRoamingSingleFingerPending)
            {
                var offset = point - _boardRoamingPendingStartPoint;
                if (offset.Length < BoardRoamingSingleFingerActivationThreshold) return;

                // 激活单指拖动：以当前位置为拖动起点，本次移动不产生位移（无跳变）。
                _isBoardRoamingSingleFingerPending = false;
                BeginBoardRoaming(inputPoint);
                return;
            }

            MoveBoardRoaming(inputPoint);
        }

        /// <summary>
        /// 漫游模式下的接触点抬起。全部抬起时提交手势历史。
        /// </summary>
        private void EndBoardRoamingContact(int contactId)
        {
            var index = _boardRoamingContacts.FindIndex(c => c.Key == contactId);
            if (index >= 0) _boardRoamingContacts.RemoveAt(index);
            _boardRoamingContactIds.Remove(contactId);
            if (contactId == _boardRoamingPrimaryContactId)
                _boardRoamingPrimaryContactId = _boardRoamingContacts.Count > 0 ? _boardRoamingContacts[0].Key : -1;

            if (_isBoardRoamingTwoFingerGesture)
            {
                if (_boardRoamingContacts.Count == 0)
                    EndBoardRoamingTwoFingerGesture();
                return;
            }

            if (_boardRoamingContacts.Count == 0)
            {
                _isBoardRoamingSingleFingerPending = false;
                EndBoardRoaming();
            }
        }

        /// <summary>
        /// 退出漫游并返回批注模式。
        /// </summary>
        internal void ExitBoardRoamingMode()
        {
            if (!IsBoardRoamingMode)
            {
                if (BoardRoamingPopup != null)
                    BoardRoamingPopup.IsOpen = false;
                ResetBoardRoamingGestureState();
                return;
            }

            var hadActiveBoardRoamingInput = _boardRoamingContacts.Count > 0
                || _isBoardRoamingPointerDown
                || _isBoardRoamingTwoFingerGesture
                || _isBoardRoamingPopupDragActive;

            if (_isBoardRoamingTwoFingerGesture)
                EndBoardRoamingTwoFingerGesture();
            else if (_isBoardRoamingPopupDragActive)
                EndBoardRoamingPopupDrag();
            else
                EndBoardRoaming();

            ResetBoardRoamingGestureState();
            if (hadActiveBoardRoamingInput)
            {
                inkCanvas.ReleaseStylusCapture();
                inkCanvas.ReleaseAllTouchCaptures();
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            }
            if (BoardRoamingPopup != null)
                BoardRoamingPopup.IsOpen = false;

            // 复用现有批注入口，确保编辑模式、工具栏高亮和原生墨迹状态同步。
            PenIcon_Click(null, null);
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private void BeginBoardRoamingTwoFingerGesture()
        {
            // 取消单指挂起并提交可能存在的单指拖动阶段历史，双指手势单独成一步撤销记录。
            _isBoardRoamingSingleFingerPending = false;
            EndBoardRoaming();
            AttachBoardRoamingInputEvents();

            _isBoardRoamingTwoFingerGesture = true;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();
        }

        private void EndBoardRoamingTwoFingerGesture()
        {
            _isBoardRoamingTwoFingerGesture = false;
            _isBoardRoamingPointerDown = false;
            CancelBoardRoamingPreviewRefresh();
            try
            {
                CommitBoardRoamingHistory();
                CompletePluginCanvasViewportTransform();
            }
            finally
            {
                ResetBoardRoamingGestureState();
                _boardRoamingStrokeHistory = null;
                _boardRoamingViewportWorldPosition = new Point();
                _boardRoamingPreviewDirty = true;
                inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
                if (BoardRoamingPopup?.IsOpen == true)
                    QueueBoardRoamingPreviewRefresh();
            }
        }

        /// <summary>
        /// 双指手势：以两指连线中点为锚点——内容跟随指心平移，按两指间距比例
        /// 等比缩放（非变形，横纵向同比例），旋转按两指角度变化绕中点进行。
        /// </summary>
        private void ApplyBoardRoamingTwoFingerGesture(Point previousFirst, Point previousSecond, Point currentFirst, Point currentSecond)
        {
            bool enableZoom = Settings.Gesture.IsEnableTwoFingerZoomRoaming;
            bool enableRotate = Settings.Gesture.IsEnableTwoFingerRotationRoaming;

            var previousMidpoint = new Point(
                (previousFirst.X + previousSecond.X) / 2,
                (previousFirst.Y + previousSecond.Y) / 2);
            var currentMidpoint = new Point(
                (currentFirst.X + currentSecond.X) / 2,
                (currentFirst.Y + currentSecond.Y) / 2);

            var m = new Matrix();
            double scale = 1;

            if (enableZoom)
            {
                var previousDistance = GetDistance(previousFirst, previousSecond);
                var currentDistance = GetDistance(currentFirst, currentSecond);
                if (previousDistance > 0.001 && currentDistance > 0.001)
                {
                    scale = currentDistance / previousDistance;
                    m.ScaleAt(scale, scale, previousMidpoint.X, previousMidpoint.Y);
                }
            }

            if (enableRotate)
            {
                var previousAngle = Math.Atan2(previousSecond.Y - previousFirst.Y, previousSecond.X - previousFirst.X) * 180 / Math.PI;
                var currentAngle = Math.Atan2(currentSecond.Y - currentFirst.Y, currentSecond.X - currentFirst.X) * 180 / Math.PI;
                m.RotateAt(currentAngle - previousAngle, previousMidpoint.X, previousMidpoint.Y);
            }

            var trans = currentMidpoint - previousMidpoint;
            m.Translate(trans.X, trans.Y);

            TransformBoardRoamingContent(m, trans, scale);
        }

        /// <summary>
        /// 对板书内容应用任意矩阵（平移/等比缩放/旋转），同步图片、圆圈标注、插件视口、
        /// 视口世界坐标与视频展台预览。平移分量单独传给展台（展台不支持缩放旋转）。
        /// </summary>
        private void TransformBoardRoamingContent(Matrix matrix, Vector translationDelta, double scale)
        {
            var previousCommitType = _currentCommitType;
            var previousHistoryCommit = _isCommittingBoardRoaming;
            _currentCommitType = CommitReason.CodeInput;
            _isCommittingBoardRoaming = true;
            try
            {
                foreach (var stroke in inkCanvas.Strokes)
                {
                    stroke.Transform(matrix, false);
                    if (scale != 1)
                    {
                        try
                        {
                            stroke.DrawingAttributes.Width *= scale;
                            stroke.DrawingAttributes.Height *= scale;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }

                TransformCanvasImages(matrix);

                foreach (var circle in circles)
                {
                    circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                        circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                    circle.Centroid = new Point(
                        (circle.Stroke.StylusPoints[0].X +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                        (circle.Stroke.StylusPoints[0].Y +
                         circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                }

                PublishPluginCanvasViewportTransform(matrix);

                if (_isVideoPresenterSpecialMode)
                {
                    _boothPreviewTranslateX += translationDelta.X;
                    _boothPreviewTranslateY += translationDelta.Y;
                    ApplyBoothPreviewTransform();
                    ResetRotationBaseline();
                }

                // 视口世界坐标跟随内容变换的逆矩阵（纯平移时等价于 pos -= delta，与既有行为一致）。
                if (matrix.HasInverse)
                {
                    var inverse = matrix;
                    inverse.Invert();
                    var origin = inverse.Transform(new Point(0, 0));
                    _boardRoamingViewportWorldPosition = new Point(
                        _boardRoamingViewportWorldPosition.X + origin.X,
                        _boardRoamingViewportWorldPosition.Y + origin.Y);
                }
            }
            finally
            {
                _isCommittingBoardRoaming = previousHistoryCommit;
                _currentCommitType = previousCommitType;
                _boardRoamingPreviewDirty = true;
            }
        }

        private void CommitBoardRoamingHistory()
        {
            var delta = _boardRoamingPendingTranslation;
            if (delta.X != 0 || delta.Y != 0)
            {
                var strokes = new StrokeCollection(inkCanvas.Strokes);
                _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>(strokes.Count);
                foreach (var stroke in strokes)
                    _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();

                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                _isCommittingBoardRoaming = true;
                try
                {
                    var matrix = Matrix.Identity;
                    matrix.Translate(delta.X, delta.Y);
                    foreach (var stroke in strokes)
                        stroke.Transform(matrix, false);
                    foreach (UIElement child in inkCanvas.Children)
                    {
                        var transform = child.RenderTransform?.Value ?? Matrix.Identity;
                        transform.Translate(delta.X, delta.Y);
                        child.SetCurrentValue(UIElement.RenderTransformProperty, new MatrixTransform(transform));
                    }
                    if (circles != null)
                        foreach (var circle in circles)
                            circle.Centroid += delta;
                    PublishPluginCanvasViewportTransform(matrix);
                }
                finally
                {
                    _isCommittingBoardRoaming = false;
                    _currentCommitType = previousCommitType;
                }
            }

            // 双指及插件联动已实时修改内容，复用开始时的快照，不再应用一次平移。
            if (_boardRoamingStrokeHistory == null) return;
            var currentStrokes = new HashSet<Stroke>(inkCanvas.Strokes);
            var history = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>(_boardRoamingStrokeHistory.Count);
            foreach (var item in _boardRoamingStrokeHistory)
            {
                if (!currentStrokes.Contains(item.Key)) continue;

                var current = item.Key.StylusPoints.Clone();
                if (!AreStylusPointsEqual(item.Value, current))
                    history[item.Key] = Tuple.Create(item.Value, current);
            }

            if (history.Count > 0)
            {
                timeMachine.CommitStrokeManipulationHistory(history);
                foreach (var item in history)
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
            }
            // 双指缩放还修改笔宽，几何和绘制属性的历史都需要提交并清理。
            if (DrawingAttributesHistory?.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                    item.Value.Clear();
            }

            if (history.Count > 0 || inkCanvas.Children.Count > 0)
                MarkCurrentPageInkChanged();
            if (_isVideoPresenterSpecialMode)
                ResetRotationBaseline();
            _boardRoamingStrokeHistory = null;
            _boardRoamingViewportWorldPosition = new Point();
            _boardRoamingPreviewDirty = true;
        }

        private void ShowBoardRoamingPopup()
        {
            if (BoardRoamingPopup == null || BoardRoamingPopupContent == null) return;

            AttachBoardRoamingPopupEvents();
            SyncBoardRoamingToggleStates();
            BoardRoamingPopup.IsOpen = false;
            RefreshBoardRoamingPopup(true, true);
            AnimationsHelper.ShowPopupWithSlideAndFade(BoardRoamingPopup);
            _popupManager?.BringToFront(BoardRoamingPopup);
        }

        private void SyncBoardRoamingToggleStates()
        {
            _isSyncingBoardRoamingToggles = true;
            try
            {
                if (BoardRoamingPopupContent.TwoFingerZoomToggle != null)
                    BoardRoamingPopupContent.TwoFingerZoomToggle.IsOn = Settings.Gesture.IsEnableTwoFingerZoomRoaming;
                if (BoardRoamingPopupContent.TwoFingerRotationToggle != null)
                    BoardRoamingPopupContent.TwoFingerRotationToggle.IsOn = Settings.Gesture.IsEnableTwoFingerRotationRoaming;
            }
            finally
            {
                _isSyncingBoardRoamingToggles = false;
            }
        }

        private void AttachBoardRoamingPopupEvents()
        {
            if (_boardRoamingPopupEventsAttached || BoardRoamingPopupContent == null) return;

            BoardRoamingPopupContent.ViewportPositionChanged += BoardRoamingPopupContent_ViewportPositionChanged;
            BoardRoamingPopupContent.ViewportDragStarted += BeginBoardRoamingPopupDrag;
            BoardRoamingPopupContent.ViewportDragCompleted += EndBoardRoamingPopupDrag;
            BoardRoamingPopup.Closed += (s, e) =>
            {
                CancelBoardRoamingPreviewRefresh();
                BoardRoamingPopupContent.CompleteViewportDrag();
            };
            if (BoardRoamingPopupContent.CloseButtonControl != null)
            {
                BoardRoamingPopupContent.CloseButtonControl.Click += (s, e) => ExitBoardRoamingMode();
                // 按下阶段立即关闭，避免首次点击被拖拽/捕获等逻辑吞掉导致需要点两次。
                BoardRoamingPopupContent.CloseButtonControl.PreviewMouseLeftButtonDown +=
                    BoardRoamingCloseButton_PreviewInputDown;
                BoardRoamingPopupContent.CloseButtonControl.PreviewStylusDown +=
                    BoardRoamingCloseButton_PreviewStylusDown;
            }
            if (BoardRoamingPopupContent.TwoFingerZoomToggle != null)
                BoardRoamingPopupContent.TwoFingerZoomToggle.Toggled += BoardRoamingTwoFingerZoom_Toggled;
            if (BoardRoamingPopupContent.TwoFingerRotationToggle != null)
                BoardRoamingPopupContent.TwoFingerRotationToggle.Toggled += BoardRoamingTwoFingerRotation_Toggled;
            _boardRoamingPopupEventsAttached = true;
        }

        private void BoardRoamingCloseButton_PreviewInputDown(object sender, MouseButtonEventArgs e)
        {
            ExitBoardRoamingMode();
            e.Handled = true;
        }

        private void BoardRoamingCloseButton_PreviewStylusDown(object sender, StylusDownEventArgs e)
        {
            ExitBoardRoamingMode();
            e.Handled = true;
        }

        private void BoardRoamingTwoFingerZoom_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingBoardRoamingToggles) return;
            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            if (toggle == null) return;
            Settings.Gesture.IsEnableTwoFingerZoomRoaming = toggle.IsOn;
            SaveSettingsToFile();
        }

        private void BoardRoamingTwoFingerRotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingBoardRoamingToggles) return;
            var toggle = sender as iNKORE.UI.WPF.Modern.Controls.ToggleSwitch;
            if (toggle == null) return;
            Settings.Gesture.IsEnableTwoFingerRotationRoaming = toggle.IsOn;
            SaveSettingsToFile();
        }

        private void RefreshBoardRoamingPopup()
        {
            RefreshBoardRoamingPopup(true);
        }

        private void RefreshBoardRoamingPopup(bool updateBounds)
            => RefreshBoardRoamingPopup(updateBounds, false);

        private void RefreshBoardRoamingPopup(bool updateBounds, bool forceRender)
        {
            if (!IsBoardRoamingMode || BoardRoamingPopupContent == null || _isUpdatingBoardRoamingPopup
                || (!forceRender && BoardRoamingPopup?.IsOpen != true)
                || !double.IsFinite(inkCanvas.ActualWidth) || !double.IsFinite(inkCanvas.ActualHeight)
                || inkCanvas.ActualWidth <= 0 || inkCanvas.ActualHeight <= 0
                || !double.IsFinite(_boardRoamingViewportWorldPosition.X)
                || !double.IsFinite(_boardRoamingViewportWorldPosition.Y))
                return;

            var viewport = new Rect(_boardRoamingViewportWorldPosition,
                new Size(inkCanvas.ActualWidth, inkCanvas.ActualHeight));
            var worldBounds = _boardRoamingWorldBounds;
            if (updateBounds || !IsValidBoardRoamingBounds(worldBounds))
            {
                var contentBounds = GetBoardRoamingContentBounds();
                if (!contentBounds.IsEmpty)
                    contentBounds.Offset(_boardRoamingViewportWorldPosition.X + _boardRoamingPendingTranslation.X,
                        _boardRoamingViewportWorldPosition.Y + _boardRoamingPendingTranslation.Y);
                worldBounds = Rect.Union(viewport, contentBounds);
                worldBounds.Inflate(Math.Max(viewport.Width * 0.5, 1), Math.Max(viewport.Height * 0.5, 1));
            }
            else
            {
                // 靠近任一边缘时预留半屏空间，让弹窗内的漫游可以持续向外扩展。
                var horizontalThreshold = Math.Max(viewport.Width * 0.1, 1);
                var verticalThreshold = Math.Max(viewport.Height * 0.1, 1);
                if (viewport.Left - worldBounds.Left <= horizontalThreshold
                    || worldBounds.Right - viewport.Right <= horizontalThreshold
                    || viewport.Top - worldBounds.Top <= verticalThreshold
                    || worldBounds.Bottom - viewport.Bottom <= verticalThreshold)
                {
                    var expandedViewport = viewport;
                    expandedViewport.Inflate(Math.Max(viewport.Width * 0.5, 1), Math.Max(viewport.Height * 0.5, 1));
                    worldBounds.Union(expandedViewport);
                }
            }
            if (!IsValidBoardRoamingBounds(worldBounds)) return;

            const double previewWidth = 352;
            const double previewHeight = 198;
            var scale = Math.Min(previewWidth / worldBounds.Width, previewHeight / worldBounds.Height);
            if (!double.IsFinite(scale) || scale <= 0) return;
            var renderedWidth = worldBounds.Width * scale;
            var renderedHeight = worldBounds.Height * scale;
            var offset = new Point((previewWidth - renderedWidth) / 2, (previewHeight - renderedHeight) / 2);
            var movementBounds = new Rect(offset, new Size(renderedWidth, renderedHeight));
            if (!IsValidBoardRoamingBounds(movementBounds)) return;

            var needsBitmap = updateBounds || _boardRoamingPreviewDirty || worldBounds != _boardRoamingWorldBounds
                || BoardRoamingPopupContent.PreviewImageControl.Source == null;
            if (needsBitmap && _isBoardRoamingPointerDown && !forceRender
                && BoardRoamingPopupContent.PreviewImageControl.Source != null)
            {
                // 拖动只移动视口框；世界边界扩展的重绘合并到下一帧。
                UpdateBoardRoamingViewport(viewport);
                QueueBoardRoamingPreviewRefresh();
                return;
            }

            var bitmap = needsBitmap ? RenderBoardRoamingPreview(worldBounds, previewWidth, previewHeight) : null;
            if (needsBitmap && bitmap == null) return;

            _boardRoamingWorldBounds = worldBounds;
            _boardRoamingPreviewScale = scale;
            _boardRoamingPreviewOffset = offset;
            _boardRoamingPreviewMovementBounds = movementBounds;
            if (bitmap != null)
                BoardRoamingPopupContent.PreviewImageControl.Source = bitmap;
            _boardRoamingPreviewDirty = false;
            UpdateBoardRoamingViewport(viewport);
        }

        private void UpdateBoardRoamingViewport(Rect viewport)
        {
            if (!IsValidBoardRoamingBounds(_boardRoamingWorldBounds)
                || !double.IsFinite(_boardRoamingPreviewScale) || _boardRoamingPreviewScale <= 0) return;

            var previewViewport = new Rect(
                _boardRoamingPreviewOffset.X + (viewport.X - _boardRoamingWorldBounds.X) * _boardRoamingPreviewScale,
                _boardRoamingPreviewOffset.Y + (viewport.Y - _boardRoamingWorldBounds.Y) * _boardRoamingPreviewScale,
                viewport.Width * _boardRoamingPreviewScale, viewport.Height * _boardRoamingPreviewScale);
            if (!IsValidBoardRoamingBounds(previewViewport)) return;
            _boardRoamingViewportInPreview = previewViewport;
            _isUpdatingBoardRoamingPopup = true;
            try
            {
                BoardRoamingPopupContent.SetViewport(previewViewport, _boardRoamingPreviewMovementBounds,
                    string.Format(FloatingBarStrings.Board_RoamingPanelScale,
                        Math.Round(_boardRoamingWorldBounds.Width / viewport.Width, 1)));
            }
            finally
            {
                _isUpdatingBoardRoamingPopup = false;
            }
        }

        private void QueueBoardRoamingPreviewRefresh()
        {
            if (_boardRoamingPreviewRefreshOperation?.Status == DispatcherOperationStatus.Pending) return;
            _boardRoamingPreviewRefreshOperation = inkCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() =>
                {
                    _boardRoamingPreviewRefreshOperation = null;
                    if (BoardRoamingPopup?.IsOpen == true)
                        RefreshBoardRoamingPopup(false, true);
                }));
        }

        private void CancelBoardRoamingPreviewRefresh()
        {
            if (_boardRoamingPreviewRefreshOperation?.Status == DispatcherOperationStatus.Pending)
                _boardRoamingPreviewRefreshOperation.Abort();
            _boardRoamingPreviewRefreshOperation = null;
        }

        private static bool IsValidBoardRoamingBounds(Rect bounds)
            => !bounds.IsEmpty && double.IsFinite(bounds.X) && double.IsFinite(bounds.Y)
                && double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height)
                && double.IsFinite(bounds.Right) && double.IsFinite(bounds.Bottom)
                && bounds.Width > 0 && bounds.Height > 0;

        private Rect GetBoardRoamingContentBounds()
        {
            var result = Rect.Empty;
            foreach (var stroke in inkCanvas.Strokes)
            {
                var bounds = stroke.GetBounds();
                if (IsValidBoardRoamingBounds(bounds)) result.Union(bounds);
            }
            foreach (UIElement child in inkCanvas.Children)
            {
                if (child is FrameworkElement element
                    && TryGetBoardRoamingElementBounds(element, out var bounds, out var parentTransform))
                {
                    bounds.Transform(parentTransform);
                    if (IsValidBoardRoamingBounds(bounds)) result.Union(bounds);
                }
            }
            return result;
        }

        private bool TryGetBoardRoamingElementBounds(FrameworkElement element, out Rect bounds, out Matrix parentTransform)
        {
            bounds = Rect.Empty;
            parentTransform = Matrix.Identity;
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return false;
            try
            {
                if (VisualTreeHelper.GetParent(element) is not Visual parent) return false;
                var localBounds = VisualTreeHelper.GetDescendantBounds(element);
                if (!IsValidBoardRoamingBounds(localBounds)) return false;
                bounds = element.TransformToAncestor(parent).TransformBounds(localBounds);
                if (!IsValidBoardRoamingBounds(bounds)) return false;

                var transform = parent.TransformToAncestor(inkCanvas);
                var origin = transform.Transform(new Point());
                var xAxis = transform.Transform(new Point(1, 0)) - origin;
                var yAxis = transform.Transform(new Point(0, 1)) - origin;
                parentTransform = new Matrix(xAxis.X, xAxis.Y, yAxis.X, yAxis.Y, origin.X, origin.Y);
                return double.IsFinite(xAxis.X) && double.IsFinite(xAxis.Y)
                    && double.IsFinite(yAxis.X) && double.IsFinite(yAxis.Y)
                    && double.IsFinite(origin.X) && double.IsFinite(origin.Y);
            }
            catch (InvalidOperationException ex)
            {
                LogHelper.WriteLogToFile($"[Board] 计算子元素边界失败，元素可能不在画布可视树内: {ex.Message}", LogHelper.LogType.Info);
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private BitmapSource RenderBoardRoamingPreview(
            Rect worldBounds,
            double previewWidth,
            double previewHeight)
        {
            if (!IsValidBoardRoamingBounds(worldBounds)
                || !double.IsFinite(previewWidth) || !double.IsFinite(previewHeight)
                || previewWidth <= 0 || previewHeight <= 0
                || previewWidth > int.MaxValue || previewHeight > int.MaxValue) return null;

            var scale = Math.Min(previewWidth / worldBounds.Width, previewHeight / worldBounds.Height);
            if (!double.IsFinite(scale) || scale <= 0) return null;
            var renderedWidth = worldBounds.Width * scale;
            var renderedHeight = worldBounds.Height * scale;
            var offset = new Point((previewWidth - renderedWidth) / 2, (previewHeight - renderedHeight) / 2);
            var movementBounds = new Rect(offset, new Size(renderedWidth, renderedHeight));
            if (!IsValidBoardRoamingBounds(movementBounds)) return null;
            var translationX = offset.X + (_boardRoamingViewportWorldPosition.X
                + _boardRoamingPendingTranslation.X - worldBounds.X) * scale;
            var translationY = offset.Y + (_boardRoamingViewportWorldPosition.Y
                + _boardRoamingPendingTranslation.Y - worldBounds.Y) * scale;
            if (!double.IsFinite(translationX) || !double.IsFinite(translationY)) return null;

            try
            {
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, previewWidth, previewHeight));
                    context.DrawRectangle(GridBackgroundCover.Background ?? Brushes.White, null, movementBounds);
                    context.PushClip(new RectangleGeometry(movementBounds));
                    context.PushTransform(new MatrixTransform(new Matrix(scale, 0, 0, scale, translationX, translationY)));

                    // 子元素的 VisualBrush 已包含自身位移/旋转，按父坐标绘制，避免重复变换。
                    foreach (UIElement child in inkCanvas.Children)
                    {
                        if (child is not FrameworkElement element
                            || !TryGetBoardRoamingElementBounds(element, out var bounds, out var parentTransform)) continue;
                        context.PushTransform(new MatrixTransform(parentTransform));
                        var brush = new VisualBrush(element)
                        {
                            AutoLayoutContent = false,
                            Stretch = Stretch.Fill,
                            ViewboxUnits = BrushMappingMode.Absolute,
                            Viewbox = bounds
                        };
                        context.DrawRectangle(brush, null, bounds);
                        context.Pop();
                    }
                    // 直接绘制内容，不读取实时 InkCanvas 的空/正在重排的视觉边界。
                    inkCanvas.Strokes.Draw(context);
                    context.Pop();
                    context.Pop();
                }
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(previewWidth), (int)Math.Ceiling(previewHeight),
                    96, 96, PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"生成漫游预览失败: {ex}; world={worldBounds}, preview={previewWidth}x{previewHeight}",
                    LogHelper.LogType.Warning);
                return null;
            }
        }

        private void BoardRoamingPopupContent_ViewportPositionChanged(Point previewPosition)
        {
            if (_isUpdatingBoardRoamingPopup || !IsBoardRoamingMode || !double.IsFinite(_boardRoamingPreviewScale)
                || _boardRoamingPreviewScale <= 0 || !double.IsFinite(previewPosition.X)
                || !double.IsFinite(previewPosition.Y)) return;

            var targetViewportX = _boardRoamingWorldBounds.X +
                                  (previewPosition.X - _boardRoamingPreviewOffset.X) / _boardRoamingPreviewScale;
            var targetViewportY = _boardRoamingWorldBounds.Y +
                                  (previewPosition.Y - _boardRoamingPreviewOffset.Y) / _boardRoamingPreviewScale;
            var deltaX = _boardRoamingViewportWorldPosition.X - targetViewportX;
            var deltaY = _boardRoamingViewportWorldPosition.Y - targetViewportY;
            if (Math.Abs(deltaX) < 0.01 && Math.Abs(deltaY) < 0.01)
            {
                RefreshBoardRoamingPopup(false);
                return;
            }

            TranslateBoardRoamingContent(deltaX, deltaY);
        }

        private void BeginBoardRoamingPopupDrag()
        {
            if (_isBoardRoamingPopupDragActive || _isBoardRoamingPointerDown || _boardRoamingContacts.Count > 0) return;
            BeginBoardRoaming(new Point());
            _isBoardRoamingPopupDrag = _isBoardRoamingPointerDown;
            _isBoardRoamingPopupDragActive = _isBoardRoamingPointerDown;
        }

        private void EndBoardRoamingPopupDrag()
        {
            if (!_isBoardRoamingPopupDragActive) return;
            EndBoardRoaming();
        }

        private void TranslateBoardRoamingContent(double deltaX, double deltaY)
        {
            if (!_isBoardRoamingPointerDown || !double.IsFinite(deltaX) || !double.IsFinite(deltaY)) return;

            if (_boardRoamingRenderTranslation == null)
            {
                var matrix = Matrix.Identity;
                matrix.Translate(deltaX, deltaY);
                TransformBoardRoamingContent(matrix, new Vector(deltaX, deltaY), 1);
                if (!_isBoardRoamingPopupDrag) RefreshBoardRoamingPopup(false);
                return;
            }

            var translation = _boardRoamingPendingTranslation + new Vector(deltaX, deltaY);
            if (!double.IsFinite(translation.X) || !double.IsFinite(translation.Y)) return;
            _boardRoamingPendingTranslation = translation;
            _boardRoamingRenderTranslation.X = translation.X;
            _boardRoamingRenderTranslation.Y = translation.Y;
            _boardRoamingViewportWorldPosition = new Point(-translation.X, -translation.Y);
            if (_isVideoPresenterSpecialMode)
            {
                _boothPreviewTranslateX = _boardRoamingBoothStartX + translation.X;
                _boothPreviewTranslateY = _boardRoamingBoothStartY + translation.Y;
                ApplyBoothPreviewTransform();
            }
            RefreshBoardRoamingPopup(false);
        }

        private static bool AreStylusPointsEqual(StylusPointCollection first, StylusPointCollection second)
        {
            if (first.Count != second.Count) return false;
            for (var i = 0; i < first.Count; i++)
            {
                if (first[i].X != second[i].X || first[i].Y != second[i].Y)
                    return false;
            }
            return true;
        }
    }
}
