using System;
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

        // 鼠标 raw/legacy 采样诊断（由 MW_NativeWetInk 定期同步自 NativePointerInputSource）
        private static long _rawMouseSampleCount;
        private static long _legacyMouseSampleCount;
        private static int _rawMouseActive;
        private static int _rawMouseRegisterError;

        // 单条 WM_POINTERUPDATE 合并历史（GetPointerPenInfoHistory / GetPointerTouchInfoHistory
        // 返回的整批样本）采样统计：用于诊断「UI 卡顿→消息合并→历史超容量→丢段→笔迹跨段直跳」。
        // 单一全局累计（不按输入类型分桶）：丢段是系统级问题，Pen/Touch 合并时同样可疑。
        private static long _pointerHistoryEventCount;
        private static long _pointerHistoryTruncatedCount;
        private static long _maxPointerHistoryTotalCount;
        private static long _maxPointerHistoryAcceptedCount;
        private static long _maxPointerHistoryDroppedCount;

        /// <summary>由装配层把 NativePointerInputSource 的 raw-mouse 诊断同步进来。</summary>
        public static void UpdateRawMouseDiagnostics(bool rawActive, long rawCount, long legacyCount, int registerError)
        {
            Volatile.Write(ref _rawMouseActive, rawActive ? 1 : 0);
            Interlocked.Exchange(ref _rawMouseSampleCount, rawCount);
            Interlocked.Exchange(ref _legacyMouseSampleCount, legacyCount);
            Interlocked.Exchange(ref _rawMouseRegisterError, registerError);
        }

        public static bool RawMouseActive => Volatile.Read(ref _rawMouseActive) != 0;
        public static long RawMouseSampleCount => Volatile.Read(ref _rawMouseSampleCount);
        public static long LegacyMouseSampleCount => Volatile.Read(ref _legacyMouseSampleCount);
        public static int RawMouseRegisterError => Volatile.Read(ref _rawMouseRegisterError);

        /// <summary>
        /// 记录一次指针合并历史读取。<paramref name="totalCount"/> 是系统报告的可用条数，
        /// <paramref name="acceptedCount"/> 是实际读入的条数；两者不等即发生永久丢段
        /// （该 API 不支持翻页，未读到的更早条目在下一条消息取走后即不可恢复）。
        /// 由 NativePointerInputSource 在 WndProc 同步调用，只做原子累加。
        /// </summary>
        public static void RecordPointerHistory(
            int totalCount,
            int acceptedCount,
            bool historyComplete)
        {
            Interlocked.Increment(ref _pointerHistoryEventCount);
            UpdateMax(ref _maxPointerHistoryTotalCount, totalCount);
            UpdateMax(ref _maxPointerHistoryAcceptedCount, acceptedCount);
            if (historyComplete)
                return;
            Interlocked.Increment(ref _pointerHistoryTruncatedCount);
            UpdateMax(ref _maxPointerHistoryDroppedCount, Math.Max(0, totalCount - acceptedCount));
        }

        public static long PointerHistoryEventCount => Interlocked.Read(ref _pointerHistoryEventCount);
        public static long PointerHistoryTruncatedCount => Interlocked.Read(ref _pointerHistoryTruncatedCount);
        public static long MaxPointerHistoryTotalCount => Interlocked.Read(ref _maxPointerHistoryTotalCount);
        public static long MaxPointerHistoryAcceptedCount => Interlocked.Read(ref _maxPointerHistoryAcceptedCount);
        public static long MaxPointerHistoryDroppedCount => Interlocked.Read(ref _maxPointerHistoryDroppedCount);

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

        // Apply 内部分段（渲染线程）：snapshot 几何/path 更新 vs Draw 绘制 vs Present
        private static long _lastApplySnapshotUpdateMs100;
        private static long _lastApplyDrawMs100;
        private static long _lastApplyPresentMs100;
        private static long _maxApplySnapshotUpdateMs1000;
        private static long _maxApplyDrawMs1000;
        private static long _maxApplyPresentMs1000;

        public static void RecordApplySegments(double snapshotUpdateMs, double drawMs, double presentMs)
        {
            Interlocked.Exchange(ref _lastApplySnapshotUpdateMs100, (long)(snapshotUpdateMs * 100));
            Interlocked.Exchange(ref _lastApplyDrawMs100, (long)(drawMs * 100));
            Interlocked.Exchange(ref _lastApplyPresentMs100, (long)(presentMs * 100));
            UpdateMax(ref _maxApplySnapshotUpdateMs1000, (long)(snapshotUpdateMs * 1000));
            UpdateMax(ref _maxApplyDrawMs1000, (long)(drawMs * 1000));
            UpdateMax(ref _maxApplyPresentMs1000, (long)(presentMs * 1000));
        }

        public static double LastApplySnapshotUpdateMs => _lastApplySnapshotUpdateMs100 / 100.0;
        public static double LastApplyDrawMs => _lastApplyDrawMs100 / 100.0;
        public static double LastApplyPresentMs => _lastApplyPresentMs100 / 100.0;
        public static double MaxApplySnapshotUpdateMs => _maxApplySnapshotUpdateMs1000 / 1000.0;
        public static double MaxApplyDrawMs => _maxApplyDrawMs1000 / 1000.0;
        public static double MaxApplyPresentMs => _maxApplyPresentMs1000 / 1000.0;

        // ---- 系统级指标：Dispatcher 延迟 / GC 分配 / Present 阻塞 ----
        private static double _lastDispatcherDelayMs;
        private static double _maxDispatcherDelayMs;
        private static long _gcAllocatedBytesAtLastFlush;
        private static double _gcAllocatedSinceLastFlushBytes;
        private static long _presentWasStillDrawingCount;
        private static long _lastPresentWasStillDrawingTicks1000;

        /// <summary>由装配层在 UI 线程 BeginInvoke 一次，测 UI 线程阻塞延迟。</summary>
        public static void RecordDispatcherDelay(double elapsedMs)
        {
            _lastDispatcherDelayMs = elapsedMs;
            if (elapsedMs > _maxDispatcherDelayMs) _maxDispatcherDelayMs = elapsedMs;
        }

        /// <summary>由 Live JSON 刷新时快照累计分配字节（测 GC 压力）。</summary>
        public static void UpdateGcAllocatedBytes()
        {
            var now = GC.GetTotalAllocatedBytes();
            if (_gcAllocatedBytesAtLastFlush != 0)
                _gcAllocatedSinceLastFlushBytes = (now - _gcAllocatedBytesAtLastFlush) / (1024.0 * 1024.0);
            _gcAllocatedBytesAtLastFlush = now;
        }

        /// <summary>Present 返回 DXGI_ERROR_WAS_STILL_DRAWING 的次数（GPU 忙→丢帧）。</summary>
        public static void RecordPresentWasStillDrawing(double elapsedMs)
        {
            Interlocked.Increment(ref _presentWasStillDrawingCount);
            Interlocked.Exchange(ref _lastPresentWasStillDrawingTicks1000, (long)(elapsedMs * 1000));
        }

        public static double LastDispatcherDelayMs => _lastDispatcherDelayMs;
        public static double MaxDispatcherDelayMs => _maxDispatcherDelayMs;
        public static double GcAllocatedSinceLastFlushBytes => _gcAllocatedSinceLastFlushBytes;
        public static long PresentWasStillDrawingCount => Interlocked.Read(ref _presentWasStillDrawingCount);
        public static double LastPresentWasStillDrawingMs => Interlocked.Read(ref _lastPresentWasStillDrawingTicks1000) / 1000.0;

        // ---- Dispatcher 慢操作来源统计 ----
        private static long _slowOpCount;
        private static long _slowOpOver20Ms;
        private static long _slowOpOver50Ms;
        private static long _slowOpOver100Ms;
        private static long _slowOpOver200Ms;
        private static long _slowOpTotalMs;
        private static string _lastSlowOpName = string.Empty;
        private static double _lastSlowOpMs;

        /// <summary>记录一个 Dispatcher 慢操作（排队+执行总时长）。</summary>
        public static void RecordSlowDispatcherOp(string name, double elapsedMs)
        {
            Interlocked.Increment(ref _slowOpCount);
            Interlocked.Add(ref _slowOpTotalMs, (long)(elapsedMs * 1000));
            if (elapsedMs > 20) Interlocked.Increment(ref _slowOpOver20Ms);
            if (elapsedMs > 50) Interlocked.Increment(ref _slowOpOver50Ms);
            if (elapsedMs > 100) Interlocked.Increment(ref _slowOpOver100Ms);
            if (elapsedMs > 200) Interlocked.Increment(ref _slowOpOver200Ms);
            lock (_perKindLock)
            {
                _lastSlowOpName = name ?? "<unknown>";
                _lastSlowOpMs = elapsedMs;
            }
        }

        public static long SlowOpCount => Interlocked.Read(ref _slowOpCount);
        public static long SlowOpOver20Ms => Interlocked.Read(ref _slowOpOver20Ms);
        public static long SlowOpOver50Ms => Interlocked.Read(ref _slowOpOver50Ms);
        public static long SlowOpOver100Ms => Interlocked.Read(ref _slowOpOver100Ms);
        public static long SlowOpOver200Ms => Interlocked.Read(ref _slowOpOver200Ms);
        public static double SlowOpTotalMs => Interlocked.Read(ref _slowOpTotalMs) / 1000.0;
        public static string LastSlowOpName { get { lock (_perKindLock) return _lastSlowOpName; } }
        public static double LastSlowOpMs { get { lock (_perKindLock) return _lastSlowOpMs; } }

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
