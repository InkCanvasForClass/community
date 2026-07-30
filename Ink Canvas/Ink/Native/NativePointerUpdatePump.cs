using System;
using System.Collections.Generic;
using System.Threading;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class NativePointerUpdatePump : IDisposable
    {
        private sealed class PendingUpdate
        {
            public PendingUpdate(
                uint pointerId,
                long sessionId,
                RawInkSample[] samplesNewestFirst,
                bool predictionEnabled)
            {
                PointerId = pointerId;
                SessionId = sessionId;
                SamplesNewestFirst = samplesNewestFirst ?? throw new ArgumentNullException(nameof(samplesNewestFirst));
                PredictionEnabled = predictionEnabled;
            }

            public uint PointerId { get; }
            public long SessionId { get; }
            public RawInkSample[] SamplesNewestFirst { get; }
            public bool PredictionEnabled { get; }
        }

        private readonly NativeInkController _controller;
        private readonly Action _signalWork;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _idleEvent = new ManualResetEventSlim(true);
        private readonly Dictionary<uint, PendingUpdate> _pendingUpdates = new Dictionary<uint, PendingUpdate>();
        private readonly Thread _workerThread;
        private readonly object _syncRoot = new object();

        private bool _shutdownRequested;
        private bool _disposed;
        private bool _hasActiveUpdate;
        private uint _activePointerId;

        public NativePointerUpdatePump(NativeInkController controller, Action signalWork)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _signalWork = signalWork ?? throw new ArgumentNullException(nameof(signalWork));
            _workerThread = new Thread(WorkerMain)
            {
                IsBackground = true,
                Name = "ICC-NativePointerUpdatePump"
            };
            _workerThread.Start();
        }

        public void Enqueue(
            uint pointerId,
            long sessionId,
            RawInkSample[] samplesNewestFirst,
            bool predictionEnabled)
        {
            if (samplesNewestFirst == null)
                throw new ArgumentNullException(nameof(samplesNewestFirst));
            if (samplesNewestFirst.Length == 0)
                return;

            lock (_syncRoot)
            {
                ThrowIfDisposed();
                _pendingUpdates[pointerId] = new PendingUpdate(
                    pointerId,
                    sessionId,
                    samplesNewestFirst,
                    predictionEnabled);
                _idleEvent.Reset();
            }

            _workEvent.Set();
        }

        public void FlushPointer(uint pointerId)
        {
            SpinWaitUntil(() =>
            {
                lock (_syncRoot)
                {
                    return !_pendingUpdates.ContainsKey(pointerId)
                           && !(_hasActiveUpdate && _activePointerId == pointerId);
                }
            });
        }

        public void DiscardPointer(uint pointerId)
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _pendingUpdates.Remove(pointerId);
                if (_pendingUpdates.Count == 0 && !_hasActiveUpdate)
                    _idleEvent.Set();
            }
        }

        public void DiscardAll()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _pendingUpdates.Clear();
                if (!_hasActiveUpdate)
                    _idleEvent.Set();
            }
        }

        public void FlushAll()
        {
            SpinWaitUntil(() =>
            {
                lock (_syncRoot)
                {
                    return _pendingUpdates.Count == 0 && !_hasActiveUpdate;
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            lock (_syncRoot)
            {
                _shutdownRequested = true;
                _pendingUpdates.Clear();
            }

            _workEvent.Set();
            if (_workerThread.IsAlive)
                _workerThread.Join(TimeSpan.FromSeconds(3));
            _workEvent.Dispose();
            _idleEvent.Dispose();
        }

        private void WorkerMain()
        {
            while (true)
            {
                _workEvent.WaitOne();

                while (true)
                {
                    PendingUpdate pending;
                    lock (_syncRoot)
                    {
                        if (_shutdownRequested)
                            return;
                        if (!TryTakeOnePendingUpdate(out pending))
                        {
                            if (_pendingUpdates.Count == 0 && !_hasActiveUpdate)
                                _idleEvent.Set();
                            break;
                        }
                        _hasActiveUpdate = true;
                        _activePointerId = pending.PointerId;
                    }

                    try
                    {
                        if (_controller.TryUpdateSessionWithPrediction(
                                pending.PointerId,
                                pending.SessionId,
                                pending.SamplesNewestFirst,
                                pending.PredictionEnabled))
                        {
                            _signalWork();
                        }
                    }
                    catch
                    {
                        // UI thread owns user-visible failure handling around pointer input.
                        // Background update failures are best-effort suppressed here; the
                        // next foreground boundary (cancel/end/device reset) will reconcile.
                    }
                    finally
                    {
                        lock (_syncRoot)
                        {
                            _hasActiveUpdate = false;
                            _activePointerId = 0;
                            if (_pendingUpdates.Count == 0)
                                _idleEvent.Set();
                        }
                    }
                }
            }
        }

        private bool TryTakeOnePendingUpdate(out PendingUpdate pending)
        {
            uint key;
            using (var enumerator = _pendingUpdates.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    pending = null;
                    return false;
                }

                var pair = enumerator.Current;
                key = pair.Key;
                pending = pair.Value;
            }

            _pendingUpdates.Remove(key);
            return true;
        }

        private void SpinWaitUntil(Func<bool> predicate)
        {
            ThrowIfDisposed();
            while (!predicate())
            {
                if (_shutdownRequested)
                    return;
                _idleEvent.Wait(5);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NativePointerUpdatePump));
        }
    }
}
