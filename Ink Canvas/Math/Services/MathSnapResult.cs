using System;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public readonly struct MathSnapResult
    {
        public MathSnapResult(
            MathPoint position,
            Guid? pointObjectId,
            Guid? solidId = null,
            MathPoint3D? solidLocalPoint = null)
        {
            Position = position;
            PointObjectId = pointObjectId;
            SolidId = solidId;
            SolidLocalPoint = solidLocalPoint;
        }

        public MathPoint Position { get; }

        public Guid? PointObjectId { get; }

        public Guid? SolidId { get; }

        public MathPoint3D? SolidLocalPoint { get; }
    }
}
