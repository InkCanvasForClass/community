namespace Ink_Canvas.Mathematics.Models
{
    public struct MathPoint
    {
        public MathPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }

        public double Y { get; set; }
    }
}
