using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink
{
    // At most one scheduled work item and 256 reports. Overflow cancels the interaction
    // before accepting more input, so a lost release cannot leave an eraser captured.
    internal sealed class PalmInputMailbox<T> : IDisposable
    {
        private readonly object _sync = new();
        private readonly Queue<T> _queue = new();
        private readonly Action<Action> _schedule;
        private readonly Action<T> _consume;
        private readonly Action _reset;
        private bool _pending, _overflow, _disposed;

        internal PalmInputMailbox(Action<Action> schedule, Action<T> consume, Action reset)
        { _schedule = schedule; _consume = consume; _reset = reset; }

        internal void Post(T input)
        {
            lock (_sync)
            {
                if (_disposed) return;
                if (_queue.Count == 256) { _queue.Clear(); _overflow = true; }
                _queue.Enqueue(input);
                if (_pending) return;
                _pending = true;
            }
            try { _schedule(Drain); }
            catch { lock (_sync) { _pending = false; _queue.Clear(); _overflow = true; } throw; }
        }

        private void Drain()
        {
            // A snapshot keeps producer activity from extending this ink-thread turn forever.
            T[] batch; bool reset;
            lock (_sync)
            {
                if (_disposed) { _pending = false; return; }
                batch = _queue.ToArray(); _queue.Clear(); reset = _overflow; _overflow = false;
            }
            try
            {
                if (reset) _reset();
                foreach (var input in batch) _consume(input);
            }
            catch { _reset(); throw; }
            finally
            {
                bool again;
                lock (_sync) { again = !_disposed && _queue.Count > 0; if (!again) _pending = false; }
                if (again)
                {
                    try { _schedule(Drain); }
                    catch { lock (_sync) { _pending = false; _queue.Clear(); _overflow = true; } throw; }
                }
            }
        }

        public void Dispose() { lock (_sync) { _disposed = true; _queue.Clear(); } }
    }
}
