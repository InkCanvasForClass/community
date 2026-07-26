using System;
using System.Diagnostics;

namespace Ink_Canvas.Ink.Native
{
    internal static class NativePointerTimestampConverter
    {
        public static long FromPerformanceCount(ulong performanceCount, long frequency)
        {
            if (performanceCount == 0)
                throw new ArgumentOutOfRangeException(nameof(performanceCount));
            if (frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequency));

            var unsignedFrequency = (ulong)frequency;
            var seconds = performanceCount / unsignedFrequency;
            var remainder = performanceCount % unsignedFrequency;
            return checked(
                (long)seconds * 1_000_000L
                + (long)(remainder * 1_000_000UL / unsignedFrequency));
        }

        public static long FromTickCount(
            uint messageTimeMilliseconds,
            long currentTickCountMilliseconds)
        {
            var currentLow = unchecked((uint)currentTickCountMilliseconds);
            var delta = unchecked((int)(messageTimeMilliseconds - currentLow));
            return checked((currentTickCountMilliseconds + delta) * 1_000L);
        }

        public static long FromCurrentStopwatch()
        {
            return FromPerformanceCount(
                unchecked((ulong)Stopwatch.GetTimestamp()),
                Stopwatch.Frequency);
        }
    }
}
