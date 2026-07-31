using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class SolidMeasurementService
    {
        public static double Volume(SolidObject solid)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return solid.SolidType switch
            {
                SolidType.Cube => solid.Width * solid.Width * solid.Width,
                SolidType.Cuboid => solid.Width * solid.Height * solid.Depth,
                SolidType.Prism => solid.Width * solid.Depth * solid.Height / 2,
                SolidType.Pyramid => solid.Width * solid.Depth * solid.Height / 3,
                SolidType.Cylinder => Math.PI * solid.Radius * solid.Radius * solid.Height,
                SolidType.Cone => Math.PI * solid.Radius * solid.Radius * solid.Height / 3,
                SolidType.Sphere => 4 * Math.PI * solid.Radius * solid.Radius * solid.Radius / 3,
                _ => 0
            };
        }

        public static double SurfaceArea(SolidObject solid)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            return solid.SolidType switch
            {
                SolidType.Cube => 6 * solid.Width * solid.Width,
                SolidType.Cuboid => 2 * (solid.Width * solid.Height + solid.Width * solid.Depth + solid.Height * solid.Depth),
                SolidType.Prism =>
                    solid.Width * solid.Height +
                    solid.Depth * (solid.Width + solid.Height + Math.Sqrt(solid.Width * solid.Width + solid.Height * solid.Height)),
                SolidType.Pyramid =>
                    solid.Width * solid.Depth +
                    solid.Width * Math.Sqrt(solid.Height * solid.Height + solid.Depth * solid.Depth / 4) +
                    solid.Depth * Math.Sqrt(solid.Height * solid.Height + solid.Width * solid.Width / 4),
                SolidType.Cylinder => 2 * Math.PI * solid.Radius * (solid.Radius + solid.Height),
                SolidType.Cone => Math.PI * solid.Radius * (solid.Radius + Math.Sqrt(solid.Radius * solid.Radius + solid.Height * solid.Height)),
                SolidType.Sphere => 4 * Math.PI * solid.Radius * solid.Radius,
                _ => double.NaN
            };
        }
    }
}
