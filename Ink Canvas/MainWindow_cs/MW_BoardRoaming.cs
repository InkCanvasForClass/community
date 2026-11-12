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
            _boardRoamingOriginalRenderTransform = inkCanvas.RenderTransform;
            _boardRoamingRenderTranslation = new TranslateTransform();
            var transform = new TransformGroup();
            transform.Children.Add(_boardRoamingRenderTranslation);
            transform.Children.Add(_boardRoamingOriginalRenderTransform ?? Transform.Identity);
            inkCanvas.SetCurrentValue(UIElement.RenderTransformProperty, transform);
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
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CancelBoardRoamingPreviewRefresh();
            _isBoardRoamingPopupDrag = false;
            try
            {
                CommitBoardRoamingHistory();
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

        private void ResetBoardRoamingContactState()
        {
            var hadContacts = _boardRoamingContactIds.Count > 0;
            _boardRoamingContactIds.Clear();
            _boardRoamingPrimaryContactId = -1;
            _isBoardRoamingMultiTouchSuppressed = false;
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
        /// 记录漫游触点。漫游只允许第一根手指拖动；第二根及以后触点会抑制本次多指操作。
        /// </summary>
        private void BeginBoardRoamingContact(int contactId, Point point)
        {
            if (!IsBoardRoamingMode || !_boardRoamingContactIds.Add(contactId)) return;

            if (_boardRoamingContactIds.Count == 1 && !_isBoardRoamingMultiTouchSuppressed)
            {
                _boardRoamingPrimaryContactId = contactId;
                BeginBoardRoaming(point);
                return;
            }

            _isBoardRoamingMultiTouchSuppressed = true;
            _boardRoamingPrimaryContactId = -1;
            if (_isBoardRoamingPointerDown)
                EndBoardRoaming();
        }

        private void MoveBoardRoamingContact(int contactId, Point point)
        {
            if (!IsBoardRoamingMode
                || _isBoardRoamingMultiTouchSuppressed
                || contactId != _boardRoamingPrimaryContactId)
                return;

            MoveBoardRoaming(point);
        }

        private void EndBoardRoamingContact(int contactId)
        {
            if (!_boardRoamingContactIds.Remove(contactId)) return;

            if (_boardRoamingContactIds.Count == 0)
            {
                if (!_isBoardRoamingMultiTouchSuppressed)
                    EndBoardRoaming();
                ResetBoardRoamingContactState();
            }
            else if (contactId == _boardRoamingPrimaryContactId)
            {
                // 多指操作期间不恢复剩余手指，避免抬起第二指后重新跳动。
                _boardRoamingPrimaryContactId = -1;
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
                ResetBoardRoamingContactState();
                return;
            }

            var hadActiveBoardRoamingInput = _boardRoamingContactIds.Count > 0 || _isBoardRoamingPointerDown;
            EndBoardRoaming();
            ResetBoardRoamingContactState();
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

        private void CommitBoardRoamingHistory()
        {
            var delta = _boardRoamingPendingTranslation;
            if (delta.X == 0 && delta.Y == 0) return;

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
            }
            finally
            {
                _isCommittingBoardRoaming = false;
                _currentCommitType = previousCommitType;
            }

            var currentStrokes = new HashSet<Stroke>(inkCanvas.Strokes);
            var history = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>(strokes.Count);
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
            if (history.Count > 0 || inkCanvas.Children.Count > 0)
                MarkCurrentPageInkChanged();
            if (_isVideoPresenterSpecialMode)
                ResetRotationBaseline();
        }

        private void ShowBoardRoamingPopup()
        {
            if (BoardRoamingPopup == null || BoardRoamingPopupContent == null) return;

            AttachBoardRoamingPopupEvents();
            BoardRoamingPopup.IsOpen = false;
            RefreshBoardRoamingPopup(true, true);
            AnimationsHelper.ShowPopupWithSlideAndFade(BoardRoamingPopup);
            _popupManager?.BringToFront(BoardRoamingPopup);
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
                BoardRoamingPopupContent.CloseButtonControl.Click += (s, e) =>
                {
                    if (BoardRoamingPopup != null)
                        BoardRoamingPopup.IsOpen = false;
                };
            _boardRoamingPopupEventsAttached = true;
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
            catch (InvalidOperationException)
            {
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
            if (_isBoardRoamingPointerDown) return;
            BeginBoardRoaming(new Point());
            _isBoardRoamingPopupDrag = _isBoardRoamingPointerDown;
        }

        private void EndBoardRoamingPopupDrag()
            => EndBoardRoaming();

        private void TranslateBoardRoamingContent(double deltaX, double deltaY)
        {
            if (!_isBoardRoamingPointerDown || _boardRoamingRenderTranslation == null
                || !double.IsFinite(deltaX) || !double.IsFinite(deltaY)) return;

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
