using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink
{
    // Retain the applied pointer-down style through delayed drying and tool changes.
    internal sealed class StrokeSessionTracker<T> where T : class
    {
        private readonly object _sync = new();
        private readonly Dictionary<uint, (T State, long Time)> _active = new();
        private readonly Func<long> _clock;
        internal StrokeSessionTracker(Func<long> clock = null) => _clock = clock ?? (() => Environment.TickCount64);
        internal void Begin(uint id, T state)
        {
            if (state == null) return;
            lock (_sync)
            {
                if (_active.Count >= 128 && !_active.ContainsKey(id))
                {
                    uint oldestId = 0; long oldest = long.MaxValue;
                    foreach (var pair in _active)
                        if (pair.Value.Time < oldest) { oldest = pair.Value.Time; oldestId = pair.Key; }
                    _active.Remove(oldestId);
                }
                _active[id] = (state, _clock());
            }
        }
        internal (T State, long DurationMs) End(uint id)
        {
            lock (_sync)
                return _active.Remove(id, out var start) ? (start.State, Math.Max(0, _clock() - start.Time)) : default;
        }
        internal void Cancel(uint id) { lock (_sync) _active.Remove(id); }
        internal void Clear() { lock (_sync) _active.Clear(); }
    }
}
