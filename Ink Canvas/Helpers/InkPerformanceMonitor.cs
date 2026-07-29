using System;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 实时墨迹渲染线程主动上报的帧样本。
    /// 对应 Steady-Ink 的 PerformanceFrameSample:
    /// https://github.com/Enigfrank/Steady-Ink/blob/main/src/performance/monitor.rs
    ///
    /// 与 Steady-Ink 不同,我们不强制要求每帧都填全字段;只在有"脏→出帧"对应关系时填 InputLatency。
    /// </summary>
    internal struct InkFrameSample
    {
        /// <summary>该帧对应的最早 RequestRedraw 时间戳(Stopwatch ticks)。0 表示无脏墨迹。</summary>
        public long DirtyStartedAtTicks;

        /// <summary>该帧实际呈现的时刻(Stopwatch ticks)。</summary>
        public long PresentedAtTicks;
    }

    /// <summary>
    /// 实时墨迹 FPS / 延迟聚合器(Steady-Ink 风格)。
    ///
    /// 设计要点:
    /// - 单一权威:由 FrameScheduler.OnRendering(旧墨迹) 和 WetInkWindowHost._renderer.Apply(新墨迹) 主动 record_frame。
    /// - HUD 不订阅事件,而是周期调用 Snapshot() 拿到已发布快照。
    /// - FPS = 活跃呈现间隔的倒数;空闲 &gt; IdleGapLimit 自动清空窗口,避免长时间静止拉低 FPS。
    /// - 延迟 = 脏墨迹请求到本次出帧的端到端耗时。
    ///
    /// 这里使用 static 实现:整个进程一份,与项目其他实时性能监控器一致。
    /// </summary>
    internal static class InkPerformanceMonitor
    {
        public const int SampleCapacity = 120;
        public const double IdleGapLimitMs = 1000.0;

        private static readonly object _sync = new object();
        private static readonly double[] _presentIntervals = new double[SampleCapacity];
        private static int _presentIntervalCount;
        private static int _presentIntervalNext;
        private static long _lastPresentedAt;

        private static readonly double[] _inputLatencies = new double[SampleCapacity];
        private static int _inputLatencyCount;
        private static int _inputLatencyNext;

        private static long _frameCount;
        private static long _inputSampleCount;

        private static bool _enabled;

        public static bool Enabled => _enabled;

        public static void RecordFrame(InkFrameSample sample)
        {
            lock (_sync)
            {
                if (!_enabled)
                {
                    _lastPresentedAt = 0L;
                    return;
                }

                var presentedAt = sample.PresentedAtTicks;

                if (_lastPresentedAt != 0L)
                {
                    var intervalMs = (presentedAt - _lastPresentedAt) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (intervalMs < 0) intervalMs = 0;
                    if (intervalMs > IdleGapLimitMs)
                    {
                        _presentIntervalCount = 0;
                        _presentIntervalNext = 0;
                    }
                    else if (intervalMs > 0)
                    {
                        _presentIntervals[_presentIntervalNext] = intervalMs;
                        _presentIntervalNext = (_presentIntervalNext + 1) % SampleCapacity;
                        if (_presentIntervalCount < SampleCapacity)
                            _presentIntervalCount++;
                    }
                }
                _lastPresentedAt = presentedAt;

                if (sample.DirtyStartedAtTicks != 0L)
                {
                    var latencyMs = (presentedAt - sample.DirtyStartedAtTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (latencyMs >= 0)
                    {
                        _inputLatencies[_inputLatencyNext] = latencyMs;
                        _inputLatencyNext = (_inputLatencyNext + 1) % SampleCapacity;
                        if (_inputLatencyCount < SampleCapacity)
                            _inputLatencyCount++;
                        _inputSampleCount++;
                    }
                }

                _frameCount++;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            lock (_sync)
            {
                if (_enabled == enabled) return;
                _enabled = enabled;
                ResetUnsafe();
            }
        }

        public static InkPerformanceSnapshot Snapshot()
        {
            lock (_sync)
            {
                var s = new InkPerformanceSnapshot
                {
                    Enabled = _enabled,
                    FrameCount = _frameCount,
                    InputSampleCount = _inputSampleCount,
                    LastIntervalMs = _presentIntervalCount > 0
                        ? _presentIntervals[(_presentIntervalNext + SampleCapacity - 1) % SampleCapacity]
                        : 0
                };

                if (_presentIntervalCount > 0)
                {
                    double sum = 0;
                    for (int i = 0; i < _presentIntervalCount; i++) sum += _presentIntervals[i];
                    var avg = sum / _presentIntervalCount;
                    s.Fps = avg > 0 ? (float)(1000.0 / avg) : 0f;
                }

                if (_inputLatencyCount > 0)
                {
                    double sumLatency = 0;
                    double maxLatency = 0;
                    for (int i = 0; i < _inputLatencyCount; i++)
                    {
                        var latency = _inputLatencies[i];
                        sumLatency += latency;
                        if (latency > maxLatency)
                            maxLatency = latency;
                    }
                    s.AverageInputLatencyMs = (float)(sumLatency / _inputLatencyCount);
                    s.MaxInputLatencyMs = (float)maxLatency;
                }

                return s;
            }
        }

        private static void ResetUnsafe()
        {
            _frameCount = 0;
            _inputSampleCount = 0;
            _lastPresentedAt = 0L;
            _presentIntervalCount = 0;
            _presentIntervalNext = 0;
            _inputLatencyCount = 0;
            _inputLatencyNext = 0;
        }
    }

    /// <summary>
    /// 已发布的墨迹性能快照(Steady-Ink:PerformanceSnapshot)。
    /// </summary>
    internal struct InkPerformanceSnapshot
    {
        public bool Enabled;
        public long FrameCount;
        public long InputSampleCount;
        public float Fps;
        public float AverageInputLatencyMs;
        public float MaxInputLatencyMs;
        public double LastIntervalMs;
    }
}