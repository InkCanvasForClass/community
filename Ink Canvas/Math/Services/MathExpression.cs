using System;

namespace Ink_Canvas.Mathematics.Services
{
    public sealed class MathExpression
    {
        private readonly Func<double, double> _evaluate;

        internal MathExpression(Func<double, double> evaluate)
        {
            _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        }

        public double Evaluate(double x)
        {
            var result = _evaluate(x);
            return double.IsFinite(result) ? result : double.NaN;
        }
    }
}
