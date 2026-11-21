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
    /// </remarks>
    internal static class PopupCurveReport
    {
        /// <summary>菜单打开时长（秒），与 AnimationsHelper.PopupPopInDurationSeconds 保持一致。</summary>
        private const double OpenSeconds = 0.40;

        /// <summary>菜单收起时长（秒），与 AnimationsHelper.PopupPopOutDurationSeconds 保持一致。</summary>
        private const double CloseSeconds = 0.30;

        /// <summary>采样帧率。</summary>
        private const double FrameRate = 60.0;

        public static DiagnosticReport Run()
        {
            var report = new DiagnosticReport();
            Console.WriteLine("== 曲线形状 ==");

            var start = PopupCurve.Evaluate(0.0);
            report.Check(
                "progress=0 时处于收起态",
                Math.Abs(start.Scale - PopupCurve.StartScale) < 1e-12 && Math.Abs(start.SlideY - PopupCurve.SlideDistance) < 1e-12,
                "scale=" + F(start.Scale) + " slide=" + F(start.SlideY));

            var end = PopupCurve.Evaluate(1.0);
            report.Check(
                "progress=1 时精确落位",
                Math.Abs(end.Scale - 1.0) < 1e-12 && Math.Abs(end.SlideY) < 1e-12 && Math.Abs(end.OpacityWeight - 1.0) < 1e-12,
                "scale=" + F(end.Scale) + " slide=" + F(end.SlideY));

            var peak = PopupCurve.Evaluate(PopupCurve.PeakAt);
            report.Check(
                "峰值缩放与留白常量一致",
                Math.Abs(peak.Scale - PopupCurve.PeakScale) < 1e-9,
                "Evaluate(PeakAt)=" + F(peak.Scale) + " PeakScale=" + F(PopupCurve.PeakScale));

            var maxScale = double.MinValue;
            var maxAt = 0.0;
            for (var i = 0; i <= 1000; i++)
            {
                var p = i / 1000.0;
                var sample = PopupCurve.Evaluate(p);
                if (sample.Scale > maxScale)
                {
                    maxScale = sample.Scale;
                    maxAt = p;
                }
            }
            // 上限检查：采样点里最大的缩放不能超过留白所依据的 PeakScale，否则会被窗体裁掉。
            report.Check(
                "全程不超过 PeakScale（留白依据）",
                maxScale <= PopupCurve.PeakScale + 1e-9,
                "最大 scale=" + F(maxScale) + " 出现在 progress=" + F(maxAt) + "，PeakScale=" + F(PopupCurve.PeakScale));

            // 透明度权重被点积进 visual opacity，超出 [0,1] 会被 WPF 静默截断，表现为"突然变实/变透"。
            var minOpacity = double.MaxValue;
            var maxOpacity = double.MinValue;
            var maxUpwardSlide = 0.0;
            var hasNaN = false;
            for (var i = 0; i <= 1000; i++)
            {
                var sample = PopupCurve.Evaluate(i / 1000.0);
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
                "透明度下限与独立时间线起点一致",
                Math.Abs(PopupCurve.OpacityWeight(0.0) - PopupCurve.MinOpacityWeight) < 1e-12,
                "OpacityWeight(0)=" + F(PopupCurve.OpacityWeight(0.0)));

            // 几何越过 1.0（开始过冲）那一刻的透明度，是"几何先到位、透明度收尾"的量化指标。
            var crossAt = 0.0;
            for (var i = 1; i <= 1000; i++)
                if (PopupCurve.Travel(i / 1000.0) >= 1.0)
                {
                    crossAt = i / 1000.0;
                    break;
                }
            report.Note("几何于 progress=" + F(crossAt) + "（" + F(crossAt * OpenSeconds * 1000.0) +
                        "ms）越过 1.0 开始过冲，此刻透明度才 " + F(PopupCurve.OpacityWeight(crossAt)) +
                        "，余下 " + F((1.0 - crossAt) * OpenSeconds * 1000.0) + "ms 由透明度独自收尾");

            // 向上滑动是否会被窗体裁掉取决于宿主留白，属于几何问题，由 PopupGeometryReport 用真窗口核对；
            // 这里只把数值摊出来，方便和那边的留白对照。
            report.Note("峰值向上的滑移量 " + F(maxUpwardSlide) + " DIP（裁切与否见几何检查）");

            PrintTable("打开", OpenSeconds);
            PrintTable("收起", CloseSeconds);

            return report;
        }

        /// <summary>按真实帧步长打印一次完整播放的采样序列。</summary>
        private static void PrintTable(string title, double seconds)
        {
            var frames = (int)Math.Round(seconds * FrameRate);
            Console.WriteLine();
            Console.WriteLine("  " + title + "  " + F(seconds) + "s / " + frames + " 帧 @ " + (int)FrameRate + "fps" +
                              "（每行：帧序号 scale slide 透明度权重）");

            var previousScale = PopupCurve.Evaluate(0.0).Scale;
            var maxDelta = 0.0;
            var maxDeltaFrame = 0;
            var firstFrameDelta = 0.0;
            var overshootFrames = 0;

            for (var frame = 0; frame <= frames; frame++)
            {
                var progress = frame / (double)frames;
                var sample = PopupCurve.Evaluate(progress);
                var delta = frame == 0 ? 0.0 : Math.Abs(sample.Scale - previousScale);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    maxDeltaFrame = frame;
                }
                if (frame == 1) firstFrameDelta = delta;
                if (sample.Scale > 1.0) overshootFrames++;

                Console.WriteLine("    " + frame.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "  " +
                                  F(sample.Scale) + "  " + F(sample.SlideY).PadLeft(7) + "  " + F(sample.OpacityWeight));
                previousScale = sample.Scale;
            }

            var averageDelta = (1.0 - PopupCurve.StartScale) / frames;
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
