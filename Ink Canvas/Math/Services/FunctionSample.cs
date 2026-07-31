using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public sealed class FunctionSample
    {
        public FunctionSample()
        {
            Segments = new List<List<MathPoint>>();
            Zeros = new List<MathPoint>();
            Extrema = new List<MathPoint>();
        }

        public List<List<MathPoint>> Segments { get; }

        public List<MathPoint> Zeros { get; }

        public List<MathPoint> Extrema { get; }
    }
}
