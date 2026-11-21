using System;
using System.Collections.Generic;
using Ink_Canvas.Helpers.Motion;

namespace InkCanvas.NativeInk.Tests
{
    /// <summary>
    /// 菜单动画时钟规则测试。
    /// </summary>
    /// <remarks>
    /// 曲线测试（<see cref="PopupCurveTests"/>）保证"progress 该多大"，
    /// 这里保证"progress 每帧走多少"：单帧上限、以及打断时不许改时长。
    /// 后者曾经是真 bug —— 打断后按新方向取另一个时长，观感是反向那一帧突然变急。
    /// </remarks>
    internal static class PopupTimingTests
    {
        private const double PopInSeconds = 0.20;
        private const double PopOutSeconds = 0.15;

        public static void RunAll()
        {
            Run(nameof(FrameStepIsClampedSoCurveIsNotSkipped), FrameStepIsClampedSoCurveIsNotSkipped);
            Run(nameof(InterruptKeepsDurationSoSpeedDoesNotJump), InterruptKeepsDurationSoSpeedDoesNotJump);
            Run(nameof(FreshStartUsesNominalDurationForBothDirections), FreshStartUsesNominalDurationForBothDirections);
            Run(nameof(InterruptedCloseReversesWithoutChangingSpeed), InterruptedCloseReversesWithoutChangingSpeed);
            Run(nameof(InvalidNominalFallsBackToCurrentOrClamp), InvalidNominalFallsBackToCurrentOrClamp);
        }

        private static void FrameStepIsClampedSoCurveIsNotSkipped()
        {
            // 一帧 100ms 的卡顿必须被夹到 40ms，否则 progress 一步跨过整个过冲区。
            True(PopupTiming.MaxFrameSeconds > 0.0);

            var clamped = Math.Min(0.1, PopupTiming.MaxFrameSeconds);
            Equal(PopupTiming.MaxFrameSeconds, clamped);

            // 夹取后单帧仍然只走完曲线的一小段：曲线越过 1.0 后的那一段收尾回弹
            // 至少要能被多帧采到，不能被一帧跨过去。
            var step = clamped / PopInSeconds;
            True(step < 0.35);
        }

        private static void InterruptKeepsDurationSoSpeedDoesNotJump()
        {
            // 打开途中被打断：开场时长 0.20 正在用，收起时不能用 0.15。
            var duration = PopupTiming.ResolveDuration(PopOutSeconds, PopInSeconds, isRunning: true);
            Equal(PopInSeconds, duration);

            // 收起途中被打断：同理保持 0.15。
            var back = PopupTiming.ResolveDuration(PopInSeconds, PopOutSeconds, isRunning: true);
            Equal(PopOutSeconds, back);
        }

        private static void FreshStartUsesNominalDurationForBothDirections()
        {
            Equal(PopInSeconds, PopupTiming.ResolveDuration(PopInSeconds, PopOutSeconds, isRunning: false));
            Equal(PopOutSeconds, PopupTiming.ResolveDuration(PopOutSeconds, PopInSeconds, isRunning: false));
        }

        private static void InterruptedCloseReversesWithoutChangingSpeed()
        {
            // 逐帧模拟：打开 6 帧后打断收起，再 4 帧后打断打开。
            // 三次播放各自的每帧步进必须一致（只有符号不同）。
            const double frame = 1.0 / 60.0;

            var progress = 0.0;
            var duration = PopupTiming.ResolveDuration(PopInSeconds, PopupTiming.MaxFrameSeconds, isRunning: false);
            var steps = new List<double>();

            for (var i = 0; i < 6; i++)
            {
                var step = frame / duration;
                steps.Add(step);
                progress = Math.Min(1.0, progress + step);
            }

            var openDuration = duration;

            duration = PopupTiming.ResolveDuration(PopOutSeconds, duration, isRunning: true);
            for (var i = 0; i < 4; i++)
            {
                var step = frame / duration;
                steps.Add(step);
                progress = Math.Max(0.0, progress - step);
            }

            duration = PopupTiming.ResolveDuration(PopInSeconds, duration, isRunning: true);

            // 打断两次之后仍然是一次都没换过时长。
            Equal(openDuration, duration);
            foreach (var step in steps) Equal(steps[0], step);
        }

        private static void InvalidNominalFallsBackToCurrentOrClamp()
        {
            Equal(PopOutSeconds, PopupTiming.ResolveDuration(0.0, PopOutSeconds, isRunning: false));
            Equal(PopOutSeconds, PopupTiming.ResolveDuration(-1.0, PopOutSeconds, isRunning: false));
            Equal(PopupTiming.MaxFrameSeconds, PopupTiming.ResolveDuration(0.0, 0.0, isRunning: false));
            Equal(PopupTiming.MaxFrameSeconds, PopupTiming.ResolveDuration(double.NaN, 0.0, isRunning: false));
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
