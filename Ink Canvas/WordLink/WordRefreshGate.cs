using System;

namespace Ink_Canvas.WordLink
{
    /// <summary>Bound live scroll queries without postponing them until scrolling stops.</summary>
    internal sealed class WordRefreshGate
    {
        private long _nextQuery, _scrollQuietUntil;
        private bool _scrollPending;
        private long _scrollCost;
        private bool _probedAfterDiscovery;
        internal void Scrolled(long now)
        {
            if (!_scrollPending) _nextQuery = Math.Min(_nextQuery, now + 16);
            _scrollPending = true; _scrollQuietUntil = now + 120;
        }
        internal bool IsScrolling(long now) => _scrollPending && now < _scrollQuietUntil;
        internal void Immediate() { _nextQuery = 0; _scrollPending = false; _scrollCost = 0; _probedAfterDiscovery = false; }
        internal void InputChanged(long now) => _nextQuery = Math.Min(_nextQuery, now + 16);
        internal void RetryAfter(long now, int delay) => _nextQuery = now + delay;
        // Throttling during motion must never delay the final settled view.
        internal int Delay(long now) => (int)Math.Clamp(
            (_scrollPending ? Math.Min(_nextQuery, _scrollQuietUntil) : _nextQuery) - now, 0, 2000);
        internal void Complete(long now, long elapsed, bool moved, bool? cachedGeometry = null)
        {
            // A slow query must not immediately start another slow query. Hints
            // emitted by Office itself cannot bypass this cooldown.
            if (IsScrolling(now))
            {
                // A successful discovery has just created cheap anchors. Probe
                // them once promptly instead of inheriting its pagination cost.
                // Consecutive discoveries still back off on slow documents.
                if (cachedGeometry == false && !_probedAfterDiscovery)
                {
                    _probedAfterDiscovery = true; _scrollCost = 0; _nextQuery = now + 16; return;
                }
                if (cachedGeometry == true)
                {
                    if (_probedAfterDiscovery) _scrollCost = 0;
                    _probedAfterDiscovery = false;
                }
                // Office serializes these calls with input/layout on its UI
                // thread. Remember slow calls across frames, rather than jumping
                // back to a high frequency after one unusually fast response.
                _scrollCost = Math.Max(Math.Clamp(elapsed, 0, 500), _scrollCost * 3 / 4);
                // Scale continuously: a small timing fluctuation must not turn
                // a smooth stream into a long pause. Reserve twice the recent
                // query cost for Word input/rendering, with a 16ms minimum cycle.
                _nextQuery = now + Math.Max(8, Math.Max(16 - _scrollCost, _scrollCost * 2));
            }
            else
            {
                _scrollPending = false;
                _scrollCost = 0;
                _probedAfterDiscovery = false;
                _nextQuery = now + Math.Max(moved ? 80 : 750, Math.Clamp(elapsed, 0, 500) * 3);
            }
        }
    }
}
