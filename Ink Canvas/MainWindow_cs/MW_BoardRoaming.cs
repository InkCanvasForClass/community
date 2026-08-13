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

                // 修复P3：最终 worldBounds 单边尺寸再次 clamp（8192 安全上限）。
                // 如果用户墨迹非常分散，Inflate 之后边界会远超 RenderTargetBitmap 上限，
                // 这里把 Width/Height 等比缩到上限内，保证后续 VisualBrush / RTB 不炸。
                const double maxSide = 8192.0;
                if (_boardRoamingWorldBounds.Width > maxSide || _boardRoamingWorldBounds.Height > maxSide)
                {
                    var sx = maxSide / _boardRoamingWorldBounds.Width;
                    var sy = maxSide / _boardRoamingWorldBounds.Height;
                    var s = Math.Min(sx, sy);
                    var newW = _boardRoamingWorldBounds.Width * s;
                    var newH = _boardRoamingWorldBounds.Height * s;
                    var cx = _boardRoamingWorldBounds.X + _boardRoamingWorldBounds.Width * 0.5;
                    var cy = _boardRoamingWorldBounds.Y + _boardRoamingWorldBounds.Height * 0.5;
                    _boardRoamingWorldBounds = new Rect(cx - newW * 0.5, cy - newH * 0.5, newW, newH);
                }
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
            // 修复P3：单边安全上限。WPF RenderTargetBitmap 在软件渲染/部分集成显卡下
            // 单维度超过 8192 会直接抛异常或返回空图；个别超大坐标来自 NaN/异常保存文件
            // 也会直接拉爆 Rect。先在源头把每个边界都夹到合理区间。
            const double maxSingleSide = 8192.0;
            var result = Rect.Empty;

            foreach (var stroke in inkCanvas.Strokes)
            {
                Rect b;
                try { b = stroke.GetBounds(); }
                catch
                {
                    continue;
                }
                if (!b.IsEmpty && double.IsFinite(b.X) && double.IsFinite(b.Y)
                    && double.IsFinite(b.Width) && double.IsFinite(b.Height)
                    && b.Width <= maxSingleSide && b.Height <= maxSingleSide)
                {
                    result.Union(b);
                }
            }

            foreach (UIElement child in inkCanvas.Children)
            {
                if (child is not FrameworkElement element) continue;
                try
                {
                    var aw = element.ActualWidth;
                    var ah = element.ActualHeight;
                    if (!double.IsFinite(aw) || !double.IsFinite(ah)
                        || aw <= 0 || ah <= 0 || aw > maxSingleSide || ah > maxSingleSide)
                    {
                        continue;
                    }
                    var bounds = element.TransformToAncestor(inkCanvas)
                        .TransformBounds(new Rect(0, 0, aw, ah));
                    if (!bounds.IsEmpty && double.IsFinite(bounds.X) && double.IsFinite(bounds.Y)
                        && double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height)
                        && bounds.Width <= maxSingleSide && bounds.Height <= maxSingleSide)
                    {
                        result.Union(bounds);
                    }
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
                // 修复P3：RTB 单边尺寸硬上限 2048（预览实际仅 352x198，再大浪费显存，
                // 且在部分老驱动/集成显卡上 >2048 直接渲染失败）。
                const int maxBitmapSide = 2048;
                var bitmapWidth = Math.Max(1, (int)Math.Ceiling(previewWidth));
                var bitmapHeight = Math.Max(1, (int)Math.Ceiling(previewHeight));
                if (bitmapWidth > maxBitmapSide) bitmapWidth = maxBitmapSide;
                if (bitmapHeight > maxBitmapSide) bitmapHeight = maxBitmapSide;

                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, previewWidth, previewHeight));
                    var background = GridBackgroundCover.Background ?? Brushes.White;
                    context.DrawRectangle(background, null, _boardRoamingPreviewMovementBounds);

                    // 修复P3：优先尝试 VisualBrush 快速路径；失败时降级为「手动画每一笔 stroke
                    // + 每个子元素缩略图」，绕开 inkCanvas 视觉树过大导致的 RTB/VisualBrush
                    // 内部异常（墨迹过多 / 子元素过多 / 布局脏都可能触发）。
                    bool fallbackNeeded;
                    try
                    {
                        var visualBrush = new VisualBrush(inkCanvas)
                        {
                            Stretch = Stretch.Fill,
                            ViewboxUnits = BrushMappingMode.Absolute,
                            Viewbox = worldBounds,
                            ViewportUnits = BrushMappingMode.Absolute,
                            Viewport = _boardRoamingPreviewMovementBounds
                        };
                        // 冻结失败不影响使用，但能减少内存占用——能冻就冻。
                        try { if (visualBrush.CanFreeze) visualBrush.Freeze(); } catch { /* best-effort */ }
                        context.DrawRectangle(visualBrush, null, _boardRoamingPreviewMovementBounds);
                        fallbackNeeded = false;
                    }
                    catch
                    {
                        fallbackNeeded = true;
                    }

                    if (fallbackNeeded)
                    {
                        DrawBoardRoamingStrokesFallback(
                            context,
                            worldBounds,
                            _boardRoamingPreviewMovementBounds);
                    }
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
                // 修复P3：记录完整堆栈，便于定位到底是 VisualBrush 还是 RTB 还是 stroke.Transform
                // 导致的异常；之前只记 Message 丢失了 90% 诊断信息。
                LogHelper.WriteLogToFile(
                    $"生成漫游预览失败: {ex}",
                    LogHelper.LogType.Warning);
                return null;
            }
        }

        /// <summary>修复P3：VisualBrush(inkCanvas) 失败时的降级渲染路径。
        /// 直接用 DrawingContext 迭代每一笔 Stroke + 每个 FrameworkElement，
        /// 自己做 world→preview 的坐标映射，不走完整 WPF 布局/渲染管线，
        /// 在墨迹极多/子元素极多的白板上稳定度显著更高。</summary>
        private void DrawBoardRoamingStrokesFallback(
            DrawingContext context,
            Rect worldBounds,
            Rect previewBounds)
        {
            if (worldBounds.Width <= 0 || worldBounds.Height <= 0
                || previewBounds.Width <= 0 || previewBounds.Height <= 0)
            {
                return;
            }

            var sx = previewBounds.Width / worldBounds.Width;
            var sy = previewBounds.Height / worldBounds.Height;
            var scale = Math.Min(sx, sy);
            if (scale <= 0) return;

            var offsetX = previewBounds.X + (previewBounds.Width - worldBounds.Width * scale) * 0.5;
            var offsetY = previewBounds.Y + (previewBounds.Height - worldBounds.Height * scale) * 0.5;

            var worldToPreview = Matrix.Identity;
            worldToPreview.Translate(-worldBounds.X, -worldBounds.Y);
            worldToPreview.Scale(scale, scale);
            worldToPreview.Translate(offsetX, offsetY);

            // 安全上限：超过 10 万笔时只画前 5 万笔（预览本来就是缩小的，
            // 大量重叠笔迹肉眼看和全画无异，避免卡死线程）。
            const int maxStrokes = 50000;
            var drawn = 0;
            var previewTransform = new MatrixTransform(worldToPreview);
            try { if (previewTransform.CanFreeze) previewTransform.Freeze(); } catch { /* best-effort */ }
            foreach (var stroke in inkCanvas.Strokes)
            {
                if (drawn++ >= maxStrokes) break;
                try
                {
                    // 用 Stroke 的 DrawingAttributes 取原始线宽，然后乘以预览缩放比，
                    // 最后对 Geometry 统一应用 world→preview 矩阵。
                    var da = stroke.DrawingAttributes;
                    var penWidth = Math.Max(0.1, da.Width * scale);
                    var pen = new Pen(new SolidColorBrush(da.Color), penWidth);
                    try { if (pen.CanFreeze) pen.Freeze(); } catch { /* best-effort */ }
                    var geo = stroke.GetGeometry(da);
                    if (geo == null) continue;
                    geo.Transform = previewTransform;
                    context.DrawGeometry(null, pen, geo);
                }
                catch
                {
                    // 单笔画失败就跳过（可能是 StylusPoints 异常），继续其余笔画。
                }
            }

            // 子元素：只画截图作为缩略（避免再递归渲染复杂布局）。
            // 数量做上限控制，和 strokes 一起共享 maxStrokes 的"节流精神"。
            const int maxChildren = 2000;
            var childDrawn = 0;
            foreach (UIElement child in inkCanvas.Children)
            {
                if (childDrawn++ >= maxChildren) break;
                if (child is not FrameworkElement element) continue;
                try
                {
                    var aw = element.ActualWidth;
                    var ah = element.ActualHeight;
                    if (!double.IsFinite(aw) || !double.IsFinite(ah) || aw <= 0 || ah <= 0)
                        continue;
                    var elementInCanvasBounds = element.TransformToAncestor(inkCanvas)
                        .TransformBounds(new Rect(0, 0, aw, ah));
                    // Matrix 没有 TransformBounds：用 Rect.Transform(matrix) 替代。
                    var elementInPreview = elementInCanvasBounds;
                    elementInPreview.Transform(worldToPreview);
                    if (elementInPreview.Width <= 0 || elementInPreview.Height <= 0) continue;

                    // 子元素简化画一个半透明色块 + 边框（代表位置）；
                    // 如果是图片/媒体再尝试抓缩略，不做重的 RTB 递归渲染。
                    var fillBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80));
                    try { if (fillBrush.CanFreeze) fillBrush.Freeze(); } catch { /* best-effort */ }
                    var pen = new Pen(Brushes.DimGray, 0.5);
                    try { if (pen.CanFreeze) pen.Freeze(); } catch { /* best-effort */ }
                    context.DrawRectangle(fillBrush, pen, elementInPreview);
                }
                catch
                {
                    // 单个子元素失败跳过
                }
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
