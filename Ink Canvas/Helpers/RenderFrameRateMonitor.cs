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
    /// 与 <see cref="InkPerformanceMonitor"/> 的区别：后者统计的是"墨迹提交帧率"
    /// （有笔迹脏标记才计数，空闲时归零），而这里统计的是**进程实际出帧率**——
    /// 也就是用户感知到的界面流畅度。判断掉帧、验证弹簧动画是否顺滑必须看这个。
    ///
    /// 设计要点：
    /// - 常驻订阅，不解绑：帧率监视一旦"没人用就停"，恰恰测不到空闲/卡顿时段，
    ///   而 UI 线程被长任务阻塞正是要从"帧间隔突然变长"里看出来的。
    /// - 用环形缓冲而不是逐帧 List：每秒 60~240 帧，长时间开 HUD 不能持续分配内存。
    /// - 统计口径分两层：1 秒窗口的平均帧率（读数稳定）与最近 N 帧的最差帧间隔
    ///   （掉帧尖峰）。只看平均会漏掉"大部分帧很快、偶尔卡住 200ms"这种典型卡顿。
    /// </remarks>
    internal static class RenderFrameRateMonitor
    {
        private const int WindowCapacity = 512;

        private static readonly object Gate = new object();
        private static readonly long[] FrameTicks = new long[WindowCapacity];
        private static readonly double[] FrameIntervalsMs = new double[WindowCapacity];

        private static int _head;
        private static int _count;
        private static bool _subscribed;
        private static long _lastFrameAt;

        // 最近若干帧的最差间隔，比平均值更能反映"顿一下"的观感
        private const int WorstWindowFrames = 60;

        /// <summary>供 HUD 读取的帧率快照。</summary>
        public readonly struct Snapshot
        {
            /// <summary>最近 1 秒的平均帧率；样本不足时为 0。</summary>
            public readonly double Fps;

            /// <summary>最近 1 秒内的出帧次数（真实渲染次数，不是定时器次数）。</summary>
            public int RenderedFrameCount => FrameCount;

            /// <summary>最近 1 秒内的最差帧耗时（毫秒）。</summary>
            public readonly double WorstFrameMs;

            /// <summary>最近 1 秒内的平均帧耗时（毫秒）。</summary>
            public readonly double AverageFrameMs;

            /// <summary>参与统计的帧数。</summary>
            public readonly int FrameCount;

            /// <summary>距上一帧的时间（毫秒）；长时间无帧时持续变大，可据此判断"卡死"。</summary>
            public readonly double SinceLastFrameMs;

            /// <summary>
            /// 是否处于空闲（没有持续渲染）。
            /// WPF 只在内容变化时出帧，静止界面本来就不渲染；
            /// 此时把低帧率显示给用户会被误解成掉帧，必须单独标注。
            /// </summary>
            public bool IsIdle => SinceLastFrameMs > IdleThresholdMs;

            /// <summary>相邻帧间隔超过该值即视为没有持续渲染（毫秒）。</summary>
            public const double IdleThresholdMs = 250;

            public Snapshot(double fps, double worstFrameMs, double averageFrameMs,
                int frameCount, double sinceLastFrameMs)
            {
                Fps = fps;
                WorstFrameMs = worstFrameMs;
                AverageFrameMs = averageFrameMs;
                FrameCount = frameCount;
                SinceLastFrameMs = sinceLastFrameMs;
            }
        }

        /// <summary>开始常驻统计。重复调用安全。</summary>
        public static void Start()
        {
            lock (Gate)
            {
                if (_subscribed) return;
                _subscribed = true;
                _lastFrameAt = 0;
                _head = 0;
                _count = 0;
            }

            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>停止统计并释放样本。HUD 关闭后调用，避免长期空转。</summary>
        public static void Stop()
        {
            lock (Gate)
            {
                if (!_subscribed) return;
                _subscribed = false;
                _head = 0;
                _count = 0;
                _lastFrameAt = 0;
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

            lock (Gate)
            {
                if (!_subscribed) return;

                double intervalMs = 0;
                if (_lastFrameAt != 0)
                {
                    intervalMs = (now - _lastFrameAt) * 1000.0 / Stopwatch.Frequency;
                }
                _lastFrameAt = now;

                FrameTicks[_head] = now;
                FrameIntervalsMs[_head] = intervalMs;
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
                    return new Snapshot(0, 0, 0, 0, 0);
                }

                var now = Stopwatch.GetTimestamp();
                double sinceLastMs = _lastFrameAt == 0
                    ? 0
                    : (now - _lastFrameAt) * 1000.0 / Stopwatch.Frequency;

                // 1 秒时间窗：倒着走环形缓冲，直到累计时长超过 1 秒
                int samples = 0;
                double windowMs = 0;
                double worstMs = 0;

                for (int i = 1; i <= _count; i++)
                {
                    int index = (_head - i + WindowCapacity) % WindowCapacity;
                    long tick = FrameTicks[index];

                    double ageMs = (now - tick) * 1000.0 / Stopwatch.Frequency;
                    if (ageMs > 1000.0) break;

                    double intervalMs = FrameIntervalsMs[index];
                    // 首帧、以及长时间暂停后的第一帧间隔不代表真实出帧节奏，跳过
                    if (intervalMs > 0 && intervalMs < 1000.0)
                    {
                        samples++;
                        windowMs += intervalMs;
                        if (intervalMs > worstMs) worstMs = intervalMs;
                    }
                }

                if (samples == 0 || windowMs <= 0)
                {
                    return new Snapshot(0, 0, 0, 0, sinceLastMs);
                }

                double averageMs = windowMs / samples;
                double fps = averageMs > 0 ? 1000.0 / averageMs : 0;

                return new Snapshot(fps, worstMs, averageMs, samples, sinceLastMs);
            }
        }

        /// <summary>
        /// 用最近 <see cref="WorstWindowFrames"/> 帧估算持续性帧率，用于区分
        /// "偶发一次尖峰"与"整体就在掉帧"。
        /// </summary>
        public static double TakeRecentFps()
        {
            lock (Gate)
            {
                if (_count < 2) return 0;

                int samples = Math.Min(WorstWindowFrames, _count - 1);
                double totalMs = 0;
                for (int i = 1; i <= samples; i++)
                {
                    int index = (_head - i + WindowCapacity) % WindowCapacity;
                    double intervalMs = FrameIntervalsMs[index];
                    if (intervalMs <= 0 || intervalMs >= 1000.0) continue;
                    totalMs += intervalMs;
                }

                return totalMs > 0 ? samples * 1000.0 / totalMs : 0;
            }
        }
    }
}
