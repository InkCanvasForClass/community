using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal enum WetInkBoundaryCommandKind
    {
        BeginStroke,
        EndStroke,
        CancelStroke,
        RetireStroke,
        Resize,
        Reset,
        Shutdown
    }

    internal readonly struct WetInkBoundaryCommand
    {
        public WetInkBoundaryCommand(
            WetInkBoundaryCommandKind kind,
            long sessionId,
            long version = 0,
            long sequence = 0)
        {
            Kind = kind;
            SessionId = sessionId;
            Version = version;
            Sequence = sequence;
        }

        public WetInkBoundaryCommandKind Kind { get; }
        public long SessionId { get; }
        public long Version { get; }
        public long Sequence { get; }

        internal WetInkBoundaryCommand WithSequence(long sequence) =>
            new WetInkBoundaryCommand(Kind, SessionId, Version, sequence);
    }

    internal sealed class WetInkRenderSnapshot
    {
        private readonly RealInkPoint[] _realPointsArray;
        private readonly PredictedInkPoint[] _predictedPointsArray;

        public WetInkRenderSnapshot(
            long sessionId,
            long version,
            InkStrokeStyleSnapshot style,
            IReadOnlyList<RealInkPoint> realPoints,
            IReadOnlyList<PredictedInkPoint> predictedPoints,
            long sequence = 0,
            long geometryGeneration = 0)
        {
            SessionId = sessionId;
            Version = version;
            Style = style;
            _realPointsArray = Copy(realPoints);
            _predictedPointsArray = Copy(predictedPoints);
            RealPoints = _realPointsArray;
            PredictedPoints = _predictedPointsArray;
            Sequence = sequence;
            GeometryGeneration = geometryGeneration;
        }

        public long SessionId { get; }
        public long Version { get; }
        public InkStrokeStyleSnapshot Style { get; }
        public IReadOnlyList<RealInkPoint> RealPoints { get; }
        public IReadOnlyList<PredictedInkPoint> PredictedPoints { get; }
        public long Sequence { get; }
        public long GeometryGeneration { get; }

        /// <summary>底层 RealInkPoint 数组（供零分配 Span 切片，只读）。</summary>
        internal RealInkPoint[] RealPointsArray => _realPointsArray;
        /// <summary>底层 PredictedInkPoint 数组（供零分配 Span 切片，只读）。</summary>
        internal PredictedInkPoint[] PredictedPointsArray => _predictedPointsArray;

        internal WetInkRenderSnapshot WithSequence(long sequence) =>
            new WetInkRenderSnapshot(
                SessionId,
                Version,
                Style,
                RealPoints,
                PredictedPoints,
                sequence,
                GeometryGeneration);

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    internal enum WetInkMailboxItemKind
    {
        Boundary,
        Snapshot
    }

    internal readonly struct WetInkMailboxItem
    {
        public WetInkMailboxItem(WetInkBoundaryCommand command)
        {
            Kind = WetInkMailboxItemKind.Boundary;
            Sequence = command.Sequence;
            BoundaryCommand = command;
            RenderSnapshot = null;
        }

        public WetInkMailboxItem(WetInkRenderSnapshot snapshot)
        {
            Kind = WetInkMailboxItemKind.Snapshot;
            Sequence = snapshot?.Sequence ?? throw new ArgumentNullException(nameof(snapshot));
            BoundaryCommand = default;
            RenderSnapshot = snapshot;
        }

        public WetInkMailboxItemKind Kind { get; }
        public long Sequence { get; }
        public WetInkBoundaryCommand BoundaryCommand { get; }
        public WetInkRenderSnapshot RenderSnapshot { get; }
    }

    internal sealed class WetInkMailboxBatch
    {
        public WetInkMailboxBatch(
            IReadOnlyList<WetInkBoundaryCommand> boundaryCommands,
            IReadOnlyList<WetInkRenderSnapshot> renderSnapshots,
            IReadOnlyList<WetInkMailboxItem> orderedItems)
        {
            BoundaryCommands = boundaryCommands;
            RenderSnapshots = renderSnapshots;
            OrderedItems = orderedItems;
        }

        public IReadOnlyList<WetInkBoundaryCommand> BoundaryCommands { get; }
        public IReadOnlyList<WetInkRenderSnapshot> RenderSnapshots { get; }
        public IReadOnlyList<WetInkMailboxItem> OrderedItems { get; }
    }

    internal sealed class WetInkCommandMailbox
    {
        private const int DefaultBoundaryCapacity = 256;
        private const int DefaultSnapshotCapacity = 64;

        private readonly object _syncRoot = new object();
        private readonly Queue<WetInkBoundaryCommand> _boundaryCommands;
        private readonly int _snapshotCapacity;
        private readonly Dictionary<long, WetInkRenderSnapshot> _latestSnapshots;
        private readonly HashSet<long> _reservedSnapshotSessions = new HashSet<long>();
        private readonly HashSet<long> _closedSessions = new HashSet<long>();
        private readonly Dictionary<long, long> _lastPublishedVersions =
            new Dictionary<long, long>();
        private long _nextSequence;

        public WetInkCommandMailbox(
            int boundaryCapacity = DefaultBoundaryCapacity,
            int snapshotCapacity = DefaultSnapshotCapacity)
        {
            if (boundaryCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(boundaryCapacity));
            if (snapshotCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotCapacity));

            _boundaryCommands = new Queue<WetInkBoundaryCommand>(boundaryCapacity);
            _snapshotCapacity = snapshotCapacity;
            _latestSnapshots =
                new Dictionary<long, WetInkRenderSnapshot>(snapshotCapacity);
        }

        public int CoalescedSnapshotCount { get; private set; }

        public int PendingBoundaryCount
        {
            get
            {
                lock (_syncRoot)
                    return _boundaryCommands.Count;
            }
        }

        public int PendingSnapshotCount
        {
            get
            {
                lock (_syncRoot)
                    return _latestSnapshots.Count;
            }
        }

        public void EnqueueBoundary(WetInkBoundaryCommand command)
        {
            lock (_syncRoot)
                EnqueueBoundaryCore(command);
        }

        public void PublishSnapshot(WetInkRenderSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            lock (_syncRoot)
                PublishSnapshotCore(snapshot);
        }

        public void PublishBegin(
            WetInkBoundaryCommand command,
            WetInkRenderSnapshot snapshot)
        {
            if (command.Kind != WetInkBoundaryCommandKind.BeginStroke)
                throw new ArgumentException("A begin publication requires a BeginStroke command.", nameof(command));
            if (snapshot != null && snapshot.SessionId != command.SessionId)
                throw new ArgumentException("The begin command and snapshot must identify the same session.", nameof(snapshot));

            lock (_syncRoot)
            {
                EnsureSnapshotCapacity(snapshot);
                if (snapshot != null)
                    _reservedSnapshotSessions.Add(snapshot.SessionId);
                try
                {
                    EnqueueBoundaryCore(command);
                    if (snapshot != null)
                        PublishSnapshotCore(snapshot);
                }
                finally
                {
                    if (snapshot != null)
                        _reservedSnapshotSessions.Remove(snapshot.SessionId);
                }
            }
        }

        public void PublishEnd(
            WetInkRenderSnapshot snapshot,
            WetInkBoundaryCommand command)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (command.Kind != WetInkBoundaryCommandKind.EndStroke)
                throw new ArgumentException("An end publication requires an EndStroke command.", nameof(command));
            if (snapshot.SessionId != command.SessionId || snapshot.Version != command.Version)
                throw new ArgumentException("The end command must fence the supplied snapshot.", nameof(command));

            lock (_syncRoot)
            {
                EnsureSnapshotCapacity(snapshot);
                PublishSnapshotCore(snapshot);
                EnqueueBoundaryCore(command);
            }
        }

        public WetInkMailboxBatch Drain()
        {
            lock (_syncRoot)
            {
                var commands = _boundaryCommands.ToArray();
                _boundaryCommands.Clear();

                var snapshots = new WetInkRenderSnapshot[_latestSnapshots.Count];
                _latestSnapshots.Values.CopyTo(snapshots, 0);
                _latestSnapshots.Clear();
                Array.Sort(snapshots, CompareSnapshotSequence);

                var orderedItems = new WetInkMailboxItem[commands.Length + snapshots.Length];
                var commandIndex = 0;
                var snapshotIndex = 0;
                var itemIndex = 0;
                while (commandIndex < commands.Length || snapshotIndex < snapshots.Length)
                {
                    if (snapshotIndex == snapshots.Length
                        || (commandIndex < commands.Length
                            && commands[commandIndex].Sequence < snapshots[snapshotIndex].Sequence))
                    {
                        orderedItems[itemIndex++] = new WetInkMailboxItem(commands[commandIndex++]);
                    }
                    else
                    {
                        orderedItems[itemIndex++] = new WetInkMailboxItem(snapshots[snapshotIndex++]);
                    }
                }

                return new WetInkMailboxBatch(commands, snapshots, orderedItems);
            }
        }

        private void EnqueueBoundaryCore(WetInkBoundaryCommand command)
        {
            var sequenced = command.WithSequence(++_nextSequence);
            _boundaryCommands.Enqueue(sequenced);

            switch (sequenced.Kind)
            {
                case WetInkBoundaryCommandKind.BeginStroke:
                    _closedSessions.Remove(sequenced.SessionId);
                    break;
                case WetInkBoundaryCommandKind.CancelStroke:
                case WetInkBoundaryCommandKind.RetireStroke:
                    _closedSessions.Add(sequenced.SessionId);
                    _latestSnapshots.Remove(sequenced.SessionId);
                    _lastPublishedVersions.Remove(sequenced.SessionId);
                    break;
                case WetInkBoundaryCommandKind.Reset:
                    _closedSessions.Clear();
                    _latestSnapshots.Clear();
                    _lastPublishedVersions.Clear();
                    break;
                case WetInkBoundaryCommandKind.Shutdown:
                    _latestSnapshots.Clear();
                    _lastPublishedVersions.Clear();
                    break;
            }
        }

        private void PublishSnapshotCore(WetInkRenderSnapshot snapshot)
        {
            if (_closedSessions.Contains(snapshot.SessionId))
                return;
            if (_lastPublishedVersions.TryGetValue(snapshot.SessionId, out var lastVersion)
                && snapshot.Version <= lastVersion)
            {
                return;
            }

            EnsureSnapshotCapacity(snapshot);
            var sequenced = snapshot.WithSequence(++_nextSequence);
            if (_latestSnapshots.ContainsKey(snapshot.SessionId))
            {
                CoalescedSnapshotCount++;
                _latestSnapshots[snapshot.SessionId] = sequenced;
            }
            else
            {
                _latestSnapshots.Add(snapshot.SessionId, sequenced);
            }

            _lastPublishedVersions[snapshot.SessionId] = snapshot.Version;
        }

        private void EnsureSnapshotCapacity(WetInkRenderSnapshot snapshot)
        {
            if (snapshot == null
                || _latestSnapshots.ContainsKey(snapshot.SessionId)
                || _reservedSnapshotSessions.Contains(snapshot.SessionId))
            {
                return;
            }
            if (_latestSnapshots.Count == _snapshotCapacity)
                throw new InvalidOperationException("The wet ink render snapshot mailbox is full.");
        }

        private static int CompareSnapshotSequence(
            WetInkRenderSnapshot left,
            WetInkRenderSnapshot right) =>
            left.Sequence.CompareTo(right.Sequence);
    }
}
