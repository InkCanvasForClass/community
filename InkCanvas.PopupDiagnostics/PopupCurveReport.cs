using System;
using System.Globalization;
using Ink_Canvas.Helpers.Motion;

namespace Ink_Canvas.PopupDiagnostics
{
    /// <summary>
    /// 把曲线按 60fps 均匀采样打印出来，并核对几条"形状性质"。
    /// </summary>
    /// <remarks>
    /// 曲线是纯数学，单测已经覆盖；这里额外做的是**把一帧一帧的数值摊开给人看** ——
    /// 调参时关心的"第几帧越过 1.0""过冲持续多少毫秒"在单测里没有、只对人有用。
    ///
    /// 采样是真机上 progress 的推进方式：时长除以帧间隔得到步长，
    /// 与 <see cref="PopupTiming"/> 描述的一致（每帧最多推进 MaxFrameSeconds 对应的一段）。
    ///
    /// 起点缩放不再是常量，所以下面所有检查要么按一条具名形状跑，要么扫一组菜单宽度，
    /// 不再引用"某个菜单的"数字 —— 否则大菜单和小菜单里总有一边测不到。
    /// </remarks>
    internal static class PopupCurveReport
    {
        /// <summary>菜单打开时长（秒），与 AnimationsHelper.PopupPopInDurationSeconds 保持一致。</summary>
        private const double OpenSeconds = 0.40;

        /// <summary>菜单收起时长（秒），与 AnimationsHelper.PopupPopOutDurationSeconds 保持一致。</summary>
        private const double CloseSeconds = 0.30;

        /// <summary>采样帧率。</summary>
        private const double FrameRate = 60.0;

        /// <summary>
        /// 逐帧表使用的菜单宽度：接近浮动栏里最大的那几个弹窗（约 381 DIP）。
        /// 它决定了起点缩放（58 / 381 ≈ 0.152），也就是收起态"只有一个按钮那么大"。
        /// </summary>
        private const double TableContentWidth = 381.0;

        /// <summary>核对形状性质时扫的一组菜单宽度，覆盖夹取的两端。</summary>
        private static readonly double[] Widths = { 1160.0, 600.0, 381.0, 260.0, 129.0 };

        public static DiagnosticReport Run()
        {
            var report = new DiagnosticReport();
            Console.WriteLine("== 曲线形状 ==");

            var shape = PopupCurveShape.ForContentWidth(TableContentWidth);

            var start = shape.Evaluate(0.0);
            report.Check(
                "progress=0 时处于收起态",
                Math.Abs(start.Scale - shape.StartScale) < 1e-12 && Math.Abs(start.SlideY - shape.SlideDistance) < 1e-12,
                "scale=" + F(start.Scale) + " slide=" + F(start.SlideY));

            var end = shape.Evaluate(1.0);
            report.Check(
                "progress=1 时精确落位",
                Math.Abs(end.Scale - 1.0) < 1e-12 && Math.Abs(end.SlideY) < 1e-12 && Math.Abs(end.OpacityWeight - 1.0) < 1e-12,
                "scale=" + F(end.Scale) + " slide=" + F(end.SlideY));

            var peak = shape.Evaluate(shape.PeakAt);
            report.Check(
                "峰值缩放与留白常量一致",
                Math.Abs(peak.Scale - PopupCurve.PeakScale) < 1e-9,
                "Evaluate(PeakAt)=" + F(peak.Scale) + " PeakScale=" + F(PopupCurve.PeakScale));
            report.Note("起步缩放 " + F(shape.StartScale) + "（" + F(PopupCurve.CompactWidth) + " / " + F(TableContentWidth) +
                        "），张力反解为 " + F(shape.Tension) + "，峰值出现在 progress=" + F(shape.PeakAt) +
                        "，收起态整体下滑 " + F(shape.SlideDistance) + " DIP");

            // 起点按菜单宽度导出：这条关系一旦被谁改回常量，大菜单与小菜单里总有一边不对。
            var drift = 0.0;
            var smallest = double.MaxValue;
            var largest = 0.0;
            var badPeak = false;
            foreach (var width in Widths)
            {
                var swept = PopupCurveShape.ForContentWidth(width);
                drift = Math.Max(drift, Math.Abs(swept.Evaluate(swept.PeakAt).Scale - PopupCurve.PeakScale));
                smallest = Math.Min(smallest, swept.StartScale);
                largest = Math.Max(largest, swept.StartScale);

                // 峰值必须仍然是这段曲线的全局最大值，且落在 PeakAt 上：
                // 张力是反解出来的，扫一遍比"相信反解"更能兜住实现回退。
                var maxScale = double.MinValue;
                var maxAt = 0.0;
                for (var i = 0; i <= 1000; i++)
                {
                    var p = i / 1000.0;
                    var sample = swept.Evaluate(p);
                    if (sample.Scale <= maxScale) continue;
                    maxScale = sample.Scale;
                    maxAt = p;
                }

                if (maxScale > PopupCurve.PeakScale + 1e-6 || Math.Abs(maxAt - swept.PeakAt) > 2e-3) badPeak = true;
            }

            report.Check(
                "不同菜单宽度下峰值恒为 PeakScale（留白不会突然不够）",
                drift < 1e-9 && !badPeak,
                "扫 " + Widths.Length + " 种宽度，最大偏差=" + F(drift) + "，起点缩放从 " + F(smallest) + " 到 " + F(largest));

            report.Check(
                "起步缩放随菜单宽度单调递减并被夹在上下限内",
                PopupCurve.StartScaleFor(1000.0) < PopupCurve.StartScaleFor(300.0) &&
                PopupCurve.StartScaleFor(300.0) < PopupCurve.StartScaleFor(130.0) &&
                PopupCurve.StartScaleFor(5000.0) >= PopupCurve.MinStartScale &&
                PopupCurve.StartScaleFor(PopupCurve.CompactWidth) <= PopupCurve.MaxStartScale &&
                Math.Abs(PopupCurve.StartScaleFor(PopupCurve.CompactWidth) - PopupCurve.MaxStartScale) < 1e-12 &&
                Math.Abs(PopupCurve.StartScaleFor(0.0) - PopupCurve.MaxStartScale) < 1e-12,
                "1160 宽 → " + F(PopupCurve.StartScaleFor(1160.0)) + "，381 宽 → " +
                F(PopupCurve.StartScaleFor(381.0)) + "，129 宽 → " + F(PopupCurve.StartScaleFor(129.0)) +
                "（夹取 [" + F(PopupCurve.MinStartScale) + ", " + F(PopupCurve.MaxStartScale) + "]）");

            // 峰值那段向上滑动量是"位移与缩放互相抵消"的产物，与起点无关：
            // 起点越小位移越大，而行程峰值恰好按比例补回来，乘完恒为 -锚点·(PeakScale - 1)。
            var slideDrift = 0.0;
            foreach (var scale in new[] { 0.05, 0.15, 0.30, 0.45 })
            {
                var other = PopupCurveShape.ForStartScale(scale);
                slideDrift = Math.Max(slideDrift,
                    Math.Abs(other.Evaluate(other.PeakAt).SlideY + PopupCurve.AnchorOffset * (PopupCurve.PeakScale - 1.0)));
            }

            report.Check(
                "峰值上滑量与起点无关，恒等于锚点偏移 × 过冲比例",
                slideDrift < 1e-9,
                "理论值 " + F(-PopupCurve.AnchorOffset * (PopupCurve.PeakScale - 1.0)) + " DIP，扫 4 个起点最大偏差=" +
                F(slideDrift));

            // 上限检查：采样点里最大的缩放不能超过留白所依据的 PeakScale，否则会被窗体裁掉。
            var maxScaleAll = double.MinValue;
            var maxAtAll = 0.0;
            for (var i = 0; i <= 1000; i++)
            {
                var p = i / 1000.0;
                var sample = shape.Evaluate(p);
                if (sample.Scale > maxScaleAll)
                {
                    maxScaleAll = sample.Scale;
                    maxAtAll = p;
                }
            }
            report.Check(
                "全程不超过 PeakScale（留白依据）",
                maxScaleAll <= PopupCurve.PeakScale + 1e-9,
                "最大 scale=" + F(maxScaleAll) + " 出现在 progress=" + F(maxAtAll) + "，PeakScale=" + F(PopupCurve.PeakScale));

            // 透明度权重被点积进 visual opacity，超出 [0,1] 会被 WPF 静默截断，表现为"突然变实/变透"。
            var minOpacity = double.MaxValue;
            var maxOpacity = double.MinValue;
            var maxUpwardSlide = 0.0;
            var hasNaN = false;
            for (var i = 0; i <= 1000; i++)
            {
                var sample = shape.Evaluate(i / 1000.0);
                if (double.IsNaN(sample.Scale) || double.IsNaN(sample.SlideY) || double.IsNaN(sample.OpacityWeight))
                {
                    hasNaN = true;
                    break;
                }
                minOpacity = Math.Min(minOpacity, sample.OpacityWeight);
                maxOpacity = Math.Max(maxOpacity, sample.OpacityWeight);
                maxUpwardSlide = Math.Max(maxUpwardSlide, -sample.SlideY);
            }

            report.Check("全程无 NaN", !hasNaN, null);
            report.Check(
                "透明度权重落在设计区间内且不被截断",
                !hasNaN && minOpacity >= PopupCurve.MinOpacityWeight - 1e-12 && maxOpacity <= 1.0 + 1e-12,
                "区间=[" + F(minOpacity) + ", " + F(maxOpacity) + "]，设计下限 " + F(PopupCurve.MinOpacityWeight));

            // 透明度走独立的 EaseOutSine，必须一路铺满到进度 1，不能跟着几何行程在半途饱和。
            // 几何在 EaseOutBack 上越过 1.0 后行程恒为 1，若透明度挂在行程上，
            // 这之后整段就再也不变亮 —— 观感是"先迅速变实，然后空转着抖一下"。
            var saturatesEarly = false;
            for (var i = 0; i <= 990; i++)
                if (PopupCurve.OpacityWeight(i / 1000.0) >= 1.0)
                {
                    saturatesEarly = true;
                    break;
                }
            report.Check(
                "透明度一路铺满到落位，不提前饱和",
                !saturatesEarly,
                "progress=0.99 时仍是 " + F(PopupCurve.OpacityWeight(0.99)) + "，到 1.0 才满");
            report.Check(
                "起始完全不可见（与 Inkeys 的 opacity 初值 0 一致）",
                Math.Abs(PopupCurve.OpacityWeight(0.0)) < 1e-12,
                "OpacityWeight(0)=" + F(PopupCurve.OpacityWeight(0.0)) + "，收起态不留半透明残影");

            // 几何越过 1.0（开始过冲）那一刻的透明度，是"几何先到位、透明度收尾"的量化指标。
            // 越界点有闭式解：行程 1 + (c+1)s³ + c·s² = 1 的非零根是 s = -c/(c+1)，
            // 于是 progress = 1 - s = 1/(c+1)。
            var crossAt = 1.0 / (shape.Tension + 1.0);
            report.Check(
                "过冲起点与张力自洽（progress = 1/(c+1)）",
                Math.Abs(shape.Evaluate(crossAt).Scale - 1.0) < 1e-9,
                "解析解 progress=" + F(crossAt) + " 处 scale=" + F(shape.Evaluate(crossAt).Scale));
            report.Note("几何于 progress=" + F(crossAt) + "（" + F(crossAt * OpenSeconds * 1000.0) +
                        "ms）越过 1.0 开始过冲，此刻透明度才 " + F(PopupCurve.OpacityWeight(crossAt)) +
                        "，余下 " + F((1.0 - crossAt) * OpenSeconds * 1000.0) + "ms 由透明度独自收尾");

            // 向上滑动是否会被窗体裁掉取决于宿主留白，属于几何问题，由 PopupGeometryReport 用真窗口核对；
            // 这里只把数值摊出来，方便和那边的留白对照。
            report.Note("峰值向上的滑移量 " + F(maxUpwardSlide) + " DIP（裁切与否见几何检查）");

            PrintTable(shape, "打开", OpenSeconds, 0.0, 1.0);
            PrintTable(shape, "收起", CloseSeconds, 1.0, 0.0);

            return report;
        }

        /// <summary>按真实帧步长打印一次完整播放的采样序列。</summary>
        private static void PrintTable(PopupCurveShape shape, string title, double seconds, double from, double to)
        {
            var frames = (int)Math.Round(seconds * FrameRate);
            Console.WriteLine();
            Console.WriteLine("  " + title + "  " + F(seconds) + "s / " + frames + " 帧 @ " + (int)FrameRate + "fps" +
                              "（每行：帧序号 scale slide 透明度权重）");

            var previousScale = shape.Evaluate(from).Scale;
            var maxDelta = 0.0;
            var maxDeltaFrame = 0;
            var firstFrameDelta = 0.0;
            var overshootFrames = 0;

            for (var frame = 0; frame <= frames; frame++)
            {
                var progress = from + (to - from) * frame / frames;
                var sample = shape.Evaluate(progress);
                var delta = frame == 0 ? 0.0 : Math.Abs(sample.Scale - previousScale);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxDeltaFrame = frame;
                }
                if (frame == 1) firstFrameDelta = delta;
                if (sample.Scale > 1.0) overshootFrames++;

                Console.WriteLine("    " + frame.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "  " +
                                  F(sample.Scale) + "  " + F(sample.SlideY).PadLeft(8) + "  " + F(sample.OpacityWeight));
                previousScale = sample.Scale;
            }

            var averageDelta = (1.0 - shape.StartScale) / frames;
            Console.WriteLine("    首帧步进 " + F(firstFrameDelta) + " = 平均步进 " + F(averageDelta) +
                              " 的 " + F(firstFrameDelta / averageDelta) + " 倍；最大步进 " + F(maxDelta) +
                              " 出现在第 " + maxDeltaFrame + " 帧；scale>1 共 " + overshootFrames + " 帧（" +
                              F(overshootFrames / FrameRate * 1000.0) + "ms）");
        }

        private static string F(double value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }
    }
}
