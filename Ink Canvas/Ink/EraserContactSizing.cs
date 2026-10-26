using System;

namespace Ink_Canvas.Ink
{
    internal static class EraserSizeCalculator
    {
        private const double BaseEraserSizeDip = 90;
        private const double RectangleWidthFactor = 0.6;
        private const int LargestPreset = 4;

        public static double GetPresetWidthDip(int preset, bool isCircle)
        {
            double scale;
            switch (preset)
            {
                case 0:
                    scale = isCircle ? 0.5 : 0.7;
                    break;
                case 1:
                    scale = isCircle ? 0.8 : 0.9;
                    break;
                case 3:
                    scale = isCircle ? 1.25 : 1.2;
                    break;
                case 4:
                    scale = isCircle ? 1.5 : 1.3;
                    break;
                default:
                    scale = 1;
                    break;
            }

            return scale * BaseEraserSizeDip * (isCircle ? 1 : RectangleWidthFactor);
        }

        public static double GetMaximumPresetWidthDip(bool isCircle)
        {
            var maximum = 0d;
            for (var preset = 0; preset <= LargestPreset; preset++)
                maximum = Math.Max(maximum, GetPresetWidthDip(preset, isCircle));
            return maximum;
        }
    }

    internal readonly struct PalmEraserPolicy
    {
        public PalmEraserPolicy(
            bool enabled,
            bool isActive,
            bool isQuadIr,
            bool isSpecialScreen,
            double boundsWidthDip,
            double thresholdFactor,
            double sensitivityMultiplier,
            double eraserSizeFactor,
            double touchMultiplier,
            double maximumEraserWidthDip)
        {
            Enabled = enabled;
            IsActive = isActive;
            IsQuadIr = isQuadIr;
            IsSpecialScreen = isSpecialScreen;
            BoundsWidthDip = boundsWidthDip;
            ThresholdFactor = thresholdFactor;
            SensitivityMultiplier = sensitivityMultiplier;
            EraserSizeFactor = eraserSizeFactor;
            TouchMultiplier = touchMultiplier;
            MaximumEraserWidthDip = maximumEraserWidthDip;
        }

        public bool Enabled { get; }
        public bool IsActive { get; }
        public bool IsQuadIr { get; }
        public bool IsSpecialScreen { get; }
        public double BoundsWidthDip { get; }
        public double ThresholdFactor { get; }
        public double SensitivityMultiplier { get; }
        public double EraserSizeFactor { get; }
        public double TouchMultiplier { get; }
        public double MaximumEraserWidthDip { get; }
    }

    internal readonly struct PalmEraserEvaluation
    {
        public PalmEraserEvaluation(
            bool routesToEraser,
            bool activatesEraser,
            double recognitionMetricDip,
            double eraserWidthDip)
        {
            RoutesToEraser = routesToEraser;
            ActivatesEraser = activatesEraser;
            RecognitionMetricDip = recognitionMetricDip;
            EraserWidthDip = eraserWidthDip;
        }

        public bool RoutesToEraser { get; }
        public bool ActivatesEraser { get; }
        public double RecognitionMetricDip { get; }
        public double EraserWidthDip { get; }
    }

    internal static class PalmEraserCalculator
    {
        public static double GetSensitivityMultiplier(int sensitivity)
        {
            switch (sensitivity)
            {
                case 0:
                    return 3;
                case 1:
                    return 2.5;
                default:
                    return 2;
            }
        }

        public static PalmEraserEvaluation Evaluate(
            double contactWidthDip,
            double contactHeightDip,
            PalmEraserPolicy policy)
        {
            if (!policy.Enabled)
                return default;
            if (policy.IsActive)
                return new PalmEraserEvaluation(true, false, 0, 0);
            if (!IsPositiveFinite(contactWidthDip)
                || policy.IsQuadIr && !IsPositiveFinite(contactHeightDip)
                || !IsPositiveFinite(policy.BoundsWidthDip)
                || !IsNonNegativeFinite(policy.ThresholdFactor)
                || !IsNonNegativeFinite(policy.SensitivityMultiplier)
                || !IsPositiveFinite(policy.EraserSizeFactor)
                || !IsPositiveFinite(policy.MaximumEraserWidthDip))
            {
                return default;
            }

            var recognitionMetricDip = policy.IsQuadIr
                ? GeometricMean(contactWidthDip, contactHeightDip)
                : contactWidthDip;
            if (!IsPositiveFinite(recognitionMetricDip))
                return default;

            var threshold = policy.BoundsWidthDip;
            if (!TryMultiply(threshold, policy.ThresholdFactor, out threshold)
                || !TryMultiply(threshold, policy.SensitivityMultiplier, out threshold)
                || recognitionMetricDip <= policy.BoundsWidthDip
                || recognitionMetricDip <= threshold)
            {
                return default;
            }

            var sizeScale = policy.EraserSizeFactor;
            if (policy.IsSpecialScreen)
            {
                if (!IsPositiveFinite(policy.TouchMultiplier))
                    return default;
                if (!TryMultiply(sizeScale, policy.TouchMultiplier, out sizeScale))
                {
                    return new PalmEraserEvaluation(
                        true,
                        true,
                        recognitionMetricDip,
                        policy.MaximumEraserWidthDip);
                }
            }

            if (!IsPositiveFinite(sizeScale))
                return default;

            var maximum = policy.MaximumEraserWidthDip;
            var eraserWidthDip = recognitionMetricDip >= maximum / sizeScale
                ? maximum
                : recognitionMetricDip * sizeScale;
            if (!IsPositiveFinite(eraserWidthDip))
                return default;

            return new PalmEraserEvaluation(
                true,
                true,
                recognitionMetricDip,
                Math.Min(eraserWidthDip, maximum));
        }

        private static double GeometricMean(double first, double second)
        {
            var maximum = Math.Max(first, second);
            var minimum = Math.Min(first, second);
            return maximum * Math.Sqrt(minimum / maximum);
        }

        private static bool TryMultiply(double first, double second, out double result)
        {
            result = first * second;
            return IsNonNegativeFinite(result);
        }

        private static bool IsPositiveFinite(double value) =>
            value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}