using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathMeasurementService
    {
        public static double Distance(MathPoint first, MathPoint second)
        {
            var deltaX = first.X - second.X;
            var deltaY = first.Y - second.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public static double AngleDegrees(MathPoint first, MathPoint vertex, MathPoint second)
        {
            var firstX = first.X - vertex.X;
            var firstY = first.Y - vertex.Y;
            var secondX = second.X - vertex.X;
            var secondY = second.Y - vertex.Y;
            var firstLength = Math.Sqrt(firstX * firstX + firstY * firstY);
            var secondLength = Math.Sqrt(secondX * secondX + secondY * secondY);
            if (firstLength <= double.Epsilon || secondLength <= double.Epsilon)
                throw new ArgumentException("Angle points must be distinct from the vertex.");

            var cosine = (firstX * secondX + firstY * secondY) / (firstLength * secondLength);
            cosine = Math.Max(-1, Math.Min(1, cosine));
            return Math.Acos(cosine) * 180 / Math.PI;
        }
    }
}
