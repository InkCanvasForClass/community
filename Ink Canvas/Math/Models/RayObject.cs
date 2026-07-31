namespace Ink_Canvas.Mathematics.Models
{
    public sealed class RayObject : MathObject
    {
        public RayObject() : base(MathObjectType.Ray)
        {
        }

        public MathPoint Start { get; set; }

        public MathPoint Through { get; set; }

        public System.Guid? StartPointId { get; set; }

        public System.Guid? ThroughPointId { get; set; }
    }
}
