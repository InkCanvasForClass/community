using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 弹簧运动参数：阻尼比 + 无阻尼角频率（质量归一化为 1）。
    /// </summary>
    /// <remarks>
    /// 用阻尼比而不是直接给 stiffness/damping，是因为"手感"由它决定，调参时不必反推单位：
    /// ζ &lt; 1 会过冲（有弹性），ζ = 1 临界阻尼（最快且不过冲），ζ &gt; 1 偏黏。
    /// 停止条件与具体像素无关，按"位移足够小且速度足够小"判定，避免不同 DPI/缩放下残留抖动。
    /// </remarks>
    internal readonly struct SpringSpec
    {
        /// <summary>阻尼比。</summary>
        public readonly double DampingRatio;

        /// <summary>无阻尼角频率 ω₀（rad/s），越大越快。</summary>
        public readonly double AngularFrequency;

        /// <summary>位移收敛阈值（DIP）。</summary>
        public readonly double PositionTolerance;

        /// <summary>速度收敛阈值（DIP/s）。</summary>
        public readonly double VelocityTolerance;

        public SpringSpec(double dampingRatio, double angularFrequency,
            double positionTolerance = 0.25, double velocityTolerance = 6)
        {
            DampingRatio = dampingRatio;
            AngularFrequency = angularFrequency;
            PositionTolerance = positionTolerance;
            VelocityTolerance = velocityTolerance;
        }

        // 三档参数由固定步长积分实测标定（800px 位移下）：
        //   Gentle   临界阻尼，约 800ms 收敛，零过冲；
        //   Standard 约 300ms 收敛，800px 时轻微过冲 8px（≈1%），有"筋道"但不夸张；
        //   Snappy   约 230ms 收敛，几乎无过冲。
        // 调参时请重新实测收敛时间与过冲，不要再凭感觉改数字。

        /// <summary>柔和：临界阻尼，稍慢，长距离滑动时不会显得急。</summary>
        public static SpringSpec Gentle => new SpringSpec(1.0, 14);

        /// <summary>标准：轻微过冲，接近系统级动效手感。</summary>
        public static SpringSpec Standard => new SpringSpec(0.78, 32);

        /// <summary>轻快：更快且几乎不过冲，用于频繁触发的小位移。</summary>
        public static SpringSpec Snappy => new SpringSpec(0.92, 48);

        /// <summary>按设置里的三档预设取参数；越界值回落到标准。</summary>
        public static SpringSpec FromPreset(int preset)
        {
            switch (preset)
            {
                case 0: return Gentle;
                case 2: return Snappy;
                default: return Standard;
            }
        }
    }

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
    /// 视觉上不会出现"停一下再重来"，这正是旧实现里 CircleEase 硬切的问题。
    /// 同优先级：每个元素只允许一条弹簧在跑，后发起的调用接管前者。
    /// </remarks>
    internal sealed class SpringTranslateAnimator
    {
        // 弱引用缓存：动画器与元素同生命周期，窗口重建后不会把旧元素留在静态表里。
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, SpringTranslateAnimator> Cache = new();

        // 固定步长积分：帧间隔不均匀（掉帧、VSync 抖动）时，用固定步长比直接乘真实 dt 稳定，
        // 否则一帧卡顿会让弹簧"跳"过去甚至发散。
        private const double FixedStepSeconds = 1.0 / 240.0;
        private const double MaxFrameSeconds = 0.1;

        private readonly FrameworkElement _element;
        private readonly TranslateTransform _transform = new TranslateTransform();
        private readonly Stopwatch _clock = new Stopwatch();

        private double _offsetX, _offsetY;
        private double _velocityX, _velocityY;
        private double _targetX, _targetY;
        private SpringSpec _spec;
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

        /// <summary>
        /// 确认我们挂的 Transform 还在元素上。
        /// 其它动画辅助类（AnimationsHelper 等）会整体替换元素或子元素的 RenderTransform，
        /// 被换掉之后继续写旧 Transform 就是"动画跑了但画面没动"。每次启动前对齐一次。
        /// </summary>
        private void EnsureTransformAttached()
        {
            if (!ReferenceEquals(_element.RenderTransform, _transform))
            {
                _element.RenderTransform = _transform;
            }
        }

        /// <summary>当前视觉偏移（相对布局位置）。</summary>
        public Vector CurrentOffset => new Vector(_offsetX, _offsetY);

        /// <summary>当前偏移速度（DIP/s），用于中断时把动量带进新弹簧。</summary>
        public Vector CurrentVelocity => new Vector(_velocityX, _velocityY);

        public bool IsRunning => _completion != null;

        /// <summary>
        /// 立刻结束当前弹簧并把偏移归零（不改布局位置）。
        /// 布局即将被外部改写时先调用它，避免旧偏移叠加到新位置上造成跳变。
        /// </summary>
        public void SnapToLayout()
        {
            Stop();
            _offsetX = _offsetY = 0;
            _velocityX = _velocityY = 0;
            _transform.X = 0;
            _transform.Y = 0;
        }

        /// <summary>
        /// 从给定偏移起步，弹簧收敛到 <paramref name="to"/>（通常是零偏移 = 布局位置）。
        /// </summary>
        /// <param name="from">起始偏移。</param>
        /// <param name="to">目标偏移。</param>
        /// <param name="initialVelocity">初始速度（DIP/s），用于衔接被打断的上一次运动。</param>
        /// <param name="spec">弹簧参数。</param>
        /// <returns>弹簧静止后完成的 Task。</returns>
        public Task RunAsync(Vector from, Vector to, Vector initialVelocity, SpringSpec spec)
        {
            Stop();

            _offsetX = from.X;
            _offsetY = from.Y;
            _targetX = to.X;
            _targetY = to.Y;
            _velocityX = initialVelocity.X;
            _velocityY = initialVelocity.Y;
            _spec = spec;

            EnsureTransformAttached();
            _transform.X = _offsetX;
            _transform.Y = _offsetY;

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

        /// <summary>跳过动画直接落到目标偏移并完成。</summary>
        private void CompleteImmediately(TaskCompletionSource<bool> completion)
        {
            _offsetX = _targetX;
            _offsetY = _targetY;
            _velocityX = 0;
            _velocityY = 0;
            EnsureTransformAttached();
            _transform.X = _offsetX;
            _transform.Y = _offsetY;

            _completion = null;
            completion.TrySetResult(false);
        }

        /// <summary>兜底：无论求解进行到哪一步，直接钉到目标并结束。</summary>
        private void ForceSettle()
        {
            var completion = _completion;
            _offsetX = _targetX;
            _offsetY = _targetY;
            _velocityX = 0;
            _velocityY = 0;
            _transform.X = _offsetX;
            _transform.Y = _offsetY;

            Stop();
            completion?.TrySetResult(false);
        }

        /// <summary>实例级渲染不活跃（窗口隐藏等）时，直接把终点当结果并完成。</summary>
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
            if (frameSeconds > MaxFrameSeconds) frameSeconds = MaxFrameSeconds;

            int steps = Math.Max(1, (int)Math.Ceiling(frameSeconds / FixedStepSeconds));
            double dt = frameSeconds / steps;

            for (int i = 0; i < steps; i++)
            {
                Step(ref _offsetX, ref _velocityX, _targetX, dt);
                Step(ref _offsetY, ref _velocityY, _targetY, dt);
            }

            if (double.IsNaN(_offsetX) || double.IsNaN(_offsetY) ||
                double.IsInfinity(_offsetX) || double.IsInfinity(_offsetY))
            {
                // 数值发散兜底：宁可瞬间到位，也不要把元素甩到屏幕外
                _offsetX = _targetX;
                _offsetY = _targetY;
                _velocityX = 0;
                _velocityY = 0;
            }

            _transform.X = _offsetX;
            _transform.Y = _offsetY;

            bool settled =
                Math.Abs(_offsetX - _targetX) < _spec.PositionTolerance &&
                Math.Abs(_offsetY - _targetY) < _spec.PositionTolerance &&
                Math.Abs(_velocityX) < _spec.VelocityTolerance &&
                Math.Abs(_velocityY) < _spec.VelocityTolerance;

            if (!settled) return;

            _offsetX = _targetX;
            _offsetY = _targetY;
            _velocityX = 0;
            _velocityY = 0;
            _transform.X = _offsetX;
            _transform.Y = _offsetY;

            Stop();
            completion.TrySetResult(true);
        }

        /// <summary>
        /// 半隐式欧拉积分一步。相比显式欧拉，速度先更新再用于位移，
        /// 在高频弹簧（ω₀ 较大）下更稳定，不容易发散。
        /// </summary>
        private void Step(ref double position, ref double velocity, double target, double dt)
        {
            double k = _spec.AngularFrequency * _spec.AngularFrequency;      // 质量归一化 m = 1
            double c = 2 * _spec.DampingRatio * _spec.AngularFrequency;      // 阻尼系数

            double displacement = position - target;
            double acceleration = -k * displacement - c * velocity;

            velocity += acceleration * dt;
            position += velocity * dt;
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
