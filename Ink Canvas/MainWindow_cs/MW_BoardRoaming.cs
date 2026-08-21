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

        internal void ActivateBoardRoamingMode()
        {
            if (currentMode != 1) return;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation();
                return;
            }

            HideEdgeExpandHint();
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

            TranslateBoardRoamingContent(delta.X, delta.Y);
            _boardRoamingViewportWorldPosition = new Point(
                _boardRoamingViewportWorldPosition.X - delta.X,
                _boardRoamingViewportWorldPosition.Y - delta.Y);

            _boardRoamingLastPoint = point;
            RefreshBoardRoamingPopup(false);
        }

        private void EndBoardRoaming()
        {
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CommitBoardRoamingHistory();
            inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
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
            BoardRoamingPopup.IsOpen = false;
            RefreshBoardRoamingPopup();
            AnimationsHelper.ShowPopupWithSlideAndFade(BoardRoamingPopup);
            _popupManager?.BringToFront(BoardRoamingPopup);
        }

        private void AttachBoardRoamingPopupEvents()
        {
            if (_boardRoamingPopupEventsAttached || BoardRoamingPopupContent == null) return;

            BoardRoamingPopupContent.ViewportPositionChanged += BoardRoamingPopupContent_ViewportPositionChanged;
            BoardRoamingPopupContent.ViewportDragStarted += BeginBoardRoamingPopupDrag;
            BoardRoamingPopupContent.ViewportDragCompleted += EndBoardRoamingPopupDrag;
            if (BoardRoamingPopupContent.CloseButtonControl != null)
                BoardRoamingPopupContent.CloseButtonControl.Click += (s, e) => BoardRoamingPopup.IsOpen = false;
            _boardRoamingPopupEventsAttached = true;
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
            if (_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = true;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();
        }

        private void EndBoardRoamingPopupDrag()
        {
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CommitBoardRoamingHistory();
            RefreshBoardRoamingPopup();
        }

        private void TranslateBoardRoamingContent(double deltaX, double deltaY)
        {
            var matrix = Matrix.Identity;
            matrix.Translate(deltaX, deltaY);
            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                foreach (var stroke in inkCanvas.Strokes)
                    stroke.Transform(matrix, false);
                TransformCanvasImages(matrix);
                // 视频展台特殊模式：漫游时预览画面与墨迹同步平移
                // （否则只有墨迹会动，展台背景不动）
                if (_isVideoPresenterSpecialMode)
                {
                    _boothPreviewTranslateX += deltaX;
                    _boothPreviewTranslateY += deltaY;
                    ApplyBoothPreviewTransform();
                    ResetRotationBaseline();
                }
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
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
