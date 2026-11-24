using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 基于弹簧模型的 2D 平移动画驱动器。
    /// </summary>
    /// <remarks>
    /// 关键设计：**不逐帧改布局属性**。WPF 里 Margin / Width / Left 这类属性每帧变化都会触发
    /// 一次完整布局 + 重绘，动画帧一多就掉帧，而且这是 UI 线程上的死开销，无法被合成线程接管。
    /// 这里改成：布局属性一次直接落到终点（只布局一次），视觉位移交给 <see cref="TranslateTransform"/>，
    /// 由合成线程插值。效果等价于"元素从 A 滑到 B"，但每帧不再有布局开销。
    ///
    /// 另一个好处是中断友好：弹簧自带速度状态，重定向时把当前位移与速度带进新的求解，
    /// 视觉上不会出现"停一下再重来"，这正是旧实现里固定缓动曲线硬切的问题。
    /// 同优先级：每个元素只允许一条弹簧在跑，后发起的调用接管前者。
    /// 数值求解本身在 <see cref="SpringSolver"/> 里，与 WPF 解耦、可单测。
    /// </remarks>
    public sealed class SpringTranslateAnimator
    {
        private static readonly ConditionalWeakTable<FrameworkElement, SpringTranslateAnimator> Cache = new();

        // 每一维独立求解（X/Y 互不影响）
        private SpringSolver _solverX;
        private SpringSolver _solverY;

        private readonly FrameworkElement _element;
        private readonly TranslateTransform _transform = new TranslateTransform();
        private readonly Stopwatch _clock = new Stopwatch();

        private double _targetX, _targetY;
        private TaskCompletionSource<bool> _completion;

        // 兜底定时器：CompositionTarget.Rendering 依赖合成线程持续出帧，
        // 窗口被隐藏/最小化（ThoroughlyHideWhenFolded）或截图冻结时可能不再回调。
        // 没有这个兜底，await RunAsync 的调用方会永远等下去——折叠状态机的
        // _foldTransitionRunning 会卡在 true，之后所有折叠/展开请求都只能排队，
        // 表现为"工具栏再也收不起来"。宁可瞬间到位，也不能挂住。
        private DispatcherTimer _watchdog;
        private static readonly TimeSpan MaxMotionDuration = TimeSpan.FromSeconds(2.5);

        private SpringTranslateAnimator(FrameworkElement element)
        {
            _element = element;
            element.RenderTransform = _transform;
        }

        /// <summary>取得元素对应的驱动器（每个元素一个，重复使用同一个 Transform）。</summary>
        public static SpringTranslateAnimator For(FrameworkElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return Cache.GetValue(element, el => new SpringTranslateAnimator(el));
        }

        /// <summary>当前视觉偏移（相对布局位置）。</summary>
        public Vector CurrentOffset => new Vector(_solverX.Position, _solverY.Position);

        /// <summary>当前偏移速度（DIP/s），用于中断时把动量带进新弹簧。</summary>
        public Vector CurrentVelocity => new Vector(_solverX.Velocity, _solverY.Velocity);

        public bool IsRunning => _completion != null;

        /// <summary>
        /// 立刻结束当前弹簧并把偏移归零（不改布局位置）。
        /// 布局即将被外部改写时先调用它，避免旧偏移叠加到新位置上造成跳变。
        /// </summary>
        public void SnapToLayout()
        {
            Stop();
            _solverX.Settle(0);
            _solverY.Settle(0);
            _transform.X = 0;
            _transform.Y = 0;
        }

        /// <summary>
        /// 立刻把视觉偏移设成指定值（不播放动画）。
        /// 用于建立控件时的初始偏移：之后第一次 <see cref="RunAsync"/> 才能从正确的位置起步，
        /// 而不是先跳一下再动。
        /// </summary>
        public void SetOffset(Vector offset)
        {
            Stop();
            _solverX.Settle(offset.X);
            _solverY.Settle(offset.Y);
            EnsureTransformAttached();
            _transform.X = offset.X;
            _transform.Y = offset.Y;
        }

        /// <summary>
        /// 从给定偏移起步，弹簧收敛到 <paramref name="to"/>（通常是零偏移 = 布局位置）。
        /// </summary>
        /// <param name="from">起始偏移。</param>
        /// <param name="to">目标偏移。</param>
        /// <param name="initialVelocity">初始速度（DIP/s），用于衔接被打断的上一次运动。</param>
        /// <param name="spec">弹簧参数。</param>
        /// <returns>弹簧静止后完成的任务。</returns>
        public Task RunAsync(Vector from, Vector to, Vector initialVelocity, SpringSpec spec)
        {
            Stop();

            _solverX = new SpringSolver(spec, from.X, initialVelocity.X);
            _solverY = new SpringSolver(spec, from.Y, initialVelocity.Y);
            _targetX = to.X;
            _targetY = to.Y;

            EnsureTransformAttached();
            _transform.X = from.X;
            _transform.Y = from.Y;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;

            // 元素不可见时不必等帧：直接到位，避免无声地耗满兜底时间
            if (!_element.IsVisible)
            {
                CompleteImmediately(completion);
                return completion.Task;
            }

            _clock.Restart();
            CompositionTarget.Rendering += OnRendering;

            _watchdog?.Stop();
            _watchdog = new DispatcherTimer(DispatcherPriority.Send, _element.Dispatcher)
            {
                Interval = MaxMotionDuration
            };
            _watchdog.Tick += (_, _) =>
            {
                _watchdog?.Stop();
                if (_completion != null) ForceSettle();
            };
            _watchdog.Start();

            return completion.Task;
        }

        /// <summary>
        /// 其它动画辅助类会整体替换元素的 RenderTransform，被换掉之后继续写旧 Transform
        /// 就是"动画跑了但画面没动"。每次启动前对齐一次。
        /// </summary>
        private void EnsureTransformAttached()
        {
            if (!ReferenceEquals(_element.RenderTransform, _transform))
            {
                _element.RenderTransform = _transform;
            }
        }

        /// <summary>跳过动画直接落到目标偏移并完成。</summary>
        private void CompleteImmediately(TaskCompletionSource<bool> completion)
        {
            _solverX.Settle(_targetX);
            _solverY.Settle(_targetY);
            EnsureTransformAttached();
            _transform.X = _targetX;
            _transform.Y = _targetY;

            _completion = null;
            completion.TrySetResult(false);
        }

        /// <summary>兜底：无论求解进行到哪一步，直接钉到目标并结束。</summary>
        private void ForceSettle()
        {
            var completion = _completion;
            _solverX.Settle(_targetX);
            _solverY.Settle(_targetY);
            _transform.X = _targetX;
            _transform.Y = _targetY;

            Stop();
            completion?.TrySetResult(false);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            var completion = _completion;
            if (completion == null)
            {
                CompositionTarget.Rendering -= OnRendering;
                return;
            }

            double frameSeconds = _clock.Elapsed.TotalSeconds;
            _clock.Restart();
            if (frameSeconds <= 0) return;

            _solverX.Advance(_targetX, frameSeconds);
            _solverY.Advance(_targetY, frameSeconds);

            if (_solverX.IsDiverged() || _solverY.IsDiverged())
            {
                // 数值发散兜底：宁可瞬间到位，也不要把元素甩到屏幕外
                ForceSettle();
                return;
            }

            _transform.X = _solverX.Position;
            _transform.Y = _solverY.Position;

            if (!_solverX.IsSettled(_targetX) || !_solverY.IsSettled(_targetY)) return;

            _solverX.Settle(_targetX);
            _solverY.Settle(_targetY);
            _transform.X = _targetX;
            _transform.Y = _targetY;

            Stop();
            completion.TrySetResult(true);
        }

        private void Stop()
        {
            CompositionTarget.Rendering -= OnRendering;
            _clock.Stop();

            _watchdog?.Stop();
            _watchdog = null;

            var completion = _completion;
            _completion = null;
            completion?.TrySetResult(true);
        }
    }
}
