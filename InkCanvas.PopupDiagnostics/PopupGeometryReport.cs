using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Ink_Canvas.Helpers.Motion;

namespace Ink_Canvas.PopupDiagnostics
{
    /// <summary>
    /// 真实弹窗上的几何检查：缩放原点是否真的钉在触发按钮处、过冲是否被窗体裁掉。
    /// </summary>
    /// <remarks>
    /// 这两条只有把 Popup 真开起来才测得出来，纯数学单测覆盖不到，也正是历史上出过的两个 bug：
    ///
    ///   1) 第二次打开时原点算成 (0.000, 0.110) —— 宿主被折叠后窗口按 0×0 重定位，
    ///      首帧拿到的还是上一次的窗口矩形，于是菜单从左上角缩放；
    ///   2) 过冲时顶部被裁 —— 留白按"原点居中、四边各半份"算，实际原点在底边中点，
    ///      纵向溢出全堆在顶边。
    ///
    /// 判定不去比对 PopupScaleOrigin 的内部算式（那是自己验自己），而是验它的**外在性质**：
    /// RenderTransformOrigin 的定义就是"缩放时保持不动的那个点"，
    /// 所以只要缩放前后该点的屏幕坐标不变，原点就确实落在它该在的位置；
    /// 再要求菜单停稳后这个点正好停在按钮正上方 8px —— 这才是"从图标处长出来"的定义。
    /// </remarks>
    internal static class PopupGeometryReport
    {
        /// <summary>纯算术比较（同一个值取两次、两个原点是否相等）的容差。</summary>
        private const double ExactTolerance = 1e-9;

        /// <summary>需要经过屏幕坐标换算的几何量允许的误差（像素）。</summary>
        /// <remarks>
        /// <c>PointToScreen</c> 的结果会被物理像素对齐量化到整像素，
        /// 因此这类测量天然带 1px 的粒度；把容差设成比它更小只会得到假失败。
        /// </remarks>
        private const double ScreenMeasureTolerancePixels = 1.0;

        /// <summary>逐帧扫描过冲的采样点数。</summary>
        private const int ProgressSamples = 20;

        public static async Task<DiagnosticReport> RunAsync(Window window)
        {
            var report = new DiagnosticReport();
            Console.WriteLine("== 弹窗几何 ==");

            var harness = MenuHarness.Create(window);
            await MenuHarness.WaitForFramesAsync(2);

            var first = await CheckOpeningAsync(report, harness, "第一次打开");
            harness.Close();
            await Task.Delay(150);

            var second = await CheckOpeningAsync(report, harness, "第二次打开");

            report.Check(
                "两次打开的缩放原点完全一致",
                Math.Abs(first.X - second.X) < ExactTolerance && Math.Abs(first.Y - second.Y) < ExactTolerance,
                "第一次=(" + F(first.X) + ", " + F(first.Y) + ") 第二次=(" + F(second.X) + ", " + F(second.Y) + ")");

            harness.Close();
            await MenuHarness.WaitForFramesAsync(2);
            return report;
        }

        private static async Task<Point> CheckOpeningAsync(DiagnosticReport report, MenuHarness harness, string tag)
        {
            Console.WriteLine();
            report.Note(tag + "：同步阶段解析原点，随后整段扫过冲是否越界");

            var origin = harness.OpenAndResolveOrigin(0.0);
            report.Check(
                tag + "原点落在菜单底边中点（菜单弹在按钮上方）",
                Math.Abs(origin.X - 0.5) <= 0.12 && origin.Y >= 0.85,
                "实测=(" + F(origin.X) + ", " + F(origin.Y) + ")");

            await MenuHarness.WaitForFrameAsync();
            var stableOrigin = harness.CurrentOrigin;
            report.Check(
                tag + "原点在首帧后不被重算",
                Math.Abs(stableOrigin.X - origin.X) < ExactTolerance && Math.Abs(stableOrigin.Y - origin.Y) < ExactTolerance,
                "同步=(" + F(origin.X) + ", " + F(origin.Y) + ") 首帧后=(" + F(stableOrigin.X) + ", " + F(stableOrigin.Y) + ")");

            report.Note("宿主留白 = " + harness.HostPadding);

            // 先在 progress=0 记录原点对应的屏幕位置，再整段扫一遍看它是否漂移、是否越界。
            harness.ApplyProgress(0.0);
            await MenuHarness.WaitForFrameAsync();
            var fixedPointAtStart = harness.OriginScreenPoint();

            var startSlide = harness.CurrentSlideY;

            var worstOverflow = 0.0;
            var worstProgress = 0.0;
            var worstScale = 1.0;
            var worstDriftX = 0.0;
            var worstDriftY = 0.0;
            var worstDriftProgress = 0.0;
            var worstDriftObserved = 0.0;
            var worstSlideDelta = 0.0;

            for (var i = 0; i <= ProgressSamples; i++)
            {
                var progress = i / (double)ProgressSamples;
                harness.ApplyProgress(progress);
                await MenuHarness.WaitForFrameAsync();

                double scale;
                var overflow = harness.MeasureOverflow(out scale);
                if (double.IsNaN(overflow))
                {
                    report.Fail(tag + "可测量弹窗窗口", "PresentationSource 不可用，progress=" + F(progress));
                    return origin;
                }

                if (overflow > worstOverflow)
                {
                    worstOverflow = overflow;
                    worstProgress = progress;
                    worstScale = scale;
                }

                var drift = harness.OriginScreenPoint() - fixedPointAtStart;
                // 原点在缩放方向上必须固定，但纵向滑动是刻意的：
                // 滑动量从 SlideDistance 收到 0，内容整体上移这么多；屏幕坐标 Y 向下为正，
                // 所以理论位移是 -slideDelta，残余应为 0。
                var slideDelta = startSlide - harness.CurrentSlideY;
                var residualY = Math.Abs(drift.Y + slideDelta);
                var residualX = Math.Abs(drift.X);
                if (residualX > worstDriftX) worstDriftX = residualX;
                if (residualY > worstDriftY)
                {
                    worstDriftY = residualY;
                    worstDriftProgress = progress;
                    worstDriftObserved = drift.Y;
                    worstSlideDelta = slideDelta;
                }
            }

            report.Check(
                tag + "过冲全程未被窗体裁切",
                worstOverflow <= 0.0,
                "最大越界=" + F(worstOverflow) + "px（progress=" + F(worstProgress) + " scale=" + F(worstScale) + "），共 " + (ProgressSamples + 1) + " 个采样点");

            report.Check(
                tag + "缩放时原点横向不动",
                worstDriftX <= ScreenMeasureTolerancePixels,
                "横向最大漂移=" + F(worstDriftX) + "px");

            report.Check(
                tag + "纵向只按设计的滑动量移动",
                worstDriftY <= ScreenMeasureTolerancePixels,
                "扣除滑动后的残余=" + F(worstDriftY) + "px（progress=" + F(worstDriftProgress) +
                " 实测位移=" + F(worstDriftObserved) + "px 理论位移=" + F(-worstSlideDelta) + "px；≤1px 属设备像素量化）");

            // 定位是否贴合按钮要在停稳后（progress=1）量，否则量到的是还带着滑动偏移的位置。
            harness.ApplyProgress(1.0);
            await MenuHarness.WaitForFrameAsync();

            var triggerCenter = harness.TriggerTopCenterOnScreen();
            var restingPoint = harness.OriginScreenPoint();
            var offset = restingPoint - triggerCenter;
            report.Check(
                tag + "停稳后菜单底边中点停在按钮正上方",
                Math.Abs(offset.X) <= ScreenMeasureTolerancePixels &&
                offset.Y <= 0.0 &&
                offset.Y >= -MenuHarness.PlacementGapPixels - ScreenMeasureTolerancePixels,
                "偏移=(" + F(offset.X) + ", " + F(offset.Y) + ")px，设计间距 " + F(MenuHarness.PlacementGapPixels) +
                "px（按钮顶边中点=" + Point(triggerCenter) + "，底边中点=" + Point(restingPoint) + "）");

            return origin;
        }

        private static string Point(Point point)
        {
            return "(" + F(point.X) + ", " + F(point.Y) + ")";
        }

        private static string F(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
