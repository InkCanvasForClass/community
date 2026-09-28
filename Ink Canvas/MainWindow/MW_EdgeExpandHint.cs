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
    /// 小于阈值 <see cref="Settings.Canvas.EdgeExpandThreshold"/>，就浮现一个提示按钮：
    ///   • 按钮贴边停靠：显示在待扩展方向的那条画布边缘上，沿边对齐书写位置；
    ///     靠近边角时显示为斜向扩展（45°）。
    ///   • 工具栏避让：若贴边位置与白板底部工具栏、浮动工具栏、IdleMiniBar、
    ///     PPT 导航、通知条等可见 UI 重叠，先向画布内侧收缩、再沿边平移寻找空位。
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
        /// 仅排除真正会冲突的场景：设置关闭 / 仅白板模式启用但当前非白板 / 浮动栏已收起 / 工具选择不是笔 / 非墨水模式 / 图形绘制模式 / 漫游中 / 页面冻结 / 被显式暂停。
        /// 默认不限制 currentMode 与 PPT 控件可见性——任何能书写的模式都应触发（包括桌面批注、白板、黑板）；
        /// 用户可在设置中开启「仅在白板模式启用」，让桌面批注、PPT 演示等非白板场景不提示。
        /// </summary>
        private bool IsEdgeExpandHintEligible()
        {
            if (!Settings.Canvas.IsEnableEdgeExpandHint) return false;
            if (Settings.Canvas.IsEnableEdgeExpandHintWhiteboardOnly && !IsWhiteboardMode) return false;
            if (isFloatingBarFolded && !IsWhiteboardMode) return false; // 浮动栏收起后批注界面整体隐藏，提示无意义
            if (inkCanvas == null) return false;
            if (EdgeExpandHintPopup == null || EdgeExpandHintButton == null) return false;
            if (IsBoardRoamingMode) return false;
            if (IsCurrentPageFrozen) return false;
            if (drawingShapeMode != 0) return false;
            // 工具选择不是笔（橡皮/框选/图形/漫游/鼠标）时不提示。
            // 注意：原生湿墨水下笔工具的物理 EditingMode 为 None，因此以逻辑工具状态判定。
            // 特例：进入白板的部分入口（启动时恢复白板状态等）直接调用 SwitchBackground，
            // 绕过了 ImageBlackboard_MouseUp 的切笔逻辑，_currentToolMode 会停留在 "cursor"，
            // 导致进入白板后的首次书写被误判为非笔工具。白板模式没有"鼠标"工具且进入时
            // 必然切回笔，因此白板下的 cursor 按笔对待。
            bool isPenToolSelected = _currentToolMode == "pen" || _currentToolMode == "color";
            if (!isPenToolSelected && !(IsWhiteboardMode && _currentToolMode == "cursor")) return false;
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

        /// <summary>提示按钮与画布边缘的最小安全间隙（像素）。</summary>
        private const double EdgeExpandHintEdgeMargin = 6;
        /// <summary>提示按钮避让工具栏时的单次平移步长间隙（像素）。</summary>
        private const double EdgeExpandHintUiGap = 8;

        /// <summary>
        /// 计算按钮相对画布边缘的"贴边停靠"位置，完成工具栏避让后把按钮放到画布坐标系内。
        /// 按钮优先停靠在待扩展方向的那条边（与边缘保持安全间隙），沿边对齐书写位置；
        /// 若与白板底部工具栏、浮动工具栏、PPT 导航等可见 UI 重叠，
        /// 先向画布内侧收缩（保持与书写位置对齐）、再沿边平移寻找空位。
        /// </summary>
        private void ShowEdgeExpandHint(Point anchor, EdgeExpandDirection direction)
        {
            var canvasW = inkCanvas.ActualWidth;
            var canvasH = inkCanvas.ActualHeight;

            // 内容/箭头随方向变化（单行胶囊样式）
            EdgeExpandHintButton.Content = BuildEdgeExpandHintGlyph(direction);

            const double btnH = 40; // 与 XAML 中按钮 Height 保持一致
            var btnW = MeasureEdgeExpandHintWidth();

            var preferred = ComputeEdgeDockedPosition(anchor, direction, canvasW, canvasH, btnW, btnH);
            var resolved = ResolveEdgeExpandHintOverlap(preferred, direction, canvasW, canvasH, btnW, btnH);

            // Popup 用 HorizontalOffset/VerticalOffset 设置相对 PlacementTarget(inkCanvas) 左上角的位置
            EdgeExpandHintPopup.HorizontalOffset = resolved.X;
            EdgeExpandHintPopup.VerticalOffset = resolved.Y;

            EdgeExpandHintButton.Visibility = Visibility.Visible;
            EdgeExpandHintPopup.IsOpen = true;
            _edgeExpandHintVisible = true;
        }

        /// <summary>
        /// 测量提示按钮的实际渲染宽度。按钮位于 Popup 中且常处于关闭状态，
        /// 通过一次显式 Measure 获取 DesiredSize；测量失败时回退到固定值。
        /// </summary>
        private double MeasureEdgeExpandHintWidth()
        {
            try
            {
                EdgeExpandHintButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (EdgeExpandHintButton.DesiredSize.Width > 16)
                    return EdgeExpandHintButton.DesiredSize.Width + 4;
            }
            catch
            {
                // ignored
            }
            return 80; // 回退：约等于 "↖ 扩展" 四字符胶囊按钮宽度
        }

        /// <summary>计算贴边停靠的理想位置：按钮贴在待扩展的那条边，沿边对齐书写位置。</summary>
        private static Rect ComputeEdgeDockedPosition(
            Point anchor, EdgeExpandDirection direction, double canvasW, double canvasH, double btnW, double btnH)
        {
            switch (direction)
            {
                case EdgeExpandDirection.Left:
                    return new Rect(EdgeExpandHintEdgeMargin,
                        ClampHintY(anchor.Y - btnH / 2, canvasH, btnH), btnW, btnH);
                case EdgeExpandDirection.Right:
                    return new Rect(canvasW - btnW - EdgeExpandHintEdgeMargin,
                        ClampHintY(anchor.Y - btnH / 2, canvasH, btnH), btnW, btnH);
                case EdgeExpandDirection.Top:
                    return new Rect(ClampHintX(anchor.X - btnW / 2, canvasW, btnW),
                        EdgeExpandHintEdgeMargin, btnW, btnH);
                case EdgeExpandDirection.Bottom:
                    return new Rect(ClampHintX(anchor.X - btnW / 2, canvasW, btnW),
                        canvasH - btnH - EdgeExpandHintEdgeMargin, btnW, btnH);
                case EdgeExpandDirection.TopLeft:
                    return new Rect(EdgeExpandHintEdgeMargin, EdgeExpandHintEdgeMargin, btnW, btnH);
                case EdgeExpandDirection.TopRight:
                    return new Rect(canvasW - btnW - EdgeExpandHintEdgeMargin, EdgeExpandHintEdgeMargin, btnW, btnH);
                case EdgeExpandDirection.BottomLeft:
                    return new Rect(EdgeExpandHintEdgeMargin, canvasH - btnH - EdgeExpandHintEdgeMargin, btnW, btnH);
                default:
                    return new Rect(canvasW - btnW - EdgeExpandHintEdgeMargin,
                        canvasH - btnH - EdgeExpandHintEdgeMargin, btnW, btnH);
            }
        }

        /// <summary>
        /// 若理想位置与可见工具栏重叠，按"向内收缩 → 沿边平移"的顺序寻找空位。
        /// 所有候选都失败时退回理想位置（提示短暂出现，总比挡住书写区域强）。
        /// </summary>
        private Rect ResolveEdgeExpandHintOverlap(
            Rect preferred, EdgeExpandDirection direction, double canvasW, double canvasH, double btnW, double btnH)
        {
            var regions = GetEdgeExpandHintBlockedRegions();
            if (regions.Count == 0 || !IntersectsAny(preferred, regions))
                return preferred;

            foreach (var candidate in BuildEdgeExpandHintEscapeCandidates(preferred, direction, canvasW, canvasH, btnW, btnH))
            {
                if (!IntersectsAny(candidate, regions))
                    return candidate;
            }
            return preferred;
        }

        /// <summary>
        /// 生成避让候选位置：
        ///   1) 向内收缩——沿垂直于该边的方向往画布中心挪一个按钮身位，
        ///      保持与书写位置的对齐（按钮悬在工具栏上方/内侧）；
        ///   2) 沿边平移——交替向两侧挪 1~3 个身位，在工具栏之间的空隙停靠。
        /// </summary>
        private static List<Rect> BuildEdgeExpandHintEscapeCandidates(
            Rect preferred, EdgeExpandDirection direction, double canvasW, double canvasH, double btnW, double btnH)
        {
            var list = new List<Rect>();

            bool verticalEdge = direction == EdgeExpandDirection.Left || direction == EdgeExpandDirection.Right;
            bool horizontalEdge = direction == EdgeExpandDirection.Top || direction == EdgeExpandDirection.Bottom;

            if (verticalEdge || horizontalEdge)
            {
                // 1) 向内收缩
                double inward = (verticalEdge ? btnW : btnH) + EdgeExpandHintUiGap;
                if (verticalEdge)
                {
                    var inwardX = direction == EdgeExpandDirection.Left
                        ? EdgeExpandHintEdgeMargin + inward
                        : canvasW - btnW - EdgeExpandHintEdgeMargin - inward;
                    list.Add(new Rect(inwardX, preferred.Y, btnW, btnH));
                }
                else
                {
                    var inwardY = direction == EdgeExpandDirection.Top
                        ? EdgeExpandHintEdgeMargin + inward
                        : canvasH - btnH - EdgeExpandHintEdgeMargin - inward;
                    list.Add(new Rect(preferred.X, inwardY, btnW, btnH));
                }

                // 2) 沿边平移：交替向两侧挪 1~3 个身位
                double slide = (verticalEdge ? btnH : btnW) + EdgeExpandHintUiGap;
                for (int i = 1; i <= 3; i++)
                {
                    foreach (double delta in new[] { -slide * i, slide * i })
                    {
                        var r = verticalEdge
                            ? new Rect(preferred.X, ClampHintY(preferred.Y + delta, canvasH, btnH), btnW, btnH)
                            : new Rect(ClampHintX(preferred.X + delta, canvasW, btnW), preferred.Y, btnW, btnH);
                        list.Add(r);
                    }
                }
            }
            else
            {
                // 角部：沿两条相邻边各挪一个身位，再沿对角线向内挪一个身位
                double dx = (direction == EdgeExpandDirection.TopLeft || direction == EdgeExpandDirection.BottomLeft) ? 1 : -1;
                double dy = (direction == EdgeExpandDirection.TopLeft || direction == EdgeExpandDirection.TopRight) ? 1 : -1;
                double stepX = btnW + EdgeExpandHintUiGap;
                double stepY = btnH + EdgeExpandHintUiGap;

                list.Add(new Rect(ClampHintX(preferred.X + dx * stepX, canvasW, btnW), preferred.Y, btnW, btnH));
                list.Add(new Rect(preferred.X, ClampHintY(preferred.Y + dy * stepY, canvasH, btnH), btnW, btnH));
                list.Add(new Rect(
                    ClampHintX(preferred.X + dx * stepX, canvasW, btnW),
                    ClampHintY(preferred.Y + dy * stepY, canvasH, btnH), btnW, btnH));
            }
            return list;
        }

        /// <summary>
        /// 收集当前可见的、可能与提示按钮重叠的 UI 区域（画布坐标系）：
        /// 白板底部三区工具栏、浮动工具栏、IdleMiniBar、PPT 侧边/底部导航、通知条。
        /// 各元素不可见或未布局时自动跳过。
        /// </summary>
        private List<Rect> GetEdgeExpandHintBlockedRegions()
        {
            var regions = new List<Rect>();
            if (inkCanvas == null) return regions;

            AddBlockedRegion(ViewboxFloatingBar, regions);                       // 桌面批注浮动工具栏
            AddBlockedRegion(IdleMiniBar, regions);                              // 闲置状态紧凑批注栏
            AddBlockedRegion(BlackboardLeftSide, regions);                       // 白板左下角导航区
            AddBlockedRegion(BlackboardCenterSide, regions);                     // 白板底部中间工具栏
            AddBlockedRegion(BlackboardRightSide, regions);                      // 白板右下角导航区
            AddBlockedRegion(LeftSidePanelForPPTNavigation, regions);            // PPT 左侧导航
            AddBlockedRegion(RightSidePanelForPPTNavigation, regions);           // PPT 右侧导航
            AddBlockedRegion(LeftBottomPanelForPPTNavigation, regions);          // PPT 左下导航
            AddBlockedRegion(RightBottomPanelForPPTNavigation, regions);         // PPT 右下导航
            AddBlockedRegion(GridNotifications, regions);                        // 底部通知条
            return regions;
        }

        /// <summary>把可见 UI 元素的边界换算到画布坐标系并加入遮挡区列表（外扩 4px 安全间隙）。</summary>
        private void AddBlockedRegion(FrameworkElement element, List<Rect> regions)
        {
            if (element == null || element.Visibility != Visibility.Visible) return;
            if (element.ActualWidth < 1 || element.ActualHeight < 1) return;
            try
            {
                var topLeft = element.TranslatePoint(new Point(0, 0), inkCanvas);
                var bottomRight = element.TranslatePoint(new Point(element.ActualWidth, element.ActualHeight), inkCanvas);
                if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y) ||
                    double.IsNaN(bottomRight.X) || double.IsNaN(bottomRight.Y)) return;
                var rect = new Rect(topLeft, bottomRight);
                rect.Inflate(4, 4);
                regions.Add(rect);
            }
            catch
            {
                // ignored
            }
        }

        private static bool IntersectsAny(Rect rect, List<Rect> regions)
        {
            foreach (var r in regions)
            {
                if (rect.IntersectsWith(r)) return true;
            }
            return false;
        }

        /// <summary>横向夹取提示按钮位置；画布过小时退化为边缘间隙本身，保证矩形合法。</summary>
        private static double ClampHintX(double value, double canvasW, double btnW)
            => Clamp(value, EdgeExpandHintEdgeMargin,
                Math.Max(EdgeExpandHintEdgeMargin, canvasW - btnW - EdgeExpandHintEdgeMargin));

        /// <summary>纵向夹取提示按钮位置；画布过小时退化为边缘间隙本身，保证矩形合法。</summary>
        private static double ClampHintY(double value, double canvasH, double btnH)
            => Clamp(value, EdgeExpandHintEdgeMargin,
                Math.Max(EdgeExpandHintEdgeMargin, canvasH - btnH - EdgeExpandHintEdgeMargin));

        /// <summary>根据方向生成对应的箭头文本（Unicode 几何字符，无需图片资源；单行胶囊样式）。</summary>
        private static string BuildEdgeExpandHintGlyph(EdgeExpandDirection direction)
        {
            switch (direction)
            {
                case EdgeExpandDirection.Left: return "← 扩展";
                case EdgeExpandDirection.Right: return "扩展 →";
                case EdgeExpandDirection.Top: return "↑ 扩展";
                case EdgeExpandDirection.Bottom: return "扩展 ↓";
                case EdgeExpandDirection.TopLeft: return "↖ 扩展";
                case EdgeExpandDirection.TopRight: return "↗ 扩展";
                case EdgeExpandDirection.BottomLeft: return "↙ 扩展";
                case EdgeExpandDirection.BottomRight: return "↘ 扩展";
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

        /// <summary>悬停提示按钮时暂停自动隐藏，避免按钮在光标下突然消失。</summary>
        private void EdgeExpandHintButton_MouseEnter(object sender, MouseEventArgs e)
        {
            StopEdgeExpandHintAutoHideTimer();
        }

        /// <summary>光标离开提示按钮后按设定时长重新开始自动隐藏倒计时。</summary>
        private void EdgeExpandHintButton_MouseLeave(object sender, MouseEventArgs e)
        {
            ResetEdgeExpandHintAutoHideTimer();
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