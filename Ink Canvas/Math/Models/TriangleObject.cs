namespace Ink_Canvas.Mathematics.Models
{
    public sealed class TriangleObject : MathObject
    {
        public TriangleObject() : base(MathObjectType.Triangle)
        {
        }

        public MathPoint First { get; set; }

        public MathPoint Second { get; set; }

        public MathPoint Third { get; set; }

        public System.Guid? FirstPointId { get; set; }

        public System.Guid? SecondPointId { get; set; }

        public System.Guid? ThirdPointId { get; set; }
    }

    public enum TriangleCircleKind
    {
        Circumcircle,
        Incircle
    }
}
