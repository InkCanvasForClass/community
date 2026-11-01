using System;
using System.Runtime.CompilerServices;
using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    // Version keys belong to inactive pages. Active pages must invalidate their
    // previous version before capture. Weak values release copies under pressure.
    internal sealed class InactiveStrokeSnapshotCache<TVersion> where TVersion : class
    {
        private readonly ConditionalWeakTable<TVersion, WeakReference<StrokeSaveSnapshot>> _snapshots = new();
        internal void Invalidate(TVersion version) => _snapshots.Remove(version);
        internal StrokeSaveSnapshot Capture(TVersion version, Func<StrokeCollection> source)
        {
            if (_snapshots.TryGetValue(version, out var weak) && weak.TryGetTarget(out var cached)) return cached;
            var snapshot = StrokeSaveSnapshot.Capture(source());
            _snapshots.Remove(version);
            _snapshots.Add(version, new WeakReference<StrokeSaveSnapshot>(snapshot));
            return snapshot;
        }
    }
}
