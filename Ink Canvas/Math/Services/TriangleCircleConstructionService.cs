using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class TriangleCircleConstructionService
    {
        public static bool TryCreate(
            TriangleObject triangle,
            TriangleCircleKind kind,
            out CircleObject circle)
        {
            circle = null;
            if (triangle == null) return false;
            if (!TryCalculate(triangle, kind, out var center, out var radius))
                return false;
            circle = new CircleObject
            {
                Center = center,
                Radius = radius,
                TriangleId = triangle.Id,
                TriangleCircleKind = kind
            };
            return true;
        }

        public static void Synchronize(MathScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is not CircleObject circle ||
                    !circle.TriangleId.HasValue ||
                    !circle.TriangleCircleKind.HasValue)
                    continue;
                var triangle = FindTriangle(scene, circle.TriangleId.Value);
                if (triangle == null ||
                    !TryCalculate(triangle, circle.TriangleCircleKind.Value, out var center, out var radius))
                {
                    circle.TriangleId = null;
                    circle.TriangleCircleKind = null;
                    continue;
                }
                circle.Center = center;
                circle.Radius = radius;
            }
        }

        public static void DetachTriangle(MathScene scene, Guid triangleId)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is CircleObject circle && circle.TriangleId == triangleId)
                {
                    circle.TriangleId = null;
                    circle.TriangleCircleKind = null;
                }
            }
        }

        private static bool TryCalculate(
            TriangleObject triangle,
            TriangleCircleKind kind,
            out MathPoint center,
            out double radius)
        {
            return kind == TriangleCircleKind.Circumcircle
                ? TryCircumcircle(triangle, out center, out radius)
                : TryIncircle(triangle, out center, out radius);
        }

        private static bool TryCircumcircle(TriangleObject triangle, out MathPoint center, out double radius)
        {
            var first = triangle.First;
            var second = triangle.Second;
            var third = triangle.Third;
            var determinant = 2 * (first.X * (second.Y - third.Y) +
                                   second.X * (third.Y - first.Y) +
                                   third.X * (first.Y - second.Y));
            if (Math.Abs(determinant) <= 1e-8)
            {
                center = default;
                radius = 0;
                return false;
            }

            var firstLength = first.X * first.X + first.Y * first.Y;
            var secondLength = second.X * second.X + second.Y * second.Y;
            var thirdLength = third.X * third.X + third.Y * third.Y;
            center = new MathPoint(
                (firstLength * (second.Y - third.Y) +
                 secondLength * (third.Y - first.Y) +
                 thirdLength * (first.Y - second.Y)) / determinant,
                (firstLength * (third.X - second.X) +
                 secondLength * (first.X - third.X) +
                 thirdLength * (second.X - first.X)) / determinant);
            radius = MathMeasurementService.Distance(center, first);
            return double.IsFinite(radius) && radius > 0;
        }

        private static bool TryIncircle(TriangleObject triangle, out MathPoint center, out double radius)
        {
            var first = triangle.First;
            var second = triangle.Second;
            var third = triangle.Third;
            var firstWeight = MathMeasurementService.Distance(second, third);
            var secondWeight = MathMeasurementService.Distance(first, third);
            var thirdWeight = MathMeasurementService.Distance(first, second);
            var perimeter = firstWeight + secondWeight + thirdWeight;
            var doubledArea = Math.Abs(
                (second.X - first.X) * (third.Y - first.Y) -
                (second.Y - first.Y) * (third.X - first.X));
            if (perimeter <= double.Epsilon || doubledArea <= 1e-8)
            {
                center = default;
                radius = 0;
                return false;
            }

            center = new MathPoint(
                (firstWeight * first.X + secondWeight * second.X + thirdWeight * third.X) / perimeter,
                (firstWeight * first.Y + secondWeight * second.Y + thirdWeight * third.Y) / perimeter);
            radius = doubledArea / perimeter;
            return double.IsFinite(radius) && radius > 0;
        }

        private static TriangleObject FindTriangle(MathScene scene, Guid id)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is TriangleObject triangle && triangle.Id == id)
                    return triangle;
            }
            return null;
        }
    }
}
