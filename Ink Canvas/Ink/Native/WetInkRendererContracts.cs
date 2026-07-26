using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal readonly struct WetInkPixelRect
    {
        public WetInkPixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    internal sealed class WetInkTargetSnapshot
    {
        public WetInkTargetSnapshot(
            WetInkPixelRect screenBounds,
            float dpiX,
            float dpiY,
            bool isVisible,
            IReadOnlyList<WetInkPixelRect> exclusionRects)
        {
            if (dpiX <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpiX));
            if (dpiY <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpiY));

            ScreenBounds = screenBounds;
            DpiX = dpiX;
            DpiY = dpiY;
            IsVisible = isVisible;
            ExclusionRects = Copy(exclusionRects);
        }

        public WetInkPixelRect ScreenBounds { get; }
        public float DpiX { get; }
        public float DpiY { get; }
        public bool IsVisible { get; }
        public IReadOnlyList<WetInkPixelRect> ExclusionRects { get; }

        private static WetInkPixelRect[] Copy(IReadOnlyList<WetInkPixelRect> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<WetInkPixelRect>();
            var copy = new WetInkPixelRect[source.Count];
            for (var i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    internal readonly struct WetInkRetirementAck
    {
        public WetInkRetirementAck(long sessionId, long version)
        {
            SessionId = sessionId;
            Version = version;
        }

        public long SessionId { get; }
        public long Version { get; }
    }

    internal enum WetInkApplyStatus
    {
        Success = 0,
        NoWork = 1,
        DeviceLost = 2,
        Failed = 3
    }

    internal sealed class WetInkApplyResult
    {
        public WetInkApplyResult(
            WetInkApplyStatus status,
            IReadOnlyList<WetInkRetirementAck> retirementAcks,
            Exception error = null)
        {
            Status = status;
            RetirementAcks = retirementAcks ?? Array.Empty<WetInkRetirementAck>();
            Error = error;
        }

        public WetInkApplyStatus Status { get; }
        public IReadOnlyList<WetInkRetirementAck> RetirementAcks { get; }
        public Exception Error { get; }

        public static WetInkApplyResult Success(
            IReadOnlyList<WetInkRetirementAck> retirementAcks) =>
            new WetInkApplyResult(WetInkApplyStatus.Success, retirementAcks);

        public static WetInkApplyResult NoWork() =>
            new WetInkApplyResult(WetInkApplyStatus.NoWork, Array.Empty<WetInkRetirementAck>());

        public static WetInkApplyResult DeviceLost(Exception error) =>
            new WetInkApplyResult(
                WetInkApplyStatus.DeviceLost,
                Array.Empty<WetInkRetirementAck>(),
                error);

        public static WetInkApplyResult Failed(Exception error) =>
            new WetInkApplyResult(
                WetInkApplyStatus.Failed,
                Array.Empty<WetInkRetirementAck>(),
                error);
    }

    internal interface IWetInkBatchRenderer : IDisposable
    {
        void BindTarget(IntPtr hwnd, WetInkTargetSnapshot target);

        void UpdateTarget(WetInkTargetSnapshot target);

        WetInkApplyResult Apply(WetInkMailboxBatch batch);

        void PresentIdleClear();
    }
}
