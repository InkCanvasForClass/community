using System;
using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    // A worker result is valid only while the source geometry/style is unchanged.
    // Observe invalidation instead of copying or comparing the whole point set.
    internal sealed class StrokeProcessingGuard : IDisposable
    {
        private Stroke _source;
        internal bool HasChanged { get; private set; }

        internal StrokeProcessingGuard(Stroke source)
        {
            _source = source;
            source.Invalidated += OnInvalidated;
        }

        private void OnInvalidated(object sender, EventArgs e) => HasChanged = true;

        public void Dispose()
        {
            if (_source == null) return;
            _source.Invalidated -= OnInvalidated;
            _source = null;
        }
    }
}
