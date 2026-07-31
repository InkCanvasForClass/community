using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public sealed class MathSceneService
    {
        private readonly MathScene _scene;

        public MathSceneService(MathScene scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        public void Add(MathObject mathObject)
        {
            if (mathObject == null) throw new ArgumentNullException(nameof(mathObject));
            if (mathObject.Id == Guid.Empty) throw new ArgumentException("Math object ID cannot be empty.", nameof(mathObject));
            if (Find(mathObject.Id) != null) throw new InvalidOperationException("A math object with the same ID already exists.");

            Validate(mathObject);
            var insertIndex = _scene.Objects.Count;
            while (insertIndex > 0 &&
                   _scene.Objects[insertIndex - 1].ZIndex > mathObject.ZIndex)
                insertIndex--;
            _scene.Objects.Insert(insertIndex, mathObject);
        }

        public bool Remove(Guid id)
        {
            var mathObject = Find(id);
            if (mathObject == null) return false;
            if (mathObject is PointObject)
                MathReferenceService.DetachPoint(_scene, id);
            if (mathObject is SolidObject)
                SolidAttachmentService.DetachSolid(_scene, id);
            if (mathObject is TriangleObject)
                TriangleCircleConstructionService.DetachTriangle(_scene, id);
            var removed = _scene.Objects.Remove(mathObject);
            if (removed && _scene.Constraints != null)
                _scene.Constraints.RemoveAll(constraint => constraint.ObjectIds.Contains(id));
            return removed;
        }

        public MathObject Find(Guid id)
        {
            for (var i = 0; i < _scene.Objects.Count; i++)
            {
                if (_scene.Objects[i].Id == id) return _scene.Objects[i];
            }

            return null;
        }

        public void Validate(MathObject mathObject)
        {
            if (mathObject.StrokeWidth <= 0 || double.IsNaN(mathObject.StrokeWidth) || double.IsInfinity(mathObject.StrokeWidth))
                throw new ArgumentOutOfRangeException(nameof(mathObject), "Stroke width must be a finite positive number.");

            if (mathObject is CircleObject circle &&
                (circle.Radius <= 0 || double.IsNaN(circle.Radius) || double.IsInfinity(circle.Radius)))
                throw new ArgumentOutOfRangeException(nameof(mathObject), "Circle radius must be a finite positive number.");

            if (mathObject is CircleObject associatedCircle &&
                (associatedCircle.TriangleId.HasValue != associatedCircle.TriangleCircleKind.HasValue ||
                 associatedCircle.TriangleId == Guid.Empty))
                throw new ArgumentException("Triangle circle association is invalid.", nameof(mathObject));

            if (mathObject is LineObject line && PointsCoincide(line.Start, line.End))
                throw new ArgumentException("Line points must be distinct.", nameof(mathObject));

            if (mathObject is RayObject ray && PointsCoincide(ray.Start, ray.Through))
                throw new ArgumentException("Ray points must be distinct.", nameof(mathObject));

            if (mathObject is AngleMeasurementObject angle &&
                (PointsCoincide(angle.Vertex, angle.First) || PointsCoincide(angle.Vertex, angle.Second)))
                throw new ArgumentException("Angle arms must be distinct from the vertex.", nameof(mathObject));

            if (mathObject is FunctionObject function)
            {
                if (function.DomainMin >= function.DomainMax ||
                    !double.IsFinite(function.DomainMin) ||
                    !double.IsFinite(function.DomainMax))
                    throw new ArgumentException("Function domain must be finite and increasing.", nameof(mathObject));
                if (function.PixelsPerUnit <= 0 || !double.IsFinite(function.PixelsPerUnit))
                    throw new ArgumentException("Function scale must be a finite positive number.", nameof(mathObject));
                if (!double.IsFinite(function.RotationDegrees))
                    throw new ArgumentException("Function rotation must be finite.", nameof(mathObject));
                if (function.SampleQuality < 1 || function.SampleQuality > 4)
                    throw new ArgumentOutOfRangeException(nameof(mathObject), "Function sample quality must be between 1 and 4.");
                MathExpressionParser.Parse(function.Expression);
            }

            if (mathObject is CoordinatePlaneObject coordinatePlane)
            {
                if (coordinatePlane.Width < 80 ||
                    coordinatePlane.Height < 80 ||
                    !double.IsFinite(coordinatePlane.Width) ||
                    !double.IsFinite(coordinatePlane.Height))
                    throw new ArgumentException("Coordinate plane size must be finite and at least 80 pixels.", nameof(mathObject));
                if (coordinatePlane.GridSpacing < 8 ||
                    !double.IsFinite(coordinatePlane.GridSpacing))
                    throw new ArgumentException("Coordinate plane grid spacing must be finite and at least 8 pixels.", nameof(mathObject));
            }

            if (mathObject is SolidObject solid)
            {
                if (solid.Width <= 0 || solid.Height <= 0 || solid.Depth <= 0 ||
                    solid.Radius <= 0 ||
                    !double.IsFinite(solid.Width) ||
                    !double.IsFinite(solid.Height) ||
                    !double.IsFinite(solid.Depth) ||
                    !double.IsFinite(solid.Radius))
                    throw new ArgumentException("Solid dimensions must be finite positive numbers.", nameof(mathObject));
                if (solid.Scale <= 0 || !double.IsFinite(solid.Scale))
                    throw new ArgumentException("Solid scale must be a finite positive number.", nameof(mathObject));
                if (solid.HorizontalScale <= 0 || solid.VerticalScale <= 0 ||
                    !double.IsFinite(solid.HorizontalScale) ||
                    !double.IsFinite(solid.VerticalScale))
                    throw new ArgumentException("Solid stretch factors must be finite positive numbers.", nameof(mathObject));
                if (solid.RenderQuality < 1 || solid.RenderQuality > 3)
                    throw new ArgumentOutOfRangeException(nameof(mathObject), "Solid render quality must be between 1 and 3.");
                if (!Enum.IsDefined(typeof(SolidViewMode), solid.ViewMode))
                    throw new ArgumentOutOfRangeException(nameof(mathObject), "Solid view mode is invalid.");
            }

            if (mathObject.SolidAttachment != null)
            {
                var attachment = mathObject.SolidAttachment;
                if (attachment.SolidId == Guid.Empty || attachment.LocalPoints == null || attachment.LocalPoints.Count == 0)
                    throw new ArgumentException("Solid attachment is invalid.", nameof(mathObject));
                for (var i = 0; i < attachment.LocalPoints.Count; i++)
                {
                    var point = attachment.LocalPoints[i];
                    if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                        throw new ArgumentException("Solid attachment point is invalid.", nameof(mathObject));
                }
            }
        }

        private static bool PointsCoincide(MathPoint first, MathPoint second)
        {
            return first.X == second.X && first.Y == second.Y;
        }
    }
}
