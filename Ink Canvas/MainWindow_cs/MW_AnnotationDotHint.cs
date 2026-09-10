using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        /// <summary>提示自动隐藏计时器。</summary>
        private DispatcherTimer _annotationDotHintTimer;
        /// <summary>提示是否正在显示。</summary>
        private bool _annotationDotHintVisible;

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

                var bounds = stroke.GetBounds();
                double maxDim = Math.Max(bounds.Width, bounds.Height);
                double strokeThreshold = Settings.Canvas.AnnotationDotHintStrokeLengthThreshold;

                // 长笔迹 = 真实书写：重置 30 秒空闲计时器，并清空点击轨迹。
                if (maxDim > strokeThreshold)
                {
                    _lastLongStrokeTime = DateTime.Now;
                    _annotationDotPositions.Clear();
                    return;
                }

                var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                if (double.IsNaN(center.X) || double.IsNaN(center.Y)) return;

                // 对单点 / 极短墨迹补画可见圆点（按笔的实际粗细，不做加粗）
                EnsureDotVisible(stroke, center);

                if (IsAnnotationIdle())
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
                bool needsDot = originalStroke.StylusPoints.Count <= 1
                    || originalStroke.GetBounds().Width < 3
                    || originalStroke.GetBounds().Height < 3;

                if (!needsDot) return;

                var drawingAttrs = originalStroke.DrawingAttributes?.Clone()
                    ?? (inkCanvas.DefaultDrawingAttributes?.Clone()
                        ?? new DrawingAttributes { Color = Colors.Black, Width = 2, Height = 2 });

                // 按笔的实际粗细渲染，不做加粗处理。

                // 构建一个由 8 个点组成的微小圆（半径 2px），确保视觉可见
                var points = new StylusPointCollection();
                double r = 2;
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI * 2 * i / 8;
                    points.Add(new StylusPoint(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle)));
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
        /// 显示批注状态提示。Popup 使用 Placement=RelativePoint 且 PlacementTarget=inkCanvas，
        /// 偏移量直接使用画布（逻辑）坐标，避免 DPI 缩放下的屏幕坐标换算偏差。
        /// 靠近画布边缘时对齐锚点而非居中。
        /// </summary>
        private void ShowAnnotationDotHint(Point anchor)
        {
            _annotationDotPositions.Clear();

            if (_annotationDotHintVisible) return;
            _annotationDotHintVisible = true;

            var popup = AnnotationDotHintPopup;
            if (popup == null) return;

            // 使用实际 Border 尺寸，与 XAML 定义一致；未布局时回退到 XAML 固定值
            double hintWidth = (AnnotationDotHintBorder?.ActualWidth > 0) ? AnnotationDotHintBorder.ActualWidth : 295;
            double hintHeight = (AnnotationDotHintBorder?.ActualHeight > 0) ? AnnotationDotHintBorder.ActualHeight : 60;
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

            popup.HorizontalOffset = hintLeft;
            popup.VerticalOffset = hintTop;
            popup.IsOpen = true;

            if (AnnotationDotHintBorder != null)
            {
                AnnotationDotHintBorder.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                AnnotationDotHintBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            StopAnnotationDotHintTimer();
            double displaySeconds = Settings?.Canvas?.AnnotationDotHintDisplayDurationSeconds ?? 3;
            _annotationDotHintTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(displaySeconds)
            };
            _annotationDotHintTimer.Tick += AnnotationDotHintTimer_Tick;
            _annotationDotHintTimer.Start();
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

        private void HideAnnotationDotHint()
        {
            _annotationDotHintVisible = false;
            StopAnnotationDotHintTimer();

            if (AnnotationDotHintBorder != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, e) =>
                {
                    if (AnnotationDotHintPopup != null)
                        AnnotationDotHintPopup.IsOpen = false;
                };
                AnnotationDotHintBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else
            {
                if (AnnotationDotHintPopup != null)
                    AnnotationDotHintPopup.IsOpen = false;
            }
        }

        private void StopAnnotationDotHintTimer()
        {
            if (_annotationDotHintTimer != null)
            {
                _annotationDotHintTimer.Stop();
                _annotationDotHintTimer.Tick -= AnnotationDotHintTimer_Tick;
                _annotationDotHintTimer = null;
            }
        }

        private void AnnotationDotHintTimer_Tick(object sender, EventArgs e)
        {
            HideAnnotationDotHint();
        }

        private void AnnotationDotHintKeep_Click(object sender, RoutedEventArgs e)
        {
            HideAnnotationDotHint();
        }

        private void AnnotationDotHintExit_Click(object sender, RoutedEventArgs e)
        {
            HideAnnotationDotHint();
            CursorIcon_Click(null, null);
        }
    }
}