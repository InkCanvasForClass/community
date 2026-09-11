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
        // 漫游触摸接触点（StylusDevice.Id -> 最近位置，按落下顺序排列）。
        // 漫游模式下 Stylus 事件全部标记 Handled，WPF 不会把它们提升为 Touch/Manipulation，
        // 双指手势必须直接由接触点计算（WispLogic.PromoteMainToOther：Stylus Handled 即跳过提升）。
        private readonly List<KeyValuePair<int, Point>> _boardRoamingContacts = new List<KeyValuePair<int, Point>>();
        private bool _isBoardRoamingTwoFingerGesture;
        private bool _isBoardRoamingPopupDragActive;
        // 单指拖动挂起：第一指落下后先不拖动，位移超过阈值才激活，
        // 避免放置第二指过程中的手部漂移带动板书产生异常位移。
        private bool _isBoardRoamingSingleFingerPending;
        private Point _boardRoamingPendingStartPoint;
        private const double BoardRoamingSingleFingerActivationThreshold = 10.0;

        internal void ActivateBoardRoamingMode()
        {
            if (currentMode != 1) return;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation();
                return;
            }

            HideEdgeExpandHint();
            ResetBoardRoamingGestureState();
            ResetTouchStates();
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
            ShowBoardRoamingPopup();
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
            if (!IsBoardRoamingMode || _isBoardRoamingPointerDown || IsCurrentPageFrozen) return;

            _isBoardRoamingPointerDown = true;
            _boardRoamingLastPoint = point;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();

            inkCanvas.Cursor = Cursors.Hand;
        }

        private void MoveBoardRoaming(Point point)
        {
            if (!_isBoardRoamingPointerDown || !IsBoardRoamingMode) return;

            var delta = point - _boardRoamingLastPoint;
            if (delta.X == 0 && delta.Y == 0) return;

            // 视口世界坐标由 TransformBoardRoamingContent 按逆矩阵统一维护
            TranslateBoardRoamingContent(delta.X, delta.Y);

            _boardRoamingLastPoint = point;
            RefreshBoardRoamingPopup(false);
        }

        private void EndBoardRoaming()
        {
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CommitBoardRoamingHistory();
            CompletePluginCanvasViewportTransform();
            inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
        }

        /// <summary>
        /// 强制结束漫游交互（单指拖动/双指手势），用于切换工具等场景。
        /// </summary>
        private void CancelBoardRoamingInteraction()
        {
            ResetBoardRoamingGestureState();
            EndBoardRoaming();
        }

        private void ResetBoardRoamingGestureState()
        {
            _boardRoamingContacts.Clear();
            _isBoardRoamingTwoFingerGesture = false;
            _isBoardRoamingPopupDragActive = false;
            _isBoardRoamingSingleFingerPending = false;
        }

        /// <summary>
        /// 漫游模式下的接触点落下：第 1 指开始单指拖动，第 2 指切换为双指手势。
        /// </summary>
        private void BeginBoardRoamingContact(int contactId, Point point)
        {
            if (!IsBoardRoamingMode) return;
            if (_boardRoamingContacts.FindIndex(c => c.Key == contactId) >= 0) return;

            _boardRoamingContacts.Add(new KeyValuePair<int, Point>(contactId, point));

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
            if (index < 0) return;

            if (!IsBoardRoamingMode)
            {
                ResetBoardRoamingGestureState();
                EndBoardRoaming();
                return;
            }

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
                BeginBoardRoaming(point);
                return;
            }

            MoveBoardRoaming(point);
        }

        /// <summary>
        /// 漫游模式下的接触点抬起。全部抬起时提交手势历史。
        /// </summary>
        private void EndBoardRoamingContact(int contactId)
        {
            var index = _boardRoamingContacts.FindIndex(c => c.Key == contactId);
            if (index >= 0) _boardRoamingContacts.RemoveAt(index);

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

        private void BeginBoardRoamingTwoFingerGesture()
        {
            // 取消单指挂起并提交可能存在的单指拖动阶段历史，双指手势单独成一步撤销记录。
            _isBoardRoamingSingleFingerPending = false;
            EndBoardRoaming();

            _isBoardRoamingTwoFingerGesture = true;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();
        }

        private void EndBoardRoamingTwoFingerGesture()
        {
            _isBoardRoamingTwoFingerGesture = false;
            _isBoardRoamingPointerDown = false;
            CommitBoardRoamingHistory();
            CompletePluginCanvasViewportTransform();
            inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
            RefreshBoardRoamingPopup();
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
            _currentCommitType = CommitReason.CodeInput;
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
                _currentCommitType = previousCommitType;
            }
        }

        private void CommitBoardRoamingHistory()
        {
            if (_boardRoamingStrokeHistory == null) return;

            var history = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            foreach (var item in _boardRoamingStrokeHistory)
            {
                if (!inkCanvas.Strokes.Contains(item.Key)) continue;

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

            _boardRoamingStrokeHistory = null;
            _boardRoamingViewportWorldPosition = new Point();
        }

        private void ShowBoardRoamingPopup()
        {
            if (BoardRoamingPopup == null || BoardRoamingPopupContent == null) return;

            AttachBoardRoamingPopupEvents();
            SyncBoardRoamingToggleStates();
            BoardRoamingPopup.IsOpen = false;
            RefreshBoardRoamingPopup();
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
            if (BoardRoamingPopupContent.CloseButtonControl != null)
            {
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
            BoardRoamingPopup.IsOpen = false;
            e.Handled = true;
        }

        private void BoardRoamingCloseButton_PreviewStylusDown(object sender, StylusDownEventArgs e)
        {
            BoardRoamingPopup.IsOpen = false;
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
        {
            if (!IsBoardRoamingMode || BoardRoamingPopupContent == null || inkCanvas.ActualWidth <= 0 || inkCanvas.ActualHeight <= 0)
                return;

            var viewport = new Rect(_boardRoamingViewportWorldPosition.X, _boardRoamingViewportWorldPosition.Y,
                inkCanvas.ActualWidth, inkCanvas.ActualHeight);
            if (updateBounds || _boardRoamingWorldBounds.IsEmpty)
            {
                var contentBounds = GetBoardRoamingContentBounds();
                var horizontalPadding = Math.Max(viewport.Width * 0.5, 1);
                var verticalPadding = Math.Max(viewport.Height * 0.5, 1);

                _boardRoamingWorldBounds = Rect.Union(viewport, contentBounds);
                _boardRoamingWorldBounds.Inflate(horizontalPadding, verticalPadding);
            }

            const double previewWidth = 352;
            const double previewHeight = 198;
            _boardRoamingPreviewScale = Math.Min(previewWidth / _boardRoamingWorldBounds.Width, previewHeight / _boardRoamingWorldBounds.Height);
            var renderedWidth = _boardRoamingWorldBounds.Width * _boardRoamingPreviewScale;
            var renderedHeight = _boardRoamingWorldBounds.Height * _boardRoamingPreviewScale;
            var offsetX = (previewWidth - renderedWidth) / 2;
            var offsetY = (previewHeight - renderedHeight) / 2;
            _boardRoamingPreviewOffset = new Point(offsetX, offsetY);
            _boardRoamingPreviewMovementBounds = new Rect(offsetX, offsetY, renderedWidth, renderedHeight);

            _boardRoamingViewportInPreview = new Rect(
                offsetX + (viewport.X - _boardRoamingWorldBounds.X) * _boardRoamingPreviewScale,
                offsetY + (viewport.Y - _boardRoamingWorldBounds.Y) * _boardRoamingPreviewScale,
                viewport.Width * _boardRoamingPreviewScale,
                viewport.Height * _boardRoamingPreviewScale);

            _isUpdatingBoardRoamingPopup = true;
            try
            {
                BoardRoamingPopupContent.PreviewImageControl.Source = RenderBoardRoamingPreview(
                    _boardRoamingWorldBounds,
                    previewWidth,
                    previewHeight);
                BoardRoamingPopupContent.SetViewport(
                    _boardRoamingViewportInPreview,
                    _boardRoamingPreviewMovementBounds,
                    string.Format(FloatingBarStrings.Board_RoamingPanelScale,
                        Math.Round(_boardRoamingWorldBounds.Width / viewport.Width, 1)));
            }
            finally
            {
                _isUpdatingBoardRoamingPopup = false;
            }
        }

        private Rect GetBoardRoamingContentBounds()
        {
            var result = Rect.Empty;
            foreach (var stroke in inkCanvas.Strokes)
                result.Union(stroke.GetBounds());

            foreach (UIElement child in inkCanvas.Children)
            {
                if (child is not FrameworkElement element) continue;
                try
                {
                    var bounds = element.TransformToAncestor(inkCanvas)
                        .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    result.Union(bounds);
                }
                catch (InvalidOperationException)
                {
                }
            }

            return result.IsEmpty
                ? new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight)
                : result;
        }

        private BitmapSource RenderBoardRoamingPreview(
            Rect worldBounds,
            double previewWidth,
            double previewHeight)
        {
            try
            {
                var bitmapWidth = Math.Max(1, (int)Math.Ceiling(previewWidth));
                var bitmapHeight = Math.Max(1, (int)Math.Ceiling(previewHeight));
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, previewWidth, previewHeight));
                    var background = GridBackgroundCover.Background ?? Brushes.White;
                    context.DrawRectangle(background, null, _boardRoamingPreviewMovementBounds);

                    var visualBrush = new VisualBrush(inkCanvas)
                    {
                        Stretch = Stretch.Fill,
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = worldBounds,
                        ViewportUnits = BrushMappingMode.Absolute,
                        Viewport = _boardRoamingPreviewMovementBounds
                    };
                    context.DrawRectangle(visualBrush, null, _boardRoamingPreviewMovementBounds);
                }

                var bitmap = new RenderTargetBitmap(
                    bitmapWidth,
                    bitmapHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"生成漫游预览失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        private void BoardRoamingPopupContent_ViewportPositionChanged(Point previewPosition)
        {
            if (_isUpdatingBoardRoamingPopup || !IsBoardRoamingMode || _boardRoamingPreviewScale <= 0) return;

            var targetViewportX = _boardRoamingWorldBounds.X +
                                  (previewPosition.X - _boardRoamingPreviewOffset.X) / _boardRoamingPreviewScale;
            var targetViewportY = _boardRoamingWorldBounds.Y +
                                  (previewPosition.Y - _boardRoamingPreviewOffset.Y) / _boardRoamingPreviewScale;
            var deltaX = _boardRoamingViewportWorldPosition.X - targetViewportX;
            var deltaY = _boardRoamingViewportWorldPosition.Y - targetViewportY;
            if (Math.Abs(deltaX) < 0.01 && Math.Abs(deltaY) < 0.01) return;

            TranslateBoardRoamingContent(deltaX, deltaY);
            _boardRoamingViewportWorldPosition = new Point(targetViewportX, targetViewportY);
        }

        private void BeginBoardRoamingPopupDrag()
        {
            if (_isBoardRoamingPopupDragActive || _isBoardRoamingPointerDown || _boardRoamingContacts.Count > 0) return;

            _isBoardRoamingPopupDragActive = true;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();
        }

        private void EndBoardRoamingPopupDrag()
        {
            if (!_isBoardRoamingPopupDragActive) return;

            _isBoardRoamingPopupDragActive = false;
            CommitBoardRoamingHistory();
            CompletePluginCanvasViewportTransform();
            RefreshBoardRoamingPopup();
        }

        private void TranslateBoardRoamingContent(double deltaX, double deltaY)
        {
            var matrix = Matrix.Identity;
            matrix.Translate(deltaX, deltaY);
            TransformBoardRoamingContent(matrix, new Vector(deltaX, deltaY), 1);
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
