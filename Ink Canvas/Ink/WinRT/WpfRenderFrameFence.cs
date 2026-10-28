using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Multi-frame WPF CompositionTarget.Rendering fence used for the wet-to-dry handoff.
    /// A single Rendering callback is not enough: the first event often fires before the
    /// newly-added dry Stroke is actually composited by DWM. Waiting a few frames ensures
    /// dry ink is on screen before EndDry tells the OS to remove the wet stroke.
    /// </summary>
    internal sealed class WpfRenderFrameFence : IDisposable
    {
        private const int FramesToWait = 5;

        private readonly Dispatcher _dispatcher;
        private readonly object _sync = new object();
        private readonly Dictionary<long, PendingFence> _pending = new Dictionary<long, PendingFence>();
        private bool _subscribed;
        private bool _disposed;
        private readonly DispatcherTimer _deadlineTimer;
        private TimeSpan _lastRenderingTime = TimeSpan.MinValue;

        public WpfRenderFrameFence(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _deadlineTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background,
                OnDeadline, dispatcher);
            _deadlineTimer.Stop();
        }

        public void Arm(long key, Action onNextFrame)
        {
            if (onNextFrame == null)
                throw new ArgumentNullException(nameof(onNextFrame));
            EnsureNotDisposed();

            lock (_sync)
            {
                _pending[key] = new PendingFence(onNextFrame, FramesToWait);
                EnsureSubscribed_NoLock();
            }
        }

        public void Cancel(long key)
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _pending.Remove(key);
                if (_pending.Count == 0)
                    Unsubscribe_NoLock();
            }
        }

        public void CancelAll()
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _pending.Clear();
                Unsubscribe_NoLock();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            lock (_sync)
            {
                _pending.Clear();
                Unsubscribe_NoLock();
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (e is RenderingEventArgs rendering)
            {
                if (_lastRenderingTime == rendering.RenderingTime) return;
                _lastRenderingTime = rendering.RenderingTime;
            }
            List<Action> callbacks = null;
            lock (_sync)
            {
                if (_pending.Count == 0)
                {
                    Unsubscribe_NoLock();
                    return;
                }

                List<long> completed = null;
                foreach (var pair in _pending)
                {
                    pair.Value.RemainingFrames--;
                    if (pair.Value.RemainingFrames > 0)
                        continue;

                    if (callbacks == null)
                        callbacks = new List<Action>();
                    callbacks.Add(pair.Value.Callback);
                    if (completed == null)
                        completed = new List<long>();
                    completed.Add(pair.Key);
                }

                if (completed != null)
                {
                    for (var i = 0; i < completed.Count; i++)
                        _pending.Remove(completed[i]);
                }

                if (_pending.Count == 0)
                    Unsubscribe_NoLock();
            }

            if (callbacks == null)
                return;

            for (var i = 0; i < callbacks.Count; i++)
            {
                try { callbacks[i](); }
                catch
                {
                    // callers own their error handling
                }
            }
        }

        private void EnsureSubscribed_NoLock()
        {
            if (_subscribed)
                return;
            CompositionTarget.Rendering += OnRendering;
            _deadlineTimer.Start();
            _subscribed = true;
        }

        private void Unsubscribe_NoLock()
        {
            if (!_subscribed)
                return;
            CompositionTarget.Rendering -= OnRendering;
            _deadlineTimer.Stop();
            _subscribed = false;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WpfRenderFrameFence));
        }

        private void OnDeadline(object sender, EventArgs e)
        {
            var callbacks = new List<Action>();
            lock (_sync)
            {
                var expired = new List<long>();
                foreach (var pair in _pending)
                    if (DateTime.UtcNow >= pair.Value.DeadlineUtc)
                    { expired.Add(pair.Key); callbacks.Add(pair.Value.Callback); }
                foreach (var key in expired) _pending.Remove(key);
                if (_pending.Count == 0) Unsubscribe_NoLock();
            }
            foreach (var callback in callbacks)
            {
                try { callback(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }

        private sealed class PendingFence
        {
            public PendingFence(Action callback, int remainingFrames)
            {
                Callback = callback;
                RemainingFrames = remainingFrames;
            }

            public Action Callback { get; }
            public DateTime DeadlineUtc { get; } = DateTime.UtcNow.AddMilliseconds(750);
            public int RemainingFrames { get; set; }
        }
    }
}
