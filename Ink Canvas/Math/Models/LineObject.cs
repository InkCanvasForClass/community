namespace Ink_Canvas.Mathematics.Models
{
    public sealed class LineObject : MathObject
    {
        public LineObject() : base(MathObjectType.Line)
        {
        }

        public MathPoint Start { get; set; }

        public MathPoint End { get; set; }

        public System.Guid? StartPointId { get; set; }

        public System.Guid? EndPointId { get; set; }
    }
}
