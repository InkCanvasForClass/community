using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// Multi-frame WPF CompositionTarget.Rendering fence used only for wet-to-dry handoff.
    /// Does not drive the wet-ink frame loop.
    ///
    /// A single Rendering callback is not enough: the first event often fires before the
    /// newly-added dry Stroke is actually composited by DWM. Waiting a second frame
    /// ensures dry ink is on screen before the wet overlay is retired / moved off-screen.
    /// </summary>
    internal sealed class WpfRenderFrameFence : IDisposable
    {
        // 5 帧：首帧常在干墨合成前触发；第二帧多数已就绪；第三帧覆盖高负载/多屏 DWM 延迟。
        private const int FramesToWait = 5;

        private readonly Dispatcher _dispatcher;
        private readonly object _sync = new object();
        private readonly Dictionary<long, PendingFence> _pending = new Dictionary<long, PendingFence>();
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
                _pending[sessionId] = new PendingFence(onNextFrame, FramesToWait);
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

        private sealed class PendingFence
        {
            public PendingFence(Action callback, int remainingFrames)
            {
                Callback = callback;
                RemainingFrames = remainingFrames;
            }

            public Action Callback { get; }
            public int RemainingFrames { get; set; }
        }
    }
}
