using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class NativeInkController
    {
        private readonly NativeInkSessionManager _sessions;
        private readonly WetInkCommandMailbox _mailbox;
        private readonly Dictionary<long, long> _snapshotVersions =
            new Dictionary<long, long>();
        private readonly object _syncRoot = new object();

        public NativeInkController(
            NativeInkSessionManager sessions,
            WetInkCommandMailbox mailbox)
        {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _mailbox = mailbox ?? throw new ArgumentNullException(nameof(mailbox));
        }

        public NativeInkSession Begin(
            uint pointerId,
            NativeInkInputKind inputKind,
            InkStrokeStyleSnapshot style,
            InkSampleProcessorSettings processorSettings,
            long startedAtMicroseconds,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            lock (_syncRoot)
            {
                if (_sessions.TryGet(pointerId, out var previous))
                {
                    _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                        WetInkBoundaryCommandKind.CancelStroke,
                        previous.SessionId));
                    _snapshotVersions.Remove(previous.SessionId);
                }

                var session = _sessions.Begin(
                    pointerId,
                    inputKind,
                    style,
                    processorSettings,
                    startedAtMicroseconds);
                try
                {
                    _snapshotVersions.Add(session.SessionId, 0);
                    AppendWithoutPublishing(session, newestFirstHistory);
                    // 落笔时如果已有足够点且开启预测，立即附上笔尾，避免首帧无预测。
                    if (session.RealPoints.Count >= 2)
                    {
                        try
                        {
                            var predicted = InkTailPredictor.Build(session.RealPoints);
                            if (predicted.Count > 0)
                                session.ReplacePrediction(predicted);
                        }
                        catch
                        {
                            // prediction is best-effort on begin
                        }
                    }
                    var snapshot = CreateNextSnapshot(session);
                    _mailbox.PublishBegin(
                        new WetInkBoundaryCommand(
                            WetInkBoundaryCommandKind.BeginStroke,
                            session.SessionId),
                        snapshot);
                    _snapshotVersions[session.SessionId] = snapshot.Version;
                    return session;
                }
                catch
                {
                    _sessions.Cancel(pointerId);
                    _snapshotVersions.Remove(session.SessionId);
                    throw;
                }
            }
        }

        public bool Update(
            uint pointerId,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session))
                    return false;
                AppendAndPublish(session, newestFirstHistory);
                return true;
            }
        }

        /// <summary>
        /// 原子地追加真实点并替换预测笔尾，避免“Update 清空预测 → 下一帧再重建”
        /// 造成的一帧空窗。predictionEnabled 为 false 时行为与 Update 一致。
        /// </summary>
        public bool UpdateWithPrediction(
            uint pointerId,
            IReadOnlyList<RawInkSample> newestFirstHistory,
            bool predictionEnabled)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session))
                    return false;

                var appended = AppendWithoutPublishing(session, newestFirstHistory);
                if (predictionEnabled && session.State == NativeInkSessionState.Active)
                {
                    var predicted = InkTailPredictor.Build(session.RealPoints);
                    session.ReplacePrediction(predicted);
                    // 只要预测启用，即使本帧没有新真实点，也要发布一次，保持笔尾连续。
                    PublishSnapshot(session);
                    return true;
                }

                if (appended != 0)
                    PublishSnapshot(session);
                return appended != 0;
            }
        }

        internal bool TryUpdateSessionWithPrediction(
            uint pointerId,
            long sessionId,
            IReadOnlyList<RawInkSample> newestFirstHistory,
            bool predictionEnabled)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session)
                    || session.SessionId != sessionId
                    || session.State != NativeInkSessionState.Active)
                {
                    return false;
                }

                var appended = AppendWithoutPublishing(session, newestFirstHistory);
                if (predictionEnabled)
                {
                    var predicted = InkTailPredictor.Build(session.RealPoints);
                    session.ReplacePrediction(predicted);
                    PublishSnapshot(session);
                    return true;
                }

                if (appended != 0)
                    PublishSnapshot(session);
                return appended != 0;
            }
        }

        public bool ReplacePrediction(
            uint pointerId,
            IReadOnlyList<PredictedInkPoint> points)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session))
                    return false;
                session.ReplacePrediction(points);
                PublishSnapshot(session);
                return true;
            }
        }

        public NativeStrokeCommitPayload End(
            uint pointerId,
            long endedAtMicroseconds,
            IReadOnlyList<RawInkSample> newestFirstHistory,
            bool bakePredictionIntoRealInk = false)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session))
                    return null;

                AppendAndPublish(session, newestFirstHistory);
                // bakePredictionIntoRealInk=true 时，预测笔尾会写入真实点并进入干墨提交。
                var payload = session.End(endedAtMicroseconds, bakePredictionIntoRealInk);
                _sessions.DetachActivePointer(pointerId, session);
                if (payload == null)
                {
                    _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                        WetInkBoundaryCommandKind.CancelStroke,
                        session.SessionId));
                    _snapshotVersions.Remove(session.SessionId);
                    _sessions.RemoveCompleted(session.SessionId);
                    return null;
                }

                var snapshot = CreateNextSnapshot(session);
                _mailbox.PublishEnd(
                    snapshot,
                    new WetInkBoundaryCommand(
                        WetInkBoundaryCommandKind.EndStroke,
                        session.SessionId,
                        snapshot.Version));
                _snapshotVersions[session.SessionId] = snapshot.Version;
                session.SetRetirementVersion(snapshot.Version);
                return payload;
            }
        }

        internal bool TryGetSession(uint pointerId, out NativeInkSession session)
        {
            lock (_syncRoot)
                return _sessions.TryGet(pointerId, out session);
        }

        internal bool TryGetSessionInfo(uint pointerId, out long sessionId, out NativeInkSessionState state)
        {
            lock (_syncRoot)
            {
                if (_sessions.TryGet(pointerId, out var session))
                {
                    sessionId = session.SessionId;
                    state = session.State;
                    return true;
                }

                sessionId = 0;
                state = default;
                return false;
            }
        }

        public bool Cancel(uint pointerId)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGet(pointerId, out var session))
                    return false;

                _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                    WetInkBoundaryCommandKind.CancelStroke,
                    session.SessionId));
                _snapshotVersions.Remove(session.SessionId);
                return _sessions.Cancel(pointerId);
            }
        }

        public void CancelAll()
        {
            lock (_syncRoot)
            {
                _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                    WetInkBoundaryCommandKind.Reset,
                    0));
                _snapshotVersions.Clear();
                _sessions.CancelAll();
            }
        }

        /// <summary>
        /// Mid-stroke straightening for the given session: replaces its in-flight
        /// points with a straight line and re-publishes a snapshot carrying the
        /// new geometry generation so the wet renderer rebuilds from the line.
        /// </summary>
        public bool TryStraightenSession(long sessionId)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGetSession(sessionId, out var session)
                    || session.State != NativeInkSessionState.Active)
                {
                    return false;
                }

                session.StraightenToLine();
                PublishSnapshot(session);
                return true;
            }
        }

        /// <summary>
        /// True while any session still renders wet ink that is not yet represented
        /// by a committed-and-painted dry stroke. This covers the whole handoff:
        /// from the moment a stroke begins until its native wet visual is retired
        /// (after the WPF frame fence confirms the dry stroke has been painted).
        /// During <see cref="NativeInkSessionState.DryCommittedAwaitingWpfFrame"/>
        /// and <see cref="NativeInkSessionState.RetiringWetVisual"/> the dry stroke
        /// is not yet on screen, so the wet overlay must stay visible to avoid a
        /// "drying" flash where the whole line disappears for a frame.
        /// </summary>
        public bool HasLiveWetVisual()
        {
            lock (_syncRoot)
            {
                foreach (var session in _sessions.Sessions)
                {
                    var state = session.State;
                    if (state == NativeInkSessionState.Active
                        || state == NativeInkSessionState.Ending
                        || state == NativeInkSessionState.DryCommittedAwaitingWpfFrame
                        || state == NativeInkSessionState.RetiringWetVisual)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int ActiveSessionCount
        {
            get
            {
                lock (_syncRoot)
                {
                    var count = 0;
                    foreach (var session in _sessions.Sessions)
                    {
                        var state = session.State;
                        if (state == NativeInkSessionState.Active
                            || state == NativeInkSessionState.Ending
                            || state == NativeInkSessionState.RetiringWetVisual)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        public void MarkDryCommitted(long sessionId)
        {
            lock (_syncRoot)
                GetSession(sessionId).MarkDryCommitted();
        }

        public void MarkWpfFrameRendered(long sessionId)
        {
            lock (_syncRoot)
            {
                var session = GetSession(sessionId);
                if (session.State == NativeInkSessionState.RetiringWetVisual)
                    return;
                if (session.State != NativeInkSessionState.DryCommittedAwaitingWpfFrame)
                    throw new InvalidOperationException(
                        $"Session {sessionId} is {session.State}; expected {NativeInkSessionState.DryCommittedAwaitingWpfFrame}.");

                if (!_snapshotVersions.TryGetValue(sessionId, out var version))
                    throw new InvalidOperationException(
                        $"Native ink session {sessionId} has no retirement version.");

                _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                    WetInkBoundaryCommandKind.RetireStroke,
                    sessionId,
                    version));
                session.MarkWpfFrameRendered();
            }
        }

        public bool TryMarkWetVisualRetired(long sessionId, long version)
        {
            lock (_syncRoot)
            {
                if (!_sessions.TryGetSession(sessionId, out var session)
                    || session.State != NativeInkSessionState.RetiringWetVisual
                    || session.RetirementVersion != version)
                {
                    return false;
                }

                session.MarkWetVisualRetired();
                _snapshotVersions.Remove(sessionId);
                _sessions.RemoveCompleted(sessionId);
                return true;
            }
        }

        private void AppendAndPublish(
            NativeInkSession session,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            if (AppendWithoutPublishing(session, newestFirstHistory) != 0)
                PublishSnapshot(session);
        }

        private static int AppendWithoutPublishing(
            NativeInkSession session,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            if (newestFirstHistory == null || newestFirstHistory.Count == 0)
                return 0;
            return session.AppendReverseChronologicalHistory(newestFirstHistory);
        }

        private void PublishSnapshot(NativeInkSession session)
        {
            var snapshot = CreateNextSnapshot(session);
            _mailbox.PublishSnapshot(snapshot);
            _snapshotVersions[session.SessionId] = snapshot.Version;
        }

        private WetInkRenderSnapshot CreateNextSnapshot(
            NativeInkSession session)
        {
            var version = _snapshotVersions[session.SessionId] + 1;
            return new WetInkRenderSnapshot(
                session.SessionId,
                version,
                session.Style,
                session.RealPoints,
                session.PredictedPoints,
                geometryGeneration: session.GeometryGeneration);
        }

        private NativeInkSession GetSession(long sessionId)
        {
            if (!_sessions.TryGetSession(sessionId, out var session))
                throw new InvalidOperationException(
                    $"Native ink session {sessionId} does not exist.");
            return session;
        }
    }
}
