using System;

namespace Ink_Canvas.Ink
{
    internal sealed class PalmEraserContactTracker
    {
        private const int HistoryCapacity = 5;
        private const double MinimumContact = 0.5;
        private const double MinimumDimension = 1;
        private const double MaximumDimension = 4096;
        private readonly double[] _widths = new double[HistoryCapacity];
        private readonly double[] _heights = new double[HistoryCapacity];
        private readonly double[] _sorted = new double[HistoryCapacity];
        private int _count;
        private int _next;
        private double _width;
        private double _height;

        public bool IsPalm { get; private set; }

        public void Reset()
        {
            _count = 0;
            _next = 0;
            _width = 0;
            _height = 0;
            IsPalm = false;
        }

        // True means the contact remains a palm, even when its dimensions did not change.
        public bool Update(
            double contactWidth, double contactHeight,
            double thresholdWidth, double sizeFactor, bool ellipse,
            out double width, out double height)
        {
            width = _width;
            height = _height;
            if (!IsValid(contactWidth, contactHeight, thresholdWidth, sizeFactor))
                return IsPalm;

            _widths[_next] = contactWidth;
            _heights[_next] = contactHeight;
            _next = (_next + 1) % HistoryCapacity;
            _count = Math.Min(_count + 1, HistoryCapacity);
            var medianWidth = Median(_widths);
            var medianHeight = Median(_heights);
            if (!IsPalm && !ExceedsThreshold(medianWidth, medianHeight, thresholdWidth))
                return false;

            GetSize(medianWidth, medianHeight, sizeFactor, ellipse, out width, out height);
            if (IsPalm)
            {
                if (Math.Abs(width - _width) <= Math.Max(1, _width * 0.03))
                    width = _width;
                if (Math.Abs(height - _height) <= Math.Max(1, _height * 0.03))
                    height = _height;
            }

            _width = width;
            _height = height;
            IsPalm = true;
            return true;
        }

        public static bool TryGetEraserSize(
            double contactWidth, double contactHeight,
            double thresholdWidth, double sizeFactor, bool ellipse,
            out double width, out double height)
        {
            width = 0;
            height = 0;
            if (!IsValid(contactWidth, contactHeight, thresholdWidth, sizeFactor)
                || !ExceedsThreshold(contactWidth, contactHeight, thresholdWidth))
                return false;

            GetSize(contactWidth, contactHeight, sizeFactor, ellipse, out width, out height);
            return true;
        }

        private double Median(double[] values)
        {
            Array.Copy(values, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            var middle = _count / 2;
            return _count % 2 == 0
                ? _sorted[middle - 1] + (_sorted[middle] - _sorted[middle - 1]) / 2
                : _sorted[middle];
        }

        private static bool IsValid(double width, double height, double threshold, double factor)
        {
            return double.IsFinite(width) && width >= MinimumContact
                   && double.IsFinite(height) && height >= MinimumContact
                   && double.IsFinite(threshold) && threshold > 0
                   && double.IsFinite(factor) && factor > 0;
        }

        private static bool ExceedsThreshold(double width, double height, double threshold)
        {
            return Math.Sqrt(width) * Math.Sqrt(height) > threshold;
        }

        private static void GetSize(
            double contactWidth, double contactHeight, double sizeFactor, bool ellipse,
            out double width, out double height)
        {
            // Scaling both axes gives contact area * sizeFactor squared. Ellipses need
            // the extra correction because their area is pi/4 of the bounding rectangle.
            var largest = Math.Max(contactWidth, contactHeight);
            var ratio = Math.Min(contactWidth, contactHeight) / largest;
            var scaled = largest * sizeFactor;
            if (ellipse)
                scaled *= Math.Sqrt(4 / Math.PI);

            // Keep the aspect ratio when both axes fit. More extreme ratios cannot fit
            // inside 1..4096, so only then saturate the smaller axis independently.
            var minimumLargest = Math.Min(MaximumDimension, MinimumDimension / ratio);
            var boundedLargest = Math.Clamp(scaled, minimumLargest, MaximumDimension);
            var boundedSmallest = Math.Clamp(boundedLargest * ratio, MinimumDimension, MaximumDimension);
            width = contactWidth >= contactHeight ? boundedLargest : boundedSmallest;
            height = contactWidth >= contactHeight ? boundedSmallest : boundedLargest;
        }
    }
}
