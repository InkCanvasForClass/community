namespace Ink_Canvas.Mathematics.Models
{
    public sealed class AngleMeasurementObject : MathObject
    {
        public AngleMeasurementObject() : base(MathObjectType.AngleMeasurement)
        {
        }

        public MathPoint First { get; set; }

        public MathPoint Vertex { get; set; }

        public MathPoint Second { get; set; }

        public System.Guid? FirstPointId { get; set; }

        public System.Guid? VertexPointId { get; set; }

        public System.Guid? SecondPointId { get; set; }
    }
}
