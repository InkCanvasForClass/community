using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// 湿墨预览笔尾预测：用最近 2~4 个真实点估速度/加速度/曲率，外推一段未来轨迹。
    /// 外推时长动态自适应（10~50ms）：快写直线取上限，慢写、急转弯、报点停滞时收敛到下限。
    /// 预测点仅进入实时预览，不进入提交/撤销/保存。
    /// </summary>
    internal static class InkTailPredictor
    {
        // 动态视界上下限（毫秒）。上限受“预测越长越容易在拐弯处甩飞”约束。
        public const double MinHorizonMilliseconds = 10.0;
        public const double MaxHorizonMilliseconds = 50.0;

        // 时间戳异常回退时用的名义报点间隔。
        private const long DefaultStepMicroseconds = 10_000L;
        private const long MinStepMicroseconds = 2_000L;

        // 视界内的采样粒度：约 4ms 一个预测点，再按上下限收敛点数。
        private const double TargetStepMilliseconds = 4.0;
        private const int MinPointCount = 3;
        private const int MaxPointCount = 12;

        private const double MaxPredictionSpeedPxPerSecond = 8_000.0;
        // 真实速度低于此值时不再强制返回空，而是按一档极小速度继续外推，
        // 避免加速度/减速阶段的帧间笔尾闪烁消失。`Build` 仍会在点数不足、停驻、完全 NaN 时返回空。
        private const double MinEffectiveSpeedPxPerSecond = 5.0;
        private const double MaxPredictionDistancePx = 140.0;

        // 速度→视界映射的两端。起点取最低预测速度，让超低速一离开门限就开始增长。
        private const double SlowSpeedPxPerSecond = 40.0;
        private const double FastSpeedPxPerSecond = 2_500.0;

        // 拐弯抑制：夹角在自由角内不抑制，超过满抑制角按最小比例保留。
        private const double TurnFreeAngleDegrees = 12.0;
        private const double TurnFullAngleDegrees = 60.0;
        private const double MinTurnScale = 0.2;

        // 报点停滞抑制：间隔越大速度越陈旧，外推越不可信。
        private const double FreshSampleIntervalMilliseconds = 12.0;
        private const double StaleSampleIntervalMilliseconds = 40.0;
        private const double MinStaleScale = 0.35;

        // 每 10ms 的速度衰减，按实际步长换算，避免笔尾发散。
        private const double DecayPer10Milliseconds = 0.97;
        // 末端压感相对笔尖的比例，形成可见的锥形尾。
        private const double TailPressureTaper = 0.69;

        /// <summary>
        /// 按当前笔速与曲率自适应决定外推时长后构建预测笔尾。
        /// </summary>
        public static IReadOnlyList<PredictedInkPoint> Build(IReadOnlyList<RealInkPoint> realPoints)
        {
            if (!TryEstimateMotion(realPoints, out var motion))
                return Array.Empty<PredictedInkPoint>();

            var horizonMicroseconds = ComputeAdaptiveHorizonMicroseconds(realPoints, motion);
            ResolveSampling(horizonMicroseconds, out var pointCount, out var stepMicroseconds);
            return Extrapolate(realPoints, motion, pointCount, stepMicroseconds);
        }

        private static IReadOnlyList<PredictedInkPoint> Extrapolate(
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailMotion motion,
            int pointCount,
            long stepMicroseconds)
        {
            var result = new List<PredictedInkPoint>(pointCount);
            var last = realPoints[realPoints.Count - 1];
            var secondLast = realPoints[realPoints.Count - 2];

            var stepSeconds = stepMicroseconds / 1_000_000.0;
            var decayPerStep = Math.Pow(DecayPer10Milliseconds, stepSeconds / 0.010);
            var stamp = last.TimestampMicroseconds;
            var currX = last.X;
            var currY = last.Y;
            var vx = motion.VelocityX;
            var vy = motion.VelocityY;
            var pressure = last.Pressure;
            var traveled = 0.0;

            for (var i = 0; i < pointCount; i++)
            {
                // 半隐式欧拉：先更新速度再积分位置，并逐步衰减，避免笔尾发散。
                vx = (vx + motion.AccelerationX * stepSeconds) * decayPerStep;
                vy = (vy + motion.AccelerationY * stepSeconds) * decayPerStep;
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

                // 越靠后压感越细，且与点数无关，保证视界变化时笔尾观感一致。
                var progress = (i + 1) / (double)pointCount;
                var taper = (float)Math.Pow(TailPressureTaper, progress);
                var predictedPressure = Math.Clamp(pressure * taper, 0.08f, 1.0f);
                result.Add(new PredictedInkPoint(currX, currY, predictedPressure, stamp));
            }

            // 反向折叠裁剪：若预测方向与最近真实方向夹角过大，去掉回折点。
            if (result.Count >= 2)
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

        /// <summary>
        /// 视界 = 速度映射基线 × 拐弯抑制 × 停滞抑制，再受最大外推距离约束，最终夹在 10~50ms。
        /// </summary>
        private static double ComputeAdaptiveHorizonMicroseconds(
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailMotion motion)
        {
            // 按速度的对数归一化：线性归一化会把 40~250px/s 整段压进 t<0.1，
            // 超低速因而贴死下限；人对笔速的感知本身也接近对数。
            var speedT = NormalizeSpeed(motion.Speed);

            var horizon = MinHorizonMilliseconds
                + (MaxHorizonMilliseconds - MinHorizonMilliseconds) * speedT;
            horizon *= motion.TurnScale;
            horizon *= ComputeStaleScale(realPoints);

            // 距离上限换算成时长一并纳入，避免积分循环中途截断导致实际视界短于预期。
            if (motion.Speed > 0)
            {
                var distanceLimitedMs = MaxPredictionDistancePx / motion.Speed * 1000.0;
                horizon = Math.Min(horizon, distanceLimitedMs);
            }

            horizon = Math.Clamp(horizon, MinHorizonMilliseconds, MaxHorizonMilliseconds);
            return horizon * 1000.0;
        }

        private static void ResolveSampling(
            double horizonMicroseconds,
            out int pointCount,
            out long stepMicroseconds)
        {
            var desired = (int)Math.Ceiling(horizonMicroseconds / (TargetStepMilliseconds * 1000.0));
            pointCount = Math.Clamp(desired, MinPointCount, MaxPointCount);
            // 向下取整，保证 点数 × 步长 不越过视界上限。
            stepMicroseconds = Math.Max(
                MinStepMicroseconds,
                (long)(horizonMicroseconds / pointCount));
            // 步长被下限抬高时相应收敛点数。
            pointCount = Math.Clamp(
                (int)(horizonMicroseconds / stepMicroseconds),
                1,
                pointCount);
        }

        /// <summary>
        /// 报点间隔越大，最近一段速度越陈旧，外推距离越应收敛。
        /// </summary>
        private static double ComputeStaleScale(IReadOnlyList<RealInkPoint> realPoints)
        {
            var last = realPoints[realPoints.Count - 1];
            var secondLast = realPoints[realPoints.Count - 2];
            var intervalMs = (last.TimestampMicroseconds - secondLast.TimestampMicroseconds) / 1000.0;
            if (intervalMs <= 0 || intervalMs <= FreshSampleIntervalMilliseconds)
                return 1.0;
            if (intervalMs >= StaleSampleIntervalMilliseconds)
                return MinStaleScale;

            var t = (intervalMs - FreshSampleIntervalMilliseconds)
                / (StaleSampleIntervalMilliseconds - FreshSampleIntervalMilliseconds);
            return 1.0 - (1.0 - MinStaleScale) * t;
        }

        /// <summary>
        /// 用最近最多三段方向的加权夹角衡量拐弯程度，返回 [MinTurnScale, 1] 的抑制系数。
        /// 越靠近笔尖的转折权重越高，直线书写返回 1。
        /// </summary>
        private static double ComputeTurnScale(IReadOnlyList<RealInkPoint> realPoints)
        {
            var count = realPoints.Count;
            if (count < 3)
                return 1.0;

            var angleSum = 0.0;
            var weightSum = 0.0;
            var weight = 1.0;
            var oldest = Math.Max(2, count - 3);
            for (var i = count - 1; i >= oldest; i--, weight *= 0.5)
            {
                if (!TryDirection(realPoints[i - 2], realPoints[i - 1], out var prevX, out var prevY))
                    continue;
                if (!TryDirection(realPoints[i - 1], realPoints[i], out var currX, out var currY))
                    continue;

                var dot = Math.Clamp(prevX * currX + prevY * currY, -1.0, 1.0);
                angleSum += weight * Math.Acos(dot) * (180.0 / Math.PI);
                weightSum += weight;
            }

            if (weightSum <= 0)
                return 1.0;

            var angle = angleSum / weightSum;
            if (angle <= TurnFreeAngleDegrees)
                return 1.0;
            if (angle >= TurnFullAngleDegrees)
                return MinTurnScale;

            var t = SmoothStep(
                (angle - TurnFreeAngleDegrees) / (TurnFullAngleDegrees - TurnFreeAngleDegrees));
            return 1.0 - (1.0 - MinTurnScale) * t;
        }

        private static bool TryEstimateMotion(
            IReadOnlyList<RealInkPoint> realPoints,
            out InkTailMotion motion)
        {
            motion = default;
            if (realPoints == null || realPoints.Count < 2)
                return false;

            EstimateVelocity(
                realPoints,
                out var velocityX,
                out var velocityY,
                out var accelerationX,
                out var accelerationY);

            var speed = Math.Sqrt(velocityX * velocityX + velocityY * velocityY);

            // 真实速度趋零（停驻、纯抖动）时没有可信方向，不外推；
            // 有方向但速度偏低（加减速阶段）时钳到最小有效速度继续外推一段短笔尾，
            // 避免笔尖变慢时笔尾整段闪烁消失。对数视界映射会在该速度下给出接近下限的短视界。
            if (speed < MinEffectiveSpeedPxPerSecond)
            {
                if (speed < 0.5)
                    return false;
                var clampScale = MinEffectiveSpeedPxPerSecond / speed;
                velocityX *= clampScale;
                velocityY *= clampScale;
                accelerationX *= clampScale;
                accelerationY *= clampScale;
                speed = MinEffectiveSpeedPxPerSecond;
            }

            if (speed > MaxPredictionSpeedPxPerSecond)
            {
                var scale = MaxPredictionSpeedPxPerSecond / speed;
                velocityX *= scale;
                velocityY *= scale;
                accelerationX *= scale;
                accelerationY *= scale;
                speed = MaxPredictionSpeedPxPerSecond;
            }

            // 拐弯处的加速度多为向心分量，直接积分会把笔尾甩到弯道外侧，同步按抑制系数衰减。
            var turnScale = ComputeTurnScale(realPoints);
            accelerationX *= turnScale;
            accelerationY *= turnScale;

            motion = new InkTailMotion(
                velocityX,
                velocityY,
                accelerationX,
                accelerationY,
                speed,
                turnScale);
            return true;
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

        private static bool TryDirection(
            RealInkPoint from,
            RealInkPoint to,
            out double directionX,
            out double directionY)
        {
            directionX = to.X - from.X;
            directionY = to.Y - from.Y;
            var length = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (length <= 0.01 || !IsFinite(length))
                return false;
            directionX /= length;
            directionY /= length;
            return true;
        }

        private static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);

        /// <summary>
        /// 把笔速对数映射到 [0,1]：慢速端分辨率高，快速端自然饱和。
        /// </summary>
        private static double NormalizeSpeed(double speed)
        {
            if (speed <= SlowSpeedPxPerSecond)
                return 0.0;
            if (speed >= FastSpeedPxPerSecond)
                return 1.0;
            return Math.Log(speed / SlowSpeedPxPerSecond)
                / Math.Log(FastSpeedPxPerSecond / SlowSpeedPxPerSecond);
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private readonly struct InkTailMotion
        {
            public InkTailMotion(
                double velocityX,
                double velocityY,
                double accelerationX,
                double accelerationY,
                double speed,
                double turnScale)
            {
                VelocityX = velocityX;
                VelocityY = velocityY;
                AccelerationX = accelerationX;
                AccelerationY = accelerationY;
                Speed = speed;
                TurnScale = turnScale;
            }

            public double VelocityX { get; }
            public double VelocityY { get; }
            public double AccelerationX { get; }
            public double AccelerationY { get; }
            public double Speed { get; }
            public double TurnScale { get; }
        }
    }
}
