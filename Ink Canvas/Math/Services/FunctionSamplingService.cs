using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class FunctionSamplingService
    {
        public const int MaximumSamples = 4096;
        private static readonly ConditionalWeakTable<FunctionObject, SampleCacheEntry> SampleCache =
            new ConditionalWeakTable<FunctionObject, SampleCacheEntry>();

        public static FunctionSample Sample(FunctionObject function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            var cache = SampleCache.GetOrCreateValue(function);
            lock (cache)
            {
                if (cache.Matches(function))
                    return cache.Sample;

                var sample = SampleCore(function);
                cache.Update(function, sample);
                return sample;
            }
        }

        private static FunctionSample SampleCore(FunctionObject function)
        {
            var expression = MathExpressionParser.Parse(function.Expression);
            var result = new FunctionSample();
            var quality = Math.Max(1, Math.Min(4, function.SampleQuality));
            var baseIntervals = 32 * quality;
            var points = new List<MathPoint>();
            var count = 0;

            for (var i = 0; i < baseIntervals && count < MaximumSamples; i++)
            {
                var x0 = Lerp(function.DomainMin, function.DomainMax, (double)i / baseIntervals);
                var x1 = Lerp(function.DomainMin, function.DomainMax, (double)(i + 1) / baseIntervals);
                AppendAdaptive(expression, x0, x1, 0, quality + 3, points, result, ref count);
            }

            Flush(points, result);
            FindMarkers(result);
            return result;
        }

        private sealed class SampleCacheEntry
        {
            private string _expression;
            private double _domainMin;
            private double _domainMax;
            private int _sampleQuality;

            public FunctionSample Sample { get; private set; }

            public bool Matches(FunctionObject function)
            {
                return Sample != null &&
                       string.Equals(_expression, function.Expression, StringComparison.Ordinal) &&
                       _domainMin.Equals(function.DomainMin) &&
                       _domainMax.Equals(function.DomainMax) &&
                       _sampleQuality == function.SampleQuality;
            }

            public void Update(FunctionObject function, FunctionSample sample)
            {
                _expression = function.Expression;
                _domainMin = function.DomainMin;
                _domainMax = function.DomainMax;
                _sampleQuality = function.SampleQuality;
                Sample = sample;
            }
        }

        private static void AppendAdaptive(
            MathExpression expression,
            double x0,
            double x1,
            int depth,
            int maximumDepth,
            List<MathPoint> current,
            FunctionSample result,
            ref int count)
        {
            if (count >= MaximumSamples) return;
            var y0 = expression.Evaluate(x0);
            var y1 = expression.Evaluate(x1);
            var midpointX = (x0 + x1) / 2;
            var midpointY = expression.Evaluate(midpointX);
            var defined = double.IsFinite(y0) && double.IsFinite(y1) && double.IsFinite(midpointY);
            var jump = defined &&
                       (Math.Abs(y1 - y0) > 1000 ||
                        (Math.Abs(y0) > 50 &&
                         Math.Abs(y1) > 50 &&
                         Math.Sign(y0) != Math.Sign(y1)));
            var deviation = defined ? Math.Abs(midpointY - (y0 + y1) / 2) : double.PositiveInfinity;

            if (depth < maximumDepth && (deviation > 0.02 * (1 + Math.Abs(midpointY)) || jump))
            {
                AppendAdaptive(expression, x0, midpointX, depth + 1, maximumDepth, current, result, ref count);
                AppendAdaptive(expression, midpointX, x1, depth + 1, maximumDepth, current, result, ref count);
                return;
            }

            if (!defined || jump)
            {
                Flush(current, result);
                return;
            }

            if (current.Count == 0)
            {
                current.Add(new MathPoint(x0, y0));
                count++;
            }
            current.Add(new MathPoint(x1, y1));
            count++;
        }

        private static void FindMarkers(FunctionSample sample)
        {
            for (var segmentIndex = 0; segmentIndex < sample.Segments.Count; segmentIndex++)
            {
                var segment = sample.Segments[segmentIndex];
                for (var i = 1; i < segment.Count; i++)
                {
                    var previous = segment[i - 1];
                    var current = segment[i];
                    if (previous.Y == 0)
                        AddDistinct(sample.Zeros, previous);
                    else if ((previous.Y < 0 && current.Y > 0) ||
                             (previous.Y > 0 && current.Y < 0))
                    {
                        var ratio = -previous.Y / (current.Y - previous.Y);
                        AddDistinct(
                            sample.Zeros,
                            new MathPoint(previous.X + (current.X - previous.X) * ratio, 0));
                    }
                }

                for (var i = 1; i < segment.Count - 1; i++)
                {
                    var previousDelta = segment[i].Y - segment[i - 1].Y;
                    var nextDelta = segment[i + 1].Y - segment[i].Y;
                    if ((previousDelta > 0 && nextDelta < 0) ||
                        (previousDelta < 0 && nextDelta > 0))
                        AddDistinct(sample.Extrema, segment[i]);
                }
            }
        }

        private static void AddDistinct(ICollection<MathPoint> points, MathPoint candidate)
        {
            foreach (var point in points)
            {
                if (MathMeasurementService.Distance(point, candidate) < 0.001)
                    return;
            }
            points.Add(candidate);
        }

        private static void Flush(List<MathPoint> current, FunctionSample result)
        {
            if (current.Count > 1)
                result.Segments.Add(new List<MathPoint>(current));
            current.Clear();
        }

        private static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * amount;
        }
    }
}
