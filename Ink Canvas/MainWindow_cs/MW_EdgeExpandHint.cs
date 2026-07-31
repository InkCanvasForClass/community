using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    /// <summary>
    /// Issue #286 — 书写位置贴近边缘时显示"扩展画布"提示按钮。
    /// 检测 inkCanvas_StrokeCollected 中的笔画触点，若任意点距画布四边的距离
    /// 小于阈值 <see cref="Settings.Canvas.EdgeExpandThreshold"/>，就在书写
    /// 位置外侧的边缘区域浮现一个按钮：
    ///   • 上下左右非边角位置：按钮显示在当前书写位置的边缘外侧（水平/垂直方向）。
    ///   • 靠近四个边角：按钮显示为斜向扩展（45°）。
    /// 点击按钮后，按 <see cref="Settings.Canvas.EdgeExpandTranslateStep"/>
    /// 平移画布上的全部墨迹和图片元素，腾出新的书写空间。
    /// </summary>
    public partial class MainWindow
    {
        // 最近一次触发提示按钮的位置（画布坐标系）
        private Point? _edgeExpandHintAnchor;
        // 最近一次触发提示按钮的"扩展方向"（按钮应位于画布的哪一侧）
        private EdgeExpandDirection _edgeExpandHintDirection = EdgeExpandDirection.None;
        // 自动隐藏计时器
        private DispatcherTimer _edgeExpandHintAutoHideTimer;
        // 按钮是否正在显示
        private bool _edgeExpandHintVisible;
        // 防重入：被外部模式切换、漫游、翻页等暂停
        private bool _edgeExpandHintSuspended;

        /// <summary>提示按钮的扩展方向枚举（与按钮相对画布的位置一一对应）。</summary>
        private enum EdgeExpandDirection
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        /// <summary>
        /// 在笔画收集完成后调用，判定当前书写位置是否贴近画布边缘。
        /// 若触发条件成立，刷新 hint 按钮的位置、可见性，并重置自动隐藏计时器。
        /// </summary>
        internal void HandleEdgeExpandHintAfterStroke(IList<Point> strokePoints)
        {
            try
            {
                if (strokePoints == null || strokePoints.Count == 0) return;
                if (inkCanvas == null || !IsLoaded || inkCanvas.ActualWidth <= 0 || inkCanvas.ActualHeight <= 0) return;
                if (EdgeExpandHintPopup == null || EdgeExpandHintButton == null) return;

                if (!IsEdgeExpandHintEligible()) return;

                var width = inkCanvas.ActualWidth;
                var height = inkCanvas.ActualHeight;
                var threshold = ClampThreshold(Settings.Canvas.EdgeExpandThreshold);

                // 找到最贴近边缘的触点位置及触发的方向
                Point anchor = strokePoints[0];
                double minDist = double.PositiveInfinity;
                EdgeExpandDirection direction = EdgeExpandDirection.None;

                foreach (var p in strokePoints)
                {
                    // 容差 2px：触点偏出一点点不算
                    if (p.X < -2 || p.X > width + 2 || p.Y < -2 || p.Y > height + 2) continue;

                    var distLeft = p.X;
                    var distRight = width - p.X;
                    var distTop = p.Y;
                    var distBottom = height - p.Y;

                    // 关键：用"到任一边的最小距离"判定（不是对角线），
                    // 否则笔在画布正中央偏右时即使距右边只有 4px，
                    // 但距其他三条边都很远，distCorner 就会很大，触发不了。
                    var minEdgeDist = Math.Min(
                        Math.Min(distLeft, distRight),
                        Math.Min(distTop, distBottom));

                    if (minEdgeDist >= threshold) continue;

                    if (minEdgeDist < minDist)
                    {
                        minDist = minEdgeDist;
                        anchor = p;
                        direction = ResolveEdgeDirection(distLeft, distRight, distTop, distBottom, threshold);
                    }
                }

                if (direction == EdgeExpandDirection.None)
                {
                    HideEdgeExpandHint();
                    return;
                }

                // 如果 hint 已经显示且方向、锚点位置变化很小，则只重置自动隐藏（避免抖动）
                if (_edgeExpandHintVisible
                    && _edgeExpandHintAnchor.HasValue
                    && _edgeExpandHintDirection == direction
                    && Distance(_edgeExpandHintAnchor.Value, anchor) < 24)
                {
                    ResetEdgeExpandHintAutoHideTimer();
                    return;
                }

                _edgeExpandHintAnchor = anchor;
                _edgeExpandHintDirection = direction;
                ShowEdgeExpandHint(anchor, direction);
                ResetEdgeExpandHintAutoHideTimer();

                LogHelper.WriteLogToFile($"EdgeExpandHint 触发: dir={direction}, anchor=({anchor.X:F0},{anchor.Y:F0}), threshold={threshold:F0}, canvas={width:F0}x{height:F0}", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"边缘扩展提示判定失败: {ex.Message}", LogHelper.LogType.Warning);
                HideEdgeExpandHint();
            }
        }

        /// <summary>
        /// 当前是否允许触发边缘扩展提示。
        /// 仅排除真正会冲突的场景：设置关闭 / 非墨水模式 / 图形绘制模式 / 漫游中 / 页面冻结 / 被显式暂停。
        /// 不限制 currentMode 与 PPT 控件可见性——任何能书写的模式都应触发（包括桌面批注、白板、黑板）。
        /// </summary>
        private bool IsEdgeExpandHintEligible()
        {
            if (!Settings.Canvas.IsEnableEdgeExpandHint) return false;
            if (inkCanvas == null) return false;
            if (EdgeExpandHintPopup == null || EdgeExpandHintButton == null) return false;
            if (IsBoardRoamingMode) return false;
            if (IsCurrentPageFrozen) return false;
            if (drawingShapeMode != 0) return false;
            if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink
                && inkCanvas.EditingMode != InkCanvasEditingMode.None)
                return false;
            if (_edgeExpandHintSuspended) return false;
            return true;
        }

        /// <summary>根据到四边的最小距离判定方向，靠近角时给出 45° 斜向。</summary>
        private static EdgeExpandDirection ResolveEdgeDirection(
            double distLeft, double distRight, double distTop, double distBottom, double threshold)
        {
            var onLeft = distLeft <= threshold;
            var onRight = distRight <= threshold;
            var onTop = distTop <= threshold;
            var onBottom = distBottom <= threshold;

            var cornerCount = (onLeft ? 1 : 0) + (onRight ? 1 : 0) + (onTop ? 1 : 0) + (onBottom ? 1 : 0);
            if (cornerCount >= 2)
            {
                if (onLeft && onTop) return EdgeExpandDirection.TopLeft;
                if (onRight && onTop) return EdgeExpandDirection.TopRight;
                if (onLeft && onBottom) return EdgeExpandDirection.BottomLeft;
                if (onRight && onBottom) return EdgeExpandDirection.BottomRight;
            }

            var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));
            if (minDist == distLeft) return EdgeExpandDirection.Left;
            if (minDist == distRight) return EdgeExpandDirection.Right;
            if (minDist == distTop) return EdgeExpandDirection.Top;
            return EdgeExpandDirection.Bottom;
        }

        /// <summary>
        /// 计算按钮相对画布边缘的"外侧偏移"位置，并把按钮放到画布坐标系内。
        /// 按钮整体大小 56×56，与画布边缘留 4px 的安全间隙。
        /// </summary>
        private void ShowEdgeExpandHint(Point anchor, EdgeExpandDirection direction)
        {
            const double btnSize = 64;
            const double margin = 4;

            double left = 0, top = 0;
            switch (direction)
            {
                case EdgeExpandDirection.Left:
                    left = Math.Max(margin, anchor.X - btnSize - margin);
                    top = Clamp(anchor.Y - btnSize / 2, margin, inkCanvas.ActualHeight - btnSize - margin);
                    break;
                case EdgeExpandDirection.Right:
                    left = Math.Min(inkCanvas.ActualWidth - btnSize - margin, anchor.X + margin);
                    top = Clamp(anchor.Y - btnSize / 2, margin, inkCanvas.ActualHeight - btnSize - margin);
                    break;
                case EdgeExpandDirection.Top:
                    left = Clamp(anchor.X - btnSize / 2, margin, inkCanvas.ActualWidth - btnSize - margin);
                    top = Math.Max(margin, anchor.Y - btnSize - margin);
                    break;
                case EdgeExpandDirection.Bottom:
                    left = Clamp(anchor.X - btnSize / 2, margin, inkCanvas.ActualWidth - btnSize - margin);
                    top = Math.Min(inkCanvas.ActualHeight - btnSize - margin, anchor.Y + margin);
                    break;
                case EdgeExpandDirection.TopLeft:
                    left = Math.Max(margin, anchor.X - btnSize - margin);
                    top = Math.Max(margin, anchor.Y - btnSize - margin);
                    break;
                case EdgeExpandDirection.TopRight:
                    left = Math.Min(inkCanvas.ActualWidth - btnSize - margin, anchor.X + margin);
                    top = Math.Max(margin, anchor.Y - btnSize - margin);
                    break;
                case EdgeExpandDirection.BottomLeft:
                    left = Math.Max(margin, anchor.X - btnSize - margin);
                    top = Math.Min(inkCanvas.ActualHeight - btnSize - margin, anchor.Y + margin);
                    break;
                case EdgeExpandDirection.BottomRight:
                    left = Math.Min(inkCanvas.ActualWidth - btnSize - margin, anchor.X + margin);
                    top = Math.Min(inkCanvas.ActualHeight - btnSize - margin, anchor.Y + margin);
                    break;
            }

            // Popup 用 HorizontalOffset/VerticalOffset 设置相对 PlacementTarget(inkCanvas) 左上角的位置
            EdgeExpandHintPopup.HorizontalOffset = left;
            EdgeExpandHintPopup.VerticalOffset = top;

            // 内容/箭头随方向变化
            EdgeExpandHintButton.Content = BuildEdgeExpandHintGlyph(direction);
            EdgeExpandHintButton.Visibility = Visibility.Visible;
            EdgeExpandHintPopup.IsOpen = true;
            _edgeExpandHintVisible = true;
        }

        /// <summary>根据方向生成对应的箭头文本（Unicode 几何字符，无需图片资源）。</summary>
        private static string BuildEdgeExpandHintGlyph(EdgeExpandDirection direction)
        {
            switch (direction)
            {
                case EdgeExpandDirection.Left: return "←\n扩展";
                case EdgeExpandDirection.Right: return "扩展\n→";
                case EdgeExpandDirection.Top: return "↑\n扩展";
                case EdgeExpandDirection.Bottom: return "扩展\n↓";
                case EdgeExpandDirection.TopLeft: return "↖\n扩展";
                case EdgeExpandDirection.TopRight: return "↗\n扩展";
                case EdgeExpandDirection.BottomLeft: return "↙\n扩展";
                case EdgeExpandDirection.BottomRight: return "↘\n扩展";
                default: return "扩展";
            }
        }

        /// <summary>手动隐藏提示按钮（清空状态、停掉计时器）。</summary>
        internal void HideEdgeExpandHint()
        {
            try
            {
                if (EdgeExpandHintPopup != null) EdgeExpandHintPopup.IsOpen = false;
            }
            catch
            {
                // ignored
            }
            _edgeExpandHintVisible = false;
            _edgeExpandHintAnchor = null;
            _edgeExpandHintDirection = EdgeExpandDirection.None;
            StopEdgeExpandHintAutoHideTimer();
        }

        /// <summary>
        /// 重置自动隐藏计时器（用户停止书写一段时间后自动消失）。
        /// </summary>
        private void ResetEdgeExpandHintAutoHideTimer()
        {
            StopEdgeExpandHintAutoHideTimer();
            if (Settings.Canvas.EdgeExpandAutoHideMs <= 0) return;
            if (_edgeExpandHintAutoHideTimer == null)
            {
                _edgeExpandHintAutoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(Settings.Canvas.EdgeExpandAutoHideMs)
                };
                _edgeExpandHintAutoHideTimer.Tick += (_, _) =>
                {
                    _edgeExpandHintAutoHideTimer.Stop();
                    HideEdgeExpandHint();
                };
            }
            else
            {
                _edgeExpandHintAutoHideTimer.Interval = TimeSpan.FromMilliseconds(Settings.Canvas.EdgeExpandAutoHideMs);
            }
            _edgeExpandHintAutoHideTimer.Start();
        }

        private void StopEdgeExpandHintAutoHideTimer()
        {
            _edgeExpandHintAutoHideTimer?.Stop();
        }

        /// <summary>
        /// 点击事件：按方向一次性平移所有墨迹和图片元素，腾出新的书写空间。
        /// 平移距离受 <see cref="Settings.Canvas.EdgeExpandTranslateStep"/> 控制。
        /// </summary>
        private void EdgeExpandHintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsCurrentPageFrozen)
                {
                    TryBlockFrozenPageMutation("扩展画布");
                    HideEdgeExpandHint();
                    return;
                }

                var direction = _edgeExpandHintDirection;
                if (direction == EdgeExpandDirection.None)
                {
                    HideEdgeExpandHint();
                    return;
                }

                var step = ClampTranslateStep(Settings.Canvas.EdgeExpandTranslateStep);
                var (dx, dy) = DirectionToDelta(direction, step);

                ApplyEdgeExpandTranslation(dx, dy);
                MarkCurrentPageInkChanged();
                HideEdgeExpandHint();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"边缘扩展操作失败: {ex.Message}", LogHelper.LogType.Error);
                HideEdgeExpandHint();
            }
        }

        /// <summary>把方向 + 步长映射成平移向量 dx/dy（坐标轴：右正、下正）。</summary>
        private static (double dx, double dy) DirectionToDelta(EdgeExpandDirection direction, double step)
        {
            // 按钮指向画布外侧 → 用户希望把当前书写位置往内挪，
            // 把另一侧留作新的书写空间。例如 ← 按钮 → 内容向右平移（dx > 0）。
            switch (direction)
            {
                case EdgeExpandDirection.Left: return (step, 0);
                case EdgeExpandDirection.Right: return (-step, 0);
                case EdgeExpandDirection.Top: return (0, step);
                case EdgeExpandDirection.Bottom: return (0, -step);
                case EdgeExpandDirection.TopLeft: return (step / Math.Sqrt(2), step / Math.Sqrt(2));
                case EdgeExpandDirection.TopRight: return (-step / Math.Sqrt(2), step / Math.Sqrt(2));
                case EdgeExpandDirection.BottomLeft: return (step / Math.Sqrt(2), -step / Math.Sqrt(2));
                case EdgeExpandDirection.BottomRight: return (-step / Math.Sqrt(2), -step / Math.Sqrt(2));
                default: return (0, 0);
            }
        }

        /// <summary>
        /// 一次性平移所有 stroke 和 inkCanvas.Children 上的图片/媒体元素。
        /// 同时写入时间机器历史，支持撤销。
        /// 关键：每个 stroke.Transform 都包在 try/catch 里，防止单个坏笔画让画布卡死。
        /// </summary>
        private void ApplyEdgeExpandTranslation(double dx, double dy)
        {
            if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) return;

            var matrix = Matrix.Identity;
            matrix.Translate(dx, dy);

            // 记录所有 stroke 的旧 / 新触点历史（用于时间机器撤销）
            var history = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            foreach (var stroke in inkCanvas.Strokes)
            {
                if (stroke == null) continue;
                StylusPointCollection oldPoints = null;
                StylusPointCollection newPoints = null;
                try
                {
                    oldPoints = stroke.StylusPoints.Clone();
                    stroke.Transform(matrix, false);
                    newPoints = stroke.StylusPoints.Clone();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"边缘扩展平移单个笔画失败: {ex.Message}", LogHelper.LogType.Warning);
                    continue;
                }
                history[stroke] = Tuple.Create(oldPoints, newPoints);
            }

            // 同步平移 inkCanvas.Children 上的图片/媒体元素
            try
            {
                TransformCanvasImages(matrix);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"边缘扩展平移图片失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            if (history.Count > 0)
            {
                try
                {
                    timeMachine.CommitStrokeManipulationHistory(history);
                    foreach (var entry in history)
                        StrokeInitialHistory[entry.Key] = entry.Value.Item2;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"边缘扩展写入时间机器失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        /// <summary>
        /// 模式切换 / 关闭批注 / 翻页时调用，强制隐藏并清空状态。
        /// </summary>
        internal void SuspendAndHideEdgeExpandHint()
        {
            _edgeExpandHintSuspended = true;
            HideEdgeExpandHint();
        }

        /// <summary>恢复提示功能（在切回白板并允许书写时调用）。</summary>
        internal void ResumeEdgeExpandHint()
        {
            _edgeExpandHintSuspended = false;
        }

        // —— 工具方法 ——

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double ClampThreshold(double value)
        {
            // 阈值合法区间 10..400 像素
            return Clamp(value <= 0 ? 60 : value, 10, 400);
        }

        private static double ClampTranslateStep(double value)
        {
            return Clamp(value <= 0 ? 220 : value, 20, 2000);
        }

        private static double Distance(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}