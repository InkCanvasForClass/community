using System;
using System.Collections.Generic;
using Ink_Canvas.Helpers.Motion;

namespace InkCanvas.NativeInk.Tests
{
    /// <summary>
    /// 菜单弹出曲线测试。
    /// </summary>
    /// <remarks>
    /// 这些断言把"手感"钉成可验证的数值：起止值、峰值位置与幅度、
    /// 先快后慢的程度、峰值处有没有折角、60fps 采样下过冲还剩几帧、
    /// 以及打断为什么天然连续（曲线只与 progress 有关，与方向无关）。
    ///
    /// 改 <see cref="PopupCurve"/> 的任何常量都必须同步更新这里的期望区间，
    /// 否则就是拿"感觉"改动画。
    /// </remarks>
    internal static class PopupCurveTests
    {
        private const double FrameSeconds = 1.0 / 60.0;

        /// <summary>出场时长，与 AnimationsHelper.PopupPopInDurationSeconds 一致。</summary>
        private const double PopInSeconds = 0.15;

        /// <summary>收场时长，与 AnimationsHelper.PopupPopOutDurationSeconds 一致。</summary>
        private const double PopOutSeconds = 0.10;

        public static void RunAll()
        {
            Run(nameof(CurveStartsCollapsedAndEndsAtNormalSize), CurveStartsCollapsedAndEndsAtNormalSize);
            Run(nameof(CurvePeaksExactlyAtDesignedPosition), CurvePeaksExactlyAtDesignedPosition);
            Run(nameof(CurveRisesThenFallsWithoutBouncing), CurveRisesThenFallsWithoutBouncing);
            Run(nameof(RiseIsFrontLoadedAndDecelerates), RiseIsFrontLoadedAndDecelerates);
            Run(nameof(PeakHasNoCorner), PeakHasNoCorner);
            Run(nameof(OpenAndCloseShareOneCurve), OpenAndCloseShareOneCurve);
            Run(nameof(NoJumpWhenDirectionFlips), NoJumpWhenDirectionFlips);
            Run(nameof(SixtyFpsOpenStillShowsOvershoot), SixtyFpsOpenStillShowsOvershoot);
            Run(nameof(SixtyFpsCloseStillShowsOvershoot), SixtyFpsCloseStillShowsOvershoot);
            Run(nameof(SlideAndOpacityFollowProgressMonotonically), SlideAndOpacityFollowProgressMonotonically);
        }

        private static void CurveStartsCollapsedAndEndsAtNormalSize()
        {
            var atZero = PopupCurve.Evaluate(0.0);
            Equal(PopupCurve.StartScale, atZero.Scale);
            Equal(PopupCurve.SlideDistance, atZero.SlideY);
            Equal(PopupCurve.MinOpacityWeight, atZero.OpacityWeight);

            var atOne = PopupCurve.Evaluate(1.0);
            Equal(1.0, atOne.Scale);
            Equal(0.0, atOne.SlideY);
            Equal(1.0, atOne.OpacityWeight);

            // 越界输入必须被夹住：动画被强行反转时进度理论上不会越界，
            // 但 dt 夹取与浮点累加仍可能产生 1.0000000001。
            Equal(PopupCurve.StartScale, PopupCurve.Evaluate(-3.0).Scale);
            Equal(1.0, PopupCurve.Evaluate(7.0).Scale);
            Equal(PopupCurve.StartScale, PopupCurve.Evaluate(double.NaN).Scale);
        }

        private static void CurvePeaksExactlyAtDesignedPosition()
        {
            Equal(PopupCurve.PeakScale, PopupCurve.Evaluate(PopupCurve.PeakAt).Scale);

            // 峰值必须是全局最大值，且就出现在 PeakAt 这一处
            var best = double.NegativeInfinity;
            var bestAt = 0.0;
            for (var i = 0; i <= 2000; i++)
            {
                var progress = i / 2000.0;
                var scale = PopupCurve.Evaluate(progress).Scale;
                if (scale <= best) continue;
                best = scale;
                bestAt = progress;
            }

            // 扫格点取最大值，格点未必正好落在 PeakAt 上，因此只要求贴近峰值
            True(Math.Abs(best - PopupCurve.PeakScale) < 0.001);
            True(Math.Abs(bestAt - PopupCurve.PeakAt) < 0.002);
        }

        private static void CurveRisesThenFallsWithoutBouncing()
        {
            // 过冲只允许发生一次：scale > 1 的进度集合必须是单个连续区间。
            // 否则视觉上会"大到小再大"，即用户说的"晃"。
            var aboveOne = 0;
            var segments = 0;
            var inside = false;
            for (var i = 0; i <= 2000; i++)
            {
                var isAbove = PopupCurve.Evaluate(i / 2000.0).Scale > 1.0;
                if (isAbove) aboveOne++;
                if (isAbove && !inside) segments++;
                inside = isAbove;
            }

            Equal(1, segments);
            True(aboveOne > 0);
        }

        private static void RiseIsFrontLoadedAndDecelerates()
        {
            // "先快后慢"：半程行程应当远早于上升段的中点完成。
            var halfTravel = PopupCurve.StartScale + (PopupCurve.PeakScale - PopupCurve.StartScale) * 0.5;
            var halfAt = -1.0;
            for (var i = 0; i <= 10000; i++)
            {
                var progress = i / 10000.0;
                if (progress > PopupCurve.PeakAt) break;
                if (PopupCurve.Evaluate(progress).Scale < halfTravel) continue;
                halfAt = progress;
                break;
            }

            True(halfAt > 0);
            True(halfAt < PopupCurve.PeakAt * 0.35);

            // 上升段首帧步进必须明显大于末帧步进（减速），否则"先快后慢"不成立。
            // 两端都取在上升段内：落到回落段比较就没有意义了。
            var firstStep = Step(PopupCurve.PeakAt * 0.02);
            var lastStep = Step(PopupCurve.PeakAt * 0.02, PopupCurve.PeakAt * 0.98);
            True(firstStep > lastStep * 3);
        }

        private static void PeakHasNoCorner()
        {
            // 峰值两侧的速度都要趋近 0：上升段是 EaseOut 的自然落零，
            // 回落段是 smoothstep 的自然起零，于是顶点是一个"停一下"的节拍，
            // 而不是尖角（尖角在 60fps 下会表现为一帧突跳）。
            const double epsilon = 1e-5;
            var before = PopupCurve.Evaluate(PopupCurve.PeakAt - epsilon).Scale;
            var peak = PopupCurve.Evaluate(PopupCurve.PeakAt).Scale;
            var after = PopupCurve.Evaluate(PopupCurve.PeakAt + epsilon).Scale;

            True(Math.Abs(peak - before) < 1e-4);
            True(Math.Abs(after - peak) < 1e-4);
        }

        private static void OpenAndCloseShareOneCurve()
        {
            // 打开与收起是同一条曲线的两个播放方向：同一 progress 必须给出同一取值，
            // 不依赖方向、不依赖历史。
            for (var i = 0; i <= 100; i++)
            {
                var progress = i / 100.0;
                var forward = PopupCurve.Evaluate(progress);
                var backward = PopupCurve.Evaluate(progress);
                Equal(forward.Scale, backward.Scale);
                Equal(forward.SlideY, backward.SlideY);
                Equal(forward.OpacityWeight, backward.OpacityWeight);
            }
        }

        private static void NoJumpWhenDirectionFlips()
        {
            // 打断 = 同一 progress 上翻转方向，曲线只与 progress 有关，
            // 所以翻转处两侧取值必然完全相等（不是"接近"，是同一个数）。
            //
            // 翻转后走的是另一个时长（收起 0.10s、打开 0.15s），
            // 因此每帧步进的上限要分别对各自时长的整段动画取，
            // 不能拿打开段的上限去要求收起段。
            var closeLimit = MaxFrameStep(0.0, 1.0, PopOutSeconds);

            foreach (var flipAt in new[] { 0.09, 0.35, 0.6, 0.72, 0.85, 0.95 })
            {
                var opening = Sample(0.0, flipAt, PopInSeconds);
                var closing = Sample(flipAt, 0.0, PopOutSeconds);
                var jump = Math.Abs(closing[0].Scale - opening[opening.Count - 1].Scale);

                Equal(0.0, jump);
                True(MaxStep(closing) <= closeLimit + 1e-9);
            }
        }

        private static void SixtyFpsOpenStillShowsOvershoot()
        {
            var frames = Sample(0.0, 1.0, PopInSeconds);

            var aboveOne = 0;
            foreach (var frame in frames)
                if (frame.Scale > 1.0) aboveOne++;

            // 时长压到 0.15s 后若过冲只剩 1 帧，肉眼就分辨不出"先放大再回落"
            True(aboveOne >= 2);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
        }

        private static void SixtyFpsCloseStillShowsOvershoot()
        {
            var frames = Sample(1.0, 0.0, PopOutSeconds);

            var aboveOne = 0;
            foreach (var frame in frames)
                if (frame.Scale > 1.0) aboveOne++;

            True(aboveOne >= 2);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
            Equal(PopupCurve.StartScale, frames[frames.Count - 1].Scale);
        }

        private static void SlideAndOpacityFollowProgressMonotonically()
        {
            var previous = PopupCurve.Evaluate(0.0);
            for (var i = 1; i <= 1000; i++)
            {
                var current = PopupCurve.Evaluate(i / 1000.0);
                True(current.SlideY <= previous.SlideY);
                True(current.OpacityWeight >= previous.OpacityWeight);
                previous = current;
            }
        }

        private static double Step(double delta, double from = 0.0)
        {
            return Math.Abs(PopupCurve.Evaluate(from + delta).Scale - PopupCurve.Evaluate(from).Scale);
        }

        private static double MaxFrameStep(double from, double to, double durationSeconds)
        {
            return MaxStep(Sample(from, to, durationSeconds));
        }

        private static double MaxStep(List<PopupCurveSample> frames)
        {
            var maximum = 0.0;
            for (var i = 1; i < frames.Count; i++)
                maximum = Math.Max(maximum, Math.Abs(frames[i].Scale - frames[i - 1].Scale));
            return maximum;
        }

        private static double MaxScale(List<PopupCurveSample> frames)
        {
            var maximum = double.NegativeInfinity;
            foreach (var frame in frames)
                maximum = Math.Max(maximum, frame.Scale);
            return maximum;
        }

        /// <summary>按 60fps 逐帧推进进度，模拟动画时钟的采样。</summary>
        private static List<PopupCurveSample> Sample(double from, double to, double durationSeconds)
        {
            var frames = new List<PopupCurveSample>();
            var progress = from;
            var direction = to >= from ? 1.0 : -1.0;
            var step = FrameSeconds / durationSeconds;

            frames.Add(PopupCurve.Evaluate(progress));
            while (direction > 0 ? progress < to : progress > to)
            {
                progress += direction * step;
                if (direction > 0) progress = Math.Min(progress, to);
                else progress = Math.Max(progress, to);
                frames.Add(PopupCurve.Evaluate(progress));
            }

            return frames;
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
