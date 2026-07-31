using System;

namespace Ink_Canvas.Mathematics.Services
{
    public static class SolidRotationSnapService
    {
        public const double RightAngleIncrement = 90;
        public const double RightAngleTolerance = 5;

        public static double SnapToRightAngle(double angle)
        {
            if (!double.IsFinite(angle)) return angle;

            var normalized = Normalize(angle);
            var nearest = Math.Round(normalized / RightAngleIncrement) * RightAngleIncrement;
            var distance = Math.Abs(normalized - nearest);
            if (distance <= RightAngleTolerance ||
                Math.Abs(distance - 360) <= RightAngleTolerance)
                return Normalize(nearest);

            return normalized;
        }

        public static double Normalize(double angle)
        {
            angle %= 360;
            return angle < 0 ? angle + 360 : angle;
        }
    }
}
