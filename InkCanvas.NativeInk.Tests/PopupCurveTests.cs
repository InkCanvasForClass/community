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
        private const double PopInSeconds = 0.40;

        /// <summary>收场时长，与 AnimationsHelper.PopupPopOutDurationSeconds 一致。</summary>
        private const double PopOutSeconds = 0.30;

        public static void RunAll()
        {
            Run(nameof(TravelMatchesUpstreamInkeysImplementation), TravelMatchesUpstreamInkeysImplementation);
            Run(nameof(CurveStartsCollapsedAndEndsAtNormalSize), CurveStartsCollapsedAndEndsAtNormalSize);
            Run(nameof(CurvePeaksExactlyAtDesignedPosition), CurvePeaksExactlyAtDesignedPosition);
            Run(nameof(CurveRisesThenFallsWithoutBouncing), CurveRisesThenFallsWithoutBouncing);
            Run(nameof(RiseIsFrontLoadedAndDecelerates), RiseIsFrontLoadedAndDecelerates);
            Run(nameof(PeakHasNoCorner), PeakHasNoCorner);
            Run(nameof(OpenAndCloseShareOneCurve), OpenAndCloseShareOneCurve);
            Run(nameof(NoJumpWhenDirectionFlips), NoJumpWhenDirectionFlips);
            Run(nameof(SixtyFpsOpenStillShowsOvershoot), SixtyFpsOpenStillShowsOvershoot);
            Run(nameof(SixtyFpsCloseStillShowsOvershoot), SixtyFpsCloseStillShowsOvershoot);
            Run(nameof(SlideAndOpacityShareTheTravelOfTheCurve), SlideAndOpacityShareTheTravelOfTheCurve);
            Run(nameof(OpacityHasItsOwnTimeline), OpacityHasItsOwnTimeline);
        }

        private static void TravelMatchesUpstreamInkeysImplementation()
        {
            // 曲线形状是照 Inkeys 搬过来的，所以要对得上 Inkeys 自己的头测：
            // InkeysHeadlessTests/animation_tests.cpp:717 断言 EaseOutBack@back=1.1, p=0.5 == 1.0125，
            // :720 断言 EaseInBack@p=0.5 == -0.0125。
            // 这两条同时钉住了公式形态（不是"长得像"的近似）与 back 的代入位置。
            True(Math.Abs(PopupCurve.TravelWithBack(1.1, 0.5) - 1.0125) < 1e-12);
            True(Math.Abs(InverseBack(1.1, 0.5) - (-0.0125)) < 1e-12);

            // Inkeys 打开用 EaseOutBack、收起用 EaseInBack，两者严格互为时间反演，
            // 所以"收起 = 同一条曲线倒放"不是我们的近似，而是与 Inkeys 等价。
            for (var i = 0; i <= 1000; i++)
            {
                var p = i / 1000.0;
                True(Math.Abs(InverseBack(PopupCurve.BackTension, p) -
                              (1.0 - PopupCurve.Travel(1.0 - p))) < 1e-12);
            }

            // 起止必须精确，否则菜单会有残留缩放或残留位移。
            Equal(0.0, PopupCurve.Travel(0.0));
            Equal(1.0, PopupCurve.Travel(1.0));
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
            // 峰值不是"调出来"的，而是 back 张力的极值性质：
            // 反解让 Evaluate(PeakAt) 落在 PeakScale 上，残差只剩浮点舍入。
            // 菜单留白按 PeakScale 计算，所以这点残差必须远小于 1 像素的等效缩放假想。
            True(Math.Abs(PopupCurve.Evaluate(PopupCurve.PeakAt).Scale - PopupCurve.PeakScale) < 1e-12);

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
            // Back 曲线天然只有一个极值，这条断言是防回归而非防参数。
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

            // Back 曲线的半程行程落在 p ≈ 0.133，即打开时长的 13%、
            // 峰值位置(0.529)的 25% —— 点下去第一帧就走完近 1/4 行程。
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
            // 峰值是曲线自身的极值：两侧速度同时趋近 0，顶点是一个"停一下"的节拍，
            // 而不是尖角（尖角在 60fps 下会表现为一帧突跳）。
            // 旧实现靠"上升段动调零 + 回落段动起零"拼接，才需要单独验这件事。
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
            // 翻转后走的是另一个时长（收起 0.30s、打开 0.40s），
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

            // 60fps 下过冲段至少要采到 4 帧，否则肉眼分辨不出"先放大再回落"。
            // 实测 0.40s 时是 16 帧 / 267ms。
            True(aboveOne >= 4);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
        }

        private static void SixtyFpsCloseStillShowsOvershoot()
        {
            var frames = Sample(1.0, 0.0, PopOutSeconds);

            var aboveOne = 0;
            foreach (var frame in frames)
                if (frame.Scale > 1.0) aboveOne++;

            // 收起的最后一帧在 IsOpen=false 之前不会被画出来，因此只统计到倒数第二帧。
            var visible = frames.Count - 1;
            aboveOne = 0;
            for (var i = 0; i < visible; i++)
                if (frames[i].Scale > 1.0) aboveOne++;

            True(aboveOne >= 4);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
            Equal(PopupCurve.StartScale, frames[frames.Count - 1].Scale);
        }

        private static void SlideAndOpacityShareTheTravelOfTheCurve()
        {
            // 位移与透明度必须和缩放走同一个行程，否则观感上"缩放先快后慢、位移匀速"，
            // 像是两个不同步的动画叠在一起。
            for (var i = 0; i <= 1000; i++)
            {
                var progress = i / 1000.0;
                var travel = PopupCurve.Travel(progress);
                var sample = PopupCurve.Evaluate(progress);

                Equal(PopupCurve.SlideDistance * (1.0 - travel), sample.SlideY);
                Equal(PopupCurve.StartScale + (1.0 - PopupCurve.StartScale) * travel, sample.Scale);
            }

            // 透明度不跟行程走（它有自己的正弦时间线），但必须同样整段单调不减、
            // 且限制在 [MinOpacityWeight, 1]：权重 >1 交给 WPF 会被静默裁掉，
            // 观感是"最后一段不再变亮"。
            var previous = PopupCurve.Evaluate(0.0).OpacityWeight;
            for (var i = 1; i <= 1000; i++)
            {
                var current = PopupCurve.Evaluate(i / 1000.0).OpacityWeight;
                True(current >= previous);
                True(current <= 1.0);
                previous = current;
            }

            Equal(1.0, previous);

            // 位移会先越过 0 再退回来（Inkeys 的呼吸感，峰值处约 -1.79 DIP），
            // 这段越位落在菜单留白里；越位量必须小到不会被看成"菜单抖了一下"。
            var overshootSlide = PopupCurve.Evaluate(PopupCurve.PeakAt).SlideY;
            True(overshootSlide < 0.0);
            True(overshootSlide > -3.0);
            Equal(0.0, PopupCurve.Evaluate(1.0).SlideY);
        }

        private static void OpacityHasItsOwnTimeline()
        {
            // 透明度走独立的 EaseOutSine，不跟几何行程走。这条测试锁的是"两个旋钮别绞在一起"：
            // 调 BackTension 只该改缩放/位移的形状，不该顺带改掉淡入的速度。
            //
            // 曾经的实现是 MinOpacityWeight + (1-MinOpacityWeight)·clamp01(travel)：
            // 张力 1.1 时透明度在进度 0.476 就满，张力 3.0 时提前到 0.250 ——
            // 想调"弹多狠"会连带把淡入压缩到 1/4 时长，正是这里要防的回归。
            for (var i = 0; i <= 1000; i++)
            {
                var progress = i / 1000.0;
                var expected = PopupCurve.MinOpacityWeight +
                               (1.0 - PopupCurve.MinOpacityWeight) * Math.Sin(progress * Math.PI / 2.0);
                Equal(expected, PopupCurve.OpacityWeight(progress));
                Equal(expected, PopupCurve.Evaluate(progress).OpacityWeight);
            }

            // 对齐 Inkeys 的形状：EaseOutSine(0.5) = √2/2 = 0.7071，权重映射到 [0.35, 1] 后是 0.8096。
            // 此刻几何已越过 1.0 在过冲，透明度才走到八成 —— "几何先到位、透明度收尾"。
            True(Math.Abs(PopupCurve.OpacityWeight(0.5) - 0.8096194077712559) < 1e-12);

            // 真正的对照点：几何在 progress 0.293 就已锁在 1.0（back=2.412 时越过 1.0 的位置），
            // 透明度那时只走到 0.6387（正弦因子 0.4442）—— 剩下 0.36 留给后半段。
            // 旧实现同一处是 0.9999，即"几何一到位，透明度也满"，这正是要修掉的耦合。
            True(PopupCurve.OpacityWeight(0.293) < 0.65);
            True(PopupCurve.OpacityWeight(0.293) > 0.60);

            // 透明度必须一路铺满到进度 1，不能在中途饱和。
            True(PopupCurve.OpacityWeight(0.9) < 1.0);
            Equal(1.0, PopupCurve.OpacityWeight(1.0));

            // 几何越界输入同样要夹稳。
            Equal(PopupCurve.MinOpacityWeight, PopupCurve.OpacityWeight(-5.0));
            Equal(1.0, PopupCurve.OpacityWeight(9.0));
            Equal(PopupCurve.MinOpacityWeight, PopupCurve.OpacityWeight(double.NaN));
        }

        /// <summary>Inkeys 收起用的 EaseInBack，仅用于验证"倒放"是数学恒等。</summary>
        private static double InverseBack(double back, double progress)
        {
            progress = Math.Min(1.0, Math.Max(0.0, progress));
            return (back + 1.0) * progress * progress * progress - back * progress * progress;
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
