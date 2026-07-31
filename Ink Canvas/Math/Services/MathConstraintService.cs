using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathConstraintService
    {
        private const double DefaultTolerance = 0.01;

        public static void Add(MathScene scene, MathConstraint constraint)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (constraint == null) throw new ArgumentNullException(nameof(constraint));
            if (constraint.Id == Guid.Empty)
                throw new ArgumentException("Constraint ID cannot be empty.", nameof(constraint));
            if (scene.Constraints.Exists(item => item.Id == constraint.Id))
                throw new InvalidOperationException("A math constraint with the same ID already exists.");

            Validate(scene, constraint);
            scene.Constraints.Add(constraint);
        }

        public static bool TryApplyAll(MathScene scene, out string error)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            error = null;
            if (scene.Constraints == null || scene.Constraints.Count == 0)
            {
                MathReferenceService.Synchronize(scene);
                return true;
            }

            var snapshot = CaptureObjects(scene);
            for (var pass = 0; pass < 8; pass++)
            {
                for (var i = 0; i < scene.Constraints.Count; i++)
                {
                    var constraint = scene.Constraints[i];
                    if (constraint == null || !constraint.IsEnabled) continue;
                    try
                    {
                        Validate(scene, constraint);
                        Apply(scene, constraint);
                        MathReferenceService.Synchronize(scene);
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException ||
                        exception is InvalidOperationException)
                    {
                        RestoreObjects(scene, snapshot);
                        MathReferenceService.Synchronize(scene);
                        error = exception.Message;
                        return false;
                    }
                }
            }

            for (var i = 0; i < scene.Constraints.Count; i++)
            {
                var constraint = scene.Constraints[i];
                if (constraint != null && constraint.IsEnabled && !IsSatisfied(scene, constraint, DefaultTolerance))
                {
                    RestoreObjects(scene, snapshot);
                    MathReferenceService.Synchronize(scene);
                    error = "The requested constraints conflict with the existing scene.";
                    return false;
                }
            }

            return true;
        }

        public static bool IsSatisfied(MathScene scene, MathConstraint constraint, double tolerance)
        {
            Validate(scene, constraint);
            var ids = constraint.ObjectIds;
            switch (constraint.Type)
            {
                case MathConstraintType.Horizontal:
                {
                    var segment = Get<SegmentObject>(scene, ids[0]);
                    return Math.Abs(segment.Start.Y - segment.End.Y) <= tolerance;
                }
                case MathConstraintType.Vertical:
                {
                    var segment = Get<SegmentObject>(scene, ids[0]);
                    return Math.Abs(segment.Start.X - segment.End.X) <= tolerance;
                }
                case MathConstraintType.EqualLength:
                {
                    var first = Get<SegmentObject>(scene, ids[0]);
                    var second = Get<SegmentObject>(scene, ids[1]);
                    return Math.Abs(Length(first) - Length(second)) <= tolerance;
                }
                case MathConstraintType.Collinear:
                    return DistanceToLine(
                        Get<PointObject>(scene, ids[2]).Position,
                        Get<PointObject>(scene, ids[0]).Position,
                        Get<PointObject>(scene, ids[1]).Position) <= tolerance;
                case MathConstraintType.PointOnLine:
                {
                    var point = Get<PointObject>(scene, ids[0]);
                    var line = Get<LineObject>(scene, ids[1]);
                    return DistanceToLine(point.Position, line.Start, line.End) <= tolerance;
                }
                case MathConstraintType.PointOnCircle:
                {
                    var point = Get<PointObject>(scene, ids[0]);
                    var circle = Get<CircleObject>(scene, ids[1]);
                    return Math.Abs(MathMeasurementService.Distance(point.Position, circle.Center) - circle.Radius) <= tolerance;
                }
                case MathConstraintType.Parallel:
                    return IsParallel(
                        Get<SegmentObject>(scene, ids[0]),
                        Get<SegmentObject>(scene, ids[1]),
                        tolerance);
                case MathConstraintType.Perpendicular:
                    return IsPerpendicular(
                        Get<SegmentObject>(scene, ids[0]),
                        Get<SegmentObject>(scene, ids[1]),
                        tolerance);
                default:
                    return false;
            }
        }

        public static void Validate(MathScene scene, MathConstraint constraint)
        {
            if (constraint.ObjectIds == null)
                throw new ArgumentException("Constraint object references are missing.", nameof(constraint));

            var expected = constraint.Type switch
            {
                MathConstraintType.Horizontal => 1,
                MathConstraintType.Vertical => 1,
                MathConstraintType.EqualLength => 2,
                MathConstraintType.Collinear => 3,
                MathConstraintType.PointOnLine => 2,
                MathConstraintType.PointOnCircle => 2,
                MathConstraintType.Parallel => 2,
                MathConstraintType.Perpendicular => 2,
                _ => throw new ArgumentException("Unknown constraint type.", nameof(constraint))
            };
            if (constraint.ObjectIds.Count != expected)
                throw new ArgumentException($"Constraint requires {expected} object references.", nameof(constraint));

            switch (constraint.Type)
            {
                case MathConstraintType.Horizontal:
                case MathConstraintType.Vertical:
                    Get<SegmentObject>(scene, constraint.ObjectIds[0]);
                    break;
                case MathConstraintType.EqualLength:
                    Get<SegmentObject>(scene, constraint.ObjectIds[0]);
                    Get<SegmentObject>(scene, constraint.ObjectIds[1]);
                    break;
                case MathConstraintType.Collinear:
                    Get<PointObject>(scene, constraint.ObjectIds[0]);
                    Get<PointObject>(scene, constraint.ObjectIds[1]);
                    Get<PointObject>(scene, constraint.ObjectIds[2]);
                    break;
                case MathConstraintType.PointOnLine:
                    Get<PointObject>(scene, constraint.ObjectIds[0]);
                    Get<LineObject>(scene, constraint.ObjectIds[1]);
                    break;
                case MathConstraintType.PointOnCircle:
                    Get<PointObject>(scene, constraint.ObjectIds[0]);
                    Get<CircleObject>(scene, constraint.ObjectIds[1]);
                    break;
                case MathConstraintType.Parallel:
                case MathConstraintType.Perpendicular:
                    Get<SegmentObject>(scene, constraint.ObjectIds[0]);
                    Get<SegmentObject>(scene, constraint.ObjectIds[1]);
                    break;
            }
        }

        private static void Apply(MathScene scene, MathConstraint constraint)
        {
            var ids = constraint.ObjectIds;
            switch (constraint.Type)
            {
                case MathConstraintType.Horizontal:
                {
                    var segment = Get<SegmentObject>(scene, ids[0]);
                    SetSegmentEnd(scene, segment, new MathPoint(segment.End.X, segment.Start.Y));
                    break;
                }
                case MathConstraintType.Vertical:
                {
                    var segment = Get<SegmentObject>(scene, ids[0]);
                    SetSegmentEnd(scene, segment, new MathPoint(segment.Start.X, segment.End.Y));
                    break;
                }
                case MathConstraintType.EqualLength:
                {
                    var first = Get<SegmentObject>(scene, ids[0]);
                    var second = Get<SegmentObject>(scene, ids[1]);
                    var targetLength = Length(first);
                    var currentLength = Length(second);
                    if (targetLength <= double.Epsilon || currentLength <= double.Epsilon)
                        throw new InvalidOperationException("Equal-length constraints require non-zero segments.");
                    var scale = targetLength / currentLength;
                    SetSegmentEnd(
                        scene,
                        second,
                        new MathPoint(
                            second.Start.X + (second.End.X - second.Start.X) * scale,
                            second.Start.Y + (second.End.Y - second.Start.Y) * scale));
                    break;
                }
                case MathConstraintType.Collinear:
                {
                    var first = Get<PointObject>(scene, ids[0]);
                    var second = Get<PointObject>(scene, ids[1]);
                    var third = Get<PointObject>(scene, ids[2]);
                    third.Position = ProjectToLine(third.Position, first.Position, second.Position);
                    break;
                }
                case MathConstraintType.PointOnLine:
                {
                    var point = Get<PointObject>(scene, ids[0]);
                    var line = Get<LineObject>(scene, ids[1]);
                    point.Position = ProjectToLine(point.Position, line.Start, line.End);
                    break;
                }
                case MathConstraintType.PointOnCircle:
                {
                    var point = Get<PointObject>(scene, ids[0]);
                    var circle = Get<CircleObject>(scene, ids[1]);
                    var deltaX = point.Position.X - circle.Center.X;
                    var deltaY = point.Position.Y - circle.Center.Y;
                    var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (length <= double.Epsilon)
                        throw new InvalidOperationException("A point at the circle center cannot be constrained to its circumference.");
                    point.Position = new MathPoint(
                        circle.Center.X + deltaX / length * circle.Radius,
                        circle.Center.Y + deltaY / length * circle.Radius);
                    break;
                }
                case MathConstraintType.Parallel:
                    AlignSegment(scene, Get<SegmentObject>(scene, ids[0]), Get<SegmentObject>(scene, ids[1]), false);
                    break;
                case MathConstraintType.Perpendicular:
                    AlignSegment(scene, Get<SegmentObject>(scene, ids[0]), Get<SegmentObject>(scene, ids[1]), true);
                    break;
            }
        }

        private static bool IsParallel(SegmentObject first, SegmentObject second, double tolerance)
        {
            var firstX = first.End.X - first.Start.X;
            var firstY = first.End.Y - first.Start.Y;
            var secondX = second.End.X - second.Start.X;
            var secondY = second.End.Y - second.Start.Y;
            var denominator = Math.Sqrt(firstX * firstX + firstY * firstY) *
                              Math.Sqrt(secondX * secondX + secondY * secondY);
            if (denominator <= double.Epsilon) return false;
            return Math.Abs(firstX * secondY - firstY * secondX) / denominator <= tolerance;
        }

        private static bool IsPerpendicular(SegmentObject first, SegmentObject second, double tolerance)
        {
            var firstX = first.End.X - first.Start.X;
            var firstY = first.End.Y - first.Start.Y;
            var secondX = second.End.X - second.Start.X;
            var secondY = second.End.Y - second.Start.Y;
            var denominator = Math.Sqrt(firstX * firstX + firstY * firstY) *
                              Math.Sqrt(secondX * secondX + secondY * secondY);
            if (denominator <= double.Epsilon) return false;
            return Math.Abs(firstX * secondX + firstY * secondY) / denominator <= tolerance;
        }

        private static void AlignSegment(
            MathScene scene,
            SegmentObject reference,
            SegmentObject target,
            bool perpendicular)
        {
            var referenceX = reference.End.X - reference.Start.X;
            var referenceY = reference.End.Y - reference.Start.Y;
            var referenceLength = Math.Sqrt(referenceX * referenceX + referenceY * referenceY);
            var targetX = target.End.X - target.Start.X;
            var targetY = target.End.Y - target.Start.Y;
            var targetLength = Math.Sqrt(targetX * targetX + targetY * targetY);
            if (referenceLength <= double.Epsilon || targetLength <= double.Epsilon)
                throw new InvalidOperationException("Parallel and perpendicular constraints require non-zero segments.");

            var directionX = referenceX / referenceLength;
            var directionY = referenceY / referenceLength;
            if (perpendicular)
            {
                var originalX = directionX;
                directionX = -directionY;
                directionY = originalX;
            }
            if (directionX * targetX + directionY * targetY < 0)
            {
                directionX = -directionX;
                directionY = -directionY;
            }
            SetSegmentEnd(
                scene,
                target,
                new MathPoint(
                    target.Start.X + directionX * targetLength,
                    target.Start.Y + directionY * targetLength));
        }

        private static void SetSegmentEnd(MathScene scene, SegmentObject segment, MathPoint value)
        {
            if (segment.EndPointId.HasValue)
                Get<PointObject>(scene, segment.EndPointId.Value).Position = value;
            segment.End = value;
        }

        private static T Get<T>(MathScene scene, Guid id) where T : MathObject
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                if (scene.Objects[i].Id == id)
                {
                    if (scene.Objects[i] is T result) return result;
                    throw new ArgumentException($"Constraint reference {id} has an incompatible object type.");
                }
            }

            throw new ArgumentException($"Constraint reference {id} does not exist.");
        }

        private static double Length(SegmentObject segment)
        {
            return MathMeasurementService.Distance(segment.Start, segment.End);
        }

        private static MathPoint ProjectToLine(MathPoint point, MathPoint start, MathPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared <= double.Epsilon)
                throw new InvalidOperationException("Constraint reference line must have distinct points.");
            var projection = ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) / lengthSquared;
            return new MathPoint(start.X + projection * deltaX, start.Y + projection * deltaY);
        }

        private static double DistanceToLine(MathPoint point, MathPoint start, MathPoint end)
        {
            return MathMeasurementService.Distance(point, ProjectToLine(point, start, end));
        }

        private static Dictionary<Guid, MathObjectState> CaptureObjects(MathScene scene)
        {
            var result = new Dictionary<Guid, MathObjectState>();
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                var mathObject = scene.Objects[i];
                result[mathObject.Id] = MathObjectState.Capture(mathObject);
            }
            return result;
        }

        private static void RestoreObjects(
            MathScene scene,
            IReadOnlyDictionary<Guid, MathObjectState> snapshot)
        {
            for (var i = 0; i < scene.Objects.Count; i++)
            {
                var mathObject = scene.Objects[i];
                if (snapshot.TryGetValue(mathObject.Id, out var state))
                    state.Restore(mathObject);
            }
        }

        private sealed class MathObjectState
        {
            private MathPoint _first;
            private MathPoint _second;
            private MathPoint _third;
            private double _radius;

            public static MathObjectState Capture(MathObject mathObject)
            {
                var state = new MathObjectState();
                switch (mathObject)
                {
                    case PointObject point:
                        state._first = point.Position;
                        break;
                    case SegmentObject segment:
                        state._first = segment.Start;
                        state._second = segment.End;
                        break;
                    case LineObject line:
                        state._first = line.Start;
                        state._second = line.End;
                        break;
                    case RayObject ray:
                        state._first = ray.Start;
                        state._second = ray.Through;
                        break;
                    case CircleObject circle:
                        state._first = circle.Center;
                        state._radius = circle.Radius;
                        break;
                    case AngleMeasurementObject angle:
                        state._first = angle.First;
                        state._second = angle.Vertex;
                        state._third = angle.Second;
                        break;
                    case TextLabelObject label:
                        state._first = label.Position;
                        break;
                    case FunctionObject function:
                        state._first = function.Origin;
                        break;
                    case SolidObject solid:
                        state._first = solid.Center;
                        state._second = new MathPoint(solid.RotationX, solid.RotationY);
                        state._third = new MathPoint(solid.RotationZ, solid.Scale);
                        break;
                }
                return state;
            }

            public void Restore(MathObject mathObject)
            {
                switch (mathObject)
                {
                    case PointObject point:
                        point.Position = _first;
                        break;
                    case SegmentObject segment:
                        segment.Start = _first;
                        segment.End = _second;
                        break;
                    case LineObject line:
                        line.Start = _first;
                        line.End = _second;
                        break;
                    case RayObject ray:
                        ray.Start = _first;
                        ray.Through = _second;
                        break;
                    case CircleObject circle:
                        circle.Center = _first;
                        circle.Radius = _radius;
                        break;
                    case AngleMeasurementObject angle:
                        angle.First = _first;
                        angle.Vertex = _second;
                        angle.Second = _third;
                        break;
                    case TextLabelObject label:
                        label.Position = _first;
                        break;
                    case FunctionObject function:
                        function.Origin = _first;
                        break;
                    case SolidObject solid:
                        solid.Center = _first;
                        solid.RotationX = _second.X;
                        solid.RotationY = _second.Y;
                        solid.RotationZ = _third.X;
                        solid.Scale = _third.Y;
                        break;
                }
            }
        }
    }
}
