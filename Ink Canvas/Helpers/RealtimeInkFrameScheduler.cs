using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

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

        private static readonly Action GlobalDispatcherProbeAction = CompleteGlobalDispatcherProbe;
        private static DispatcherOperation GlobalDispatcherProbeOperation;
        private static long GlobalProbeStartedAt;
        private static double GlobalProbeDelayMs;

        private static bool IsRenderingSubscribed;
        private static long LastRenderingAt;
        private static long LastKickAt;
        private static int ActiveStrokeSessions;
        private static int IdleEmptyFrameCount;
        private static bool FrameKickPending;

        // Input-side coalesce window: multiple TouchMove requests within this window
        // share one render pass without forcing another kick.
        private const double CoalesceWindowMs = 10.0;
        // If Rendering has not run for this long while dirty, force another kick.
        private const double StalledRenderKickMs = 24.0;
        // Keep a short warm subscription after strokes go idle, then unbind.
        private const int IdleEmptyFrameUnbindThreshold = 2;

        public static void BeginStrokeSession()
        {
            var wasIdle = ActiveStrokeSessions == 0;
            ActiveStrokeSessions++;
            EnsureRenderingSubscribed();
            if (wasIdle)
            {
                // Reset stall clocks so the first dirty request of a new stroke session
                // always takes the immediate-kick path.
                LastKickAt = 0;
                IdleEmptyFrameCount = 0;
                FrameKickPending = false;
            }
        }

        public static void EndStrokeSession()
        {
            if (ActiveStrokeSessions > 0)
                ActiveStrokeSessions--;
            if (ActiveStrokeSessions == 0)
                UnsubscribeIfIdle();
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
                {
                    RealtimeInkPerformanceMonitor.RecordFrameWait(
                        strokeVisual,
                        Stopwatch.GetTimestamp() - pending.RequestedAt,
                        pending.Gen0CollectionCountStart,
                        pending.Gen1CollectionCountStart,
                        pending.Gen2CollectionCountStart,
                        GlobalProbeDelayMs,
                        0);
                }
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
            LastKickAt = 0L;
            ActiveStrokeSessions = 0;
            IdleEmptyFrameCount = 0;
            FrameKickPending = false;
            GlobalProbeDelayMs = 0;
            GlobalProbeStartedAt = 0;
            if (GlobalDispatcherProbeOperation?.Status == DispatcherOperationStatus.Pending)
                GlobalDispatcherProbeOperation.Abort();
            GlobalDispatcherProbeOperation = null;
            if (!IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            IsRenderingSubscribed = false;
        }

        private static void Request(StrokeVisual strokeVisual, RedrawRequestKind requestKind)
        {
            if (strokeVisual == null)
                return;

            var now = Stopwatch.GetTimestamp();
            var isMonitoring = RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled;

            if (PendingRedraws.TryGetValue(strokeVisual, out var pending))
            {
                if (requestKind == RedrawRequestKind.Force)
                    pending.Kind = RedrawRequestKind.Force;

                // Already dirty: re-kick only if the render loop looks stalled.
                if (ShouldForceKick(now))
                    KickRender(strokeVisual, now);
                else
                    EnsureRenderingSubscribed();
                return;
            }

            var wasEmpty = PendingRedraws.Count == 0;
            PendingRedraws[strokeVisual] = new PendingRedraw
            {
                Kind = requestKind,
                RequestedAt = isMonitoring ? now : 0L,
                Gen0CollectionCountStart = isMonitoring ? GC.CollectionCount(0) : -1,
                Gen1CollectionCountStart = isMonitoring ? GC.CollectionCount(1) : -1,
                Gen2CollectionCountStart = isMonitoring ? GC.CollectionCount(2) : -1
            };

            if (wasEmpty || ShouldForceKick(now))
            {
                // First dirty of a batch, or coalesce window expired / render stalled.
                KickRender(strokeVisual, now);
            }
            else
            {
                // Inside coalesce window: mark dirty only. The already-kicked frame
                // (or sticky Rendering subscription) will pick it up.
                EnsureRenderingSubscribed();
            }
        }

        private static bool ShouldForceKick(long nowTicks)
        {
            if (!IsRenderingSubscribed || !FrameKickPending)
                return true;

            // No render observed yet after a kick — wait unless stalled.
            if (LastKickAt != 0L && (LastRenderingAt == 0L || LastRenderingAt < LastKickAt))
            {
                return ToMilliseconds(nowTicks - LastKickAt) >= StalledRenderKickMs;
            }

            // Render has run since last kick: only force a new kick after coalesce window.
            if (LastKickAt != 0L)
                return ToMilliseconds(nowTicks - LastKickAt) >= CoalesceWindowMs;

            return true;
        }

        private static void KickRender(StrokeVisual strokeVisual, long nowTicks)
        {
            EnsureRenderingSubscribed();
            FrameKickPending = true;
            IdleEmptyFrameCount = 0;
            LastKickAt = nowTicks;
            strokeVisual.InvalidateVisual();
            if (RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled)
                BeginGlobalDispatcherProbe(strokeVisual.Dispatcher, nowTicks);
        }

        private static void BeginGlobalDispatcherProbe(Dispatcher dispatcher, long startedAt)
        {
            if (dispatcher == null)
                return;

            if (GlobalDispatcherProbeOperation?.Status == DispatcherOperationStatus.Pending)
                GlobalDispatcherProbeOperation.Abort();

            GlobalProbeStartedAt = startedAt;
            GlobalProbeDelayMs = 0;
            GlobalDispatcherProbeOperation = dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                GlobalDispatcherProbeAction);
        }

        private static void CompleteGlobalDispatcherProbe()
        {
            var startedAt = GlobalProbeStartedAt;
            if (startedAt != 0L)
                GlobalProbeDelayMs = ToMilliseconds(Stopwatch.GetTimestamp() - startedAt);
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
            if (ActiveStrokeSessions != 0 || PendingRedraws.Count != 0 || !IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            IsRenderingSubscribed = false;
            FrameKickPending = false;
            IdleEmptyFrameCount = 0;
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            FrameKickPending = false;
            var renderedAt = Stopwatch.GetTimestamp();

            if (PendingRedraws.Count == 0)
            {
                IdleEmptyFrameCount++;
                if (ActiveStrokeSessions == 0 || IdleEmptyFrameCount >= IdleEmptyFrameUnbindThreshold)
                {
                    if (IsRenderingSubscribed)
                    {
                        CompositionTarget.Rendering -= OnRendering;
                        IsRenderingSubscribed = false;
                    }
                    IdleEmptyFrameCount = 0;
                }
                return;
            }

            IdleEmptyFrameCount = 0;
            var renderingIntervalMs = LastRenderingAt != 0L
                ? ToMilliseconds(renderedAt - LastRenderingAt)
                : 0;
            if (renderingIntervalMs > 100)
                renderingIntervalMs = 0;
            LastRenderingAt = renderedAt;

            var probeDelayMs = GlobalProbeDelayMs;
            GlobalProbeDelayMs = 0;
            GlobalProbeStartedAt = 0;

            PendingSnapshot.Clear();
            KeyValuePair<StrokeVisual, PendingRedraw>? earliest = null;
            foreach (var pair in PendingRedraws)
            {
                PendingSnapshot.Add(pair);
                if (pair.Value.RequestedAt != 0L
                    && (earliest == null || pair.Value.RequestedAt < earliest.Value.Value.RequestedAt))
                {
                    earliest = pair;
                }
            }
            PendingRedraws.Clear();

            // One FrameWait sample per render pass (earliest dirty).
            if (earliest.HasValue)
            {
                var earliestPair = earliest.Value;
                RealtimeInkPerformanceMonitor.RecordFrameWait(
                    earliestPair.Key,
                    renderedAt - earliestPair.Value.RequestedAt,
                    earliestPair.Value.Gen0CollectionCountStart,
                    earliestPair.Value.Gen1CollectionCountStart,
                    earliestPair.Value.Gen2CollectionCountStart,
                    probeDelayMs,
                    renderingIntervalMs);
            }

            foreach (var pair in PendingSnapshot)
                ExecuteRedraw(pair.Key, pair.Value.Kind);

            PendingSnapshot.Clear();

            if (PendingRedraws.Count > 0)
            {
                // Dirty arrived during redraw — schedule next frame once.
                EnsureRenderingSubscribed();
                FrameKickPending = true;
                LastKickAt = renderedAt;
                foreach (var pair in PendingRedraws)
                {
                    pair.Key.InvalidateVisual();
                    break;
                }
            }
            else if (ActiveStrokeSessions == 0)
            {
                UnsubscribeIfIdle();
            }
            else
            {
                // Keep a short warm window while strokes are active.
                EnsureRenderingSubscribed();
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
