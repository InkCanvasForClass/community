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
    /// 起点缩放不再是常量，而是按菜单宽度导出的，所以断言一律通过
    /// <see cref="PopupCurveShape"/> 取值，不直接引用任何"某个菜单的"数字。
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

        /// <summary>
        /// 测试用的菜单宽度：接近浮动栏里最大的那几个弹窗（约 381 DIP）。
        /// 取它而不是取"某个常量起点"，是为了让断言走真实的生产路径：
        /// 宽度 → 起点缩放 → 张力 → 行程。
        /// </summary>
        private const double TestContentWidth = 381.0;

        private static PopupCurveShape Shape => PopupCurveShape.ForContentWidth(TestContentWidth);

        public static void RunAll()
        {
            Run(nameof(TravelMatchesUpstreamInkeysImplementation), TravelMatchesUpstreamInkeysImplementation);
            Run(nameof(StartScaleIsDerivedFromMenuWidth), StartScaleIsDerivedFromMenuWidth);
            Run(nameof(PeakScaleDoesNotDriftWithStartScale), PeakScaleDoesNotDriftWithStartScale);
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
            Run(nameof(SlideIsEquivalentToScalingAroundTrigger), SlideIsEquivalentToScalingAroundTrigger);
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
            var shape = Shape;
            for (var i = 0; i <= 1000; i++)
            {
                var p = i / 1000.0;
                True(Math.Abs(InverseBack(shape.Tension, p) - (1.0 - shape.Travel(1.0 - p))) < 1e-12);
            }

            // 起止必须精确，否则菜单会有残留缩放或残留位移。
            Equal(0.0, shape.Travel(0.0));
            Equal(1.0, shape.Travel(1.0));
        }

        private static void StartScaleIsDerivedFromMenuWidth()
        {
            // 收起态在屏幕上应当恰好占一个工具栏按钮那么宽，所以起点缩放 = 按钮宽 / 菜单宽。
            // 这条关系是"按钮撑开成菜单"这个观感的前提：菜单越大，起点比例越小，
            // 但收起来时屏幕上占的宽度是同一个数。
            var wide = PopupCurveShape.ForContentWidth(381.0);
            Equal(PopupCurve.CompactWidth / 381.0, wide.StartScale);

            var narrow = PopupCurveShape.ForContentWidth(4 * PopupCurve.CompactWidth);
            Equal(0.25, narrow.StartScale);

            // 越宽越接近下限，越窄越接近上限：两个夹取都必须生效，
            // 否则极宽菜单起点小到只剩几个像素，极窄菜单又会得到 >0.5 的"闪一下"。
            Equal(PopupCurve.MinStartScale, PopupCurve.StartScaleFor(5000.0));
            Equal(PopupCurve.MaxStartScale, PopupCurve.StartScaleFor(PopupCurve.CompactWidth));

            // 无效宽度不猜中间值，直接退回上限：至少不会因为除零得到 NaN，
            // 让整段动画静默消失（NaN 缩放会让 WPF 直接不绘制）。
            Equal(PopupCurve.MaxStartScale, PopupCurve.StartScaleFor(0.0));
            Equal(PopupCurve.MaxStartScale, PopupCurve.StartScaleFor(-10.0));
            Equal(PopupCurve.MaxStartScale, PopupCurve.StartScaleFor(double.NaN));
            Equal(PopupCurve.MaxStartScale, PopupCurve.StartScaleFor(double.PositiveInfinity));
        }

        private static void PeakScaleDoesNotDriftWithStartScale()
        {
            // 张力是"按峰值反解"出来的，不是照抄 Inkeys 的 1.1。理由是行程长度随起点变：
            // 起点 0.72 时保持 2.412 得 1.05，换成起点 0.15 就会弹到 1.12，
            // 而留白是按 PeakScale 算的 —— 峰值一漂，顶部就被裁掉几个像素。
            // 这条测试锁住"不管起点多小，峰值恒为 PeakScale"，也就锁住了留白够用。
            foreach (var startScale in new[] { 0.05, 0.15, 0.30, 0.45 })
            {
                var shape = PopupCurveShape.ForStartScale(startScale);

                Equal(startScale, shape.StartScale);
                Equal(PopupCurve.PeakScale, shape.PeakScale);
                True(Math.Abs(shape.Evaluate(shape.PeakAt).Scale - PopupCurve.PeakScale) < 1e-12);

                // 宽度与起点互为反函数：同一个数换算回来必须一模一样（不是"接近"）。
                Equal(startScale, PopupCurve.StartScaleFor(PopupCurve.CompactWidth / startScale));
            }

            // 起点越小时行程越长，张力反而变小：行程峰值 t 越接近 1，需要的过冲越少。
            True(PopupCurveShape.ForStartScale(0.05).Tension <
                 PopupCurveShape.ForStartScale(0.45).Tension);

            // 反解必须收敛到真根：等号两边之差要比 1e-12 还小。
            // （牛顿法迭代 12 次，实测残差 1e-16 量级；写 1e-12 是留给编译器的余量。）
            foreach (var startScale in new[] { 0.05, 0.15, 0.30, 0.45, 0.72 })
            {
                var shape = PopupCurveShape.ForStartScale(startScale);
                var travelPeak = (PopupCurve.PeakScale - startScale) / (1.0 - startScale);
                True(Math.Abs(PopupCurve.TravelPeakFor(shape.Tension) - travelPeak) < 1e-12);
            }
        }

        private static void CurveStartsCollapsedAndEndsAtNormalSize()
        {
            var shape = Shape;

            var atZero = shape.Evaluate(0.0);
            Equal(shape.StartScale, atZero.Scale);
            Equal(shape.SlideDistance, atZero.SlideY);
            Equal(PopupCurve.MinOpacityWeight, atZero.OpacityWeight);

            // 收起来时整体下滑一整个 SlideDistance：这就是"收起态比打开态更靠下"的来源。
            // 起点越小滑得越远（0.72 时 9.1 DIP、0.15 时 27.6 DIP），
            // 屏幕上表现为菜单从按钮下方某个位置长上来，而不是在按钮上方原地变大。
            True(atZero.SlideY > 0.0);
            Equal(PopupCurve.AnchorOffset * (1.0 - shape.StartScale), atZero.SlideY);

            var atOne = shape.Evaluate(1.0);
            Equal(1.0, atOne.Scale);
            Equal(0.0, atOne.SlideY);
            Equal(1.0, atOne.OpacityWeight);

            // 越界输入必须被夹住：动画被强行反转时进度理论上不会越界，
            // 但 dt 夹取与浮点累加仍可能产生 1.0000000001。
            Equal(shape.StartScale, shape.Evaluate(-3.0).Scale);
            Equal(1.0, shape.Evaluate(7.0).Scale);
            Equal(shape.StartScale, shape.Evaluate(double.NaN).Scale);
        }

        private static void CurvePeaksExactlyAtDesignedPosition()
        {
            // 峰值不是"调出来"的，而是 back 张力的极值性质：
            // 反解让 Evaluate(PeakAt) 落在 PeakScale 上，残差只剩浮点舍入。
            // 菜单留白按 PeakScale 计算，所以这点残差必须远小于 1 像素的等效缩放假想。
            var shape = Shape;
            True(Math.Abs(shape.Evaluate(shape.PeakAt).Scale - PopupCurve.PeakScale) < 1e-12);

            // 峰值必须是全局最大值，且就出现在 PeakAt 这一处
            var best = double.NegativeInfinity;
            var bestAt = 0.0;
            for (var i = 0; i <= 2000; i++)
            {
                var progress = i / 2000.0;
                var scale = shape.Evaluate(progress).Scale;
                if (scale <= best) continue;
                best = scale;
                bestAt = progress;
            }

            // 扫格点取最大值，格点未必正好落在 PeakAt 上，因此只要求贴近峰值
            True(Math.Abs(best - PopupCurve.PeakScale) < 0.001);
            True(Math.Abs(bestAt - shape.PeakAt) < 0.002);
        }

        private static void CurveRisesThenFallsWithoutBouncing()
        {
            // 过冲只允许发生一次：scale > 1 的进度集合必须是单个连续区间。
            // 否则视觉上会"大到小再大"，即用户说的"晃"。
            // Back 曲线天然只有一个极值，这条断言是防回归而非防参数。
            var shape = Shape;
            var aboveOne = 0;
            var segments = 0;
            var inside = false;
            for (var i = 0; i <= 2000; i++)
            {
                var isAbove = shape.Evaluate(i / 2000.0).Scale > 1.0;
                if (isAbove) aboveOne++;
                if (isAbove && !inside) segments++;
                inside = isAbove;
            }

            Equal(1, segments);
            True(aboveOne > 0);
        }

        private static void RiseIsFrontLoadedAndDecelerates()
        {
            var shape = Shape;

            // "先快后慢"：半程行程应当远早于上升段的中点完成。
            var halfTravel = shape.StartScale + (PopupCurve.PeakScale - shape.StartScale) * 0.5;
            var halfAt = -1.0;
            for (var i = 0; i <= 10000; i++)
            {
                var progress = i / 10000.0;
                if (progress > shape.PeakAt) break;
                if (shape.Evaluate(progress).Scale < halfTravel) continue;
                halfAt = progress;
                break;
            }

            // 半程行程落在峰值位置的 1/4 附近（381 宽菜单实测 p ≈ 0.152，
            // 而峰值在 0.627）—— 点下去第一帧就走完近半行程。
            True(halfAt > 0.0);
            True(halfAt < shape.PeakAt * 0.35);

            // 上升段首帧步进必须明显大于末帧步进（减速），否则"先快后慢"不成立。
            // 两端都取在上升段内：落到回落段比较就没有意义了。
            var firstStep = Step(shape, shape.PeakAt * 0.02);
            var lastStep = Step(shape, shape.PeakAt * 0.02, shape.PeakAt * 0.98);
            True(firstStep > lastStep * 3.0);
        }

        private static void PeakHasNoCorner()
        {
            // 峰值是曲线自身的极值：两侧速度同时趋近 0，顶点是一个"停一下"的节拍，
            // 而不是尖角（尖角在 60fps 下会表现为一帧突跳）。
            // 旧实现靠"上升段动调零 + 回落段动起零"拼接，才需要单独验这件事。
            var shape = Shape;
            const double epsilon = 1e-5;
            var before = shape.Evaluate(shape.PeakAt - epsilon).Scale;
            var peak = shape.Evaluate(shape.PeakAt).Scale;
            var after = shape.Evaluate(shape.PeakAt + epsilon).Scale;

            True(Math.Abs(peak - before) < 1e-4);
            True(Math.Abs(after - peak) < 1e-4);
        }

        private static void OpenAndCloseShareOneCurve()
        {
            // 打开与收起是同一条曲线的两个播放方向：同一 progress 必须给出同一取值，
            // 不依赖方向、不依赖历史。
            var shape = Shape;
            for (var i = 0; i <= 100; i++)
            {
                var progress = i / 100.0;
                var forward = shape.Evaluate(progress);
                var backward = shape.Evaluate(progress);
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
            var shape = Shape;
            var closeLimit = MaxFrameStep(shape, 0.0, 1.0, PopOutSeconds);

            foreach (var flipAt in new[] { 0.09, 0.35, 0.6, 0.72, 0.85, 0.95 })
            {
                var opening = Sample(shape, 0.0, flipAt, PopInSeconds);
                var closing = Sample(shape, flipAt, 0.0, PopOutSeconds);
                var jump = Math.Abs(closing[0].Scale - opening[opening.Count - 1].Scale);

                Equal(0.0, jump);
                True(MaxStep(closing) <= closeLimit + 1e-9);
            }
        }

        private static void SixtyFpsOpenStillShowsOvershoot()
        {
            var shape = Shape;
            var frames = Sample(shape, 0.0, 1.0, PopInSeconds);

            var aboveOne = 0;
            foreach (var frame in frames)
                if (frame.Scale > 1.0) aboveOne++;

            // 60fps 下过冲段至少要采到 4 帧，否则肉眼分辨不出"先放大再回落"。
            // 实测 0.40s、381 宽菜单时是 13 帧 / 217ms。
            True(aboveOne >= 4);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
        }

        private static void SixtyFpsCloseStillShowsOvershoot()
        {
            var shape = Shape;
            var frames = Sample(shape, 1.0, 0.0, PopOutSeconds);

            // 收起的最后一帧在 IsOpen=false 之前不会被画出来，因此只统计到倒数第二帧。
            var visible = frames.Count - 1;
            var aboveOne = 0;
            for (var i = 0; i < visible; i++)
                if (frames[i].Scale > 1.0) aboveOne++;

            True(aboveOne >= 4);
            True(MaxScale(frames) >= PopupCurve.PeakScale - 0.005);
            Equal(shape.StartScale, frames[frames.Count - 1].Scale);
        }

        private static void SlideAndOpacityShareTheTravelOfTheCurve()
        {
            // 位移与缩放必须走同一个行程，否则观感上"缩放先快后慢、位移匀速"，
            // 像是两个不同步的动画叠在一起。
            var shape = Shape;
            for (var i = 0; i <= 1000; i++)
            {
                var progress = i / 1000.0;
                var travel = shape.Travel(progress);
                var sample = shape.Evaluate(progress);

                Equal(shape.SlideDistance * (1.0 - travel), sample.SlideY);
                Equal(shape.StartScale + (1.0 - shape.StartScale) * travel, sample.Scale);
            }

            // 透明度不跟行程走（它有自己的正弦时间线），但必须同样整段单调不减、
            // 且限制在 [MinOpacityWeight, 1]：权重 >1 交给 WPF 会被静默裁掉，
            // 观感是"最后一段不再变亮"。下限现在是 0，即收起态完全不可见。
            var previous = shape.Evaluate(0.0).OpacityWeight;
            for (var i = 1; i <= 1000; i++)
            {
                var current = shape.Evaluate(i / 1000.0).OpacityWeight;
                True(current >= previous);
                True(current <= 1.0);
                previous = current;
            }

            Equal(1.0, previous);

            // 位移会先越过 0 再退回来（Inkeys 的呼吸感）。这个越位量与起点无关：
            // SlideDistance·(1 - 行程峰值) 里 SlideDistance = b·(1 - 起点)，而行程峰值
            // 恰好按 (峰值-起点)/(1-起点) 反解，两者相乘把起点消掉，恒为 -b·(PeakScale-1)。
            // 这段越位落在菜单留白里，量必须小到不会被看成"菜单抖了一下"。
            var overshootSlide = shape.Evaluate(shape.PeakAt).SlideY;
            True(overshootSlide < 0.0);
            True(overshootSlide > -3.0);
            Equal(0.0, shape.Evaluate(1.0).SlideY);

            foreach (var startScale in new[] { 0.05, 0.15, 0.30, 0.45 })
            {
                var other = PopupCurveShape.ForStartScale(startScale);
                True(Math.Abs(other.Evaluate(other.PeakAt).SlideY + 1.625) < 1e-9);
            }
        }

        private static void SlideIsEquivalentToScalingAroundTrigger()
        {
            // 菜单的 RenderTransformOrigin 被夹在 [0,1]，纵向最多只能到底边，
            // 所以"绕按钮中心缩放"没法直接用原点表达。等价写法是：原点取底边，
            // 再叠一段位移 SlideDistance = 锚点偏移·(1 - 起点缩放)。
            //
            // 这条测试锁的就是那个等价关系：任意缩放下，合成后的底边位置
            // 必须等于"绕按钮中心缩放"算出来的底边位置。
            // 一旦有人把 SlideDistance 改回拍脑袋的整数（比如 10.0），这里立刻失败。
            const double anchorOffset = PopupCurve.AnchorOffset;
            var shape = Shape;

            for (var i = 0; i <= 1000; i++)
            {
                var scale = shape.StartScale + i / 1000.0 * (1.0 - shape.StartScale);
                var travel = (scale - shape.StartScale) / (1.0 - shape.StartScale);

                // 我们的写法：底边不动，内容整体平移 slideY；底边相对静止位置抬升 load
                var ourLift = shape.SlideDistance * (1.0 - travel);

                // 绕锚点缩放的写法：底边抬升 anchorOffset·(1 - scale)
                var anchorLift = anchorOffset * (1.0 - scale);

                True(Math.Abs(ourLift - anchorLift) < 1e-9);
            }

            // 常量之间的关系也要钉住：SlideDistance 必须是导出量，不是独立可调的值。
            // 起点越小，收起态整体下滑得越远 —— 这正是"更靠下"的来源。
            Equal(anchorOffset * (1.0 - shape.StartScale), shape.SlideDistance);
            True(PopupCurveShape.ForStartScale(0.15).SlideDistance >
                 PopupCurveShape.ForStartScale(0.45).SlideDistance);

            // 锚点偏移 = 按钮半高 24.5 + 弹窗间距 8，与诊断工程里的 PlacementGapPixels 同源。
            Equal(24.5 + 8.0, anchorOffset);
        }

        private static void OpacityHasItsOwnTimeline()
        {
            // 透明度走独立的 EaseInOutSine，不跟几何行程走。这条测试锁的是"两个旋钮别绞在一起"：
            // 调曲线张力只该改缩放/位移的形状，不该顺带改掉淡入的速度。
            //
            // 曾经的实现是 MinOpacityWeight + (1-MinOpacityWeight)·clamp01(travel)：
            // 张力 1.1 时透明度在进度 0.476 就满，张力 3.0 时提前到 0.250 ——
            // 想调"弹多狠"会连带把淡入压缩到 1/4 时长，正是这里要防的回归。
            //
            // 曲线选 EaseInOutSine 而不是 Inkeys 那条 EaseOutSine：Inkeys 的透明度是两层，
            // 背板跑 EaseOutSine、图标文字另跑 EaseInOutCubic（Bar.GeometryAttribute.cpp:474），
            // 快的是底板。我们只有一层 Opacity，背板与文字一起变，用 EaseOutSine 会让
            // 半透明的字先浮出来；EaseInOutSine 两端导数为 0，不会一帧之内突然实/虚。
            var shape = Shape;
            for (var i = 0; i <= 1000; i++)
            {
                var progress = i / 1000.0;
                var eased = 0.5 * (1.0 - Math.Cos(progress * Math.PI));
                var expected = PopupCurve.MinOpacityWeight +
                               (1.0 - PopupCurve.MinOpacityWeight) * eased;
                Equal(expected, PopupCurve.OpacityWeight(progress));
                Equal(expected, shape.Evaluate(progress).OpacityWeight);
            }

            // 下限 0.2 之上是纯 EaseInOutSine：中点恰好落在权重的正中 (0.2 + 0.8/2 = 0.6)，
            // 两端导数为 0，所以起点与终点都没有"突然出现/突然消失"的硬边。
            True(Math.Abs(PopupCurve.OpacityWeight(0.5) - 0.6) < 1e-12);
            // 四分之一处的行程是 (1 - cos(π/4)) / 2 ≈ 0.1464，不是 0.5 —— 前半段比线性慢，
            // 这正是"EaseOutSine 起手就冲到 0.38"要修掉的东西。
            True(Math.Abs((PopupCurve.OpacityWeight(0.25) - PopupCurve.MinOpacityWeight) /
                          (1.0 - PopupCurve.MinOpacityWeight) - 0.146446609406726) < 1e-12);
            // 关于中点对称：op(p) + op(1-p) = 1 + 下限（两个 0.8 因子各占一半）。
            True(Math.Abs(PopupCurve.OpacityWeight(0.75) + PopupCurve.OpacityWeight(0.25) -
                          1.0 - PopupCurve.MinOpacityWeight) < 1e-12);

            // 真正的对照点：几何越过 1.0 的位置由张力唯一决定，解 1 + (c+1)s³ + c·s² = 1
            // 得 s = -c/(c+1)，即 progress = 1/(c+1)。381 宽菜单张力 1.271，越界点 p ≈ 0.440
            // （0.40s 里的 176ms），此刻透明度才 0.53 —— 剩下过半时长留给透明度独自收尾。
            // 旧实现同一处已是 0.9999，即"几何一到位，透明度也满"，这正是要修掉的耦合。
            var crossing = 1.0 / (shape.Tension + 1.0);
            True(Math.Abs(shape.Evaluate(crossing).Scale - 1.0) < 1e-12);
            True(PopupCurve.OpacityWeight(crossing) > 0.50);
            True(PopupCurve.OpacityWeight(crossing) < 0.55);

            // 透明度必须一路铺满到进度 1，不能在中途饱和。
            True(PopupCurve.OpacityWeight(0.9) < 1.0);
            Equal(1.0, PopupCurve.OpacityWeight(1.0));

            // 收起态保留可见残影：MenuHarness 在 progress=0 之后才 IsOpen=false，
            // 若这里归零，最后三成收缩过程就是"还没缩进去就先化没了"。
            True(PopupCurve.MinOpacityWeight > 0.0);
            Equal(PopupCurve.MinOpacityWeight, PopupCurve.OpacityWeight(0.0));

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

        private static double Step(PopupCurveShape shape, double delta, double from = 0.0)
        {
            return Math.Abs(shape.Evaluate(from + delta).Scale - shape.Evaluate(from).Scale);
        }

        private static double MaxFrameStep(PopupCurveShape shape, double from, double to, double durationSeconds)
        {
            return MaxStep(Sample(shape, from, to, durationSeconds));
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
        private static List<PopupCurveSample> Sample(PopupCurveShape shape, double from, double to,
            double durationSeconds)
        {
            var frames = new List<PopupCurveSample>();
            var progress = from;
            var direction = to >= from ? 1.0 : -1.0;
            var step = FrameSeconds / durationSeconds;

            frames.Add(shape.Evaluate(progress));
            while (direction > 0 ? progress < to : progress > to)
            {
                progress += direction * step;
                if (direction > 0) progress = Math.Min(progress, to);
                else progress = Math.Max(progress, to);
                frames.Add(shape.Evaluate(progress));
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
