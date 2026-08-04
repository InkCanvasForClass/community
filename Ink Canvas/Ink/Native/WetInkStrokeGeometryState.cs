using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class WetInkGeometryUpdate
    {
        public WetInkGeometryUpdate(
            IReadOnlyList<WetInkRibbonGeometry> fixedSegments,
            WetInkRibbonGeometry dynamicTail,
            int fixedRealPointCount,
            bool reset = false)
        {
            FixedSegments = fixedSegments
                ?? throw new ArgumentNullException(nameof(fixedSegments));
            DynamicTail = dynamicTail
                ?? throw new ArgumentNullException(nameof(dynamicTail));
            FixedRealPointCount = fixedRealPointCount;
            Reset = reset;
        }

        public IReadOnlyList<WetInkRibbonGeometry> FixedSegments { get; }
        public WetInkRibbonGeometry DynamicTail { get; }
        public int FixedRealPointCount { get; }
        /// <summary>
        /// True when the producer replaced the point baseline (e.g. mid-stroke
        /// straightening). Consumers must discard accumulated fixed geometry and
        /// rebuild from this update as the new baseline.
        /// </summary>
        public bool Reset { get; }
    }

    internal sealed class WetInkStrokeGeometryState
    {
        internal const int FixedSegmentPointCount = 48;
        internal const int DynamicTailPointCount = 24;

        private readonly WetInkGeometryBuilder _builder;
        private readonly List<WetInkRibbonGeometry> _fixedSegments =
            new List<WetInkRibbonGeometry>();
        private RealInkPoint[] _lastRealPoints = Array.Empty<RealInkPoint>();
        private int _fixedRealPointCount;
        private long _lastVersion;
        private long _sessionId;
        private InkStrokeStyleSnapshot _style;
        private bool _initialized;
        private long _geometryGeneration;
        private bool _firstPointCorrected;

        public WetInkStrokeGeometryState(WetInkGeometryBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>
        /// Replaces the baseline points (e.g. after mid-stroke straightening).
        /// Clears accumulated fixed segments and seeds the state so the next
        /// update rebuilds geometry from <paramref name="newBaseline"/>.
        /// </summary>
        public void Reset(IReadOnlyList<RealInkPoint> newBaseline)
        {
            if (newBaseline == null) throw new ArgumentNullException(nameof(newBaseline));
            _fixedSegments.Clear();
            _fixedRealPointCount = 0;
            _lastRealPoints = CopyRange(newBaseline, 0, newBaseline.Count);
            // Keep _lastVersion unchanged: the producer increments the snapshot
            // version and flips the generation, so the next Update still passes
            // the version check while treating newBaseline as the new baseline.
            _geometryGeneration++;
        }

        public WetInkGeometryUpdate Update(WetInkRenderSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Version <= _lastVersion)
            {
                throw new InvalidOperationException(
                    "Wet ink geometry snapshots must have increasing versions.");
            }

            var reset = snapshot.GeometryGeneration != _geometryGeneration;
            if (reset)
            {
                // Baseline replaced out-of-band: discard accumulated fixed geometry
                // and rebuild from the new points as the fresh baseline.
                _fixedSegments.Clear();
                _fixedRealPointCount = 0;
                _lastRealPoints = CopyRange(snapshot.RealPoints, 0, snapshot.RealPoints.Count);
                _geometryGeneration = snapshot.GeometryGeneration;
                _style = snapshot.Style;
                _sessionId = snapshot.SessionId;
                _initialized = true;
                var tail = _builder.Build(
                    snapshot.RealPointsArray.AsSpan(0, snapshot.RealPoints.Count),
                    snapshot.PredictedPointsArray.AsSpan(0, snapshot.PredictedPoints.Count),
                    snapshot.Style);
                return new WetInkGeometryUpdate(
                    Array.Empty<WetInkRibbonGeometry>(),
                    tail,
                    0,
                    reset: true);
            }

            ValidateIdentity(snapshot);
            ValidateAppendOnlyPoints(snapshot.RealPoints);
            ValidatePredictions(snapshot.RealPoints, snapshot.PredictedPoints);

            var stablePointCount = Math.Max(
                0,
                snapshot.RealPoints.Count - DynamicTailPointCount);
            // 取 snapshot 内部数组的 Span，零分配切片（snapshot 在渲染线程只读）。
            var realSpan = snapshot.RealPointsArray.AsSpan(0, snapshot.RealPoints.Count);
            while (stablePointCount - _fixedRealPointCount
                >= FixedSegmentPointCount)
            {
                var startIndex = _fixedRealPointCount == 0
                    ? 0
                    : _fixedRealPointCount - 1;
                var endIndex =
                    _fixedRealPointCount
                    + FixedSegmentPointCount;
                var contextEndIndex = Math.Min(
                    snapshot.RealPoints.Count,
                    endIndex + 1);
                var outputPointCount = endIndex - startIndex;
                _fixedSegments.Add(_builder.Build(
                    realSpan.Slice(startIndex, contextEndIndex - startIndex),
                    default,
                    snapshot.Style,
                    outputPointCount));
                _fixedRealPointCount = endIndex;
            }

            var tailStart = _fixedRealPointCount == 0
                ? 0
                : _fixedRealPointCount - 1;
            var dynamicTail = _builder.Build(
                realSpan.Slice(tailStart),
                snapshot.PredictedPointsArray.AsSpan(0, snapshot.PredictedPoints.Count),
                snapshot.Style);

            _lastRealPoints = CopyRange(
                snapshot.RealPoints,
                0,
                snapshot.RealPoints.Count);
            _lastVersion = snapshot.Version;
            return new WetInkGeometryUpdate(
                _fixedSegments.ToArray(),
                dynamicTail,
                _fixedRealPointCount);
        }

        private void ValidateIdentity(WetInkRenderSnapshot snapshot)
        {
            if (!_initialized)
            {
                _sessionId = snapshot.SessionId;
                _style = snapshot.Style;
                _initialized = true;
                return;
            }

            if (snapshot.SessionId != _sessionId)
            {
                throw new InvalidOperationException(
                    "A wet ink geometry state cannot be reused for another session.");
            }

            if (!SameStyle(_style, snapshot.Style))
            {
                throw new InvalidOperationException(
                    "Wet ink style and generation values cannot change within a session.");
            }
        }

        private void ValidateAppendOnlyPoints(
            IReadOnlyList<RealInkPoint> points)
        {
            if (points.Count < _lastRealPoints.Length)
            {
                throw new InvalidOperationException(
                    "Real wet ink points cannot shrink within a session.");
            }

            // 首点回修：落笔后按首段速度修正首点压感（避免起笔闪变），允许首点压感在
            // 发布后被修正一次。此修正仅在点数从 1 增至 2 时发生，之后首点固定。
            var allowFirstPointCorrection = !_firstPointCorrected
                                            && _lastRealPoints.Length == 1
                                            && points.Count >= 2;
            var checkEnd = allowFirstPointCorrection
                ? _lastRealPoints.Length - 1
                : _lastRealPoints.Length;

            for (var i = 0; i < checkEnd; i++)
            {
                if (!SamePoint(_lastRealPoints[i], points[i]))
                {
                    throw new InvalidOperationException(
                        "Previously published real wet ink points cannot change.");
                }
            }

            if (allowFirstPointCorrection && points.Count >= 1
                && !SamePoint(_lastRealPoints[0], points[0]))
            {
                _firstPointCorrected = true;
            }
        }

        private static void ValidatePredictions(
            IReadOnlyList<RealInkPoint> realPoints,
            IReadOnlyList<PredictedInkPoint> predictedPoints)
        {
            if (predictedPoints == null || predictedPoints.Count == 0)
                return;

            var previousTimestamp = realPoints.Count == 0
                ? long.MinValue
                : realPoints[realPoints.Count - 1].TimestampMicroseconds;
            for (var i = 0; i < predictedPoints.Count; i++)
            {
                var point = predictedPoints[i];
                if (!IsFinite(point.X)
                    || !IsFinite(point.Y)
                    || !IsFinite(point.Pressure)
                    || point.TimestampMicroseconds <= previousTimestamp)
                {
                    throw new InvalidOperationException(
                        "Predicted wet ink points must be finite and strictly chronological after real points.");
                }

                previousTimestamp = point.TimestampMicroseconds;
            }
        }

        private static bool SameStyle(
            InkStrokeStyleSnapshot left,
            InkStrokeStyleSnapshot right) =>
            left.ColorArgb == right.ColorArgb
            && left.Width == right.Width
            && left.Height == right.Height
            && left.IgnorePressure == right.IgnorePressure
            && left.IsHighlighter == right.IsHighlighter
            && left.UseVelocityBrushTip == right.UseVelocityBrushTip
            && left.VelocityBrushTipMix == right.VelocityBrushTipMix
            && left.MinimumDistanceScale == right.MinimumDistanceScale
            && left.CoordinateGeneration == right.CoordinateGeneration
            && left.PageGeneration == right.PageGeneration
            && left.StylusTipShape == right.StylusTipShape
            && left.RenderMode == right.RenderMode;

        private static bool SamePoint(
            RealInkPoint left,
            RealInkPoint right) =>
            left.X == right.X
            && left.Y == right.Y
            && left.Pressure == right.Pressure
            && left.TimestampMicroseconds == right.TimestampMicroseconds;

        private static RealInkPoint[] CopyRange(
            IReadOnlyList<RealInkPoint> points,
            int startIndex,
            int count)
        {
            if (count <= 0)
                return Array.Empty<RealInkPoint>();
            var result = new RealInkPoint[count];
            for (var i = 0; i < count; i++)
                result[i] = points[startIndex + i];
            return result;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
