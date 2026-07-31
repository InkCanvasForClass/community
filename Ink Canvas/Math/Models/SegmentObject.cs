namespace Ink_Canvas.Mathematics.Models
{
    public sealed class SegmentObject : MathObject
    {
        public SegmentObject() : base(MathObjectType.Segment)
        {
        }

        public MathPoint Start { get; set; }

        public MathPoint End { get; set; }

        public System.Guid? StartPointId { get; set; }

        public System.Guid? EndPointId { get; set; }
    }
}
