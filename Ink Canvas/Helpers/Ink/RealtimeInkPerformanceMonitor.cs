using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Ink_Canvas.Helpers
{
    internal enum RealtimeInkInputKind
    {
        Stylus,
        TouchVelocity,
        TouchInterpolated,
        Mouse
    }

    public sealed class RealtimeInkSlowEventSnapshot
    {
        public string Timestamp { get; internal set; } = string.Empty;
        public string StartedAt { get; internal set; } = string.Empty;
        public string CompletedAt { get; internal set; } = string.Empty;
        public string EventType { get; internal set; } = string.Empty;
        public string InputKind { get; internal set; } = string.Empty;
        public double ElapsedMs { get; internal set; }
        public int PointCount { get; internal set; }
        public int ActivePointCount { get; internal set; }
        public int LastCommittedPointCount { get; internal set; }
        public bool Committed { get; internal set; }
        public bool ForceRedraw { get; internal set; }
        public double DispatcherProbeDelayMs { get; internal set; }
        public double RenderingIntervalMs { get; internal set; }
        public int Gen0CollectionCountStart { get; internal set; }
        public int Gen0CollectionCountEnd { get; internal set; }
        public int Gen1CollectionCountStart { get; internal set; }
        public int Gen1CollectionCountEnd { get; internal set; }
        public int Gen2CollectionCountStart { get; internal set; }
        public int Gen2CollectionCountEnd { get; internal set; }
        public long ManagedMemoryBytes { get; internal set; }
    }

    public sealed class RealtimeInkInputPerformanceSnapshot
    {
        public long StrokeCount { get; internal set; }
        public long InputEventCount { get; internal set; }
        public long RawInputPointCount { get; internal set; }
        public long AddedPointCount { get; internal set; }
        public long RedrawCount { get; internal set; }
        public long CommitCount { get; internal set; }
        public long ForceRedrawCount { get; internal set; }
        public double TotalInputProcessingMs { get; internal set; }
        public double MaxInputProcessingMs { get; internal set; }
        public double TotalRedrawMs { get; internal set; }
        public double MaxRedrawMs { get; internal set; }
        public long FrameWaitSampleCount { get; internal set; }
        public double TotalFrameWaitMs { get; internal set; }
        public double MaxFrameWaitMs { get; internal set; }
        public long SlowInputOver1MsCount { get; internal set; }
        public long SlowRedrawOver1MsCount { get; internal set; }
        public long SlowRedrawOver3MsCount { get; internal set; }
        public long SlowRedrawOver5MsCount { get; internal set; }
        public long NormalRedrawCount { get; internal set; }
        public double TotalNormalRedrawMs { get; internal set; }
        public double MaxNormalRedrawMs { get; internal set; }
        public double TotalForceRedrawMs { get; internal set; }
        public double MaxForceRedrawMs { get; internal set; }
        public double TotalCommitRedrawMs { get; internal set; }
        public double MaxCommitRedrawMs { get; internal set; }
        public long ActiveRedrawCount { get; internal set; }
        public double TotalActiveRedrawMs { get; internal set; }
        public double MaxActiveRedrawMs { get; internal set; }
    }

    internal sealed class RealtimeInkPerformanceSnapshot
    {
        public long StrokeCount { get; internal set; }
        public long InputEventCount { get; internal set; }
        public long RawInputPointCount { get; internal set; }
        public long AddedPointCount { get; internal set; }
        public long RedrawCount { get; internal set; }
        public long CommitCount { get; internal set; }
        public long ForceRedrawCount { get; internal set; }
        public double TotalInputProcessingMs { get; internal set; }
        public double MaxInputProcessingMs { get; internal set; }
        public double TotalRedrawMs { get; internal set; }
        public double MaxRedrawMs { get; internal set; }
        public long FrameWaitSampleCount { get; internal set; }
        public double TotalFrameWaitMs { get; internal set; }
        public double MaxFrameWaitMs { get; internal set; }
        public long SlowInputOver1MsCount { get; internal set; }
        public long SlowRedrawOver1MsCount { get; internal set; }
        public long SlowRedrawOver3MsCount { get; internal set; }
        public long SlowRedrawOver5MsCount { get; internal set; }
        public long NormalRedrawCount { get; internal set; }
        public double TotalNormalRedrawMs { get; internal set; }
        public double MaxNormalRedrawMs { get; internal set; }
        public double TotalForceRedrawMs { get; internal set; }
        public double MaxForceRedrawMs { get; internal set; }
        public double TotalCommitRedrawMs { get; internal set; }
        public double MaxCommitRedrawMs { get; internal set; }
        public long ActiveRedrawCount { get; internal set; }
        public double TotalActiveRedrawMs { get; internal set; }
        public double MaxActiveRedrawMs { get; internal set; }
        public Dictionary<string, RealtimeInkInputPerformanceSnapshot> ByInputKind { get; internal set; }
            = new Dictionary<string, RealtimeInkInputPerformanceSnapshot>();
        public List<RealtimeInkSlowEventSnapshot> SlowEvents { get; internal set; }
            = new List<RealtimeInkSlowEventSnapshot>();
    }

    internal static class RealtimeInkPerformanceMonitor
    {
        private sealed class StrokeStats
        {
            public RealtimeInkInputKind InputKind { get; set; }
            public long InputEventCount { get; set; }
            public long RawInputPointCount { get; set; }
            public long AddedPointCount { get; set; }
            public long RedrawCount { get; set; }
            public long CommitCount { get; set; }
            public long ForceRedrawCount { get; set; }
            public double TotalInputProcessingMs { get; set; }
            public double MaxInputProcessingMs { get; set; }
            public double TotalRedrawMs { get; set; }
            public double MaxRedrawMs { get; set; }
        }

        private sealed class AggregateStats
        {
            public long StrokeCount;
            public long InputEventCount;
            public long RawInputPointCount;
            public long AddedPointCount;
            public long RedrawCount;
            public long CommitCount;
            public long ForceRedrawCount;
            public double TotalInputProcessingMs;
            public double MaxInputProcessingMs;
            public double TotalRedrawMs;
            public double MaxRedrawMs;
            public long FrameWaitSampleCount;
            public double TotalFrameWaitMs;
            public double MaxFrameWaitMs;
            public long SlowInputOver1MsCount;
            public long SlowRedrawOver1MsCount;
            public long SlowRedrawOver3MsCount;
            public long SlowRedrawOver5MsCount;
            public long NormalRedrawCount;
            public double TotalNormalRedrawMs;
            public double MaxNormalRedrawMs;
            public double TotalForceRedrawMs;
            public double MaxForceRedrawMs;
            public double TotalCommitRedrawMs;
            public double MaxCommitRedrawMs;
            public long ActiveRedrawCount;
            public double TotalActiveRedrawMs;
            public double MaxActiveRedrawMs;
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<StrokeVisual, StrokeStats> ActiveStrokes =
            new Dictionary<StrokeVisual, StrokeStats>();
        private static readonly Dictionary<RealtimeInkInputKind, AggregateStats> ByInputKind =
            new Dictionary<RealtimeInkInputKind, AggregateStats>();
        private static readonly AggregateStats Aggregate = new AggregateStats();
        private static readonly Queue<RealtimeInkSlowEventSnapshot> SlowEvents =
            new Queue<RealtimeInkSlowEventSnapshot>();
        private const int MaxSlowEventCount = 64;
        private const double SlowEventThresholdMs = 5;

        // Hot-path gate for the detailed realtime-ink debug log. Independent of CPU monitoring.
        private static volatile bool _isDebugLoggingEnabled;
        private static DateTime _sessionStart = DateTime.MinValue;
        private static string _sessionStartKey = string.Empty;
        private static int _endStrokeSinceLastFlush;
        private const string HistoryFileName = "Configs/PerformanceHistory.json";
        private const string LiveStatusFileName = "Configs/RealtimeInkDebugLive.json";
        private const int MaxHistoryCount = 30;
        // 每完成 N 笔抬笔刷新一次落盘，保证开关开启期间就能看到 realtimeInk* 字段。
        private const int FlushEveryEndStrokeCount = 1;

        private static readonly JsonSerializerSettings CompactJsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// Whether super-detailed realtime ink debug logging is active.
        /// Controlled by Advanced.IsRealtimeInkDebugLogEnabled (Debug page), default off.
        /// </summary>
        public static bool IsDebugLoggingEnabled => _isDebugLoggingEnabled;

        /// <summary>
        /// Enable or disable detailed realtime-ink debug logging at runtime.
        /// Enabling resets the current session; disabling saves one history record then stops.
        /// </summary>
        public static void SetDebugLoggingEnabled(bool enabled)
        {
            if (enabled)
            {
                if (_isDebugLoggingEnabled)
                    return;
                Reset();
                _sessionStart = DateTime.Now;
                _sessionStartKey = _sessionStart.ToString("yyyy-MM-dd HH:mm:ss");
                _endStrokeSinceLastFlush = 0;
                _isDebugLoggingEnabled = true;
                try
                {
                    WriteLiveStatus(GetSnapshot(), isActive: true);
                    LogHelper.WriteLogToFile(
                        $"[RealtimeInkDebug] 已启用详细日志。实时状态: {GetLiveStatusPath()} ；关闭/退出时写入 {GetHistoryPath()}",
                        LogHelper.LogType.Info);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[RealtimeInkDebug] 启用时写状态失败: {ex.Message}", LogHelper.LogType.Warning);
                }
                return;
            }

            if (!_isDebugLoggingEnabled)
                return;

            try
            {
                StopAndSave();
            }
            finally
            {
                _isDebugLoggingEnabled = false;
                Reset();
                _sessionStart = DateTime.MinValue;
                _sessionStartKey = string.Empty;
                _endStrokeSinceLastFlush = 0;
                try
                {
                    WriteLiveStatus(new RealtimeInkPerformanceSnapshot(), isActive: false);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Call once at app startup after settings are loaded.
        /// </summary>
        public static void StartIfEnabled()
        {
            try
            {
                var enabled = MainWindow.Settings?.Advanced?.IsRealtimeInkDebugLogEnabled == true
                    || SettingsManager.Settings?.Advanced?.IsRealtimeInkDebugLogEnabled == true;
                if (enabled)
                    SetDebugLoggingEnabled(true);
                else
                {
                    _isDebugLoggingEnabled = false;
                    WriteLiveStatus(new RealtimeInkPerformanceSnapshot(), isActive: false);
                }
            }
            catch (Exception ex)
            {
                _isDebugLoggingEnabled = false;
                LogHelper.WriteLogToFile($"[RealtimeInkDebug] StartIfEnabled 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// Stop detailed logging and append/update one PerformanceHistory record (ink fields only).
        /// No-op when the debug log was never enabled and has no session.
        /// </summary>
        public static void StopAndSave()
        {
            if (!_isDebugLoggingEnabled && _sessionStart == DateTime.MinValue)
                return;

            try
            {
                var snapshot = GetSnapshot();
                if (snapshot.InputEventCount <= 0 && snapshot.StrokeCount <= 0 && snapshot.SlowEvents.Count <= 0)
                {
                    LogHelper.WriteLogToFile(
                        "[RealtimeInkDebug] 会话结束但无笔迹采样（可能未走实时笔迹路径或未落笔）",
                        LogHelper.LogType.Warning);
                    return;
                }

                UpsertSessionHistoryRecord(snapshot, finalize: true);
                LogHelper.WriteLogToFile(
                    $"[RealtimeInkDebug] 已保存: strokes={snapshot.StrokeCount}, events={snapshot.InputEventCount}, " +
                    $"raw={snapshot.RawInputPointCount}, added={snapshot.AddedPointCount}, redraws={snapshot.RedrawCount} -> {GetHistoryPath()}",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[RealtimeInkDebug] StopAndSave 失败: {ex.Message}", LogHelper.LogType.Error);
                Debug.WriteLine($"RealtimeInkPerformanceMonitor.StopAndSave: {ex.Message}");
            }
        }

        /// <summary>
        /// 抬笔后增量刷新：更新 Live 状态文件，并 upsert 到 PerformanceHistory。
        /// </summary>
        private static void FlushAfterEndStroke()
        {
            if (!_isDebugLoggingEnabled)
                return;

            _endStrokeSinceLastFlush++;
            if (_endStrokeSinceLastFlush < FlushEveryEndStrokeCount)
                return;
            _endStrokeSinceLastFlush = 0;

            try
            {
                var snapshot = GetSnapshot();
                WriteLiveStatus(snapshot, isActive: true);
                if (snapshot.StrokeCount > 0 || snapshot.InputEventCount > 0)
                    UpsertSessionHistoryRecord(snapshot, finalize: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RealtimeInkPerformanceMonitor.FlushAfterEndStroke: {ex.Message}");
            }
        }

        private static PerformanceRunRecord BuildRecord(RealtimeInkPerformanceSnapshot snapshot, bool finalize)
        {
            var started = _sessionStart == DateTime.MinValue ? DateTime.Now : _sessionStart;
            var startKey = string.IsNullOrEmpty(_sessionStartKey)
                ? started.ToString("yyyy-MM-dd HH:mm:ss")
                : _sessionStartKey;

            return new PerformanceRunRecord
            {
                StartTime = startKey,
                EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DurationSeconds = Math.Max(0, (DateTime.Now - started).TotalSeconds),
                RealtimeInkStrokeCount = snapshot.StrokeCount,
                RealtimeInkInputEventCount = snapshot.InputEventCount,
                RealtimeInkRawInputPointCount = snapshot.RawInputPointCount,
                RealtimeInkAddedPointCount = snapshot.AddedPointCount,
                RealtimeInkRedrawCount = snapshot.RedrawCount,
                RealtimeInkCommitCount = snapshot.CommitCount,
                RealtimeInkForceRedrawCount = snapshot.ForceRedrawCount,
                RealtimeInkTotalInputProcessingMs = Math.Round(snapshot.TotalInputProcessingMs, 3),
                RealtimeInkMaxInputProcessingMs = Math.Round(snapshot.MaxInputProcessingMs, 3),
                RealtimeInkTotalRedrawMs = Math.Round(snapshot.TotalRedrawMs, 3),
                RealtimeInkMaxRedrawMs = Math.Round(snapshot.MaxRedrawMs, 3),
                RealtimeInkFrameWaitSampleCount = snapshot.FrameWaitSampleCount,
                RealtimeInkTotalFrameWaitMs = Math.Round(snapshot.TotalFrameWaitMs, 3),
                RealtimeInkMaxFrameWaitMs = Math.Round(snapshot.MaxFrameWaitMs, 3),
                RealtimeInkSlowInputOver1MsCount = snapshot.SlowInputOver1MsCount,
                RealtimeInkSlowRedrawOver1MsCount = snapshot.SlowRedrawOver1MsCount,
                RealtimeInkSlowRedrawOver3MsCount = snapshot.SlowRedrawOver3MsCount,
                RealtimeInkSlowRedrawOver5MsCount = snapshot.SlowRedrawOver5MsCount,
                RealtimeInkNormalRedrawCount = snapshot.NormalRedrawCount,
                RealtimeInkTotalNormalRedrawMs = Math.Round(snapshot.TotalNormalRedrawMs, 3),
                RealtimeInkMaxNormalRedrawMs = Math.Round(snapshot.MaxNormalRedrawMs, 3),
                RealtimeInkTotalForceRedrawMs = Math.Round(snapshot.TotalForceRedrawMs, 3),
                RealtimeInkMaxForceRedrawMs = Math.Round(snapshot.MaxForceRedrawMs, 3),
                RealtimeInkTotalCommitRedrawMs = Math.Round(snapshot.TotalCommitRedrawMs, 3),
                RealtimeInkMaxCommitRedrawMs = Math.Round(snapshot.MaxCommitRedrawMs, 3),
                RealtimeInkActiveRedrawCount = snapshot.ActiveRedrawCount,
                RealtimeInkTotalActiveRedrawMs = Math.Round(snapshot.TotalActiveRedrawMs, 3),
                RealtimeInkMaxActiveRedrawMs = Math.Round(snapshot.MaxActiveRedrawMs, 3),
                RealtimeInkByInputKind = snapshot.ByInputKind != null && snapshot.ByInputKind.Count > 0
                    ? snapshot.ByInputKind
                    : null,
                // 仅会话结束时落盘慢事件列表，避免抬笔时频繁写大数组
                RealtimeInkSlowEvents = finalize && snapshot.SlowEvents != null && snapshot.SlowEvents.Count > 0
                    ? snapshot.SlowEvents
                    : null
            };
        }

        private static void UpsertSessionHistoryRecord(RealtimeInkPerformanceSnapshot snapshot, bool finalize)
        {
            var record = BuildRecord(snapshot, finalize);
            var path = GetHistoryPath();
            EnsureParentDirectory(path);

            var history = LoadHistoryList(path);
            var startKey = record.StartTime;
            var existingIndex = history.FindIndex(r =>
                r != null
                && r.StartTime == startKey
                && r.SampleCount == 0
                && r.AvgCpuPercent == 0
                && r.RealtimeInkStrokeCount >= 0);

            // 优先匹配本会话（无 CPU 采样、同 StartTime）的 ink-only 记录
            if (existingIndex < 0)
            {
                existingIndex = history.FindLastIndex(r =>
                    r != null
                    && r.StartTime == startKey
                    && r.SampleCount == 0
                    && Math.Abs(r.AvgCpuPercent) < 0.0001
                    && Math.Abs(r.AvgMemoryMb) < 0.0001);
            }

            if (existingIndex >= 0)
                history[existingIndex] = record;
            else
                history.Add(record);

            while (history.Count > MaxHistoryCount)
                history.RemoveAt(0);

            File.WriteAllText(path, JsonConvert.SerializeObject(history, CompactJsonSettings));
        }

        private static List<PerformanceRunRecord> LoadHistoryList(string path)
        {
            if (!File.Exists(path))
                return new List<PerformanceRunRecord>();
            try
            {
                var existing = JsonConvert.DeserializeObject<List<PerformanceRunRecord>>(File.ReadAllText(path));
                return existing ?? new List<PerformanceRunRecord>();
            }
            catch
            {
                return new List<PerformanceRunRecord>();
            }
        }

        private static void WriteLiveStatus(RealtimeInkPerformanceSnapshot snapshot, bool isActive)
        {
            var path = GetLiveStatusPath();
            EnsureParentDirectory(path);
            var payload = new
            {
                active = isActive,
                sessionStart = string.IsNullOrEmpty(_sessionStartKey) ? null : _sessionStartKey,
                updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                strokeCount = snapshot?.StrokeCount ?? 0,
                inputEventCount = snapshot?.InputEventCount ?? 0,
                rawInputPointCount = snapshot?.RawInputPointCount ?? 0,
                addedPointCount = snapshot?.AddedPointCount ?? 0,
                redrawCount = snapshot?.RedrawCount ?? 0,
                commitCount = snapshot?.CommitCount ?? 0,
                forceRedrawCount = snapshot?.ForceRedrawCount ?? 0,
                maxInputProcessingMs = Math.Round(snapshot?.MaxInputProcessingMs ?? 0, 3),
                maxRedrawMs = Math.Round(snapshot?.MaxRedrawMs ?? 0, 3),
                maxFrameWaitMs = Math.Round(snapshot?.MaxFrameWaitMs ?? 0, 3),
                frameWaitSampleCount = snapshot?.FrameWaitSampleCount ?? 0,
                slowRedrawOver5MsCount = snapshot?.SlowRedrawOver5MsCount ?? 0,
                byInputKind = snapshot?.ByInputKind,
                historyPath = GetHistoryPath()
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(payload, CompactJsonSettings));
        }

        private static string GetHistoryPath() => Path.Combine(App.RootPath ?? string.Empty, HistoryFileName);
        private static string GetLiveStatusPath() => Path.Combine(App.RootPath ?? string.Empty, LiveStatusFileName);

        private static void EnsureParentDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static void BeginStroke(StrokeVisual strokeVisual, RealtimeInkInputKind inputKind)
        {
            if (strokeVisual == null)
                return;

            RealtimeInkFrameScheduler.BeginStrokeSession();

            if (!_isDebugLoggingEnabled)
                return;

            lock (SyncRoot)
            {
                ActiveStrokes[strokeVisual] = new StrokeStats
                {
                    InputKind = inputKind
                };
                GetInputAggregate(inputKind);
            }
        }

        public static void RecordInputEvent(
            StrokeVisual strokeVisual,
            long rawInputPointCount,
            long addedPointCount,
            long elapsedTicks)
        {
            if (!RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled || strokeVisual == null)
                return;

            var safeRawPointCount = Math.Max(0, rawInputPointCount);
            var safeAddedPointCount = Math.Max(0, addedPointCount);
            var elapsedMs = ToMilliseconds(elapsedTicks);
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out var stats))
                    return;

                AddInputEvent(stats, safeRawPointCount, safeAddedPointCount, elapsedMs);
                AddInputEvent(Aggregate, safeRawPointCount, safeAddedPointCount, elapsedMs);
                AddInputEvent(GetInputAggregate(stats.InputKind), safeRawPointCount, safeAddedPointCount, elapsedMs);
            }
        }

        public static void RecordRedraw(
            StrokeVisual strokeVisual,
            long elapsedTicks,
            bool committed,
            bool forceRedraw,
            int gen0CollectionCountStart = -1,
            int gen1CollectionCountStart = -1,
            int gen2CollectionCountStart = -1)
        {
            if (!RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled || strokeVisual == null)
                return;

            var elapsedMs = ToMilliseconds(elapsedTicks);
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out var stats))
                    return;

                AddRedraw(stats, elapsedMs, committed, forceRedraw);
                AddRedraw(Aggregate, elapsedMs, committed, forceRedraw);
                AddRedraw(GetInputAggregate(stats.InputKind), elapsedMs, committed, forceRedraw);
                if (elapsedMs > SlowEventThresholdMs)
                    AddSlowEvent(CreateSlowEvent(
                        strokeVisual,
                        "Redraw",
                        stats.InputKind,
                        elapsedMs,
                        committed,
                        forceRedraw,
                        gen0CollectionCountStart,
                        gen1CollectionCountStart,
                        gen2CollectionCountStart));
            }
        }

        public static void RecordForceRedraw(StrokeVisual strokeVisual)
        {
            if (!RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled || strokeVisual == null)
                return;

            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out var stats))
                    return;

                stats.ForceRedrawCount++;
                Aggregate.ForceRedrawCount++;
                GetInputAggregate(stats.InputKind).ForceRedrawCount++;
            }
        }

        public static void RecordFrameWait(
            StrokeVisual strokeVisual,
            long elapsedTicks,
            int gen0CollectionCountStart = -1,
            int gen1CollectionCountStart = -1,
            int gen2CollectionCountStart = -1,
            double dispatcherProbeDelayMs = 0,
            double renderingIntervalMs = 0)
        {
            if (!RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled || strokeVisual == null)
                return;

            var elapsedMs = ToMilliseconds(elapsedTicks);
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out var stats))
                    return;

                AddFrameWait(Aggregate, elapsedMs);
                AddFrameWait(GetInputAggregate(stats.InputKind), elapsedMs);
                if (elapsedMs > SlowEventThresholdMs)
                    AddSlowEvent(CreateSlowEvent(
                        strokeVisual,
                        "FrameWait",
                        stats.InputKind,
                        elapsedMs,
                        false,
                        false,
                        gen0CollectionCountStart,
                        gen1CollectionCountStart,
                        gen2CollectionCountStart,
                        dispatcherProbeDelayMs,
                        renderingIntervalMs));
            }
        }

        public static void EndStroke(StrokeVisual strokeVisual)
        {
            if (strokeVisual == null)
            {
                RealtimeInkFrameScheduler.EndStrokeSession();
                return;
            }

            RealtimeInkFrameScheduler.EndStrokeSession();

            StrokeStats stats;
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out stats))
                    return;

                ActiveStrokes.Remove(strokeVisual);
                Aggregate.StrokeCount++;
                GetInputAggregate(stats.InputKind).StrokeCount++;
            }

            if (RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled)
            {
                Debug.WriteLine(
                    $"RealtimeInkPerf [{stats.InputKind}] "
                    + $"events={stats.InputEventCount}, rawPoints={stats.RawInputPointCount}, "
                    + $"addedPoints={stats.AddedPointCount}, redraws={stats.RedrawCount}, "
                    + $"commits={stats.CommitCount}, forceRedraws={stats.ForceRedrawCount}, "
                    + $"processMs={stats.TotalInputProcessingMs:F3}/max:{stats.MaxInputProcessingMs:F3}, "
                    + $"redrawMs={stats.TotalRedrawMs:F3}/max:{stats.MaxRedrawMs:F3}");
                FlushAfterEndStroke();
            }
        }

        public static RealtimeInkPerformanceSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                var snapshot = ToSnapshot(Aggregate);
                foreach (var pair in ByInputKind)
                    snapshot.ByInputKind[pair.Key.ToString()] = ToPublicSnapshot(pair.Value);
                snapshot.SlowEvents = new List<RealtimeInkSlowEventSnapshot>(SlowEvents);
                return snapshot;
            }
        }

        public static void Reset()
        {
            lock (SyncRoot)
            {
                ActiveStrokes.Clear();
                ByInputKind.Clear();
                SlowEvents.Clear();
                ResetAggregate(Aggregate);
            }
        }

        private static void AddInputEvent(
            AggregateStats stats,
            long rawInputPointCount,
            long addedPointCount,
            double elapsedMs)
        {
            stats.InputEventCount++;
            stats.RawInputPointCount += rawInputPointCount;
            stats.AddedPointCount += addedPointCount;
            stats.TotalInputProcessingMs += elapsedMs;
            stats.MaxInputProcessingMs = Math.Max(stats.MaxInputProcessingMs, elapsedMs);
            if (elapsedMs > 1)
                stats.SlowInputOver1MsCount++;
        }

        private static void AddInputEvent(
            StrokeStats stats,
            long rawInputPointCount,
            long addedPointCount,
            double elapsedMs)
        {
            stats.InputEventCount++;
            stats.RawInputPointCount += rawInputPointCount;
            stats.AddedPointCount += addedPointCount;
            stats.TotalInputProcessingMs += elapsedMs;
            stats.MaxInputProcessingMs = Math.Max(stats.MaxInputProcessingMs, elapsedMs);
        }

        private static void AddRedraw(AggregateStats stats, double elapsedMs, bool committed, bool forceRedraw)
        {
            stats.RedrawCount++;
            if (committed)
            {
                stats.CommitCount++;
                stats.TotalCommitRedrawMs += elapsedMs;
                stats.MaxCommitRedrawMs = Math.Max(stats.MaxCommitRedrawMs, elapsedMs);
            }
            else
            {
                stats.ActiveRedrawCount++;
                stats.TotalActiveRedrawMs += elapsedMs;
                stats.MaxActiveRedrawMs = Math.Max(stats.MaxActiveRedrawMs, elapsedMs);
            }
            stats.TotalRedrawMs += elapsedMs;
            stats.MaxRedrawMs = Math.Max(stats.MaxRedrawMs, elapsedMs);
            if (elapsedMs > 1)
                stats.SlowRedrawOver1MsCount++;
            if (elapsedMs > 3)
                stats.SlowRedrawOver3MsCount++;
            if (elapsedMs > 5)
                stats.SlowRedrawOver5MsCount++;

            if (forceRedraw)
            {
                stats.TotalForceRedrawMs += elapsedMs;
                stats.MaxForceRedrawMs = Math.Max(stats.MaxForceRedrawMs, elapsedMs);
            }
            else
            {
                stats.NormalRedrawCount++;
                stats.TotalNormalRedrawMs += elapsedMs;
                stats.MaxNormalRedrawMs = Math.Max(stats.MaxNormalRedrawMs, elapsedMs);
            }
        }

        private static void AddRedraw(StrokeStats stats, double elapsedMs, bool committed, bool forceRedraw)
        {
            stats.RedrawCount++;
            if (committed)
                stats.CommitCount++;
            stats.TotalRedrawMs += elapsedMs;
            stats.MaxRedrawMs = Math.Max(stats.MaxRedrawMs, elapsedMs);
        }

        private static void AddFrameWait(AggregateStats stats, double elapsedMs)
        {
            stats.FrameWaitSampleCount++;
            stats.TotalFrameWaitMs += elapsedMs;
            stats.MaxFrameWaitMs = Math.Max(stats.MaxFrameWaitMs, elapsedMs);
        }

        private static void AddSlowEvent(RealtimeInkSlowEventSnapshot slowEvent)
        {
            SlowEvents.Enqueue(slowEvent);
            while (SlowEvents.Count > MaxSlowEventCount)
                SlowEvents.Dequeue();
        }

        private static RealtimeInkSlowEventSnapshot CreateSlowEvent(
            StrokeVisual strokeVisual,
            string eventType,
            RealtimeInkInputKind inputKind,
            double elapsedMs,
            bool committed,
            bool forceRedraw,
            int gen0CollectionCountStart,
            int gen1CollectionCountStart,
            int gen2CollectionCountStart,
            double dispatcherProbeDelayMs = 0,
            double renderingIntervalMs = 0)
        {
            var completedAt = DateTime.Now;
            var startedAt = completedAt.AddMilliseconds(-elapsedMs);
            return new RealtimeInkSlowEventSnapshot
            {
                Timestamp = completedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                StartedAt = startedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                CompletedAt = completedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EventType = eventType,
                InputKind = inputKind.ToString(),
                ElapsedMs = elapsedMs,
                PointCount = strokeVisual.PointCount,
                ActivePointCount = strokeVisual.ActivePointCount,
                LastCommittedPointCount = strokeVisual.LastCommittedPointCount,
                Committed = committed,
                ForceRedraw = forceRedraw,
                DispatcherProbeDelayMs = dispatcherProbeDelayMs,
                RenderingIntervalMs = renderingIntervalMs,
                Gen0CollectionCountStart = gen0CollectionCountStart,
                Gen0CollectionCountEnd = GC.CollectionCount(0),
                Gen1CollectionCountStart = gen1CollectionCountStart,
                Gen1CollectionCountEnd = GC.CollectionCount(1),
                Gen2CollectionCountStart = gen2CollectionCountStart,
                Gen2CollectionCountEnd = GC.CollectionCount(2),
                ManagedMemoryBytes = GC.GetTotalMemory(false)
            };
        }

        private static AggregateStats GetInputAggregate(RealtimeInkInputKind inputKind)
        {
            if (!ByInputKind.TryGetValue(inputKind, out var stats))
            {
                stats = new AggregateStats();
                ByInputKind[inputKind] = stats;
            }
            return stats;
        }

        private static RealtimeInkPerformanceSnapshot ToSnapshot(AggregateStats stats)
        {
            return new RealtimeInkPerformanceSnapshot
            {
                StrokeCount = stats.StrokeCount,
                InputEventCount = stats.InputEventCount,
                RawInputPointCount = stats.RawInputPointCount,
                AddedPointCount = stats.AddedPointCount,
                RedrawCount = stats.RedrawCount,
                CommitCount = stats.CommitCount,
                ForceRedrawCount = stats.ForceRedrawCount,
                TotalInputProcessingMs = stats.TotalInputProcessingMs,
                MaxInputProcessingMs = stats.MaxInputProcessingMs,
                TotalRedrawMs = stats.TotalRedrawMs,
                MaxRedrawMs = stats.MaxRedrawMs,
                FrameWaitSampleCount = stats.FrameWaitSampleCount,
                TotalFrameWaitMs = stats.TotalFrameWaitMs,
                MaxFrameWaitMs = stats.MaxFrameWaitMs,
                SlowInputOver1MsCount = stats.SlowInputOver1MsCount,
                SlowRedrawOver1MsCount = stats.SlowRedrawOver1MsCount,
                SlowRedrawOver3MsCount = stats.SlowRedrawOver3MsCount,
                SlowRedrawOver5MsCount = stats.SlowRedrawOver5MsCount,
                NormalRedrawCount = stats.NormalRedrawCount,
                TotalNormalRedrawMs = stats.TotalNormalRedrawMs,
                MaxNormalRedrawMs = stats.MaxNormalRedrawMs,
                TotalForceRedrawMs = stats.TotalForceRedrawMs,
                MaxForceRedrawMs = stats.MaxForceRedrawMs,
                TotalCommitRedrawMs = stats.TotalCommitRedrawMs,
                MaxCommitRedrawMs = stats.MaxCommitRedrawMs,
                ActiveRedrawCount = stats.ActiveRedrawCount,
                TotalActiveRedrawMs = stats.TotalActiveRedrawMs,
                MaxActiveRedrawMs = stats.MaxActiveRedrawMs
            };
        }

        private static RealtimeInkInputPerformanceSnapshot ToPublicSnapshot(AggregateStats stats)
        {
            var snapshot = ToSnapshot(stats);
            return new RealtimeInkInputPerformanceSnapshot
            {
                StrokeCount = snapshot.StrokeCount,
                InputEventCount = snapshot.InputEventCount,
                RawInputPointCount = snapshot.RawInputPointCount,
                AddedPointCount = snapshot.AddedPointCount,
                RedrawCount = snapshot.RedrawCount,
                CommitCount = snapshot.CommitCount,
                ForceRedrawCount = snapshot.ForceRedrawCount,
                TotalInputProcessingMs = snapshot.TotalInputProcessingMs,
                MaxInputProcessingMs = snapshot.MaxInputProcessingMs,
                TotalRedrawMs = snapshot.TotalRedrawMs,
                MaxRedrawMs = snapshot.MaxRedrawMs,
                FrameWaitSampleCount = snapshot.FrameWaitSampleCount,
                TotalFrameWaitMs = snapshot.TotalFrameWaitMs,
                MaxFrameWaitMs = snapshot.MaxFrameWaitMs,
                SlowInputOver1MsCount = snapshot.SlowInputOver1MsCount,
                SlowRedrawOver1MsCount = snapshot.SlowRedrawOver1MsCount,
                SlowRedrawOver3MsCount = snapshot.SlowRedrawOver3MsCount,
                SlowRedrawOver5MsCount = snapshot.SlowRedrawOver5MsCount,
                NormalRedrawCount = snapshot.NormalRedrawCount,
                TotalNormalRedrawMs = snapshot.TotalNormalRedrawMs,
                MaxNormalRedrawMs = snapshot.MaxNormalRedrawMs,
                TotalForceRedrawMs = snapshot.TotalForceRedrawMs,
                MaxForceRedrawMs = snapshot.MaxForceRedrawMs,
                TotalCommitRedrawMs = snapshot.TotalCommitRedrawMs,
                MaxCommitRedrawMs = snapshot.MaxCommitRedrawMs,
                ActiveRedrawCount = snapshot.ActiveRedrawCount,
                TotalActiveRedrawMs = snapshot.TotalActiveRedrawMs,
                MaxActiveRedrawMs = snapshot.MaxActiveRedrawMs
            };
        }

        private static void ResetAggregate(AggregateStats stats)
        {
            stats.StrokeCount = 0;
            stats.InputEventCount = 0;
            stats.RawInputPointCount = 0;
            stats.AddedPointCount = 0;
            stats.RedrawCount = 0;
            stats.CommitCount = 0;
            stats.ForceRedrawCount = 0;
            stats.TotalInputProcessingMs = 0;
            stats.MaxInputProcessingMs = 0;
            stats.TotalRedrawMs = 0;
            stats.MaxRedrawMs = 0;
            stats.FrameWaitSampleCount = 0;
            stats.TotalFrameWaitMs = 0;
            stats.MaxFrameWaitMs = 0;
            stats.SlowInputOver1MsCount = 0;
            stats.SlowRedrawOver1MsCount = 0;
            stats.SlowRedrawOver3MsCount = 0;
            stats.SlowRedrawOver5MsCount = 0;
            stats.NormalRedrawCount = 0;
            stats.TotalNormalRedrawMs = 0;
            stats.MaxNormalRedrawMs = 0;
            stats.TotalForceRedrawMs = 0;
            stats.MaxForceRedrawMs = 0;
            stats.TotalCommitRedrawMs = 0;
            stats.MaxCommitRedrawMs = 0;
            stats.ActiveRedrawCount = 0;
            stats.TotalActiveRedrawMs = 0;
            stats.MaxActiveRedrawMs = 0;
        }

        private static double ToMilliseconds(long elapsedTicks)
        {
            return elapsedTicks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
