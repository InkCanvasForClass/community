namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 实时墨迹渲染线程主动上报的帧样本。
    /// </summary>
    internal struct InkFrameSample
    {
        /// <summary>
        /// 参与本帧提交的最早新增输入样本时间戳（Stopwatch ticks）。
        /// 0 表示该帧没有可用于帧龄统计的输入样本。
        /// </summary>
        public long EarliestSampleAtTicks;

        /// <summary>
        /// 参与本帧提交的最新输入样本时间戳（Stopwatch ticks）。
        /// 0 表示该帧没有可用于提交延迟统计的输入样本。
        /// </summary>
        public long LatestSampleAtTicks;

        /// <summary>
        /// 渲染线程完成本次提交记账的时刻（Stopwatch ticks）。
        /// 这不是像素真正出现在屏幕上的时刻。
        /// </summary>
        public long SubmittedAtTicks;
    }

    /// <summary>
    /// 实时墨迹 FPS / 时序聚合器。
    ///
    /// 设计要点:
    /// - 单一权威：由渲染路径主动调用 RecordFrame 上报样本。
    /// - HUD 不订阅事件，而是周期调用 Snapshot() 读取已发布快照。
    /// - FPS = 活跃提交间隔的倒数；空闲 > IdleGapLimit 自动清空窗口，避免长时间静止拉低 FPS。
    /// - 提交延迟 = 本帧最新输入样本到渲染线程完成本次提交记账的耗时。
    /// - 帧龄 = 本帧最早新增输入样本到渲染线程完成本次提交记账的耗时。
    ///   两者都不是 pen-to-photon 端到端显示延迟，也不是湿墨到干墨交接延迟。
    /// </summary>
    internal static class InkPerformanceMonitor
    {
        public const int SampleCapacity = 120;
        public const double IdleGapLimitMs = 1000.0;

        private static readonly object _sync = new object();
        private static readonly double[] _submitIntervals = new double[SampleCapacity];
        private static int _submitIntervalCount;
        private static int _submitIntervalNext;
        private static long _lastSubmittedAt;

        private static readonly double[] _submitLatencies = new double[SampleCapacity];
        private static int _submitLatencyCount;
        private static int _submitLatencyNext;

        private static readonly double[] _frameAges = new double[SampleCapacity];
        private static int _frameAgeCount;
        private static int _frameAgeNext;

        private static long _frameCount;
        private static long _submitLatencySampleCount;
        private static long _frameAgeSampleCount;

        private static bool _enabled;

        public static bool Enabled => _enabled;

        public static void RecordFrame(InkFrameSample sample)
        {
            lock (_sync)
            {
                if (!_enabled)
                {
                    _lastSubmittedAt = 0L;
                    return;
                }

                var submittedAt = sample.SubmittedAtTicks;

                if (_lastSubmittedAt != 0L)
                {
                    var intervalMs = (submittedAt - _lastSubmittedAt) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (intervalMs < 0)
                        intervalMs = 0;
                    if (intervalMs > IdleGapLimitMs)
                    {
                        _submitIntervalCount = 0;
                        _submitIntervalNext = 0;
                    }
                    else if (intervalMs > 0)
                    {
                        _submitIntervals[_submitIntervalNext] = intervalMs;
                        _submitIntervalNext = (_submitIntervalNext + 1) % SampleCapacity;
                        if (_submitIntervalCount < SampleCapacity)
                            _submitIntervalCount++;
                    }
                }
                _lastSubmittedAt = submittedAt;

                if (sample.LatestSampleAtTicks != 0L)
                {
                    var latencyMs = (submittedAt - sample.LatestSampleAtTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (latencyMs >= 0)
                    {
                        _submitLatencies[_submitLatencyNext] = latencyMs;
                        _submitLatencyNext = (_submitLatencyNext + 1) % SampleCapacity;
                        if (_submitLatencyCount < SampleCapacity)
                            _submitLatencyCount++;
                        _submitLatencySampleCount++;
                    }
                }

                if (sample.EarliestSampleAtTicks != 0L)
                {
                    var frameAgeMs = (submittedAt - sample.EarliestSampleAtTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (frameAgeMs >= 0)
                    {
                        _frameAges[_frameAgeNext] = frameAgeMs;
                        _frameAgeNext = (_frameAgeNext + 1) % SampleCapacity;
                        if (_frameAgeCount < SampleCapacity)
                            _frameAgeCount++;
                        _frameAgeSampleCount++;
                    }
                }

                _frameCount++;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            lock (_sync)
            {
                if (_enabled == enabled)
                    return;
                _enabled = enabled;
                ResetUnsafe();
            }
        }

        public static InkPerformanceSnapshot Snapshot()
        {
            lock (_sync)
            {
                var snapshot = new InkPerformanceSnapshot
                {
                    Enabled = _enabled,
                    FrameCount = _frameCount,
                    SubmitLatencySampleCount = _submitLatencySampleCount,
                    FrameAgeSampleCount = _frameAgeSampleCount,
                    LastIntervalMs = _submitIntervalCount > 0
                        ? _submitIntervals[(_submitIntervalNext + SampleCapacity - 1) % SampleCapacity]
                        : 0
                };

                if (_submitIntervalCount > 0)
                {
                    double sum = 0;
                    for (int i = 0; i < _submitIntervalCount; i++)
                        sum += _submitIntervals[i];
                    var avg = sum / _submitIntervalCount;
                    snapshot.Fps = avg > 0 ? (float)(1000.0 / avg) : 0f;
                }

                if (_submitLatencyCount > 0)
                {
                    double sumLatency = 0;
                    double maxLatency = 0;
                    for (int i = 0; i < _submitLatencyCount; i++)
                    {
                        var latency = _submitLatencies[i];
                        sumLatency += latency;
                        if (latency > maxLatency)
                            maxLatency = latency;
                    }
                    snapshot.AverageSubmitLatencyMs = (float)(sumLatency / _submitLatencyCount);
                    snapshot.MaxSubmitLatencyMs = (float)maxLatency;
                }

                if (_frameAgeCount > 0)
                {
                    double sumFrameAge = 0;
                    double maxFrameAge = 0;
                    for (int i = 0; i < _frameAgeCount; i++)
                    {
                        var frameAge = _frameAges[i];
                        sumFrameAge += frameAge;
                        if (frameAge > maxFrameAge)
                            maxFrameAge = frameAge;
                    }
                    snapshot.AverageFrameAgeMs = (float)(sumFrameAge / _frameAgeCount);
                    snapshot.MaxFrameAgeMs = (float)maxFrameAge;
                }

                return snapshot;
            }
        }

        private static void ResetUnsafe()
        {
            _frameCount = 0;
            _submitLatencySampleCount = 0;
            _frameAgeSampleCount = 0;
            _lastSubmittedAt = 0L;
            _submitIntervalCount = 0;
            _submitIntervalNext = 0;
            _submitLatencyCount = 0;
            _submitLatencyNext = 0;
            _frameAgeCount = 0;
            _frameAgeNext = 0;
        }
    }

    /// <summary>
    /// 已发布的墨迹性能快照。
    /// </summary>
    internal struct InkPerformanceSnapshot
    {
        public bool Enabled;
        public long FrameCount;
        public long SubmitLatencySampleCount;
        public long FrameAgeSampleCount;
        public float Fps;
        public float AverageSubmitLatencyMs;
        public float MaxSubmitLatencyMs;
        public float AverageFrameAgeMs;
        public float MaxFrameAgeMs;
        public double LastIntervalMs;
    }
}
