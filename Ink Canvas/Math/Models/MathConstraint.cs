using System;
using System.Collections.Generic;

namespace Ink_Canvas.Mathematics.Models
{
    public enum MathConstraintType
    {
        Collinear,
        Horizontal,
        Vertical,
        EqualLength,
        PointOnLine,
        PointOnCircle,
        Parallel,
        Perpendicular
    }

    public sealed class MathConstraint
    {
        public MathConstraint()
        {
            Id = Guid.NewGuid();
            ObjectIds = new List<Guid>();
            IsEnabled = true;
        }

        public Guid Id { get; set; }

        public MathConstraintType Type { get; set; }

        public List<Guid> ObjectIds { get; set; }

        public bool IsEnabled { get; set; }
    }
}
