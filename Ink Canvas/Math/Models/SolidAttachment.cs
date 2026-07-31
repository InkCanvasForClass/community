using System;
using System.Collections.Generic;

namespace Ink_Canvas.Mathematics.Models
{
    public sealed class SolidAttachment
    {
        public SolidAttachment()
        {
            LocalPoints = new List<MathPoint3D>();
        }

        public Guid SolidId { get; set; }

        public List<MathPoint3D> LocalPoints { get; set; }
    }
}
