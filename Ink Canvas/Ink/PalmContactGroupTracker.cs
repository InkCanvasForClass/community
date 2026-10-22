using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Ink
{
    // Position-only palm grouping: contact rectangles and pressure are deliberately not required.
    internal sealed class PalmContactGroupTracker
    {
        private sealed class Contact
        {
            public long Down;
            public double X;
            public double Y;
            public bool Decided;
            public bool Palm;
            public readonly Queue<(double X, double Y)> History = new();
        }

        private readonly Dictionary<long, Contact> _contacts = new();
        public int MinimumContacts { get; set; } = 3;
        public int WindowMilliseconds { get; set; } = 200;
        public double Width { get; set; }
        public double Height { get; set; }
        public double Tolerance { get; set; } = 1;
        public int Count => _contacts.Count;
        public IEnumerable<long> PalmIds => _contacts.Where(p => p.Value.Palm).Select(p => p.Key);
        public bool IsPalm(long id) => _contacts.TryGetValue(id, out var contact) && contact.Palm;
        public bool IsDecided(long id) => _contacts.TryGetValue(id, out var contact) && contact.Decided;

        public void Down(long id, double x, double y, long milliseconds)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || _contacts.ContainsKey(id)) return;
            _contacts[id] = new Contact { Down = milliseconds };
            Move(id, x, y);
        }

        public void Move(long id, double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || !_contacts.TryGetValue(id, out var contact)) return;
            contact.X = x;
            contact.Y = y;
            if (contact.History.Count == 5) contact.History.Dequeue();
            contact.History.Enqueue((x, y));
        }

        public void Up(long id) => _contacts.Remove(id);
        public void Reset() => _contacts.Clear();

        public void Evaluate(long milliseconds)
        {
            if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width <= 0 || Height <= 0
                || !double.IsFinite(Tolerance) || Tolerance <= 0) return;
            int window = Math.Clamp(WindowMilliseconds, 0, 2000);
            int minimum = Math.Clamp(MinimumContacts, 2, 5);
            foreach (var anchor in _contacts.Values.OrderBy(c => c.Down))
            {
                if (anchor.Decided || milliseconds - anchor.Down < window) continue;
                // Already decided contacts cannot be recruited to overturn an earlier decision.
                var group = _contacts.Values.Where(c => !c.Decided && Math.Abs(c.Down - anchor.Down) <= window).ToArray();
                if (group.Length < minimum) continue;
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var contact in group)
                    foreach (var point in contact.History)
                    {
                        minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                        minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
                    }
                bool palm = maxX - minX <= Width * Tolerance && maxY - minY <= Height * Tolerance;
                foreach (var contact in group) { contact.Decided = true; contact.Palm = palm; }
            }
        }

        public bool TryGetCenter(out double x, out double y)
        {
            x = y = 0;
            int count = 0;
            foreach (var contact in _contacts.Values)
            {
                if (!contact.Palm) continue;
                x += contact.X; y += contact.Y; count++;
            }
            if (count == 0) return false;
            x /= count; y /= count;
            return true;
        }
    }
}
