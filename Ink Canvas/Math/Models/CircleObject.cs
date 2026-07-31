namespace Ink_Canvas.Mathematics.Models
{
    public sealed class CircleObject : MathObject
    {
        public CircleObject() : base(MathObjectType.Circle)
        {
        }

        public MathPoint Center { get; set; }

        public double Radius { get; set; }

        public System.Guid? CenterPointId { get; set; }

        public System.Guid? RadiusPointId { get; set; }

        public System.Guid? TriangleId { get; set; }

        public TriangleCircleKind? TriangleCircleKind { get; set; }
    }
}
