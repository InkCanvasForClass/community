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

                // 仅对极短墨迹（点击）进行追踪
                if (maxDim > strokeThreshold) return;

                var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                if (double.IsNaN(center.X) || double.IsNaN(center.Y)) return;

                // 对单点 / 极短墨迹补画可见圆点（不影响原始墨迹管线）
                EnsureDotVisible(stroke, center);

                TrackAnnotationDotPosition(center);
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

                drawingAttrs.Width = Math.Max(drawingAttrs.Width, 3);
                drawingAttrs.Height = Math.Max(drawingAttrs.Height, 3);

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
        /// 记录点击位置到追踪队列，并检查是否需要显示提示。
        /// 仅检查最近 N 个点（N = 点击次数阈值），而非队列全部点，
        /// 避免跨区域点击导致判定失败。
        /// </summary>
        private void TrackAnnotationDotPosition(Point position)
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
                ShowAnnotationDotHint(position);
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
        /// 显示批注状态提示。使用屏幕坐标绝对定位，边缘点击时对齐锚点而非居中。
        /// </summary>
        private void ShowAnnotationDotHint(Point anchor)
        {
            _annotationDotPositions.Clear();

            if (_annotationDotHintVisible) return;
            _annotationDotHintVisible = true;

            var popup = AnnotationDotHintPopup;
            if (popup == null) return;

            // 直接将画布坐标转为屏幕坐标（考虑 RenderTransform 等）
            var clickScreen = inkCanvas.PointToScreen(anchor);

            // 使用实际 Border 宽度，确保与 XAML 定义一致
            double hintWidth = (AnnotationDotHintBorder?.ActualWidth > 0) ? AnnotationDotHintBorder.ActualWidth : 380;
            double hintHeight = 60;
            const double margin = 20;

            var workArea = SystemParameters.WorkArea;
            double screenW = workArea.Width;
            double screenH = workArea.Height;

            double hintLeft, hintTop;

            // 水平：靠近左边缘时对齐左边缘，靠近右边缘时对齐右边缘
            if (clickScreen.X < workArea.Left + screenW / 2)
            {
                // 左半屏：提示左边缘对齐锚点
                hintLeft = clickScreen.X;
            }
            else
            {
                // 右半屏：提示右边缘对齐锚点
                hintLeft = clickScreen.X - hintWidth;
            }

            // 垂直：上半屏放锚点下方，下半屏放锚点上方
            if (clickScreen.Y < workArea.Top + screenH / 2)
            {
                hintTop = clickScreen.Y + 10;
            }
            else
            {
                hintTop = clickScreen.Y - hintHeight - 10;
            }

            // 钳制到屏幕工作区域内
            if (hintLeft < workArea.Left + margin)
                hintLeft = workArea.Left + margin;
            if (hintLeft + hintWidth > workArea.Right - margin)
                hintLeft = workArea.Right - hintWidth - margin;
            if (hintTop < workArea.Top + margin)
                hintTop = workArea.Top + margin;
            if (hintTop + hintHeight > workArea.Bottom - margin)
                hintTop = workArea.Bottom - hintHeight - margin;

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