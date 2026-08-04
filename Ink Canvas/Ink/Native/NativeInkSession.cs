using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal enum NativeInkSessionState
    {
        Active,
        Ending,
        DryCommittedAwaitingWpfFrame,
        RetiringWetVisual,
        Completed,
        Canceled
    }

    internal sealed class NativeInkSession
    {
        private readonly List<RealInkPoint> _realPoints = new List<RealInkPoint>();
        private PredictedInkPoint[] _predictedPoints = Array.Empty<PredictedInkPoint>();

        public NativeInkSession(
            long sessionId,
            uint pointerId,
            NativeInkInputKind inputKind,
            InkStrokeStyleSnapshot style,
            InkSampleProcessor processor,
            long startedAtMicroseconds)
        {
            SessionId = sessionId;
            PointerId = pointerId;
            InputKind = inputKind;
            Style = style;
            Processor = processor ?? throw new ArgumentNullException(nameof(processor));
            StartedAtMicroseconds = startedAtMicroseconds;
            State = NativeInkSessionState.Active;
        }

        public long SessionId { get; }
        public uint PointerId { get; }
        public NativeInkInputKind InputKind { get; }
        public InkStrokeStyleSnapshot Style { get; }
        public InkSampleProcessor Processor { get; }
        public long StartedAtMicroseconds { get; }
        public long LastAcceptedTimestampMicroseconds { get; private set; } = -1;
        public uint LastAcceptedFrameId { get; private set; }
        public NativeInkSessionState State { get; private set; }
        public long RetirementVersion { get; private set; }
        public IReadOnlyList<RealInkPoint> RealPoints => _realPoints;
        public IReadOnlyList<PredictedInkPoint> PredictedPoints => _predictedPoints;
        /// <summary>
        /// 本笔专属的预测视界时间平滑器，跨帧保持状态以压住笔尾长度的帧间抖动。
        /// </summary>
        internal InkTailPredictionSmoother TailSmoother { get; } = new InkTailPredictionSmoother();
        /// <summary>
        // Increases whenever the point baseline is replaced mid-stroke (e.g.
        // straightening). Carried on snapshots so the wet renderer can discard
        // accumulated fixed geometry and rebuild.
        /// </summary>
        private long _geometryGeneration;

        public int AppendReverseChronologicalHistory(IReadOnlyList<RawInkSample> newestFirst)
        {
            EnsureState(NativeInkSessionState.Active);
            var normalized = InkSampleHistoryNormalizer.NormalizeReverseChronological(
                newestFirst,
                LastAcceptedTimestampMicroseconds,
                LastAcceptedFrameId);
            if (normalized.Count == 0)
                return 0;

            var previousRealPointCount = _realPoints.Count;
            Processor.Append(normalized, _realPoints);
            var last = normalized[normalized.Count - 1];
            LastAcceptedTimestampMicroseconds = last.TimestampMicroseconds;
            LastAcceptedFrameId = last.FrameId;
            if (_realPoints.Count != previousRealPointCount)
                _predictedPoints = Array.Empty<PredictedInkPoint>();
            return _realPoints.Count - previousRealPointCount;
        }

        /// <summary>
        /// Mid-stroke straightening: replaces the in-flight real points with a
        /// straight line from the first to the last point and flips the geometry
        /// generation so the wet-ink renderer rebuilds from the new baseline.
        /// Caller must re-publish a snapshot afterwards.
        /// </summary>
        public void StraightenToLine()
        {
            EnsureState(NativeInkSessionState.Active);
            if (_realPoints.Count < 2)
                return;

            var first = _realPoints[0];
            var last = _realPoints[_realPoints.Count - 1];
            _realPoints.Clear();
            _realPoints.Add(first);
            _realPoints.Add(last);
            _predictedPoints = Array.Empty<PredictedInkPoint>();
            // 基线被整体替换，之前累积的视界平滑状态不再对应当前轨迹。
            TailSmoother.Reset();
            _geometryGeneration++;
        }

        public long GeometryGeneration => _geometryGeneration;

        public void ReplacePrediction(IReadOnlyList<PredictedInkPoint> points)
        {
            EnsureState(NativeInkSessionState.Active);
            if (points == null || points.Count == 0)
            {
                _predictedPoints = Array.Empty<PredictedInkPoint>();
                return;
            }

            var previousTimestamp = _realPoints.Count == 0
                ? long.MinValue
                : _realPoints[_realPoints.Count - 1].TimestampMicroseconds;
            _predictedPoints = new PredictedInkPoint[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!IsFinite(point.X)
                    || !IsFinite(point.Y)
                    || !IsFinite(point.Pressure)
                    || point.TimestampMicroseconds <= previousTimestamp)
                {
                    throw new ArgumentException(
                        "Predicted points must be finite and strictly chronological after real points.",
                        nameof(points));
                }

                _predictedPoints[i] = point;
                previousTimestamp = point.TimestampMicroseconds;
            }
        }

        /// <summary>
        /// 将当前预测笔尾烘焙进真实点集，使其进入干墨提交/撤销/保存。
        /// 调用后预测缓冲会被清空。
        /// </summary>
        public int BakePredictionIntoRealPoints()
        {
            EnsureState(NativeInkSessionState.Active);
            if (_predictedPoints.Length == 0)
                return 0;

            var previousTimestamp = _realPoints.Count == 0
                ? long.MinValue
                : _realPoints[_realPoints.Count - 1].TimestampMicroseconds;
            var baked = 0;
            for (var i = 0; i < _predictedPoints.Length; i++)
            {
                var point = _predictedPoints[i];
                if (!IsFinite(point.X)
                    || !IsFinite(point.Y)
                    || !IsFinite(point.Pressure)
                    || point.TimestampMicroseconds <= previousTimestamp)
                {
                    continue;
                }

                _realPoints.Add(new RealInkPoint(
                    point.X,
                    point.Y,
                    Math.Clamp(point.Pressure, 0.05f, 1f),
                    point.TimestampMicroseconds));
                previousTimestamp = point.TimestampMicroseconds;
                LastAcceptedTimestampMicroseconds = point.TimestampMicroseconds;
                baked++;
            }

            _predictedPoints = Array.Empty<PredictedInkPoint>();
            if (baked > 0)
                _geometryGeneration++;
            return baked;
        }

        public NativeStrokeCommitPayload End(long endedAtMicroseconds, bool bakePredictionIntoRealInk = false)
        {
            EnsureState(NativeInkSessionState.Active);
            if (bakePredictionIntoRealInk)
            {
                // 抬笔前若预测缓冲为空，按最终真实点再算一次，保证预览与干墨一致。
                if (_predictedPoints.Length == 0 && _realPoints.Count >= 2)
                {
                    try
                    {
                        ReplacePrediction(InkTailPredictor.Build(_realPoints));
                    }
                    catch
                    {
                        // best-effort
                    }
                }
                BakePredictionIntoRealPoints();
            }
            else
            {
                _predictedPoints = Array.Empty<PredictedInkPoint>();
            }

            if (_realPoints.Count == 0)
            {
                State = NativeInkSessionState.Canceled;
                return null;
            }

            // 点集/速率笔锋在抬笔时整笔写入，与旧 WPF 干墨后处理一致；
            // 实时笔锋已在 Append 过程中写入。
            Processor.ApplyFinalBrushTip(_realPoints);
            if (Processor.FinalBrushTipApplied)
                _geometryGeneration++;

            State = NativeInkSessionState.Ending;
            return new NativeStrokeCommitPayload(
                SessionId,
                PointerId,
                InputKind,
                Style,
                _realPoints,
                StartedAtMicroseconds,
                endedAtMicroseconds,
                Processor.VelocityBrushTipApplied,
                Processor.FinalBrushTipApplied);
        }

        public void SetRetirementVersion(long version)
        {
            EnsureState(NativeInkSessionState.Ending);
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            RetirementVersion = version;
        }

        public void MarkDryCommitted()
        {
            EnsureState(NativeInkSessionState.Ending);
            State = NativeInkSessionState.DryCommittedAwaitingWpfFrame;
        }

        public void MarkWpfFrameRendered()
        {
            EnsureState(NativeInkSessionState.DryCommittedAwaitingWpfFrame);
            State = NativeInkSessionState.RetiringWetVisual;
        }

        public void MarkWetVisualRetired()
        {
            EnsureState(NativeInkSessionState.RetiringWetVisual);
            State = NativeInkSessionState.Completed;
        }

        public void Cancel()
        {
            if (State == NativeInkSessionState.Completed || State == NativeInkSessionState.Canceled)
                return;
            _predictedPoints = Array.Empty<PredictedInkPoint>();
            State = NativeInkSessionState.Canceled;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private void EnsureState(NativeInkSessionState expected)
        {
            if (State != expected)
                throw new InvalidOperationException($"Session {SessionId} is {State}; expected {expected}.");
        }
    }

    internal sealed class NativeInkSessionManager
    {
        // 双字典：pointerId → session（pointing sessions）+ sessionId → session（含 retired 状态）。
        // 拆分是必要的：End 后 _activeSessions 已 detach（同一 pointerId 可立即复笔），
        // 但 retirement 回调仍要按 sessionId 找到该 session 标记退休+清理。
        private readonly Dictionary<uint, NativeInkSession> _activeSessions = new Dictionary<uint, NativeInkSession>();
        private readonly Dictionary<long, NativeInkSession> _sessionsById = new Dictionary<long, NativeInkSession>();
        private long _nextSessionId;

        public IReadOnlyCollection<NativeInkSession> Sessions => _sessionsById.Values;

        public NativeInkSession Begin(
            uint pointerId,
            NativeInkInputKind inputKind,
            InkStrokeStyleSnapshot style,
            InkSampleProcessorSettings processorSettings,
            long startedAtMicroseconds)
        {
            if (_activeSessions.TryGetValue(pointerId, out var previous))
            {
                previous.Cancel();
                _sessionsById.Remove(previous.SessionId);
            }

            var session = new NativeInkSession(
                ++_nextSessionId,
                pointerId,
                inputKind,
                style,
                new InkSampleProcessor(processorSettings),
                startedAtMicroseconds);
            _activeSessions[pointerId] = session;
            _sessionsById[session.SessionId] = session;
            return session;
        }

        public bool TryGet(uint pointerId, out NativeInkSession session) =>
            _activeSessions.TryGetValue(pointerId, out session);

        public bool TryGetSession(long sessionId, out NativeInkSession session) =>
            _sessionsById.TryGetValue(sessionId, out session);

        public bool DetachActivePointer(uint pointerId, NativeInkSession expectedSession)
        {
            if (!_activeSessions.TryGetValue(pointerId, out var session)
                || !ReferenceEquals(session, expectedSession))
            {
                return false;
            }

            _activeSessions.Remove(pointerId);
            return true;
        }

        public bool Cancel(uint pointerId)
        {
            if (!_activeSessions.TryGetValue(pointerId, out var session))
                return false;
            session.Cancel();
            _activeSessions.Remove(pointerId);
            _sessionsById.Remove(session.SessionId);
            return true;
        }

        public void RemoveCompleted(long sessionId)
        {
            if (!_sessionsById.TryGetValue(sessionId, out var session)
                || (session.State != NativeInkSessionState.Completed && session.State != NativeInkSessionState.Canceled))
            {
                return;
            }

            _sessionsById.Remove(sessionId);
            if (_activeSessions.TryGetValue(session.PointerId, out var active)
                && ReferenceEquals(active, session))
            {
                _activeSessions.Remove(session.PointerId);
            }
        }

        public void CancelAll()
        {
            foreach (var session in _sessionsById.Values)
                session.Cancel();
            _activeSessions.Clear();
            _sessionsById.Clear();
        }
    }
}
