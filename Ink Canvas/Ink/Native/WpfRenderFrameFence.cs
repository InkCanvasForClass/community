using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// One-shot WPF CompositionTarget.Rendering fence used only for wet-to-dry handoff.
    /// Does not drive the wet-ink frame loop.
    /// </summary>
    internal sealed class WpfRenderFrameFence : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly object _sync = new object();
        private readonly Dictionary<long, Action> _pending = new Dictionary<long, Action>();
        private bool _subscribed;
        private bool _disposed;

        public WpfRenderFrameFence(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Arm(long sessionId, Action onNextFrame)
        {
            if (onNextFrame == null)
                throw new ArgumentNullException(nameof(onNextFrame));
            EnsureNotDisposed();

            lock (_sync)
            {
                _pending[sessionId] = onNextFrame;
                EnsureSubscribed_NoLock();
            }
        }

        public void Cancel(long sessionId)
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _pending.Remove(sessionId);
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
            List<Action> callbacks;
            lock (_sync)
            {
                if (_pending.Count == 0)
                {
                    Unsubscribe_NoLock();
                    return;
                }

                callbacks = new List<Action>(_pending.Count);
                foreach (var pair in _pending)
                    callbacks.Add(pair.Value);
                _pending.Clear();
                Unsubscribe_NoLock();
            }

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
            _subscribed = true;
        }

        private void Unsubscribe_NoLock()
        {
            if (!_subscribed)
                return;
            CompositionTarget.Rendering -= OnRendering;
            _subscribed = false;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WpfRenderFrameFence));
        }
    }
}
