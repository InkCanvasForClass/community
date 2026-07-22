using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    internal static class RealtimeInkFrameScheduler
    {
        private enum RedrawRequestKind
        {
            Normal,
            Force
        }

        private sealed class PendingRedraw
        {
            public RedrawRequestKind Kind;
            public long RequestedAt;
            public int Gen0CollectionCountStart;
            public int Gen1CollectionCountStart;
            public int Gen2CollectionCountStart;
        }

        private static readonly Dictionary<StrokeVisual, PendingRedraw> PendingRedraws =
            new Dictionary<StrokeVisual, PendingRedraw>();
        private static readonly List<KeyValuePair<StrokeVisual, PendingRedraw>> PendingSnapshot =
            new List<KeyValuePair<StrokeVisual, PendingRedraw>>(8);
        private static bool IsRenderingSubscribed;
        private static long LastRenderingAt;
        private static int ActiveStrokeSessions;
        private static bool KeepRenderingSubscribed;

        public static void BeginStrokeSession()
        {
            ActiveStrokeSessions++;
            KeepRenderingSubscribed = true;
            EnsureRenderingSubscribed();
        }

        public static void EndStrokeSession()
        {
            if (ActiveStrokeSessions > 0)
                ActiveStrokeSessions--;
            if (ActiveStrokeSessions == 0)
            {
                KeepRenderingSubscribed = false;
                UnsubscribeIfIdle();
            }
        }

        public static void RequestRedraw(StrokeVisual strokeVisual)
        {
            Request(strokeVisual, RedrawRequestKind.Normal);
        }

        public static void RequestForceRedraw(StrokeVisual strokeVisual)
        {
            Request(strokeVisual, RedrawRequestKind.Force);
        }

        public static void Flush(StrokeVisual strokeVisual, bool forceRedraw = false)
        {
            if (strokeVisual == null)
                return;

            var requestKind = forceRedraw ? RedrawRequestKind.Force : RedrawRequestKind.Normal;
            if (PendingRedraws.TryGetValue(strokeVisual, out var pending))
            {
                if (pending.Kind == RedrawRequestKind.Force)
                    requestKind = RedrawRequestKind.Force;
                if (pending.RequestedAt != 0L)
                    RealtimeInkPerformanceMonitor.RecordFrameWait(
                        strokeVisual,
                        Stopwatch.GetTimestamp() - pending.RequestedAt,
                        pending.Gen0CollectionCountStart,
                        pending.Gen1CollectionCountStart,
                        pending.Gen2CollectionCountStart);
                PendingRedraws.Remove(strokeVisual);
            }

            UnsubscribeIfIdle();
            ExecuteRedraw(strokeVisual, requestKind);
        }

        public static void Cancel(StrokeVisual strokeVisual)
        {
            if (strokeVisual == null)
                return;

            PendingRedraws.Remove(strokeVisual);
            UnsubscribeIfIdle();
        }

        public static void Clear()
        {
            PendingRedraws.Clear();
            PendingSnapshot.Clear();
            LastRenderingAt = 0L;
            ActiveStrokeSessions = 0;
            KeepRenderingSubscribed = false;
            if (!IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            IsRenderingSubscribed = false;
        }

        private static void Request(StrokeVisual strokeVisual, RedrawRequestKind requestKind)
        {
            if (strokeVisual == null)
                return;

            if (PendingRedraws.TryGetValue(strokeVisual, out var pending))
            {
                if (requestKind == RedrawRequestKind.Force)
                    pending.Kind = RedrawRequestKind.Force;
                EnsureRenderingSubscribed();
                return;
            }

            var isMonitoring = PerformanceMonitorHelper.IsMonitoring;
            PendingRedraws[strokeVisual] = new PendingRedraw
            {
                Kind = requestKind,
                RequestedAt = isMonitoring ? Stopwatch.GetTimestamp() : 0L,
                Gen0CollectionCountStart = isMonitoring ? GC.CollectionCount(0) : -1,
                Gen1CollectionCountStart = isMonitoring ? GC.CollectionCount(1) : -1,
                Gen2CollectionCountStart = isMonitoring ? GC.CollectionCount(2) : -1
            };
            // Keep the rendering subscription sticky while strokes are active.
            // Avoid per-request InvalidateVisual/BeginInvoke work that stalls the UI queue.
            EnsureRenderingSubscribed();
        }

        private static void EnsureRenderingSubscribed()
        {
            if (IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering += OnRendering;
            IsRenderingSubscribed = true;
        }

        private static void UnsubscribeIfIdle()
        {
            if (KeepRenderingSubscribed || PendingRedraws.Count != 0 || !IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            IsRenderingSubscribed = false;
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            if (PendingRedraws.Count == 0)
            {
                if (!KeepRenderingSubscribed)
                {
                    CompositionTarget.Rendering -= OnRendering;
                    IsRenderingSubscribed = false;
                }
                return;
            }

            var renderedAt = Stopwatch.GetTimestamp();
            var renderingIntervalMs = LastRenderingAt != 0L
                ? ToMilliseconds(renderedAt - LastRenderingAt)
                : 0;
            if (renderingIntervalMs > 100)
                renderingIntervalMs = 0;
            LastRenderingAt = renderedAt;

            PendingSnapshot.Clear();
            foreach (var pair in PendingRedraws)
                PendingSnapshot.Add(pair);
            PendingRedraws.Clear();

            foreach (var pair in PendingSnapshot)
            {
                if (pair.Value.RequestedAt != 0L)
                    RealtimeInkPerformanceMonitor.RecordFrameWait(
                        pair.Key,
                        renderedAt - pair.Value.RequestedAt,
                        pair.Value.Gen0CollectionCountStart,
                        pair.Value.Gen1CollectionCountStart,
                        pair.Value.Gen2CollectionCountStart,
                        0,
                        renderingIntervalMs);
                ExecuteRedraw(pair.Key, pair.Value.Kind);
            }

            PendingSnapshot.Clear();
            if (PendingRedraws.Count > 0 || KeepRenderingSubscribed)
                EnsureRenderingSubscribed();
            else if (IsRenderingSubscribed)
            {
                CompositionTarget.Rendering -= OnRendering;
                IsRenderingSubscribed = false;
            }
        }

        private static double ToMilliseconds(long elapsedTicks)
        {
            return elapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        private static void ExecuteRedraw(StrokeVisual strokeVisual, RedrawRequestKind requestKind)
        {
            try
            {
                if (requestKind == RedrawRequestKind.Force)
                    strokeVisual.ForceRedraw();
                else
                    strokeVisual.Redraw();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
