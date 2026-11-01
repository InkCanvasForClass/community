using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    internal static class StrokePressure
    {
        // Replace at most once so render/history observers see one complete update.
        // Clone the collection to preserve its point description (including tilt).
        internal static void Normalize(Stroke stroke)
        {
            var source = stroke.StylusPoints;
            int first = 0;
            while (first < source.Count && source[first].PressureFactor == 0.5f) first++;
            if (first == source.Count) return;

            var normalized = source.Clone();
            for (int i = first; i < normalized.Count; i++)
            {
                var point = normalized[i];
                if (point.PressureFactor == 0.5f) continue;
                point.PressureFactor = 0.5f;
                normalized[i] = point;
            }
            stroke.StylusPoints = normalized;
        }
    }
}
