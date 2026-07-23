using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal static class InkSampleHistoryNormalizer
    {
        public static List<RawInkSample> NormalizeReverseChronological(
            IReadOnlyList<RawInkSample> newestFirst,
            long lastAcceptedTimestampMicroseconds,
            uint lastAcceptedFrameId)
        {
            var result = new List<RawInkSample>(newestFirst?.Count ?? 0);
            if (newestFirst == null || newestFirst.Count == 0)
                return result;

            for (var i = newestFirst.Count - 1; i >= 0; i--)
            {
                var sample = newestFirst[i];
                if (IsBeforeOrEqual(sample, lastAcceptedTimestampMicroseconds, lastAcceptedFrameId))
                    continue;
                if (result.Count != 0 && IsDuplicate(result[result.Count - 1], sample))
                    continue;
                result.Add(sample);
            }

            result.Sort(CompareChronologically);
            RemoveAdjacentDuplicates(result);
            return result;
        }

        private static bool IsBeforeOrEqual(RawInkSample sample, long timestampMicroseconds, uint frameId)
        {
            if (sample.TimestampMicroseconds < timestampMicroseconds)
                return true;
            return sample.TimestampMicroseconds == timestampMicroseconds && sample.FrameId <= frameId;
        }

        private static int CompareChronologically(RawInkSample left, RawInkSample right)
        {
            var timestampComparison = left.TimestampMicroseconds.CompareTo(right.TimestampMicroseconds);
            return timestampComparison != 0 ? timestampComparison : left.FrameId.CompareTo(right.FrameId);
        }

        private static bool IsDuplicate(RawInkSample left, RawInkSample right)
        {
            if (left.PointerId != right.PointerId)
                return false;
            if (left.TimestampMicroseconds == right.TimestampMicroseconds && left.FrameId == right.FrameId)
                return true;
            return left.TimestampMicroseconds == right.TimestampMicroseconds
                   && Math.Abs(left.X - right.X) < 0.0001
                   && Math.Abs(left.Y - right.Y) < 0.0001;
        }

        private static void RemoveAdjacentDuplicates(List<RawInkSample> samples)
        {
            for (var i = samples.Count - 1; i > 0; i--)
            {
                if (IsDuplicate(samples[i - 1], samples[i]))
                    samples.RemoveAt(i);
            }
        }
    }
}
