using System;

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
    public readonly struct SpringSpec
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

        // 三档参数由 SpringSolver 的固定步长积分实测标定（800px 位移下）：
        //   Gentle   临界阻尼，约 800ms 收敛，零过冲；
        //   Standard 约 300ms 收敛，800px 时轻微过冲 8px（≈1%），有"筋道"但不夸张；
        //   Snappy   约 230ms 收敛，几乎无过冲。
        // 调参时请用 SpringSolverTests 重新实测收敛时间与过冲，不要凭感觉改数字。

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
    /// 一维阻尼弹簧的数值求解器（纯数学，不依赖 WPF，可直接单元测试）。
    /// </summary>
    /// <remarks>
    /// 质量归一化为 1，方程：x'' = -ω₀²(x - target) - 2ζω₀·x'。
    /// 采用固定步长的半隐式（辛）欧拉：先更新速度再用新速度更新位置。
    /// 相比显式欧拉，它在高 ω₀ 下不发散；固定步长则保证掉帧（帧间隔突然变大）时
    /// 行为不跳变——调用方把真实帧间隔切成若干等长小步即可。
    /// </remarks>
    public struct SpringSolver
    {
        /// <summary>推荐积分步长（秒）。</summary>
        public const double DefaultFixedStepSeconds = 1.0 / 240.0;

        /// <summary>单帧最大推进时间（秒）：长时间卡顿后不要一次跨过整段动画。</summary>
        public const double MaxFrameSeconds = 0.1;

        public double Position;
        public double Velocity;

        public readonly SpringSpec Spec;

        public SpringSolver(SpringSpec spec, double position, double velocity)
        {
            Spec = spec;
            Position = position;
            Velocity = velocity;
        }

        /// <summary>按给定步长推进一次。</summary>
        public void StepTowards(double target, double dt)
        {
            if (dt <= 0) return;

            double k = Spec.AngularFrequency * Spec.AngularFrequency;
            double c = 2 * Spec.DampingRatio * Spec.AngularFrequency;

            double displacement = Position - target;
            double acceleration = -k * displacement - c * Velocity;

            Velocity += acceleration * dt;
            Position += Velocity * dt;
        }

        /// <summary>
        /// 按真实帧间隔推进：内部切成等长小步，超过 <see cref="MaxFrameSeconds"/> 的部分被截断。
        /// </summary>
        /// <returns>本帧实际推进的时间（秒），截断后的值。</returns>
        public double Advance(double target, double frameSeconds)
        {
            if (frameSeconds <= 0) return 0;
            if (frameSeconds > MaxFrameSeconds) frameSeconds = MaxFrameSeconds;

            int steps = Math.Max(1, (int)Math.Ceiling(frameSeconds / DefaultFixedStepSeconds));
            double dt = frameSeconds / steps;

            for (int i = 0; i < steps; i++)
            {
                StepTowards(target, dt);
            }

            return frameSeconds;
        }

        /// <summary>是否已静止在目标附近。</summary>
        public readonly bool IsSettled(double target)
        {
            return Math.Abs(Position - target) < Spec.PositionTolerance
                && Math.Abs(Velocity) < Spec.VelocityTolerance;
        }

        /// <summary>数值是否发散（NaN/Infinity）。</summary>
        public readonly bool IsDiverged()
        {
            return double.IsNaN(Position) || double.IsInfinity(Position)
                || double.IsNaN(Velocity) || double.IsInfinity(Velocity);
        }

        /// <summary>直接钉到目标并清零速度（兜底、强制结束时用）。</summary>
        public void Settle(double target)
        {
            Position = target;
            Velocity = 0;
        }
    }
}
