using System;

namespace Ink_Canvas.Helpers
{
    internal static class InkPacketTiming
    {
        // WPF timestamps are signed millisecond counters and wrap. Packet points
        // have no individual timestamps: distribute the observed interval evenly.
        internal static float SampleInterval(int timestamp, int? previous, int count, float fallbackRate)
        {
            int elapsed = previous.HasValue ? unchecked(timestamp - previous.Value) : 0;
            double seconds = elapsed > 0 ? elapsed / 1000.0 : 1.0 / Math.Max(1, fallbackRate);
            return (float)Math.Max(0.000001, seconds / Math.Max(1, count));
        }
    }
}
