using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// 湿墨预览笔尾预测：用最近 2~3 个真实点估速度/加速度，外推一段未来轨迹。
    /// 预测点仅进入实时预览，不进入提交/撤销/保存。
    /// </summary>
    internal static class InkTailPredictor
    {
        // 约 120ms 视界：12 点 × 10ms。书写时通常可见 30~120px 的笔尾。
        public const int DefaultPointCount = 12;
        public const long DefaultStepMicroseconds = 10_000L;
        public const long MinStepMicroseconds = 2_000L;
        private const double MaxPredictionSpeedPxPerSecond = 8_000.0;
        private const double MinPredictionSpeedPxPerSecond = 40.0;
        private const double MaxPredictionDistancePx = 140.0;
        private const double DecayPerStep = 0.97;

        public static IReadOnlyList<PredictedInkPoint> Build(IReadOnlyList<RealInkPoint> realPoints)
        {
            return Build(realPoints, DefaultPointCount, DefaultStepMicroseconds);
        }

        public static IReadOnlyList<PredictedInkPoint> Build(
            IReadOnlyList<RealInkPoint> realPoints,
            int pointCount,
            long stepMicroseconds)
        {
            if (pointCount <= 0)
                pointCount = DefaultPointCount;
            stepMicroseconds = Math.Max(MinStepMicroseconds, stepMicroseconds);

            var result = new List<PredictedInkPoint>(pointCount);
            if (realPoints == null || realPoints.Count < 2)
                return result;

            var last = realPoints[realPoints.Count - 1];
            var secondLast = realPoints[realPoints.Count - 2];
            EstimateVelocity(
                realPoints,
                out var velocityX,
                out var velocityY,
                out var accelerationX,
                out var accelerationY);

            var speed = Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
            if (speed < MinPredictionSpeedPxPerSecond)
                return result;

            if (speed > MaxPredictionSpeedPxPerSecond)
            {
                var scale = MaxPredictionSpeedPxPerSecond / speed;
                velocityX *= scale;
                velocityY *= scale;
                accelerationX *= scale;
                accelerationY *= scale;
                speed = MaxPredictionSpeedPxPerSecond;
            }

            var stepSeconds = stepMicroseconds / 1_000_000.0;
            var stamp = last.TimestampMicroseconds;
            var currX = last.X;
            var currY = last.Y;
            var vx = velocityX;
            var vy = velocityY;
            var pressure = last.Pressure;
            var traveled = 0.0;

            for (var i = 0; i < pointCount; i++)
            {
                // 半隐式欧拉：先更新速度再积分位置，并逐步衰减，避免笔尾发散。
                vx = (vx + accelerationX * stepSeconds) * DecayPerStep;
                vy = (vy + accelerationY * stepSeconds) * DecayPerStep;
                var nextX = currX + vx * stepSeconds;
                var nextY = currY + vy * stepSeconds;
                if (!IsFinite(nextX) || !IsFinite(nextY))
                    break;

                var stepDistance = Math.Sqrt(
                    (nextX - currX) * (nextX - currX) + (nextY - currY) * (nextY - currY));
                traveled += stepDistance;
                if (traveled > MaxPredictionDistancePx)
                    break;

                stamp += stepMicroseconds;
                currX = nextX;
                currY = nextY;

                // 越靠后压感越细，强化“预言之笔”的可见锥形尾。
                var taper = (float)Math.Pow(DecayPerStep, i + 1);
                var predictedPressure = Math.Clamp(pressure * (0.55f + 0.45f * taper), 0.08f, 1.0f);
                result.Add(new PredictedInkPoint(currX, currY, predictedPressure, stamp));
            }

            // 反向折叠裁剪：若预测方向与最近真实方向夹角过大，去掉回折点。
            if (realPoints.Count >= 2 && result.Count >= 2)
            {
                var refX = last.X - secondLast.X;
                var refY = last.Y - secondLast.Y;
                var refLen = Math.Sqrt(refX * refX + refY * refY);
                if (refLen > 0.01)
                {
                    refX /= refLen;
                    refY /= refLen;
                    for (var i = result.Count - 1; i >= 0; i--)
                    {
                        var dx = result[i].X - last.X;
                        var dy = result[i].Y - last.Y;
                        if (dx * refX + dy * refY < 0)
                            result.RemoveAt(i);
                    }
                }
            }

            return result;
        }

        private static void EstimateVelocity(
            IReadOnlyList<RealInkPoint> realPoints,
            out double velocityX,
            out double velocityY,
            out double accelerationX,
            out double accelerationY)
        {
            velocityX = 0;
            velocityY = 0;
            accelerationX = 0;
            accelerationY = 0;

            var last = realPoints[realPoints.Count - 1];
            var secondLast = realPoints[realPoints.Count - 2];
            if (!TrySegmentVelocity(secondLast, last, out velocityX, out velocityY))
            {
                // 时间戳异常时，按默认步长把弦长当作速度。
                var chordX = last.X - secondLast.X;
                var chordY = last.Y - secondLast.Y;
                var chordLen = Math.Sqrt(chordX * chordX + chordY * chordY);
                if (chordLen <= 0.0001)
                    return;
                var fallbackSpeed = Math.Min(
                    chordLen / (DefaultStepMicroseconds / 1_000_000.0),
                    MaxPredictionSpeedPxPerSecond);
                velocityX = chordX / chordLen * fallbackSpeed;
                velocityY = chordY / chordLen * fallbackSpeed;
                return;
            }

            if (realPoints.Count < 3)
                return;

            var thirdLast = realPoints[realPoints.Count - 3];
            if (!TrySegmentVelocity(thirdLast, secondLast, out var prevVx, out var prevVy))
                return;

            // 用最近两段速度差估计加速度，使转弯时的预测更跟手。
            var dt = (last.TimestampMicroseconds - secondLast.TimestampMicroseconds) / 1_000_000.0;
            if (dt <= 0.000001)
                dt = DefaultStepMicroseconds / 1_000_000.0;
            accelerationX = (velocityX - prevVx) / dt;
            accelerationY = (velocityY - prevVy) / dt;

            // 限制加速度，避免噪声把笔尾甩飞。
            var accel = Math.Sqrt(accelerationX * accelerationX + accelerationY * accelerationY);
            const double maxAccel = 80_000.0;
            if (accel > maxAccel)
            {
                var scale = maxAccel / accel;
                accelerationX *= scale;
                accelerationY *= scale;
            }
        }

        private static bool TrySegmentVelocity(
            RealInkPoint from,
            RealInkPoint to,
            out double velocityX,
            out double velocityY)
        {
            velocityX = 0;
            velocityY = 0;
            var dtUs = to.TimestampMicroseconds - from.TimestampMicroseconds;
            if (dtUs <= 0)
                return false;
            velocityX = (to.X - from.X) / dtUs * 1_000_000.0;
            velocityY = (to.Y - from.Y) / dtUs * 1_000_000.0;
            return true;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
