using System.Threading;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// 新墨迹管线（WetInkWindowHost / NativeInkController）专用性能计数器。
    /// 与 legacy StrokeVisual RealtimeInkPerformanceMonitor 解耦，只暴露原子字段；
    /// 由 RealtimeInkPerformanceMonitor.WriteLiveStatus 读取并写入 RealtimeInkDebugLive.json。
    ///
    /// per-input-kind 索引约定：Pen=0, Touch=1, Mouse=2（与 NativeInkInputKind 枚举对齐）。
    /// </summary>
    internal static class NativeInkPerfProbe
    {
        private static long _lastApplyTicks100;
        private static long _lastApplySampleCount;
        private static long _lastApplySessionCount;

        private static long _lastStrokeCommitMsTimes100;

        private static long _beginStrokeCount;
        private static long _endStrokeCount;
        private static long _cancelStrokeCount;

        private static long _applyFrameCount;
        private static long _totalApplyTicks100;
        private static long _totalSampleCount;

        // 单帧 controller Update 路径分段计时（毫秒，*1000 ticks）
        private static long _maxUpdateAppendTicks1000;
        private static long _maxUpdatePredictTicks1000;
        private static long _maxUpdatePublishTicks1000;
        private static long _maxUpdateTotalTicks1000;
        private static long _lastUpdateTotalMs;
        private static long _updateCount;

        // ---- per-input-kind 累计（与 RealtimeInkPerformanceMonitor.byInputKind 对齐）----
        private static readonly PerKindStats[] _perKind = new[]
        {
            new PerKindStats(), // Pen
            new PerKindStats(), // Touch
            new PerKindStats()  // Mouse
        };

        private static readonly object _perKindLock = new object();

        internal sealed class PerKindStats
        {
            public long InputEventCount;
            public long RawInputPointCount;
            public long AddedPointCount;
            public long RedrawCount;
            public long ForceRedrawCount;
            public long CommitCount;
            public double TotalInputProcessingMs;
            public double MaxInputProcessingMs;
            public double TotalRedrawMs;
            public double MaxRedrawMs;
            public long SlowInputOver1MsCount;
            public long SlowRedrawOver1MsCount;
            public long SlowRedrawOver3MsCount;
            public long SlowRedrawOver5MsCount;

            public void AddInputEvent(int rawCount, int addedCount, double elapsedMs)
            {
                InputEventCount++;
                RawInputPointCount += rawCount;
                AddedPointCount += addedCount;
                TotalInputProcessingMs += elapsedMs;
                if (elapsedMs > MaxInputProcessingMs) MaxInputProcessingMs = elapsedMs;
                if (elapsedMs > 1.0) SlowInputOver1MsCount++;
            }

            public void AddRedraw(double elapsedMs, bool forceRedraw, bool committed)
            {
                RedrawCount++;
                if (forceRedraw) ForceRedrawCount++;
                if (committed) CommitCount++;
                TotalRedrawMs += elapsedMs;
                if (elapsedMs > MaxRedrawMs) MaxRedrawMs = elapsedMs;
                if (elapsedMs > 1.0) SlowRedrawOver1MsCount++;
                if (elapsedMs > 3.0) SlowRedrawOver3MsCount++;
                if (elapsedMs > 5.0) SlowRedrawOver5MsCount++;
            }
        }

        public static void RecordApply(double elapsedMs, int sampleCount, int sessionCount)
        {
            Interlocked.Increment(ref _applyFrameCount);
            Interlocked.Add(ref _totalApplyTicks100, (long)(elapsedMs * 100));
            Interlocked.Add(ref _totalSampleCount, sampleCount);
            Interlocked.Exchange(ref _lastApplyTicks100, (long)(elapsedMs * 100));
            Interlocked.Exchange(ref _lastApplySampleCount, sampleCount);
            Interlocked.Exchange(ref _lastApplySessionCount, sessionCount);
        }

        /// <summary>UI 线程 controller/marshal 入口记录一次 input 处理。</summary>
        public static void RecordInputEvent(int inputKindIndex, int rawInputPointCount, int addedPointCount, double elapsedMs)
        {
            if (inputKindIndex < 0 || inputKindIndex >= _perKind.Length) return;
            lock (_perKindLock) _perKind[inputKindIndex].AddInputEvent(rawInputPointCount, addedPointCount, elapsedMs);
        }

        /// <summary>MTA 渲染线程 Apply 完成后记录一次 redraw。</summary>
        public static void RecordRedraw(int inputKindIndex, double elapsedMs, bool forceRedraw, bool committed)
        {
            if (inputKindIndex < 0 || inputKindIndex >= _perKind.Length) return;
            lock (_perKindLock) _perKind[inputKindIndex].AddRedraw(elapsedMs, forceRedraw, committed);
        }

        public static void RecordBeginStroke()
        {
            Interlocked.Increment(ref _beginStrokeCount);
        }

        /// <summary>
        /// 记录一次 controller Update 的分段耗时（毫秒）。
        /// appendMs = normalizer + Processor；predictMs = predictor.Build + ReplacePrediction；
        /// publishMs = CreateNextSnapshot + mailbox.PublishSnapshot。
        /// </summary>
        public static void RecordUpdateSegments(
            double appendMs, double predictMs, double publishMs, double totalMs)
        {
            Interlocked.Increment(ref _updateCount);
            Interlocked.Exchange(ref _lastUpdateTotalMs, (long)(totalMs * 100));
            UpdateMax(ref _maxUpdateAppendTicks1000, (long)(appendMs * 1000));
            UpdateMax(ref _maxUpdatePredictTicks1000, (long)(predictMs * 1000));
            UpdateMax(ref _maxUpdatePublishTicks1000, (long)(publishMs * 1000));
            UpdateMax(ref _maxUpdateTotalTicks1000, (long)(totalMs * 1000));
        }

        private static void UpdateMax(ref long field, long value)
        {
            while (true)
            {
                var current = System.Threading.Volatile.Read(ref field);
                if (value <= current || System.Threading.Interlocked.CompareExchange(ref field, value, current) == current)
                    return;
            }
        }

        public static double MaxUpdateAppendMs => _maxUpdateAppendTicks1000 / 1000.0;
        public static double MaxUpdatePredictMs => _maxUpdatePredictTicks1000 / 1000.0;
        public static double MaxUpdatePublishMs => _maxUpdatePublishTicks1000 / 1000.0;
        public static double MaxUpdateTotalMs => _maxUpdateTotalTicks1000 / 1000.0;
        public static double LastUpdateTotalMs => _lastUpdateTotalMs / 100.0;
        public static long UpdateCount => _updateCount;

        public static void RecordEndStroke()
        {
            Interlocked.Increment(ref _endStrokeCount);
        }

        public static void RecordCancelStroke()
        {
            Interlocked.Increment(ref _cancelStrokeCount);
        }

        public static void RecordStrokeCommit(double elapsedMs)
        {
            Interlocked.Exchange(ref _lastStrokeCommitMsTimes100, (long)(elapsedMs * 100));
        }

        public static double LastApplyMs => _lastApplyTicks100 / 100.0;
        public static long LastApplySampleCount => _lastApplySampleCount;
        public static long LastApplySessionCount => _lastApplySessionCount;
        public static double LastStrokeCommitMs => _lastStrokeCommitMsTimes100 / 100.0;
        public static long ApplyFrameCount => _applyFrameCount;
        public static double AverageApplyMs => _applyFrameCount == 0 ? 0 : (_totalApplyTicks100 / 100.0) / _applyFrameCount;
        public static double AverageSamplePerFrame => _applyFrameCount == 0 ? 0 : (double)_totalSampleCount / _applyFrameCount;
        public static long BeginStrokeCount => _beginStrokeCount;
        public static long EndStrokeCount => _endStrokeCount;
        public static long CancelStrokeCount => _cancelStrokeCount;

        /// <summary>Per-kind stats snapshot（用于 Live JSON 写入）。</summary>
        public static PerKindStats[] PerKindSnapshot()
        {
            var copy = new PerKindStats[_perKind.Length];
            lock (_perKindLock)
            {
                for (var i = 0; i < _perKind.Length; i++)
                {
                    copy[i] = new PerKindStats
                    {
                        InputEventCount = _perKind[i].InputEventCount,
                        RawInputPointCount = _perKind[i].RawInputPointCount,
                        AddedPointCount = _perKind[i].AddedPointCount,
                        RedrawCount = _perKind[i].RedrawCount,
                        ForceRedrawCount = _perKind[i].ForceRedrawCount,
                        CommitCount = _perKind[i].CommitCount,
                        TotalInputProcessingMs = _perKind[i].TotalInputProcessingMs,
                        MaxInputProcessingMs = _perKind[i].MaxInputProcessingMs,
                        TotalRedrawMs = _perKind[i].TotalRedrawMs,
                        MaxRedrawMs = _perKind[i].MaxRedrawMs,
                        SlowInputOver1MsCount = _perKind[i].SlowInputOver1MsCount,
                        SlowRedrawOver1MsCount = _perKind[i].SlowRedrawOver1MsCount,
                        SlowRedrawOver3MsCount = _perKind[i].SlowRedrawOver3MsCount,
                        SlowRedrawOver5MsCount = _perKind[i].SlowRedrawOver5MsCount
                    };
                }
            }
            return copy;
        }
    }
}
