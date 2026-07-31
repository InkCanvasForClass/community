using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class FunctionAnalysisService
    {
        private static readonly ConditionalWeakTable<FunctionObject, ConditionalWeakTable<FunctionObject, IntersectionCacheEntry>>
            IntersectionCache =
                new ConditionalWeakTable<FunctionObject, ConditionalWeakTable<FunctionObject, IntersectionCacheEntry>>();

        public static bool ShareCoordinateFrame(FunctionObject first, FunctionObject second)
        {
            if (first == null || second == null) return false;
            return Math.Abs(first.Origin.X - second.Origin.X) < 0.001 &&
                   Math.Abs(first.Origin.Y - second.Origin.Y) < 0.001 &&
                   Math.Abs(first.PixelsPerUnit - second.PixelsPerUnit) < 0.001 &&
                   Math.Abs(first.RotationDegrees - second.RotationDegrees) < 0.001;
        }

        public static FunctionAnalysisResult Analyze(FunctionObject function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));

            var sample = FunctionSamplingService.Sample(function);
            var result = new FunctionAnalysisResult();
            for (var i = 0; i < sample.Zeros.Count; i++)
                result.Zeros.Add(sample.Zeros[i]);
            for (var i = 0; i < sample.Extrema.Count; i++)
                result.Extrema.Add(sample.Extrema[i]);

            if (function.DomainMin <= 0 && function.DomainMax >= 0)
            {
                var value = MathExpressionParser.Parse(function.Expression).Evaluate(0);
                if (double.IsFinite(value))
                    result.YAxisIntercept = new MathPoint(0, value);
            }

            for (var i = 0; i < sample.Segments.Count; i++)
                AddMonotonicIntervals(sample.Segments[i], result.MonotonicIntervals);
            return result;
        }

        public static IReadOnlyList<MathPoint> FindIntersections(
            FunctionObject first,
            FunctionObject second)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            var signature = string.Join("|",
                first.Expression,
                first.DomainMin.ToString("R", CultureInfo.InvariantCulture),
                first.DomainMax.ToString("R", CultureInfo.InvariantCulture),
                second.Expression,
                second.DomainMin.ToString("R", CultureInfo.InvariantCulture),
                second.DomainMax.ToString("R", CultureInfo.InvariantCulture));
            var cache = IntersectionCache
                .GetOrCreateValue(first)
                .GetOrCreateValue(second);
            lock (cache)
            {
                if (string.Equals(cache.Signature, signature, StringComparison.Ordinal))
                    return cache.Points;
                cache.Points = FindIntersectionsCore(first, second);
                cache.Signature = signature;
                return cache.Points;
            }
        }

        private static IReadOnlyList<MathPoint> FindIntersectionsCore(
            FunctionObject first,
            FunctionObject second)
        {

            var minimum = Math.Max(first.DomainMin, second.DomainMin);
            var maximum = Math.Min(first.DomainMax, second.DomainMax);
            if (minimum >= maximum) return Array.Empty<MathPoint>();

            var firstExpression = MathExpressionParser.Parse(first.Expression);
            var secondExpression = MathExpressionParser.Parse(second.Expression);
            var result = new List<MathPoint>();
            const int Intervals = 512;
            var previousX = minimum;
            var previousDifference = Difference(firstExpression, secondExpression, previousX);

            for (var i = 1; i <= Intervals; i++)
            {
                var currentX = minimum + (maximum - minimum) * i / Intervals;
                var currentDifference = Difference(firstExpression, secondExpression, currentX);
                if (double.IsFinite(previousDifference) && double.IsFinite(currentDifference))
                {
                    if (Math.Abs(previousDifference) < 1e-8)
                        AddDistinct(result, new MathPoint(previousX, firstExpression.Evaluate(previousX)));
                    else if (previousDifference * currentDifference < 0)
                    {
                        var root = Bisect(
                            firstExpression,
                            secondExpression,
                            previousX,
                            currentX,
                            previousDifference);
                        var y = firstExpression.Evaluate(root);
                        if (double.IsFinite(y))
                            AddDistinct(result, new MathPoint(root, y));
                    }
                }
                previousX = currentX;
                previousDifference = currentDifference;
            }

            return result;
        }

        private sealed class IntersectionCacheEntry
        {
            public string Signature { get; set; }

            public IReadOnlyList<MathPoint> Points { get; set; } = Array.Empty<MathPoint>();
        }

        private static double Bisect(
            MathExpression first,
            MathExpression second,
            double minimum,
            double maximum,
            double minimumDifference)
        {
            for (var i = 0; i < 48; i++)
            {
                var midpoint = (minimum + maximum) / 2;
                var midpointDifference = Difference(first, second, midpoint);
                if (!double.IsFinite(midpointDifference)) return midpoint;
                if (Math.Abs(midpointDifference) < 1e-10) return midpoint;
                if (minimumDifference * midpointDifference <= 0)
                {
                    maximum = midpoint;
                }
                else
                {
                    minimum = midpoint;
                    minimumDifference = midpointDifference;
                }
            }
            return (minimum + maximum) / 2;
        }

        private static double Difference(MathExpression first, MathExpression second, double x)
        {
            var firstValue = first.Evaluate(x);
            var secondValue = second.Evaluate(x);
            return double.IsFinite(firstValue) && double.IsFinite(secondValue)
                ? firstValue - secondValue
                : double.NaN;
        }

        private static void AddDistinct(ICollection<MathPoint> points, MathPoint candidate)
        {
            foreach (var point in points)
            {
                if (Math.Abs(point.X - candidate.X) < 0.001)
                    return;
            }
            points.Add(candidate);
        }

        private static void AddMonotonicIntervals(
            IReadOnlyList<MathPoint> points,
            ICollection<FunctionMonotonicInterval> intervals)
        {
            if (points == null || points.Count < 2) return;

            FunctionMonotonicity? current = null;
            var start = points[0].X;
            for (var i = 1; i < points.Count; i++)
            {
                var delta = points[i].Y - points[i - 1].Y;
                if (Math.Abs(delta) <= 1e-9) continue;
                var next = delta > 0
                    ? FunctionMonotonicity.Increasing
                    : FunctionMonotonicity.Decreasing;
                if (!current.HasValue)
                {
                    current = next;
                    start = points[i - 1].X;
                    continue;
                }
                if (current.Value == next) continue;

                AddInterval(intervals, start, points[i - 1].X, current.Value);
                start = points[i - 1].X;
                current = next;
            }

            if (current.HasValue)
                AddInterval(intervals, start, points[points.Count - 1].X, current.Value);
        }

        private static void AddInterval(
            ICollection<FunctionMonotonicInterval> intervals,
            double start,
            double end,
            FunctionMonotonicity monotonicity)
        {
            if (end - start <= 1e-6) return;
            intervals.Add(new FunctionMonotonicInterval
            {
                Start = start,
                End = end,
                Monotonicity = monotonicity
            });
        }
    }
}
