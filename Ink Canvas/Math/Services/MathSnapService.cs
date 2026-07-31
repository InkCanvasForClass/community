using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathSnapService
    {
        public static bool TrySnap(
            MathScene scene,
            MathPoint input,
            double tolerance,
            out MathPoint snapped)
        {
            var found = TrySnap(scene, input, tolerance, out MathSnapResult result);
            snapped = result.Position;
            return found;
        }

        public static bool TrySnap(
            MathScene scene,
            MathPoint input,
            double tolerance,
            out MathSnapResult result)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

            MathReferenceService.Synchronize(scene);
            var bestDistance = tolerance;
            result = new MathSnapResult(input, null);
            var found = false;

            foreach (var candidate in GetReferencePoints(scene))
            {
                var distance = MathMeasurementService.Distance(input, candidate.Position);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                result = candidate;
                found = true;
            }

            var intersectionCandidates = GetIntersectionCandidates(scene);
            for (var firstIndex = 0; firstIndex < intersectionCandidates.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < intersectionCandidates.Count; secondIndex++)
                {
                    var intersections = MathIntersectionService.Intersect(
                        intersectionCandidates[firstIndex],
                        intersectionCandidates[secondIndex]);
                    for (var i = 0; !found && i < intersections.Count; i++)
                    {
                        var distance = MathMeasurementService.Distance(input, intersections[i]);
                        if (distance > bestDistance) continue;
                        bestDistance = distance;
                        result = new MathSnapResult(intersections[i], null);
                        found = true;
                    }
                }
            }

            if (!found)
            {
                foreach (var candidate in GetNearestGeometryPoints(scene, input))
                {
                    var distance = MathMeasurementService.Distance(input, candidate.Position);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    result = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static List<MathObject> GetIntersectionCandidates(MathScene scene)
        {
            var result = new List<MathObject>();
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is SegmentObject or LineObject or RayObject or CircleObject)
                    result.Add(scene.Objects[i]);
            }
            return result;
        }

        private static IEnumerable<MathSnapResult> GetReferencePoints(MathScene scene)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                switch (scene.Objects[i])
                {
                    case PointObject point:
                        yield return new MathSnapResult(point.Position, point.Id);
                        break;
                    case SegmentObject segment:
                        yield return new MathSnapResult(segment.Start, segment.StartPointId);
                        yield return new MathSnapResult(segment.End, segment.EndPointId);
                        yield return new MathSnapResult(Midpoint(segment.Start, segment.End), null);
                        break;
                    case TriangleObject triangle:
                        yield return new MathSnapResult(triangle.First, triangle.FirstPointId);
                        yield return new MathSnapResult(triangle.Second, triangle.SecondPointId);
                        yield return new MathSnapResult(triangle.Third, triangle.ThirdPointId);
                        break;
                    case LineObject line:
                        yield return new MathSnapResult(line.Start, line.StartPointId);
                        yield return new MathSnapResult(line.End, line.EndPointId);
                        break;
                    case RayObject ray:
                        yield return new MathSnapResult(ray.Start, ray.StartPointId);
                        yield return new MathSnapResult(ray.Through, ray.ThroughPointId);
                        break;
                    case CircleObject circle:
                        yield return new MathSnapResult(circle.Center, circle.CenterPointId);
                        break;
                    case AngleMeasurementObject angle:
                        yield return new MathSnapResult(angle.Vertex, angle.VertexPointId);
                        yield return new MathSnapResult(angle.First, angle.FirstPointId);
                        yield return new MathSnapResult(angle.Second, angle.SecondPointId);
                        break;
                    case SolidObject solid:
                        var solidMesh = SolidMeshBuilder.Build(solid);
                        var projection = SolidProjectionService.Project(solid);
                        for (var pointIndex = 0; pointIndex < projection.Points.Count; pointIndex++)
                            yield return new MathSnapResult(
                                projection.Points[pointIndex],
                                null,
                                solid.Id,
                                solidMesh.Vertices[pointIndex]);
                        break;
                }
            }
        }

        private static IEnumerable<MathSnapResult> GetNearestGeometryPoints(
            MathScene scene,
            MathPoint input)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                switch (scene.Objects[i])
                {
                    case SegmentObject segment:
                        yield return ProjectToSegment(input, segment.Start, segment.End);
                        break;
                    case TriangleObject triangle:
                        yield return ProjectToSegment(input, triangle.First, triangle.Second);
                        yield return ProjectToSegment(input, triangle.Second, triangle.Third);
                        yield return ProjectToSegment(input, triangle.Third, triangle.First);
                        break;
                    case LineObject line:
                        yield return ProjectToLine(input, line.Start, line.End, false);
                        break;
                    case RayObject ray:
                        yield return ProjectToLine(input, ray.Start, ray.Through, true);
                        break;
                    case CircleObject circle:
                        yield return ProjectToCircle(input, circle);
                        break;
                    case SolidObject solid:
                        var solidMesh = SolidMeshBuilder.Build(solid);
                        var projection = SolidProjectionService.Project(solid);
                        for (var edgeIndex = 0; edgeIndex < projection.Edges.Count; edgeIndex++)
                        {
                            var edge = projection.Edges[edgeIndex];
                            var modelEdge = solidMesh.Edges[edgeIndex];
                            yield return ProjectToSolidEdge(
                                input,
                                edge.Start,
                                edge.End,
                                solid.Id,
                                solidMesh.Vertices[modelEdge.Start],
                                solidMesh.Vertices[modelEdge.End]);
                        }
                        break;
                }
            }
        }

        private static MathSnapResult ProjectToSegment(MathPoint point, MathPoint start, MathPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared <= double.Epsilon) return new MathSnapResult(start, null);
            var parameter = ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared;
            parameter = Math.Max(0, Math.Min(1, parameter));
            return new MathSnapResult(
                new MathPoint(start.X + deltaX * parameter, start.Y + deltaY * parameter),
                null);
        }

        private static MathSnapResult ProjectToSolidEdge(
            MathPoint point,
            MathPoint start,
            MathPoint end,
            Guid solidId,
            MathPoint3D modelStart,
            MathPoint3D modelEnd)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            var parameter = lengthSquared <= double.Epsilon
                ? 0d
                : ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared;
            parameter = Math.Max(0, Math.Min(1, parameter));
            return new MathSnapResult(
                new MathPoint(start.X + deltaX * parameter, start.Y + deltaY * parameter),
                null,
                solidId,
                new MathPoint3D(
                    modelStart.X + (modelEnd.X - modelStart.X) * parameter,
                    modelStart.Y + (modelEnd.Y - modelStart.Y) * parameter,
                    modelStart.Z + (modelEnd.Z - modelStart.Z) * parameter));
        }

        private static MathSnapResult ProjectToLine(
            MathPoint point,
            MathPoint start,
            MathPoint through,
            bool isRay)
        {
            var deltaX = through.X - start.X;
            var deltaY = through.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared <= double.Epsilon) return new MathSnapResult(start, null);
            var parameter = ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared;
            if (isRay) parameter = Math.Max(0, parameter);
            return new MathSnapResult(
                new MathPoint(start.X + deltaX * parameter, start.Y + deltaY * parameter),
                null);
        }

        private static MathSnapResult ProjectToCircle(MathPoint point, CircleObject circle)
        {
            var deltaX = point.X - circle.Center.X;
            var deltaY = point.Y - circle.Center.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length <= double.Epsilon)
                return new MathSnapResult(new MathPoint(circle.Center.X + circle.Radius, circle.Center.Y), null);
            return new MathSnapResult(
                new MathPoint(
                    circle.Center.X + deltaX * circle.Radius / length,
                    circle.Center.Y + deltaY * circle.Radius / length),
                null);
        }

        private static MathPoint Midpoint(MathPoint first, MathPoint second)
        {
            return new MathPoint((first.X + second.X) / 2, (first.Y + second.Y) / 2);
        }
    }
}
