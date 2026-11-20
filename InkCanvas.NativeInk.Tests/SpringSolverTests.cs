using System;
using System.Collections.Generic;
using Ink_Canvas.Helpers.Motion;

namespace InkCanvas.NativeInk.Tests
{
    /// <summary>
    /// 弹簧求解器行为测试。
    /// 这些断言把"手感"钉成可验证的数值：收敛时间、过冲幅度、掉帧稳定性、发散兜底。
    /// 改 <see cref="SpringSpec"/> 的预设参数必须同步更新这里的期望区间，
    /// 否则就是拿"感觉"改动画。
    /// </summary>
    internal static class SpringSolverTests
    {
        private const double FrameSeconds = 1.0 / 60.0;

        public static void RunAll()
        {
            Run(nameof(StandardSettlesWithinExpectedWindow), StandardSettlesWithinExpectedWindow);
            Run(nameof(GentleIsSlowerThanStandardAndSnappyIsFaster), GentleIsSlowerThanStandardAndSnappyIsFaster);
            Run(nameof(StandardOvershootStaysSubtle), StandardOvershootStaysSubtle);
            Run(nameof(CriticalDampingNeverOvershoots), CriticalDampingNeverOvershoots);
            Run(nameof(SettlesAtExactTargetWithoutResidue), SettlesAtExactTargetWithoutResidue);
            Run(nameof(DroppedFramesDoNotDestabilize), DroppedFramesDoNotDestabilize);
            Run(nameof(LongStallIsClamped), LongStallIsClamped);
            Run(nameof(InitialVelocityIsPreserved), InitialVelocityIsPreserved);
            Run(nameof(AnimationIsMonotonicTowardsTargetForShortMove), AnimationIsMonotonicTowardsTargetForShortMove);
            Run(nameof(FromPresetFallsBackToStandard), FromPresetFallsBackToStandard);
            Run(nameof(SnapToLayoutIsImmediate), SnapToLayoutIsImmediate);
        }

        private static Simulation Simulate(SpringSpec spec, double start, double target,
            double initialVelocity = 0, double frameSeconds = FrameSeconds, int maxFrames = 600)
        {
            var solver = new SpringSolver(spec, start, initialVelocity);
            var positions = new List<double>();
            var maxOvershoot = 0.0;
            var settleFrame = -1;

            for (var frame = 0; frame < maxFrames; frame++)
            {
                solver.Advance(target, frameSeconds);
                positions.Add(solver.Position);

                var overshoot = (target - start) > 0
                    ? solver.Position - target
                    : target - solver.Position;
                if (overshoot > maxOvershoot) maxOvershoot = overshoot;

                if (settleFrame < 0 && solver.IsSettled(target))
                {
                    settleFrame = frame;
                }

                if (solver.IsDiverged()) break;
            }

            return new Simulation(positions, maxOvershoot, settleFrame, solver.Position, solver.Velocity);
        }

        /// <summary>标准档：800px 位移、60fps 下应在 0.2s ~ 0.6s 之间收敛，且确实收敛。</summary>
        private static void StandardSettlesWithinExpectedWindow()
        {
            var result = Simulate(SpringSpec.Standard, -800, 0);
            True(result.SettleFrame >= 0);
            var seconds = result.SettleFrame * FrameSeconds;
            True(seconds >= 0.20 && seconds <= 0.60);
        }

        /// <summary>三档快慢顺序必须是 柔和 &lt; 标准 &lt; 轻快（这里指耗时递减）。</summary>
        private static void GentleIsSlowerThanStandardAndSnappyIsFaster()
        {
            var gentle = Simulate(SpringSpec.Gentle, -800, 0).SettleFrame;
            var standard = Simulate(SpringSpec.Standard, -800, 0).SettleFrame;
            var snappy = Simulate(SpringSpec.Snappy, -800, 0).SettleFrame;

            True(gentle > standard);
            True(standard > snappy);
        }

        /// <summary>标准档允许轻微过冲，但必须低于位移的 3%（800px 即 24px）。</summary>
        private static void StandardOvershootStaysSubtle()
        {
            var result = Simulate(SpringSpec.Standard, -800, 0);
            True(result.MaxOvershoot < 24);
        }

        /// <summary>临界阻尼（ζ = 1）不允许过冲，否则"柔和"档会显得弹。</summary>
        private static void CriticalDampingNeverOvershoots()
        {
            var result = Simulate(SpringSpec.Gentle, -800, 0);
            True(result.MaxOvershoot < 1.0);
        }

        /// <summary>收敛后位置必须精确等于目标、速度归零，不留残差抖动。</summary>
        private static void SettlesAtExactTargetWithoutResidue()
        {
            var solver = new SpringSolver(SpringSpec.Standard, -800, 0);
            for (var i = 0; i < 300 && !solver.IsSettled(0); i++)
            {
                solver.Advance(0, FrameSeconds);
            }

            solver.Settle(0);
            Equal(0.0, solver.Position);
            Equal(0.0, solver.Velocity);
        }

        /// <summary>
        /// 掉帧必须稳定：同样的总时长，用 60fps 和用 15fps（每帧 66ms）推进，
        /// 最终位置都应收敛且不出现发散。固定步长积分是这条的前提。
        /// </summary>
        private static void DroppedFramesDoNotDestabilize()
        {
            var normal = Simulate(SpringSpec.Standard, -800, 0, 0, 1.0 / 60.0);
            var janky = Simulate(SpringSpec.Standard, -800, 0, 0, 1.0 / 15.0, 200);

            True(normal.SettleFrame >= 0);
            True(janky.SettleFrame >= 0);
            True(Math.Abs(normal.FinalPosition) < 1.0);
            True(Math.Abs(janky.FinalPosition) < 1.0);
        }

        /// <summary>超长卡顿（&gt; MaxFrameSeconds）必须被截断，不能一步跨过整段动画。</summary>
        private static void LongStallIsClamped()
        {
            var solver = new SpringSolver(SpringSpec.Standard, -800, 0);
            var advanced = solver.Advance(0, 5.0);

            Equal(SpringSolver.MaxFrameSeconds, advanced);
            True(solver.Position > -800);
        }

        /// <summary>打断时的动量必须带进新弹簧：初始速度要真的生效。</summary>
        private static void InitialVelocityIsPreserved()
        {
            var withVelocity = new SpringSolver(SpringSpec.Standard, 0, 4000);
            var withoutVelocity = new SpringSolver(SpringSpec.Standard, 0, 0);

            withVelocity.Advance(0, FrameSeconds);
            withoutVelocity.Advance(0, FrameSeconds);

            // 初始速度为正，第一帧位移必须比无初速的大
            True(withVelocity.Position > withoutVelocity.Position);
        }

        /// <summary>小位移（20px）不应过冲：短距离的手感要干净。</summary>
        private static void AnimationIsMonotonicTowardsTargetForShortMove()
        {
            var result = Simulate(SpringSpec.Standard, -20, 0);
            True(result.MaxOvershoot < 2.0);
            True(result.SettleFrame >= 0);
        }

        /// <summary>预设越界必须回落到标准档，避免坏配置导致动画"消失"。</summary>
        private static void FromPresetFallsBackToStandard()
        {
            Equal(SpringSpec.Standard.AngularFrequency, SpringSpec.FromPreset(-5).AngularFrequency);
            Equal(SpringSpec.Standard.AngularFrequency, SpringSpec.FromPreset(99).AngularFrequency);
            Equal(SpringSpec.Gentle.AngularFrequency, SpringSpec.FromPreset(0).AngularFrequency);
            Equal(SpringSpec.Snappy.AngularFrequency, SpringSpec.FromPreset(2).AngularFrequency);
        }

        /// <summary>SnapToLayout 语义：钉到目标立即静止，速度清零。</summary>
        private static void SnapToLayoutIsImmediate()
        {
            var solver = new SpringSolver(SpringSpec.Standard, -500, 1200);
            solver.Settle(0);

            Equal(0.0, solver.Position);
            Equal(0.0, solver.Velocity);
            True(solver.IsSettled(0));
        }

        private readonly struct Simulation
        {
            public readonly List<double> Positions;
            public readonly double MaxOvershoot;
            public readonly int SettleFrame;
            public readonly double FinalPosition;
            public readonly double FinalVelocity;

            public Simulation(List<double> positions, double maxOvershoot, int settleFrame,
                double finalPosition, double finalVelocity)
            {
                Positions = positions;
                MaxOvershoot = maxOvershoot;
                SettleFrame = settleFrame;
                FinalPosition = finalPosition;
                FinalVelocity = finalVelocity;
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
                Environment.ExitCode = 1;
                throw;
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("Expected true.");
        }
    }
}
