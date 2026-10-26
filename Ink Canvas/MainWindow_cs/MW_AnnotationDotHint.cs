using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    /// <summary>
    /// 批注状态点提示：当用户在批注模式下反复点击同一区域时，
    /// 在非屏幕边缘区域显示「当前正处于批注状态」的半透明提示，
    /// 帮助教师意识到当前处于批注模式而非鼠标模式。
    /// 同时支持点击画布即留下可见点状墨迹。
    /// <para>
    /// 实现策略：全部逻辑在 <see cref="ProcessCommittedStroke"/> 后处理中完成，
    /// 不拦截 PreviewMouse 事件，避免干扰 InkCanvas 的墨迹采集与平滑管线。
    /// </para>
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>最近点击位置队列（画布坐标），用于判断是否在狭小范围内连续点击。</summary>
        private readonly Queue<Point> _annotationDotPositions = new Queue<Point>();
        /// <summary>最近点击位置队列的最大容量。</summary>
        private const int AnnotationDotMaxQueueSize = 10;
        /// <summary>最近一次「长笔迹」（真实书写）的提交时间；用于 30 秒未书写的空闲门控。</summary>
        private DateTime? _lastLongStrokeTime;
        /// <summary>距离上次真实书写多久后，功能按「空闲」状态触发（秒）。</summary>
        private const double AnnotationDotIdleSeconds = 30;
        /// <summary>上一次点击笔迹处理时的空闲状态；用于检测刚跨越 30 秒空闲边界的时刻。</summary>
        private bool _lastClickWasIdle;
        /// <summary>提示是否正在显示。</summary>
        private bool _annotationDotHintVisible;
        /// <summary>当前显示中的「批注中」提示弹窗（iNKORE MessageBox，非模态），用于重复触发时判断与外部关闭。</summary>
        private iNKORE.UI.WPF.Modern.Controls.MessageBox _annotationDotHintBox;

        /// <summary>
        /// 在 <see cref="ProcessCommittedStroke"/> 后调用，检测短墨迹（点击）并判断是否需要显示提示。
        /// 对极短墨迹（单点/包围盒小于阈值）补充可见点状墨迹。
        /// </summary>
        internal void HandleAnnotationDotAfterStroke(Stroke stroke)
        {
            try
            {
                if (stroke == null || stroke.StylusPoints.Count == 0) return;
                if (!IsAnnotating) return;
                if (currentMode == 1) return; // 白板模式不启用
                if (!Settings?.Canvas?.IsEnableAnnotationDotHint ?? true) return;

                // 注意：Stroke.GetBounds() 会按笔宽向外扩展（单击 ≈ 笔宽 + 路径跨度），
                // 笔较粗时单击也会超过阈值而被误判为书写，导致本功能整体失效。
                // 因此这里改用笔迹路径本身的跨度（仅由采样点构成，不含笔宽）判断。
                var pathExtent = GetStrokePathExtent(stroke);
                if (pathExtent.IsEmpty) return;
                double maxDim = Math.Max(pathExtent.Width, pathExtent.Height);
                double strokeThreshold = Settings.Canvas.AnnotationDotHintStrokeLengthThreshold;

                // 长笔迹 = 真实书写：重置 30 秒空闲计时器，并清空点击轨迹。
                if (maxDim > strokeThreshold)
                {
                    _lastLongStrokeTime = DateTime.Now;
                    _annotationDotPositions.Clear();
                    _lastClickWasIdle = false;
                    return;
                }

                var center = new Point(pathExtent.Left + pathExtent.Width / 2, pathExtent.Top + pathExtent.Height / 2);
                if (double.IsNaN(center.X) || double.IsNaN(center.Y)) return;

                // 对单点 / 极短墨迹补画可见圆点（视觉直径与笔的实际粗细一致，不加粗）
                EnsureDotVisible(stroke, center);

                bool isIdle = IsAnnotationIdle();

                // 刚跨过 30 秒空闲边界时清空点击轨迹：边界前按「非空闲」规则记录的点击
                // 不参与空闲期的「批注中」提示计数，否则空闲后的首次点击就可能误弹提示。
                if (isIdle && !_lastClickWasIdle)
                    _annotationDotPositions.Clear();
                _lastClickWasIdle = isIdle;

                if (isIdle)
                {
                    // 30 秒未书写：每次点击都显示指示小圆点
                    ShowAnnotationDotIndicator(center);
                    // 短时内连续点击达到阈值 → 显示「批注中」提示
                    TrackAnnotationDotPosition(center, showHint: true);
                }
                else
                {
                    // 30 秒内有书写：仅连续点击达到阈值时，在末次点击显示一次指示小圆点（不显示批注提示）
                    TrackAnnotationDotPosition(center, showHint: false);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"批注点提示判定失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 计算笔迹路径本身的包围盒（仅由采样点构成，不含笔宽外扩）。
        /// <see cref="Stroke.GetBounds"/> 会按 DrawingAttributes 宽高向外各扩一圈，
        /// 结果随笔粗增大，不能用于区分「点击」与「书写」。
        /// </summary>
        private static Rect GetStrokePathExtent(Stroke stroke)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (StylusPoint sp in stroke.StylusPoints)
            {
                if (sp.X < minX) minX = sp.X;
                if (sp.X > maxX) maxX = sp.X;
                if (sp.Y < minY) minY = sp.Y;
                if (sp.Y > maxY) maxY = sp.Y;
            }
            if (minX > maxX || minY > maxY) return Rect.Empty;
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 对点击产生的极短墨迹补充可见圆点。
        /// 使用 <see cref="CommitReason.CodeInput"/> 避免触发 <see cref="ProcessCommittedStroke"/> 递归。
        /// </summary>
        private void EnsureDotVisible(Stroke originalStroke, Point center)
        {
            if (inkCanvas == null) return;
            if (IsCurrentPageFrozen) return;

            try
            {
                // 单点墨迹（StylusPoints.Count == 1）在视觉上不可见，需补点
                // 多点但极短墨迹（如 2px 线段）可能也不明显，同样补点
                var pathExtent = GetStrokePathExtent(originalStroke);
                bool needsDot = originalStroke.StylusPoints.Count <= 1
                    || pathExtent.Width < 3
                    || pathExtent.Height < 3;

                if (!needsDot) return;

                var drawingAttrs = originalStroke.DrawingAttributes?.Clone()
                    ?? (inkCanvas.DefaultDrawingAttributes?.Clone()
                        ?? new DrawingAttributes { Color = Colors.Black, Width = 2, Height = 2 });

                // 墨迹的渲染直径 = 2×路径半径 + 笔画宽。取路径半径 = 原笔宽/4、笔画宽减半，
                // 使圆点最终视觉直径恰好等于笔的实际粗细，与非点击墨迹粗细一致。
                double radius = Math.Min(drawingAttrs.Width, drawingAttrs.Height) / 4;
                drawingAttrs.Width /= 2;
                drawingAttrs.Height /= 2;

                // 构建一个由 8 个点组成的微小圆，确保视觉可见
                var points = new StylusPointCollection();
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI * 2 * i / 8;
                    points.Add(new StylusPoint(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), 0.5f));
                }
                var dotStroke = new Stroke(points) { DrawingAttributes = drawingAttrs };

                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    inkCanvas.Strokes.Add(dotStroke);
                    timeMachine?.CommitStrokeUserInputHistory(new StrokeCollection { dotStroke });
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"批注点绘制失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 记录点击位置到追踪队列，并在连续点击达到阈值时执行相应动作。
        /// <paramref name="showHint"/> 为 true 时触发「批注中」提示（30 秒空闲后路径）；
        /// 为 false 时仅显示一次指示小圆点（30 秒内路径，不显示批注提示）。
        /// 仅检查最近 N 个点（N = 点击次数阈值），而非队列全部点，避免跨区域误判。
        /// </summary>
        private void TrackAnnotationDotPosition(Point position, bool showHint)
        {
            if (double.IsNaN(position.X) || double.IsNaN(position.Y)) return;

            _annotationDotPositions.Enqueue(position);
            while (_annotationDotPositions.Count > AnnotationDotMaxQueueSize)
                _annotationDotPositions.Dequeue();

            int clickCount = Settings.Canvas.AnnotationDotHintClickCount;
            double clusterRadius = Settings.Canvas.AnnotationDotHintClusterRadius;

            if (_annotationDotPositions.Count < clickCount) return;

            // 只检查最近 clickCount 个点是否在 clusterRadius 范围内
            // 而非队列中所有点，避免队列中混入旧区域点导致误判
            if (IsRecentClusterWithinRadius(clickCount, clusterRadius))
            {
                if (showHint)
                {
                    ShowAnnotationDotHint(position);
                }
                else
                {
                    // 30 秒内连续点击达到阈值：末次点击显示一次指示小圆点，但不显示「批注中」提示
                    ShowAnnotationDotIndicator(position);
                }
                _annotationDotPositions.Clear();
            }
        }

        /// <summary>
        /// 判断最近 N 个点击位置是否在指定半径内。
        /// </summary>
        private bool IsRecentClusterWithinRadius(int count, double radius)
        {
            // 将队列中最近 count 个点取出
            var points = new Point[count];
            var arr = _annotationDotPositions.ToArray();
            int start = arr.Length - count;
            for (int i = 0; i < count; i++)
                points[i] = arr[start + i];

            // 计算中心
            double cx = 0, cy = 0;
            for (int i = 0; i < count; i++)
            {
                cx += points[i].X;
                cy += points[i].Y;
            }
            cx /= count;
            cy /= count;

            // 检查每个点是否都在半径内
            double radiusSq = radius * radius;
            for (int i = 0; i < count; i++)
            {
                double dx = points[i].X - cx;
                double dy = points[i].Y - cy;
                if (dx * dx + dy * dy > radiusSq) return false;
            }
            return true;
        }

        /// <summary>
        /// 显示「批注中」提示弹框：使用 iNKORE MessageBox（非模态、不抢焦点、自动关闭）替代原 Popup。
        /// 定位策略与原 Popup 版本一致：先在画布坐标系内按锚点所在半屏对齐并钳制到画布内，
        /// 再通过 <see cref="MessageBoxHelper.TranslateToScreen"/> 换算为屏幕 DIP 坐标传给弹窗。
        /// </summary>
        private void ShowAnnotationDotHint(Point anchor)
        {
            _annotationDotPositions.Clear();

            if (_annotationDotHintVisible) return;
            if (inkCanvas == null) return;

            // MessageBox 按内容自适应，此处为对齐与钳制用的估算尺寸
            const double hintWidth = 360;
            const double hintHeight = 170;
            const double margin = 20;

            double canvasW = inkCanvas.ActualWidth;
            double canvasH = inkCanvas.ActualHeight;

            double hintLeft, hintTop;

            // 水平：靠近左半边时对齐左边缘，靠近右半边时对齐右边缘
            if (anchor.X < canvasW / 2)
            {
                // 左半边：提示左边缘对齐锚点
                hintLeft = anchor.X;
            }
            else
            {
                // 右半边：提示右边缘对齐锚点
                hintLeft = anchor.X - hintWidth;
            }

            // 垂直：上半边放锚点下方，下半边放锚点上方
            if (anchor.Y < canvasH / 2)
            {
                hintTop = anchor.Y + 10;
            }
            else
            {
                hintTop = anchor.Y - hintHeight - 10;
            }

            // 钳制到画布可见区域内
            if (hintLeft < margin)
                hintLeft = margin;
            if (hintLeft + hintWidth > canvasW - margin)
                hintLeft = canvasW - hintWidth - margin;
            if (hintTop < margin)
                hintTop = margin;
            if (hintTop + hintHeight > canvasH - margin)
                hintTop = canvasH - hintHeight - margin;

            // 画布坐标 → 屏幕 DIP 坐标（DPI 安全换算），失败则放弃本次提示
            var screenPoint = MessageBoxHelper.TranslateToScreen(inkCanvas, new Point(hintLeft, hintTop));
            if (double.IsNaN(screenPoint.X) || double.IsNaN(screenPoint.Y)) return;

            double displaySeconds = Settings?.Canvas?.AnnotationDotHintDisplayDurationSeconds ?? 3;

            var box = MessageBoxHelper.ShowAtNonBlocking(
                this,
                screenPoint.X, screenPoint.Y,
                FloatingBarStrings.Canvas_AnnotationDotHint_Text,
                string.Empty,
                MessageBoxButton.YesNo,
                MessageBoxImage.None,
                onClosed: result =>
                {
                    _annotationDotHintVisible = false;
                    _annotationDotHintBox = null;
                    if (result == MessageBoxResult.No)
                    {
                        // 「退出批注」：退出批注模式
                        CursorIcon_Click(null, null);
                    }
                },
                autoCloseSeconds: displaySeconds > 0 ? displaySeconds : (double?)null,
                configure: b =>
                {
                    b.YesButtonText = FloatingBarStrings.Canvas_AnnotationDotHint_Keep;
                    b.NoButtonText = FloatingBarStrings.Canvas_AnnotationDotHint_Exit;
                });

            if (box == null) return;
            _annotationDotHintVisible = true;
            _annotationDotHintBox = box;
        }

        /// <summary>
        /// 当前是否满足「30 秒未在白板内书写」的空闲条件。
        /// 从未书写过（<see cref="_lastLongStrokeTime"/> 为空）时视为空闲。
        /// </summary>
        private bool IsAnnotationIdle()
        {
            return !_lastLongStrokeTime.HasValue
                || (DateTime.Now - _lastLongStrokeTime.Value).TotalSeconds >= AnnotationDotIdleSeconds;
        }

        /// <summary>
        /// 显示「处于批注状态」的指示小圆点：带圆角容器，渐显出现、短暂停留后渐隐消失。
        /// Popup 使用 Placement=RelativePoint 且 PlacementTarget=inkCanvas，坐标直接使用画布（逻辑）坐标。
        /// </summary>
        private void ShowAnnotationDotIndicator(Point anchor)
        {
            var popup = AnnotationDotIndicatorPopup;
            var border = AnnotationDotIndicatorBorder;
            if (popup == null || border == null) return;

            // 容器尺寸与 XAML 定义一致（28×28），居中对齐点击位置
            const double size = 28;
            popup.HorizontalOffset = anchor.X - size / 2;
            popup.VerticalOffset = anchor.Y - size / 2;
            popup.IsOpen = true;

            // 渐显 → 短暂停留 → 渐隐
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(750))));
            anim.Completed += (s, e) =>
            {
                if (AnnotationDotIndicatorPopup != null)
                    AnnotationDotIndicatorPopup.IsOpen = false;
            };

            // 结束上一次未完成的动画，避免快速连点时叠加
            border.BeginAnimation(UIElement.OpacityProperty, null);
            border.Opacity = 0;
            border.BeginAnimation(UIElement.OpacityProperty, anim);
        }

    }
}