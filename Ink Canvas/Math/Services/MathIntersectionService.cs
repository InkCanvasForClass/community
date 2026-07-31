using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathIntersectionService
    {
        private const double Epsilon = 1e-9;

        public static IReadOnlyList<MathPoint> Intersect(MathObject first, MathObject second)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));

            if (TryGetLinear(first, out var firstLinear) &&
                TryGetLinear(second, out var secondLinear))
                return IntersectLinear(firstLinear, secondLinear);

            if (TryGetLinear(first, out var linear) && second is CircleObject circle)
                return IntersectLinearCircle(linear, circle);

            if (first is CircleObject firstCircle && TryGetLinear(second, out linear))
                return IntersectLinearCircle(linear, firstCircle);

            if (first is CircleObject circle1 && second is CircleObject circle2)
                return IntersectCircles(circle1, circle2);

            return Array.Empty<MathPoint>();
        }

        private static IReadOnlyList<MathPoint> IntersectLinear(LinearPrimitive first, LinearPrimitive second)
        {
            var cross = Cross(first.DirectionX, first.DirectionY, second.DirectionX, second.DirectionY);
            if (Math.Abs(cross) <= Epsilon) return Array.Empty<MathPoint>();

            var deltaX = second.Start.X - first.Start.X;
            var deltaY = second.Start.Y - first.Start.Y;
            var firstT = Cross(deltaX, deltaY, second.DirectionX, second.DirectionY) / cross;
            var secondT = Cross(deltaX, deltaY, first.DirectionX, first.DirectionY) / cross;
            if (!first.Contains(firstT) || !second.Contains(secondT))
                return Array.Empty<MathPoint>();

            return new[]
            {
                new MathPoint(
                    first.Start.X + firstT * first.DirectionX,
                    first.Start.Y + firstT * first.DirectionY)
            };
        }

        private static IReadOnlyList<MathPoint> IntersectLinearCircle(LinearPrimitive linear, CircleObject circle)
        {
            var offsetX = linear.Start.X - circle.Center.X;
            var offsetY = linear.Start.Y - circle.Center.Y;
            var a = linear.DirectionX * linear.DirectionX + linear.DirectionY * linear.DirectionY;
            if (a <= Epsilon) return Array.Empty<MathPoint>();

            var b = 2 * (offsetX * linear.DirectionX + offsetY * linear.DirectionY);
            var c = offsetX * offsetX + offsetY * offsetY - circle.Radius * circle.Radius;
            var discriminant = b * b - 4 * a * c;
            if (discriminant < -Epsilon) return Array.Empty<MathPoint>();

            if (Math.Abs(discriminant) <= Epsilon)
            {
                var t = -b / (2 * a);
                return linear.Contains(t)
                    ? new[] { PointAt(linear, t) }
                    : Array.Empty<MathPoint>();
            }

            var root = Math.Sqrt(discriminant);
            var firstT = (-b - root) / (2 * a);
            var secondT = (-b + root) / (2 * a);
            var result = new List<MathPoint>(2);
            if (linear.Contains(firstT)) result.Add(PointAt(linear, firstT));
            if (linear.Contains(secondT)) result.Add(PointAt(linear, secondT));
            return result;
        }

        private static IReadOnlyList<MathPoint> IntersectCircles(CircleObject first, CircleObject second)
        {
            var deltaX = second.Center.X - first.Center.X;
            var deltaY = second.Center.Y - first.Center.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance <= Epsilon ||
                distance > first.Radius + second.Radius + Epsilon ||
                distance < Math.Abs(first.Radius - second.Radius) - Epsilon)
                return Array.Empty<MathPoint>();

            var along = (first.Radius * first.Radius - second.Radius * second.Radius + distance * distance) /
                        (2 * distance);
            var heightSquared = first.Radius * first.Radius - along * along;
            var height = Math.Sqrt(Math.Max(0, heightSquared));
            var baseX = first.Center.X + along * deltaX / distance;
            var baseY = first.Center.Y + along * deltaY / distance;
            if (height <= Epsilon)
                return new[] { new MathPoint(baseX, baseY) };

            var offsetX = -deltaY * height / distance;
            var offsetY = deltaX * height / distance;
            return new[]
            {
                new MathPoint(baseX + offsetX, baseY + offsetY),
                new MathPoint(baseX - offsetX, baseY - offsetY)
            };
        }

        private static MathPoint PointAt(LinearPrimitive linear, double t)
        {
            return new MathPoint(
                linear.Start.X + t * linear.DirectionX,
                linear.Start.Y + t * linear.DirectionY);
        }

        private static bool TryGetLinear(MathObject mathObject, out LinearPrimitive linear)
        {
            switch (mathObject)
            {
                case SegmentObject segment:
                    linear = new LinearPrimitive(segment.Start, segment.End, 0, 1);
                    return true;
                case LineObject line:
                    linear = new LinearPrimitive(
                        line.Start,
                        line.End,
                        double.NegativeInfinity,
                        double.PositiveInfinity);
                    return true;
                case RayObject ray:
                    linear = new LinearPrimitive(
                        ray.Start,
                        ray.Through,
                        0,
                        double.PositiveInfinity);
                    return true;
                default:
                    linear = default;
                    return false;
            }
        }

        private static double Cross(double firstX, double firstY, double secondX, double secondY)
        {
            return firstX * secondY - firstY * secondX;
        }

        private readonly struct LinearPrimitive
        {
            public LinearPrimitive(MathPoint start, MathPoint through, double minimumT, double maximumT)
            {
                Start = start;
                DirectionX = through.X - start.X;
                DirectionY = through.Y - start.Y;
                MinimumT = minimumT;
                MaximumT = maximumT;
            }

            public MathPoint Start { get; }
            public double DirectionX { get; }
            public double DirectionY { get; }
            private double MinimumT { get; }
            private double MaximumT { get; }

            public bool Contains(double t)
            {
                return t >= MinimumT - Epsilon && t <= MaximumT + Epsilon;
            }
        }
    }
}
