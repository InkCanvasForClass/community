using System;
using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public readonly struct ProjectedSolidEdge
    {
        public ProjectedSolidEdge(MathPoint start, MathPoint end, bool isHidden)
        {
            Start = start;
            End = end;
            IsHidden = isHidden;
        }

        public MathPoint Start { get; }

        public MathPoint End { get; }

        public bool IsHidden { get; }
    }

    public sealed class SolidProjection
    {
        public SolidProjection()
        {
            Points = new List<MathPoint>();
            Edges = new List<ProjectedSolidEdge>();
        }

        public List<MathPoint> Points { get; }

        public List<ProjectedSolidEdge> Edges { get; }
    }

    public static class SolidProjectionService
    {
        public static SolidProjection Project(SolidObject solid)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            var mesh = SolidMeshBuilder.Build(solid);
            var rotated = new List<MathPoint3D>(mesh.Vertices.Count);
            var result = new SolidProjection();
            var averageZ = 0d;
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var point = Rotate(mesh.Vertices[i], solid);
                rotated.Add(point);
                averageZ += point.Z;
                result.Points.Add(ProjectPoint(point, solid));
            }
            if (rotated.Count > 0) averageZ /= rotated.Count;

            for (var i = 0; i < mesh.Edges.Count; i++)
            {
                var edge = mesh.Edges[i];
                var hidden = rotated[edge.Start].Z < averageZ &&
                             rotated[edge.End].Z < averageZ;
                result.Edges.Add(new ProjectedSolidEdge(
                    result.Points[edge.Start],
                    result.Points[edge.End],
                    hidden));
            }
            return result;
        }

        public static MathPoint ProjectModelPoint(SolidObject solid, MathPoint3D point)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return ProjectPoint(Rotate(point, solid), solid);
        }

        public static MathPoint3D TransformModelPoint(SolidObject solid, MathPoint3D point)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return Rotate(point, solid);
        }

        public static MathPoint ProjectWorldPoint(SolidObject solid, MathPoint3D point)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return ProjectPoint(point, solid);
        }

        private static MathPoint3D Rotate(MathPoint3D point, SolidObject solid)
        {
            var xAngle = solid.RotationX * Math.PI / 180;
            var yAngle = solid.RotationY * Math.PI / 180;
            var zAngle = solid.RotationZ * Math.PI / 180;

            var y1 = point.Y * Math.Cos(xAngle) - point.Z * Math.Sin(xAngle);
            var z1 = point.Y * Math.Sin(xAngle) + point.Z * Math.Cos(xAngle);
            var x2 = point.X * Math.Cos(yAngle) + z1 * Math.Sin(yAngle);
            var z2 = -point.X * Math.Sin(yAngle) + z1 * Math.Cos(yAngle);
            var x3 = x2 * Math.Cos(zAngle) - y1 * Math.Sin(zAngle);
            var y3 = x2 * Math.Sin(zAngle) + y1 * Math.Cos(zAngle);
            return new MathPoint3D(x3, y3, z2);
        }

        private static MathPoint ProjectPoint(MathPoint3D point, SolidObject solid)
        {
            if (solid.ViewMode == SolidViewMode.Front)
            {
                return new MathPoint(
                    solid.Center.X + point.X * solid.Scale * solid.HorizontalScale,
                    solid.Center.Y - point.Y * solid.Scale * solid.VerticalScale);
            }

            var factor = 1d;
            if (solid.ProjectionMode == SolidProjectionMode.Perspective)
            {
                const double CameraDistance = 12;
                factor = CameraDistance / Math.Max(1, CameraDistance - point.Z);
            }
            return new MathPoint(
                solid.Center.X + (point.X + point.Z * 0.55) * solid.Scale * solid.HorizontalScale * factor,
                solid.Center.Y - (point.Y + point.Z * 0.35) * solid.Scale * solid.VerticalScale * factor);
        }
    }
}
