using System.Collections.Generic;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Services
{
    public enum FunctionMonotonicity
    {
        Increasing,
        Decreasing
    }

    public sealed class FunctionMonotonicInterval
    {
        public double Start { get; set; }

        public double End { get; set; }

        public FunctionMonotonicity Monotonicity { get; set; }
    }

    public sealed class FunctionAnalysisResult
    {
        public FunctionAnalysisResult()
        {
            Zeros = new List<MathPoint>();
            Extrema = new List<MathPoint>();
            MonotonicIntervals = new List<FunctionMonotonicInterval>();
        }

        public List<MathPoint> Zeros { get; }

        public List<MathPoint> Extrema { get; }

        public MathPoint? YAxisIntercept { get; set; }

        public List<FunctionMonotonicInterval> MonotonicIntervals { get; }
    }
}
