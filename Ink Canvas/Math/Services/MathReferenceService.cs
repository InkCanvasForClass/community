using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathReferenceService
    {
        public static void Synchronize(MathScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            SolidAttachmentService.Synchronize(scene);
            var points = IndexPoints(scene);
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                switch (scene.Objects[i])
                {
                    case SegmentObject segment:
                        segment.Start = Resolve(points, segment.StartPointId, segment.Start);
                        segment.End = Resolve(points, segment.EndPointId, segment.End);
                        break;
                    case LineObject line:
                        line.Start = Resolve(points, line.StartPointId, line.Start);
                        line.End = Resolve(points, line.EndPointId, line.End);
                        break;
                    case RayObject ray:
                        ray.Start = Resolve(points, ray.StartPointId, ray.Start);
                        ray.Through = Resolve(points, ray.ThroughPointId, ray.Through);
                        break;
                    case CircleObject circle:
                        circle.Center = Resolve(points, circle.CenterPointId, circle.Center);
                        if (circle.RadiusPointId.HasValue &&
                            points.TryGetValue(circle.RadiusPointId.Value, out var radiusPoint))
                            circle.Radius = MathMeasurementService.Distance(circle.Center, radiusPoint.Position);
                        break;
                    case AngleMeasurementObject angle:
                        angle.First = Resolve(points, angle.FirstPointId, angle.First);
                        angle.Vertex = Resolve(points, angle.VertexPointId, angle.Vertex);
                        angle.Second = Resolve(points, angle.SecondPointId, angle.Second);
                        break;
                    case TriangleObject triangle:
                        triangle.First = Resolve(points, triangle.FirstPointId, triangle.First);
                        triangle.Second = Resolve(points, triangle.SecondPointId, triangle.Second);
                        triangle.Third = Resolve(points, triangle.ThirdPointId, triangle.Third);
                        break;
                }
            }
            TriangleCircleConstructionService.Synchronize(scene);
        }

        public static void Translate(MathScene scene, MathObject mathObject, double deltaX, double deltaY)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (mathObject == null) throw new ArgumentNullException(nameof(mathObject));
            if (mathObject.IsLocked) return;

            if (SolidAttachmentService.TryTranslateWithParent(scene, mathObject, deltaX, deltaY))
                return;

            if (mathObject is FunctionObject function)
            {
                if (!function.CoordinatePlaneId.HasValue)
                    function.CoordinatePlaneId = FindContainingCoordinatePlane(scene, function.Origin)?.Id;
                if (function.CoordinatePlaneId.HasValue)
                {
                    TranslateFunctionFrame(scene, function, deltaX, deltaY);
                    return;
                }
            }

            var pointIds = GetReferencedPointIds(mathObject);
            if (pointIds.Count == 0)
            {
                MathGeometryService.Translate(mathObject, deltaX, deltaY);
                Synchronize(scene);
                return;
            }

            var moved = new HashSet<Guid>();
            for (var i = 0; i < pointIds.Count; i++)
            {
                var id = pointIds[i];
                if (!moved.Add(id)) continue;
                var point = FindPoint(scene, id);
                if (point == null || point.IsLocked) continue;
                point.Position = new MathPoint(
                    point.Position.X + deltaX,
                    point.Position.Y + deltaY);
            }

            Synchronize(scene);
        }

        private static void TranslateFunctionFrame(
            MathScene scene,
            FunctionObject selectedFunction,
            double deltaX,
            double deltaY)
        {
            var planeId = selectedFunction.CoordinatePlaneId.Value;
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is CoordinatePlaneObject plane &&
                    plane.Id == planeId &&
                    !plane.IsLocked)
                {
                    MathGeometryService.Translate(plane, deltaX, deltaY);
                }
                else if (scene.Objects[i] is FunctionObject function &&
                         function.CoordinatePlaneId == planeId &&
                         !function.IsLocked)
                {
                    MathGeometryService.Translate(function, deltaX, deltaY);
                }
            }

            Synchronize(scene);
        }

        private static CoordinatePlaneObject FindContainingCoordinatePlane(
            MathScene scene,
            MathPoint point)
        {
            for (var i = scene.Objects.Count - 1; i >= 0; i--)
            {
                if (scene.Objects[i] is not CoordinatePlaneObject plane) continue;
                if (Math.Abs(point.X - plane.Center.X) <= plane.Width / 2 &&
                    Math.Abs(point.Y - plane.Center.Y) <= plane.Height / 2)
                    return plane;
            }
            return null;
        }

        public static void DetachPoint(MathScene scene, Guid pointId)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            Synchronize(scene);
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                switch (scene.Objects[i])
                {
                    case SegmentObject segment:
                        if (segment.StartPointId == pointId) segment.StartPointId = null;
                        if (segment.EndPointId == pointId) segment.EndPointId = null;
                        break;
                    case LineObject line:
                        if (line.StartPointId == pointId) line.StartPointId = null;
                        if (line.EndPointId == pointId) line.EndPointId = null;
                        break;
                    case RayObject ray:
                        if (ray.StartPointId == pointId) ray.StartPointId = null;
                        if (ray.ThroughPointId == pointId) ray.ThroughPointId = null;
                        break;
                    case CircleObject circle:
                        if (circle.CenterPointId == pointId) circle.CenterPointId = null;
                        if (circle.RadiusPointId == pointId) circle.RadiusPointId = null;
                        break;
                    case AngleMeasurementObject angle:
                        if (angle.FirstPointId == pointId) angle.FirstPointId = null;
                        if (angle.VertexPointId == pointId) angle.VertexPointId = null;
                        if (angle.SecondPointId == pointId) angle.SecondPointId = null;
                        break;
                    case TriangleObject triangle:
                        if (triangle.FirstPointId == pointId) triangle.FirstPointId = null;
                        if (triangle.SecondPointId == pointId) triangle.SecondPointId = null;
                        if (triangle.ThirdPointId == pointId) triangle.ThirdPointId = null;
                        break;
                }
            }
        }

        private static Dictionary<Guid, PointObject> IndexPoints(MathScene scene)
        {
            var result = new Dictionary<Guid, PointObject>();
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is PointObject point)
                    result[point.Id] = point;
            }

            return result;
        }

        private static PointObject FindPoint(MathScene scene, Guid id)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is PointObject point && point.Id == id)
                    return point;
            }

            return null;
        }

        private static MathPoint Resolve(
            IReadOnlyDictionary<Guid, PointObject> points,
            Guid? id,
            MathPoint fallback)
        {
            return id.HasValue && points.TryGetValue(id.Value, out var point)
                ? point.Position
                : fallback;
        }

        private static List<Guid> GetReferencedPointIds(MathObject mathObject)
        {
            var result = new List<Guid>();
            switch (mathObject)
            {
                case PointObject point:
                    result.Add(point.Id);
                    break;
                case SegmentObject segment:
                    Add(result, segment.StartPointId);
                    Add(result, segment.EndPointId);
                    break;
                case LineObject line:
                    Add(result, line.StartPointId);
                    Add(result, line.EndPointId);
                    break;
                case RayObject ray:
                    Add(result, ray.StartPointId);
                    Add(result, ray.ThroughPointId);
                    break;
                case CircleObject circle:
                    Add(result, circle.CenterPointId);
                    Add(result, circle.RadiusPointId);
                    break;
                case AngleMeasurementObject angle:
                    Add(result, angle.FirstPointId);
                    Add(result, angle.VertexPointId);
                    Add(result, angle.SecondPointId);
                    break;
                case TriangleObject triangle:
                    Add(result, triangle.FirstPointId);
                    Add(result, triangle.SecondPointId);
                    Add(result, triangle.ThirdPointId);
                    break;
            }

            return result;
        }

        private static void Add(ICollection<Guid> ids, Guid? id)
        {
            if (id.HasValue) ids.Add(id.Value);
        }

    }
}
