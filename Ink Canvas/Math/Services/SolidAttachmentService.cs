using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class SolidAttachmentService
    {
        public static bool TryAttach(MathObject mathObject, params MathSnapResult[] points)
        {
            if (mathObject == null) throw new ArgumentNullException(nameof(mathObject));
            var expectedCount = GetRequiredPointCount(mathObject);
            if (expectedCount == 0 || points == null || points.Length != expectedCount)
                return false;

            var solidId = points[0].SolidId;
            if (!solidId.HasValue || !points[0].SolidLocalPoint.HasValue)
                return false;

            var attachment = new SolidAttachment { SolidId = solidId.Value };
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i].SolidId != solidId || !points[i].SolidLocalPoint.HasValue)
                    return false;
                attachment.LocalPoints.Add(points[i].SolidLocalPoint.Value);
            }

            mathObject.SolidAttachment = attachment;
            return true;
        }

        public static void Synchronize(MathScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            for (var i = 0; i < scene.Objects.Count; i++)
            {
                var mathObject = scene.Objects[i];
                var attachment = mathObject?.SolidAttachment;
                if (attachment == null) continue;
                var parent = FindSolid(scene, attachment.SolidId);
                if (parent == null || parent.Id == mathObject.Id ||
                    !HasExpectedLocalPoints(mathObject, attachment))
                {
                    mathObject.SolidAttachment = null;
                    continue;
                }

                var points = new List<MathPoint>(attachment.LocalPoints.Count);
                for (var pointIndex = 0; pointIndex < attachment.LocalPoints.Count; pointIndex++)
                    points.Add(SolidProjectionService.ProjectModelPoint(parent, attachment.LocalPoints[pointIndex]));
                ApplyProjectedPoints(mathObject, parent, points);
            }
        }

        public static bool TryTranslateWithParent(
            MathScene scene,
            MathObject mathObject,
            double deltaX,
            double deltaY)
        {
            var attachment = mathObject?.SolidAttachment;
            if (attachment == null) return false;
            var parent = FindSolid(scene, attachment.SolidId);
            if (parent == null)
            {
                mathObject.SolidAttachment = null;
                return false;
            }

            MathGeometryService.Translate(parent, deltaX, deltaY);
            Synchronize(scene);
            return true;
        }

        public static void DetachSolid(MathScene scene, Guid solidId)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                var mathObject = scene.Objects[i];
                if (mathObject?.SolidAttachment?.SolidId == solidId)
                    mathObject.SolidAttachment = null;
            }
        }

        private static int GetRequiredPointCount(MathObject mathObject)
        {
            return mathObject switch
            {
                PointObject or TextLabelObject or SolidObject => 1,
                SegmentObject or LineObject or RayObject or CircleObject => 2,
                TriangleObject or AngleMeasurementObject => 3,
                _ => 0
            };
        }

        private static bool HasExpectedLocalPoints(MathObject mathObject, SolidAttachment attachment)
        {
            if (attachment.SolidId == Guid.Empty || attachment.LocalPoints == null ||
                attachment.LocalPoints.Count != GetRequiredPointCount(mathObject))
                return false;
            for (var i = 0; i < attachment.LocalPoints.Count; i++)
            {
                var point = attachment.LocalPoints[i];
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                    return false;
            }
            return true;
        }

        private static SolidObject FindSolid(MathScene scene, Guid id)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i] is SolidObject solid && solid.Id == id)
                    return solid;
            }
            return null;
        }

        private static void ApplyProjectedPoints(
            MathObject mathObject,
            SolidObject parent,
            IReadOnlyList<MathPoint> points)
        {
            switch (mathObject)
            {
                case PointObject point:
                    point.Position = points[0];
                    break;
                case SegmentObject segment:
                    segment.Start = points[0];
                    segment.End = points[1];
                    break;
                case LineObject line:
                    line.Start = points[0];
                    line.End = points[1];
                    break;
                case RayObject ray:
                    ray.Start = points[0];
                    ray.Through = points[1];
                    break;
                case CircleObject circle:
                    circle.Center = points[0];
                    circle.Radius = MathMeasurementService.Distance(points[0], points[1]);
                    break;
                case TriangleObject triangle:
                    triangle.First = points[0];
                    triangle.Second = points[1];
                    triangle.Third = points[2];
                    break;
                case AngleMeasurementObject angle:
                    angle.Vertex = points[0];
                    angle.First = points[1];
                    angle.Second = points[2];
                    break;
                case TextLabelObject label:
                    label.Position = points[0];
                    break;
                case SolidObject sphere:
                    sphere.Center = points[0];
                    sphere.Scale = parent.Scale;
                    sphere.HorizontalScale = parent.HorizontalScale;
                    sphere.VerticalScale = parent.VerticalScale;
                    sphere.RotationX = parent.RotationX;
                    sphere.RotationY = parent.RotationY;
                    sphere.RotationZ = parent.RotationZ;
                    sphere.ViewMode = parent.ViewMode;
                    sphere.ProjectionMode = parent.ProjectionMode;
                    break;
            }
        }
    }
}
