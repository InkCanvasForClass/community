using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathGeometryService
    {
        public static MathObject HitTest(MathScene scene, MathPoint point, double tolerance)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

            for (var i = scene.Objects.Count - 1; i >= 0; i--)
            {
                var mathObject = scene.Objects[i];
                if (mathObject == null || !mathObject.IsVisible) continue;

                if (DistanceTo(mathObject, point) <= tolerance)
                    return mathObject;
            }

            return null;
        }

        public static void Translate(MathObject mathObject, double deltaX, double deltaY)
        {
            if (mathObject == null) throw new ArgumentNullException(nameof(mathObject));
            if (mathObject.IsLocked) return;

            switch (mathObject)
            {
                case PointObject point:
                    point.Position = Translate(point.Position, deltaX, deltaY);
                    break;
                case SegmentObject segment:
                    segment.Start = Translate(segment.Start, deltaX, deltaY);
                    segment.End = Translate(segment.End, deltaX, deltaY);
                    break;
                case TriangleObject triangle:
                    triangle.First = Translate(triangle.First, deltaX, deltaY);
                    triangle.Second = Translate(triangle.Second, deltaX, deltaY);
                    triangle.Third = Translate(triangle.Third, deltaX, deltaY);
                    break;
                case CircleObject circle:
                    circle.Center = Translate(circle.Center, deltaX, deltaY);
                    break;
                case TextLabelObject label:
                    label.Position = Translate(label.Position, deltaX, deltaY);
                    break;
                case LineObject line:
                    line.Start = Translate(line.Start, deltaX, deltaY);
                    line.End = Translate(line.End, deltaX, deltaY);
                    break;
                case RayObject ray:
                    ray.Start = Translate(ray.Start, deltaX, deltaY);
                    ray.Through = Translate(ray.Through, deltaX, deltaY);
                    break;
                case AngleMeasurementObject angle:
                    angle.First = Translate(angle.First, deltaX, deltaY);
                    angle.Vertex = Translate(angle.Vertex, deltaX, deltaY);
                    angle.Second = Translate(angle.Second, deltaX, deltaY);
                    break;
                case FunctionObject function:
                    function.Origin = Translate(function.Origin, deltaX, deltaY);
                    break;
                case SolidObject solid:
                    solid.Center = Translate(solid.Center, deltaX, deltaY);
                    break;
                case CoordinatePlaneObject coordinatePlane:
                    coordinatePlane.Center = Translate(coordinatePlane.Center, deltaX, deltaY);
                    break;
            }
        }

        public static void StretchSolid(SolidObject solid, double horizontalFactor, double verticalFactor)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            if (solid.IsLocked) return;
            solid.HorizontalScale = Math.Max(0.2, Math.Min(5, solid.HorizontalScale * horizontalFactor));
            solid.VerticalScale = Math.Max(0.2, Math.Min(5, solid.VerticalScale * verticalFactor));
        }

        private static double DistanceTo(MathObject mathObject, MathPoint point)
        {
            return mathObject switch
            {
                PointObject pointObject => Distance(pointObject.Position, point),
                SegmentObject segment => DistanceToSegment(point, segment.Start, segment.End),
                TriangleObject triangle => Math.Min(
                    DistanceToSegment(point, triangle.First, triangle.Second),
                    Math.Min(
                        DistanceToSegment(point, triangle.Second, triangle.Third),
                        DistanceToSegment(point, triangle.Third, triangle.First))),
                CircleObject circle => Math.Max(0, Distance(circle.Center, point) - circle.Radius),
                TextLabelObject label => Distance(label.Position, point),
                LineObject line => DistanceToInfiniteLine(point, line.Start, line.End),
                RayObject ray => DistanceToRay(point, ray.Start, ray.Through),
                AngleMeasurementObject angle => Math.Min(
                    DistanceToSegment(point, angle.Vertex, angle.First),
                    DistanceToSegment(point, angle.Vertex, angle.Second)),
                FunctionObject function => DistanceToFunction(point, function),
                SolidObject solid => DistanceToSolid(point, solid),
                CoordinatePlaneObject coordinatePlane => DistanceToCoordinatePlane(point, coordinatePlane),
                _ => double.PositiveInfinity
            };
        }

        private static double DistanceToCoordinatePlane(
            MathPoint point,
            CoordinatePlaneObject coordinatePlane)
        {
            var halfWidth = coordinatePlane.Width / 2;
            var halfHeight = coordinatePlane.Height / 2;
            var left = coordinatePlane.Center.X - halfWidth;
            var right = coordinatePlane.Center.X + halfWidth;
            var top = coordinatePlane.Center.Y - halfHeight;
            var bottom = coordinatePlane.Center.Y + halfHeight;
            if (point.X < left || point.X > right || point.Y < top || point.Y > bottom)
                return Math.Sqrt(
                    Math.Pow(Math.Max(left - point.X, Math.Max(0, point.X - right)), 2) +
                    Math.Pow(Math.Max(top - point.Y, Math.Max(0, point.Y - bottom)), 2));

            return 0;
        }

        private static double DistanceToSolid(MathPoint point, SolidObject solid)
        {
            var projection = SolidProjectionService.Project(solid);
            if (IsInsideConvexHull(point, projection.Points))
                return 0;
            var best = double.PositiveInfinity;
            for (var i = 0; i < projection.Edges.Count; i++)
            {
                var edge = projection.Edges[i];
                best = Math.Min(best, DistanceToSegment(point, edge.Start, edge.End));
            }
            return best;
        }

        private static bool IsInsideConvexHull(MathPoint point, IList<MathPoint> points)
        {
            if (points == null || points.Count < 3) return false;
            var hull = new List<MathPoint>(points);
            hull.Sort((first, second) =>
            {
                var xComparison = first.X.CompareTo(second.X);
                return xComparison != 0 ? xComparison : first.Y.CompareTo(second.Y);
            });
            var lower = new List<MathPoint>();
            for (var i = 0; i < hull.Count; i++)
            {
                while (lower.Count >= 2 &&
                       Cross(lower[lower.Count - 2], lower[lower.Count - 1], hull[i]) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(hull[i]);
            }
            var upper = new List<MathPoint>();
            for (var i = hull.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 &&
                       Cross(upper[upper.Count - 2], upper[upper.Count - 1], hull[i]) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(hull[i]);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            if (lower.Count < 3) return false;
            for (var i = 0; i < lower.Count; i++)
            {
                if (Cross(lower[i], lower[(i + 1) % lower.Count], point) < 0)
                    return false;
            }
            return true;
        }

        private static double Cross(MathPoint first, MathPoint second, MathPoint third)
        {
            return (second.X - first.X) * (third.Y - first.Y) -
                   (second.Y - first.Y) * (third.X - first.X);
        }

        private static double DistanceToFunction(MathPoint point, FunctionObject function)
        {
            try
            {
                point = RotateAround(
                    point,
                    function.Origin,
                    -function.RotationDegrees);
                var sample = FunctionSamplingService.Sample(function);
                var best = double.PositiveInfinity;
                for (var segmentIndex = 0; segmentIndex < sample.Segments.Count; segmentIndex++)
                {
                    var segment = sample.Segments[segmentIndex];
                    for (var i = 1; i < segment.Count; i++)
                    {
                        var start = ToScreen(function, segment[i - 1]);
                        var end = ToScreen(function, segment[i]);
                        best = Math.Min(best, DistanceToSegment(point, start, end));
                    }
                }
                return best;
            }
            catch (FormatException)
            {
                return double.PositiveInfinity;
            }
        }

        private static MathPoint ToScreen(FunctionObject function, MathPoint point)
        {
            return new MathPoint(
                function.Origin.X + point.X * function.PixelsPerUnit,
                function.Origin.Y - point.Y * function.PixelsPerUnit);
        }

        private static MathPoint RotateAround(
            MathPoint point,
            MathPoint center,
            double angleDegrees)
        {
            if (Math.Abs(angleDegrees) <= double.Epsilon) return point;
            var radians = angleDegrees * Math.PI / 180;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var deltaX = point.X - center.X;
            var deltaY = point.Y - center.Y;
            return new MathPoint(
                center.X + deltaX * cosine - deltaY * sine,
                center.Y + deltaX * sine + deltaY * cosine);
        }

        private static double DistanceToInfiniteLine(MathPoint point, MathPoint start, MathPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length <= double.Epsilon) return Distance(point, start);
            return Math.Abs(deltaY * point.X - deltaX * point.Y + end.X * start.Y - end.Y * start.X) / length;
        }

        private static double DistanceToRay(MathPoint point, MathPoint start, MathPoint through)
        {
            var directionX = through.X - start.X;
            var directionY = through.Y - start.Y;
            var lengthSquared = directionX * directionX + directionY * directionY;
            if (lengthSquared <= double.Epsilon) return Distance(point, start);
            var projection = ((point.X - start.X) * directionX + (point.Y - start.Y) * directionY) / lengthSquared;
            return projection < 0
                ? Distance(point, start)
                : DistanceToInfiniteLine(point, start, through);
        }

        private static double DistanceToSegment(MathPoint point, MathPoint start, MathPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared <= double.Epsilon) return Distance(point, start);

            var projection = ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared;
            projection = Math.Max(0, Math.Min(1, projection));
            var closest = new MathPoint(start.X + projection * deltaX, start.Y + projection * deltaY);
            return Distance(point, closest);
        }

        private static double Distance(MathPoint first, MathPoint second)
        {
            var deltaX = first.X - second.X;
            var deltaY = first.Y - second.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static MathPoint Translate(MathPoint point, double deltaX, double deltaY)
        {
            return new MathPoint(point.X + deltaX, point.Y + deltaY);
        }
    }
}
