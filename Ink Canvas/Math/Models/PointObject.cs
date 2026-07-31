namespace Ink_Canvas.Mathematics.Models
{
    public sealed class PointObject : MathObject
    {
        public PointObject() : base(MathObjectType.Point)
        {
        }

        public MathPoint Position { get; set; }
    }
}
