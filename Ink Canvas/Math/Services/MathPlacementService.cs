using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathPlacementService
    {
        public const double MinimumCoordinatePlaneDrag = 80;
        public const double DefaultCoordinatePlaneWidth = 480;
        public const double DefaultCoordinatePlaneHeight = 320;

        public static CoordinatePlaneObject CreateCoordinatePlane(
            MathPoint start,
            MathPoint end,
            double gridSpacing,
            bool showGrid,
            bool showAxes)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var hasCustomSize =
                Math.Abs(deltaX) >= MinimumCoordinatePlaneDrag &&
                Math.Abs(deltaY) >= MinimumCoordinatePlaneDrag;

            return new CoordinatePlaneObject
            {
                Center = hasCustomSize
                    ? new MathPoint(
                        (start.X + end.X) / 2,
                        (start.Y + end.Y) / 2)
                    : start,
                Width = hasCustomSize ? Math.Abs(deltaX) : DefaultCoordinatePlaneWidth,
                Height = hasCustomSize ? Math.Abs(deltaY) : DefaultCoordinatePlaneHeight,
                GridSpacing = Math.Max(8, gridSpacing),
                ShowGrid = showGrid,
                ShowAxes = showAxes
            };
        }
    }
}
