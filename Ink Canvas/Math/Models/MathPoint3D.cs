using System.Text.Json.Serialization;

namespace Ink_Canvas.Mathematics.Models
{
    public readonly struct MathPoint3D
    {
        [JsonConstructor]
        public MathPoint3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }
}
