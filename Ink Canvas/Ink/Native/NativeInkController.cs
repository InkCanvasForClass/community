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

        public bool Update(
            uint pointerId,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            if (!_sessions.TryGet(pointerId, out var session))
                return false;
            AppendAndPublish(session, newestFirstHistory);
            return true;
        }

        public bool ReplacePrediction(
            uint pointerId,
            IReadOnlyList<PredictedInkPoint> points)
        {
            if (!_sessions.TryGet(pointerId, out var session))
                return false;
            session.ReplacePrediction(points);
            PublishSnapshot(session);
            return true;
        }

        public NativeStrokeCommitPayload End(
            uint pointerId,
            long endedAtMicroseconds,
            IReadOnlyList<RawInkSample> newestFirstHistory)
        {
            if (!_sessions.TryGet(pointerId, out var session))
                return null;

            AppendAndPublish(session, newestFirstHistory);
            var payload = session.End(endedAtMicroseconds);
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

        internal bool TryGetSession(uint pointerId, out NativeInkSession session) =>
            _sessions.TryGet(pointerId, out session);

        public bool Cancel(uint pointerId)
        {
            if (!_sessions.TryGet(pointerId, out var session))
                return false;

            _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.CancelStroke,
                session.SessionId));
            _snapshotVersions.Remove(session.SessionId);
            return _sessions.Cancel(pointerId);
        }

        public void CancelAll()
        {
            _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                WetInkBoundaryCommandKind.Reset,
                0));
            _snapshotVersions.Clear();
            _sessions.CancelAll();
        }

        /// <summary>
        /// Mid-stroke straightening for the given session: replaces its in-flight
        /// points with a straight line and re-publishes a snapshot carrying the
        /// new geometry generation so the wet renderer rebuilds from the line.
        /// </summary>
        public bool TryStraightenSession(long sessionId)
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

        /// <summary>
        /// True while any session still renders wet ink that is not yet represented
        /// by a committed dry stroke. Drives overlay visibility so the transparent
        /// overlay HWND is hidden when there is nothing to render, letting the
        /// main window's own WPF content receive input normally. Once a stroke is
        /// committed to the WPF InkCanvas (DryCommittedAwaitingWpfFrame) the dry
        /// stroke owns the visual, so the overlay hides even while the native
        /// session is still being retired.
        /// </summary>
        public bool HasLiveWetVisual()
        {
            foreach (var session in _sessions.Sessions)
            {
                var state = session.State;
                if (state == NativeInkSessionState.Active
                    || state == NativeInkSessionState.Ending)
                {
                    return true;
                }
            }

            return false;
        }

        public int ActiveSessionCount
        {
            get
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

        public void MarkDryCommitted(long sessionId)
        {
            GetSession(sessionId).MarkDryCommitted();
        }

        public void MarkWpfFrameRendered(long sessionId)
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

        public bool TryMarkWetVisualRetired(long sessionId, long version)
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
