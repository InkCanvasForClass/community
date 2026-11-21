using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// WPF 渲染帧率监视器：统计 CompositionTarget.Rendering 的真实出帧频率。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="InkPerformanceMonitor"/> 的区别：后者统计"墨迹提交帧率"
    /// （有笔迹脏标记才计数，空闲时归零），而这里统计的是**进程实际出帧率**——
    /// 也就是用户感知到的界面流畅度。判断掉帧、验证弹簧动画是否顺滑必须看这个。
    ///
    /// 关键点：<c>CompositionTarget.Rendering</c> **不是"每帧一次"**。
    /// 一次合成帧内，事件可能被多次触发（重负载、反复 InvalidateVisual、
    /// 多窗口各自提交时都会出现），这些多余触发的间隔可以是亚毫秒级。
    /// 若把每次回调都当成一帧去求平均，会得到"平均 1.5ms（≈666fps）"这种
    /// 物理上不可能、却又不时报最差 50ms 的自相矛盾读数。
    ///
    /// 所以这里用 <see cref="RenderingEventArgs.RenderingTime"/> 作为帧身份：
    /// RenderingTime 相同即同一帧，只算一次。帧率 = 单位时间内不同 RenderingTime 的个数，
    /// 最差帧 = 相邻两个不同 RenderingTime 的最大间隔。
    /// </remarks>
    internal static class RenderFrameRateMonitor
    {
        private const int WindowCapacity = 1024;

        private static readonly object Gate = new object();

        // 环形缓冲：只记录"真实帧"（RenderingTime 变化时）
        private static readonly long[] FrameRenderTimeTicks = new long[WindowCapacity];
        private static readonly long[] FrameStopwatchTicks = new long[WindowCapacity];

        private static int _head;
        private static int _count;
        private static bool _subscribed;

        private static long _lastRenderTimeTicks = long.MinValue;
        private static long _lastStopwatchTicks;
        private static long _subFrameCallbackCount;

        /// <summary>1 秒统计窗口（毫秒）。</summary>
        private const double StatsWindowMs = 1000.0;

        /// <summary>相邻帧间隔超过该值视为没有持续渲染（毫秒）。</summary>
        public const double IdleThresholdMs = 250;

        /// <summary>供 HUD 读取的帧率快照。</summary>
        public readonly struct Snapshot
        {
            /// <summary>最近 1 秒的平均帧率（按真实帧计算）；样本不足时为 0。</summary>
            public readonly double Fps;

            /// <summary>最近 1 秒内的最差帧耗时（毫秒，相邻真实帧间隔）。</summary>
            public readonly double WorstFrameMs;

            /// <summary>最近 1 秒内的平均帧耗时（毫秒）。</summary>
            public readonly double AverageFrameMs;

            /// <summary>参与统计的真实帧数。</summary>
            public readonly int FrameCount;

            /// <summary>距上一真实帧的时间（毫秒）。</summary>
            public readonly double SinceLastFrameMs;

            /// <summary>被合并掉的同帧重复回调次数（诊断用，不影响帧率）。</summary>
            public readonly long MergedCallbackCount;

            /// <summary>
            /// 是否处于空闲（没有持续渲染）。
            /// WPF 只在内容变化时出帧，静止界面本来就不渲染；
            /// 此时把低帧率显示给用户会被误解成掉帧，必须单独标注。
            /// </summary>
            public bool IsIdle => SinceLastFrameMs > IdleThresholdMs;

            public Snapshot(double fps, double worstFrameMs, double averageFrameMs,
                int frameCount, double sinceLastFrameMs, long mergedCallbackCount)
            {
                Fps = fps;
                WorstFrameMs = worstFrameMs;
                AverageFrameMs = averageFrameMs;
                FrameCount = frameCount;
                SinceLastFrameMs = sinceLastFrameMs;
                MergedCallbackCount = mergedCallbackCount;
            }
        }

        /// <summary>开始常驻统计。重复调用安全。</summary>
        public static void Start()
        {
            lock (Gate)
            {
                if (_subscribed) return;
                _subscribed = true;
                _head = 0;
                _count = 0;
                _lastRenderTimeTicks = long.MinValue;
                _lastStopwatchTicks = 0;
                _subFrameCallbackCount = 0;
            }

            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>停止统计并释放样本。</summary>
        public static void Stop()
        {
            lock (Gate)
            {
                if (!_subscribed) return;
                _subscribed = false;
                _head = 0;
                _count = 0;
                _lastRenderTimeTicks = long.MinValue;
                _lastStopwatchTicks = 0;
            }

            CompositionTarget.Rendering -= OnRendering;
        }

        public static bool IsRunning
        {
            get { lock (Gate) return _subscribed; }
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            var now = Stopwatch.GetTimestamp();
            var renderingTime = (e as RenderingEventArgs)?.RenderingTime.Ticks ?? 0L;

            lock (Gate)
            {
                if (!_subscribed) return;

                // 同一帧内的重复回调：合并掉，不参与帧率统计。
                // 没有 RenderingTime 时（理论上不会发生）退化为按时间戳判断：
                // 与上一帧相差不足 1ms 视为同一帧。
                bool sameFrame = renderingTime != 0L
                    ? renderingTime == _lastRenderTimeTicks
                    : _lastStopwatchTicks != 0
                      && (now - _lastStopwatchTicks) * 1000.0 / Stopwatch.Frequency < 1.0;

                if (sameFrame)
                {
                    _subFrameCallbackCount++;
                    return;
                }

                _lastRenderTimeTicks = renderingTime;
                _lastStopwatchTicks = now;

                FrameRenderTimeTicks[_head] = renderingTime;
                FrameStopwatchTicks[_head] = now;
                _head = (_head + 1) % WindowCapacity;
                if (_count < WindowCapacity) _count++;
            }
        }

        /// <summary>读取当前快照（只读，不清空样本）。</summary>
        public static Snapshot TakeSnapshot()
        {
            lock (Gate)
            {
                if (_count == 0)
                {
                    return new Snapshot(0, 0, 0, 0, 0, _subFrameCallbackCount);
                }

                var now = Stopwatch.GetTimestamp();
                double sinceLastMs = _lastStopwatchTicks == 0
                    ? 0
                    : (now - _lastStopwatchTicks) * 1000.0 / Stopwatch.Frequency;

                // 1 秒窗口：倒着走环形缓冲
                int samples = 0;
                double worstMs = 0;
                long windowNewestRenderTime = 0;
                long windowOldestRenderTime = 0;
                long newestStopwatchTick = 0;

                for (int i = 1; i <= _count; i++)
                {
                    int index = (_head - i + WindowCapacity) % WindowCapacity;
                    long stopwatchTick = FrameStopwatchTicks[index];

                    double ageMs = (now - stopwatchTick) * 1000.0 / Stopwatch.Frequency;
                    if (ageMs > StatsWindowMs) break;

                    samples++;
                    if (windowNewestRenderTime == 0)
                    {
                        windowNewestRenderTime = FrameRenderTimeTicks[index];
                        newestStopwatchTick = stopwatchTick;
                    }
                    windowOldestRenderTime = FrameRenderTimeTicks[index];

                    // 相邻真实帧的"实际等待时间"用墙钟算，而不是 RenderingTime。
                    // RenderingTime 是合成器给的理论帧时刻：一次 45ms 的 UI 线程阻塞
                    // 在 RenderingTime 上只体现出约 33ms 的跳变，用墙钟才接近用户真实等待。
                    // 帧率仍用 RenderingTime 跨度计算（更稳，不受采样抖动影响）。
                    if (i < _count)
                    {
                        int prevIndex = (_head - i - 1 + WindowCapacity) % WindowCapacity;
                        long prevStopwatchTick = FrameStopwatchTicks[prevIndex];
                        if (stopwatchTick > prevStopwatchTick)
                        {
                            double intervalMs = (stopwatchTick - prevStopwatchTick) * 1000.0 / Stopwatch.Frequency;
                            if (intervalMs > worstMs && intervalMs < StatsWindowMs) worstMs = intervalMs;
                        }
                    }
                }

                if (samples < 2)
                {
                    return new Snapshot(0, 0, 0, samples, sinceLastMs, _subFrameCallbackCount);
                }

                // 帧率用 RenderingTime 跨度算，比"帧数/墙钟时长"更稳
                double spanMs = (windowNewestRenderTime - windowOldestRenderTime) / 10000.0;
                double fps = spanMs > 0 ? (samples - 1) * 1000.0 / spanMs : 0;

                // 平均帧耗时优先用墙钟跨度（用户真实感知），Span 异常时退回帧率换算
                double wallSpanMs = newestStopwatchTick > 0
                    ? (newestStopwatchTick - FrameStopwatchTicks[(_head - samples + WindowCapacity) % WindowCapacity])
                      * 1000.0 / Stopwatch.Frequency
                    : 0;
                double averageMs = wallSpanMs > 0 && samples > 1
                    ? wallSpanMs / (samples - 1)
                    : (fps > 0 ? 1000.0 / fps : 0);

                // 自检：平均帧耗时短于 2ms（>500fps）在真实显示上不可能出现。
                // 若出现，说明仍有同一帧的重复回调漏过去重（不同 RenderingTime 但实际同帧），
                // 这时宁可退回"按墙钟总跨度 / 样本数"的口径，也不能把不可能的读数显示给用户。
                if (averageMs > 0 && averageMs < 2.0 && wallSpanMs > 0)
                {
                    double fallbackAverage = wallSpanMs / Math.Max(1, samples);
                    double fallbackFps = 1000.0 / Math.Max(fallbackAverage, 0.0001);
                    if (fallbackFps > 5 && fallbackFps < 500)
                    {
                        fps = fallbackFps;
                        averageMs = fallbackAverage;
                    }
                }

                return new Snapshot(fps, worstMs, averageMs, samples, sinceLastMs, _subFrameCallbackCount);
            }
        }

        /// <summary>
        /// 用最近 60 个真实帧估算持续性帧率，用于区分"偶发一次尖峰"与"整体在掉帧"。
        /// </summary>
        public static double TakeRecentFps()
        {
            lock (Gate)
            {
                if (_count < 2) return 0;

                int samples = Math.Min(60, _count);
                long newest = FrameRenderTimeTicks[(_head - 1 + WindowCapacity) % WindowCapacity];
                long oldest = FrameRenderTimeTicks[(_head - samples + WindowCapacity) % WindowCapacity];

                double spanMs = (newest - oldest) / 10000.0;
                return spanMs > 0 ? (samples - 1) * 1000.0 / spanMs : 0;
            }
        }
    }
}
