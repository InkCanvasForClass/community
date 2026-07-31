using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class SolidSphereConstructionService
    {
        public static bool TryCreateCircumsphere(SolidObject solid, out SolidObject sphere)
        {
            return TryCreate(solid, false, out sphere);
        }

        public static bool TryCreateInsphere(SolidObject solid, out SolidObject sphere)
        {
            return TryCreate(solid, true, out sphere);
        }

        private static bool TryCreate(SolidObject solid, bool inscribed, out SolidObject sphere)
        {
            sphere = null;
            if (solid == null || solid.SolidType == SolidType.Sphere) return false;
            if (!TryGetSphere(solid, inscribed, out var center, out var radius)) return false;

            sphere = new SolidObject
            {
                SolidType = SolidType.Sphere,
                Center = SolidProjectionService.ProjectModelPoint(solid, center),
                Radius = radius,
                Width = radius * 2,
                Height = radius * 2,
                Depth = radius * 2,
                Scale = solid.Scale,
                HorizontalScale = solid.HorizontalScale,
                VerticalScale = solid.VerticalScale,
                RotationX = solid.RotationX,
                RotationY = solid.RotationY,
                RotationZ = solid.RotationZ,
                ViewMode = solid.ViewMode,
                ProjectionMode = solid.ProjectionMode,
                ShowHiddenEdges = solid.ShowHiddenEdges,
                ShowLabels = solid.ShowLabels,
                RenderQuality = solid.RenderQuality,
                SolidAttachment = new SolidAttachment { SolidId = solid.Id }
            };
            sphere.SolidAttachment.LocalPoints.Add(center);
            return true;
        }

        private static bool TryGetSphere(
            SolidObject solid,
            bool inscribed,
            out MathPoint3D center,
            out double radius)
        {
            center = new MathPoint3D(0, 0, 0);
            radius = 0;
            switch (solid.SolidType)
            {
                case SolidType.Cube:
                    radius = inscribed
                        ? solid.Width / 2
                        : Math.Sqrt(3 * solid.Width * solid.Width) / 2;
                    return true;
                case SolidType.Cuboid:
                    if (inscribed && !NearlyEqual(solid.Width, solid.Height, solid.Depth)) return false;
                    radius = inscribed
                        ? solid.Width / 2
                        : Math.Sqrt(solid.Width * solid.Width + solid.Height * solid.Height + solid.Depth * solid.Depth) / 2;
                    return true;
                case SolidType.Prism:
                    var baseInradius = (solid.Width + solid.Height - Math.Sqrt(solid.Width * solid.Width + solid.Height * solid.Height)) / 2;
                    if (inscribed)
                    {
                        if (!NearlyEqual(solid.Depth, 2 * baseInradius)) return false;
                        center = new MathPoint3D(-solid.Width / 2 + baseInradius, -solid.Height / 2 + baseInradius, 0);
                        radius = baseInradius;
                    }
                    else
                    {
                        radius = Math.Sqrt(solid.Width * solid.Width + solid.Height * solid.Height + solid.Depth * solid.Depth) / 2;
                    }
                    return true;
                case SolidType.Pyramid:
                    var baseRadiusSquared = (solid.Width * solid.Width + solid.Depth * solid.Depth) / 4;
                    if (inscribed)
                    {
                        if (!NearlyEqual(solid.Width, solid.Depth)) return false;
                        var slantHeight = Math.Sqrt(solid.Height * solid.Height + solid.Width * solid.Width / 4);
                        radius = solid.Width * solid.Height / (solid.Width + 2 * slantHeight);
                        center = new MathPoint3D(0, -solid.Height / 2 + radius, 0);
                    }
                    else
                    {
                        var fromBase = (solid.Height * solid.Height - baseRadiusSquared) / (2 * solid.Height);
                        center = new MathPoint3D(0, -solid.Height / 2 + fromBase, 0);
                        radius = (solid.Height * solid.Height + baseRadiusSquared) / (2 * solid.Height);
                    }
                    return true;
                case SolidType.Cylinder:
                    if (inscribed && !NearlyEqual(solid.Height, solid.Radius * 2)) return false;
                    radius = inscribed
                        ? solid.Radius
                        : Math.Sqrt(solid.Radius * solid.Radius + solid.Height * solid.Height / 4);
                    return true;
                case SolidType.Cone:
                    if (inscribed)
                    {
                        radius = solid.Radius * solid.Height /
                                 (solid.Radius + Math.Sqrt(solid.Radius * solid.Radius + solid.Height * solid.Height));
                        center = new MathPoint3D(0, -solid.Height / 2 + radius, 0);
                    }
                    else
                    {
                        var fromBase = (solid.Height * solid.Height - solid.Radius * solid.Radius) / (2 * solid.Height);
                        center = new MathPoint3D(0, -solid.Height / 2 + fromBase, 0);
                        radius = (solid.Height * solid.Height + solid.Radius * solid.Radius) / (2 * solid.Height);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static bool NearlyEqual(double first, params double[] others)
        {
            for (var i = 0; i < others.Length; i++)
            {
                if (Math.Abs(first - others[i]) > 1e-6 * Math.Max(1, Math.Max(Math.Abs(first), Math.Abs(others[i]))))
                    return false;
            }
            return true;
        }
    }
}
