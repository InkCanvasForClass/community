using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        }

        private static readonly Dictionary<StrokeVisual, PendingRedraw> PendingRedraws =
            new Dictionary<StrokeVisual, PendingRedraw>();
        private static bool IsRenderingSubscribed;

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
                        Stopwatch.GetTimestamp() - pending.RequestedAt);
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
                return;
            }

            PendingRedraws[strokeVisual] = new PendingRedraw
            {
                Kind = requestKind,
                RequestedAt = PerformanceMonitorHelper.IsMonitoring ? Stopwatch.GetTimestamp() : 0L
            };
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
            if (PendingRedraws.Count != 0 || !IsRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            IsRenderingSubscribed = false;
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            if (IsRenderingSubscribed)
            {
                CompositionTarget.Rendering -= OnRendering;
                IsRenderingSubscribed = false;
            }

            var renderedAt = Stopwatch.GetTimestamp();
            var pending = PendingRedraws.ToArray();
            PendingRedraws.Clear();

            foreach (var pair in pending)
            {
                if (pair.Value.RequestedAt != 0L)
                    RealtimeInkPerformanceMonitor.RecordFrameWait(
                        pair.Key,
                        renderedAt - pair.Value.RequestedAt);
                ExecuteRedraw(pair.Key, pair.Value.Kind);
            }

            if (PendingRedraws.Count > 0)
                EnsureRenderingSubscribed();
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
