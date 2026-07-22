using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    internal enum RealtimeInkInputKind
    {
        Stylus,
        TouchVelocity,
        TouchInterpolated,
        Mouse
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

        private sealed class PendingFrame
        {
            public long StartedAt;
            public RealtimeInkInputKind InputKind;
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<StrokeVisual, StrokeStats> ActiveStrokes =
            new Dictionary<StrokeVisual, StrokeStats>();
        private static readonly Dictionary<StrokeVisual, PendingFrame> PendingFrames =
            new Dictionary<StrokeVisual, PendingFrame>();
        private static readonly Dictionary<RealtimeInkInputKind, AggregateStats> ByInputKind =
            new Dictionary<RealtimeInkInputKind, AggregateStats>();
        private static readonly AggregateStats Aggregate = new AggregateStats();
        private static bool IsFrameTrackingEnabled;
        private static bool IsRenderingSubscribed;

        public static void BeginStroke(StrokeVisual strokeVisual, RealtimeInkInputKind inputKind)
        {
            if (!PerformanceMonitorHelper.IsMonitoring || strokeVisual == null)
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
            if (!PerformanceMonitorHelper.IsMonitoring || strokeVisual == null)
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
            bool forceRedraw)
        {
            if (!PerformanceMonitorHelper.IsMonitoring || strokeVisual == null)
                return;

            var elapsedMs = ToMilliseconds(elapsedTicks);
            var completedAt = Stopwatch.GetTimestamp();
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out var stats))
                    return;

                AddRedraw(stats, elapsedMs, committed, forceRedraw);
                AddRedraw(Aggregate, elapsedMs, committed, forceRedraw);
                AddRedraw(GetInputAggregate(stats.InputKind), elapsedMs, committed, forceRedraw);
                PendingFrames[strokeVisual] = new PendingFrame
                {
                    StartedAt = completedAt,
                    InputKind = stats.InputKind
                };
                EnsureRenderingSubscribed();
            }
        }

        public static void RecordForceRedraw(StrokeVisual strokeVisual)
        {
            if (!PerformanceMonitorHelper.IsMonitoring || strokeVisual == null)
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

        public static void EndStroke(StrokeVisual strokeVisual)
        {
            if (strokeVisual == null)
                return;

            StrokeStats stats;
            lock (SyncRoot)
            {
                if (!ActiveStrokes.TryGetValue(strokeVisual, out stats))
                    return;

                ActiveStrokes.Remove(strokeVisual);
                Aggregate.StrokeCount++;
                GetInputAggregate(stats.InputKind).StrokeCount++;
            }

            if (PerformanceMonitorHelper.IsMonitoring)
            {
                Debug.WriteLine(
                    $"RealtimeInkPerf [{stats.InputKind}] "
                    + $"events={stats.InputEventCount}, rawPoints={stats.RawInputPointCount}, "
                    + $"addedPoints={stats.AddedPointCount}, redraws={stats.RedrawCount}, "
                    + $"commits={stats.CommitCount}, forceRedraws={stats.ForceRedrawCount}, "
                    + $"processMs={stats.TotalInputProcessingMs:F3}/max:{stats.MaxInputProcessingMs:F3}, "
                    + $"redrawMs={stats.TotalRedrawMs:F3}/max:{stats.MaxRedrawMs:F3}");
            }
        }

        public static RealtimeInkPerformanceSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                var snapshot = ToSnapshot(Aggregate);
                foreach (var pair in ByInputKind)
                    snapshot.ByInputKind[pair.Key.ToString()] = ToPublicSnapshot(pair.Value);
                return snapshot;
            }
        }

        public static void Reset()
        {
            StopFrameTracking();
            lock (SyncRoot)
            {
                ActiveStrokes.Clear();
                PendingFrames.Clear();
                ByInputKind.Clear();
                ResetAggregate(Aggregate);
            }
        }

        public static void StartFrameTracking()
        {
            lock (SyncRoot)
                IsFrameTrackingEnabled = PerformanceMonitorHelper.IsMonitoring;
        }

        public static void StopFrameTracking()
        {
            lock (SyncRoot)
            {
                IsFrameTrackingEnabled = false;
                PendingFrames.Clear();
                if (!IsRenderingSubscribed)
                    return;

                CompositionTarget.Rendering -= OnRendering;
                IsRenderingSubscribed = false;
            }
        }

        private static void EnsureRenderingSubscribed()
        {
            if (!IsFrameTrackingEnabled || IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering += OnRendering;
            IsRenderingSubscribed = true;
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            var renderedAt = Stopwatch.GetTimestamp();
            lock (SyncRoot)
            {
                if (IsRenderingSubscribed)
                {
                    CompositionTarget.Rendering -= OnRendering;
                    IsRenderingSubscribed = false;
                }

                if (!IsFrameTrackingEnabled || !PerformanceMonitorHelper.IsMonitoring)
                {
                    PendingFrames.Clear();
                    return;
                }

                foreach (var pending in PendingFrames.Values)
                {
                    var elapsedMs = ToMilliseconds(renderedAt - pending.StartedAt);
                    AddFrameWait(Aggregate, elapsedMs);
                    AddFrameWait(GetInputAggregate(pending.InputKind), elapsedMs);
                }
                PendingFrames.Clear();
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
