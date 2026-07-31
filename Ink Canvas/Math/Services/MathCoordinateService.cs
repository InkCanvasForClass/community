using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathCoordinateService
    {
        public static MathPoint ToGridCoordinate(
            MathPoint screenPoint,
            double canvasWidth,
            double canvasHeight,
            double gridSpacing)
        {
            if (gridSpacing < 8 || !double.IsFinite(gridSpacing))
                throw new ArgumentOutOfRangeException(nameof(gridSpacing));
            return new MathPoint(
                (screenPoint.X - canvasWidth / 2) / gridSpacing,
                (canvasHeight / 2 - screenPoint.Y) / gridSpacing);
        }

        public static double ToGridLength(double pixels, double gridSpacing)
        {
            if (gridSpacing < 8 || !double.IsFinite(gridSpacing))
                throw new ArgumentOutOfRangeException(nameof(gridSpacing));
            return pixels / gridSpacing;
        }
    }
}
