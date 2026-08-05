using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    /// <summary>
    /// 湿墨预览笔尾预测：用最近 2~4 个真实点估速度/加速度/曲率，外推一段未来轨迹。
    /// 外推时长动态自适应（10~36ms）：快写直线取上限，慢写、急转弯、报点停滞时收敛到下限。
    /// 预测点默认仅进入实时预览；是否在抬笔时烘焙进干墨由调用方显式决定。
    /// </summary>
    internal static class InkTailPredictor
    {
        // 动态视界上下限（毫秒）。低速时取 MinHorizon（保证低速也有可感知的预测尾，
        // 避免"低速不跟手"）；高速时取 MaxHorizon。
        public const double MinHorizonMilliseconds = 14.0;
        public const double MaxHorizonMilliseconds = 30.0;

        // 时间戳异常回退时用的名义报点间隔。
        private const long DefaultStepMicroseconds = 10_000L;

        // 预测点数：拉满 18 个让点距变小（24ms 视界 / 18 = 1.33ms/步 ≈ 1-2px 步长，
        // D2D 折线接近视觉连续曲线，避免"拉长的几条线"观感）。
        private const int PredictionPointCount = 18;

        // 速度估计窗口：单段差分对报点间隔抖动过于敏感，用指数加权的最近若干段取代。
        private const int VelocityWindowSegments = 5;
        private const double VelocityWindowDecay = 0.6;

        private const double MaxPredictionSpeedPxPerSecond = 8_000.0;
        // 真实速度低于此值时不再强制返回空，而是按一档极小速度继续外推，
        // 避免加速度/减速阶段的帧间笔尾闪烁消失。`Build` 仍会在点数不足、停驻、完全 NaN 时返回空。
        private const double MinEffectiveSpeedPxPerSecond = 5.0;
        private const double MaxPredictionDistancePx = 50.0;
        // 低速下限：速度极慢时预测尾也不小于此值，保证低速跟手（可感知的预测尾）。
        private const double MinPredictionDistancePx = 12.0;
        // 曲率外推单步最大转角（弧度，≈7°）。配合 18 个预测点，最大总转角 ≈126°，
        // 加上距离 cap 截断，笔尾呈自然弧线，避免小半径+高速下"瞬时甩飞"。
        private const double MaxStepAngleRadians = 0.12;

        // 有符号曲率 κ = ±1/R 的可信区间。
        //  - |κ| > 1/80（R < 80px）视为噪声，钳到上限；
        //  - |κ| < 1/1500（R > 1500px）视为直线，退化为线性外推。
        // 用曲率而非半径做量纲：直线对应 κ→0 而不是 R→∞，跨帧平滑才有连续意义
        // （半径在近直线时会在 600↔+∞ 之间乱跳，曲率则平滑穿过 0）。
        private const double MaxUsableCurvature = 1.0 / 80.0;
        private const double MinUsableCurvature = 1.0 / 1500.0;

        // 曲率跨帧平滑时间常数（毫秒）。比速度平滑快得多：真实拐弯需要当帧跟上，
        // 但要足以抹掉三点拟合在近直线时的符号翻转（笔尾"上下乱甩"的根因）。
        internal const double CurvatureTauMilliseconds = 70.0;

        // 速度→视界映射的两端。起点取最低预测速度，让超低速一离开门限就开始增长。
        private const double SlowSpeedPxPerSecond = 40.0;
        private const double FastSpeedPxPerSecond = 2_500.0;

        // 拐弯抑制：夹角在自由角内不抑制，超过满抑制角按最小比例保留。
        private const double TurnFreeAngleDegrees = 12.0;
        private const double TurnFullAngleDegrees = 60.0;
        private const double MinTurnScale = 0.2;

        // 笔速时间平滑常数（毫秒）。速度估计即使做了多段加权仍有残余抖动，
        // 逐帧重算会让笔尾长度反复伸缩（“一抽一抽”）。在源头平滑速度可一次性
        // 覆盖视界基线、距离约束与外推积分全部下游路径。按真实报点间隔推进。
        // 加速/匀速用本常量（快速跟手）；减速用 SlowdownTauMilliseconds（极慢，
        // 让预测尾慢慢收缩，不突然缩短）。
        internal const double SpeedTauMilliseconds = 32.0;

        // 报点停滞抑制：间隔越大速度越陈旧，外推越不可信。
        private const double FreshSampleIntervalMilliseconds = 12.0;
        private const double StaleSampleIntervalMilliseconds = 40.0;
        private const double MinStaleScale = 0.35;

        // 速度减速时的衰减时间常数（毫秒）。比加速/匀速的 SpeedTau 长得多，
        // 让速度变慢时预测视界缓慢收缩而非瞬间消失（避免笔尾"突然缩短"）。
        // 1500ms 让减速时预测尾花约 3-4 秒才收敛大半段，平滑感极强。
        internal const double SlowdownTauMilliseconds = 1500.0;
        // 速度加速时的伸长时间常数（毫秒）。600ms 让预测尾随加速渐进伸长，
        // 而非瞬间拉到全长（自然书写感）。比旧 SpeedTau(32ms) 慢得多。
        internal const double AccelerationTauMilliseconds = 600.0;

        // 每 10ms 的速度衰减，按实际步长换算，避免笔尾发散。
        private const double DecayPer10Milliseconds = 0.97;
        // 预测尾压力约减半，避免预测几何即使较短仍显示成明显粗条。
        private const double TailPressureTaper = 0.5;
        // ARM64 上 MathF.Pow(float, float) 走单精度库，循环热路径避免
        // Math.Pow(double, double) 的 libm 标量调用。float 精度对压感衰减
        // （TailPressureTaper = 0.5 的幂函数）完全足够。
        private const float TailPressureTaperF = 0.5f;

        /// <summary>
        /// 按当前笔速与曲率自适应决定外推时长后构建预测笔尾。
        /// 无跨帧状态：视界完全由当前点集决定，逐帧重算会有抖动。
        /// 实时书写路径应改用 <see cref="InkTailPredictionSmoother"/>。
        /// </summary>
        public static IReadOnlyList<PredictedInkPoint> Build(IReadOnlyList<RealInkPoint> realPoints)
        {
            if (!TryEstimateMotion(realPoints, out var motion))
                return Array.Empty<PredictedInkPoint>();

            var horizonMicroseconds = ComputeAdaptiveHorizonMicroseconds(realPoints, motion);
            ResolveSampling(horizonMicroseconds, out var pointCount, out var stepMicroseconds);
            return Extrapolate(
                realPoints,
                motion,
                pointCount,
                stepMicroseconds,
                EstimateSignedCurvature(realPoints));
        }

        /// <summary>
        /// 与 <see cref="Build"/> 相同，但笔速先经过调用方持有的时间平滑器。
        /// 平滑速度而非平滑视界：视界基线、最大外推距离约束、外推积分本身全都由速度派生，
        /// 只平滑其中一项会被其余未平滑项重新引入抖动。方向与拐弯/停滞抑制不受影响。
        /// </summary>
        internal static IReadOnlyList<PredictedInkPoint> Build(
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailPredictionSmoother smoother)
        {
            if (smoother == null)
                return Build(realPoints);
            if (!TryEstimateMotion(realPoints, out var motion))
            {
                smoother.Reset();
                return Array.Empty<PredictedInkPoint>();
            }

            motion = RescaleToSpeed(
                motion,
                smoother.SmoothSpeed(motion.Speed, realPoints));
            // 视界再做一次跨帧平滑：吸收 turnScale / staleScale / 距离截断 /
            // 曲率有效性切换等每帧突变，让笔尾长度只能渐变（不"卡"）。
            var horizonMicroseconds = smoother.SmoothHorizon(
                ComputeAdaptiveHorizonMicroseconds(realPoints, motion),
                realPoints);
            // 曲率同样跨帧平滑：三点拟合在近直线时符号会硬翻转（笔尾左右乱甩），
            // 平滑后穿过 0 是连续的。
            var curvature = smoother.SmoothCurvature(
                EstimateSignedCurvature(realPoints),
                realPoints);
            ResolveSampling(horizonMicroseconds, out var pointCount, out var stepMicroseconds);
            return Extrapolate(
                realPoints,
                motion,
                pointCount,
                stepMicroseconds,
                curvature);
        }

        /// <summary>
        /// 把运动量整体缩放到目标速率，保持方向、加速度比例与拐弯抑制系数不变。
        /// </summary>
        private static InkTailMotion RescaleToSpeed(InkTailMotion motion, double targetSpeed)
        {
            if (motion.Speed <= 0.0001 || targetSpeed <= 0)
                return motion;

            var scale = targetSpeed / motion.Speed;
            return new InkTailMotion(
                motion.VelocityX * scale,
                motion.VelocityY * scale,
                motion.AccelerationX * scale,
                motion.AccelerationY * scale,
                targetSpeed,
                motion.TurnScale);
        }

        private static IReadOnlyList<PredictedInkPoint> Extrapolate(
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailMotion motion,
            int pointCount,
            long stepMicroseconds,
            double curvature)
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

            // 曲率外推：直接按有符号曲率 κ 每步旋转速度矢量再积分位置，
            // 不再用三点拟合的圆心。
            //  - 弧线起点切线恒等于当前速度方向（用圆心时圆的切线与速度方向不一致，
            //    笔尾根部每帧带一个抖动的折角）；
            //  - κ 已跨帧平滑，穿过 0 连续，近直线自动退化为线性外推，
            //    不再有「有效/无效」的算法整体切换。
            var useCurvature = Math.Abs(curvature) > MinUsableCurvature;
            var prevX = last.X;
            var prevY = last.Y;
            // 距离上限循环外算一次（用平滑后的初速度）：放在循环内会随
            // decayPerStep 每步变化，导致截断点抖动 → 笔尾长度突变。
            // ARM64 上 Math.Sqrt(double) 没有 NEON 单指令通路（走 libm 标量），
            // 循环内每步调用是主要热路径。改用「缓存平方」：循环里直接读 speedSq，
            // 只在速度矢量旋转后做一次 sqrt 更新。
            var initialSpeedSq = (float)(vx * vx + vy * vy);
            var initialSpeed = MathF.Sqrt(initialSpeedSq);
            var distanceCap = Math.Min(
                MaxPredictionDistancePx,
                Math.Max(MinPredictionDistancePx, initialSpeed * (MinHorizonMilliseconds * 0.001)));

            // 当前步的速度平方缓存（useCurvature 分支里每步更新）。
            var speedSq = initialSpeedSq;

            for (var i = 0; i < pointCount; i++)
            {
                // 半隐式欧拉：先更新速度再积分位置，并逐步衰减，避免笔尾发散。
                if (useCurvature)
                {
                    // 直接读缓存的平方，避免 Math.Sqrt；速度变化只是方向旋转，
                    // magnitude^2 = vx^2 + vy^2 在 (c,s) 旋转下守恒。
                    var stepDist = stepSeconds * MathF.Sqrt(speedSq);
                    // Δθ = κ · Δs（弧长 × 曲率）。截到 MaxStepAngleRadians 防止
                    // 小半径 + 大步长导致单步大幅旋转（高速过急弯时笔尾瞬时甩飞）。
                    var deltaAngle = Math.Clamp(
                        curvature * stepDist,
                        -MaxStepAngleRadians,
                        MaxStepAngleRadians);
                    var c = Math.Cos(deltaAngle);
                    var s = Math.Sin(deltaAngle);
                    var nvx = vx * c - vy * s;
                    var nvy = vx * s + vy * c;
                    vx = nvx * decayPerStep;
                    vy = nvy * decayPerStep;
                    // 旋转 + 衰减后的速度平方（直接乘平方，避免 sqrt）：
                    // |new| = |rotated| = sqrt(speedSq)，* decayPerStep 后平方 = speedSq * decayPerStep^2
                    speedSq = (float)(vx * vx + vy * vy);
                }
                else
                {
                    vx = (vx + motion.AccelerationX * stepSeconds) * decayPerStep;
                    vy = (vy + motion.AccelerationY * stepSeconds) * decayPerStep;
                    speedSq = (float)(vx * vx + vy * vy);
                }

                currX += vx * stepSeconds;
                currY += vy * stepSeconds;

                if (!IsFinite(currX) || !IsFinite(currY))
                    break;

                var stepDistance = Math.Sqrt(
                    (currX - prevX) * (currX - prevX)
                    + (currY - prevY) * (currY - prevY));

                // 距离上限：不再整步 break（那样笔尾长度按步长量化，点数在 k↔k+1
                // 之间跳，末端一跳一个步长 ≈ 突变）。改为在超出处按比例插值出末点，
                // 笔尾末端随 distanceCap / 速度连续移动。
                if (traveled + stepDistance >= distanceCap)
                {
                    if (stepDistance > 0.0001)
                    {
                        var t = Math.Clamp((distanceCap - traveled) / stepDistance, 0.0, 1.0);
                        var endX = prevX + (currX - prevX) * t;
                        var endY = prevY + (currY - prevY) * t;
                        if (IsFinite(endX) && IsFinite(endY) && t > 0.001)
                        {
                            stamp += (long)(stepMicroseconds * t);
                            var endTaper = MathF.Pow(
                                TailPressureTaperF,
                                (float)((i + t) / pointCount));
                            result.Add(new PredictedInkPoint(
                                endX,
                                endY,
                                Math.Clamp(pressure * endTaper, 0.08f, 1.0f),
                                stamp));
                        }
                    }

                    break;
                }

                traveled += stepDistance;
                stamp += stepMicroseconds;
                prevX = currX;
                prevY = currY;

                // 越靠后压感越细，且与点数无关，保证视界变化时笔尾观感一致。
                // ARM64 上 Math.Pow 走 libm 标量，MathF.Pow 至少走单精度库；
                // 18 步 × ~120Hz = ~2k calls/s，主热路径之一。
                var progress = (i + 1) / (float)pointCount;
                var taper = MathF.Pow(TailPressureTaperF, progress);
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
        /// 视界 = 速度映射基线 × 拐弯抑制 × 停滞抑制，再受最大外推距离约束，最终夹在 10~36ms。
        /// </summary>
        private static double ComputeAdaptiveHorizonMicroseconds(
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailMotion motion)
        {
            return ApplyHorizonSuppression(
                ComputeSpeedBaselineMilliseconds(motion),
                realPoints,
                motion);
        }

        /// <summary>
        /// 仅由笔速决定的视界基线（毫秒）。这一项含速度估计的残余抖动，是需要跨帧平滑的部分。
        /// </summary>
        private static double ComputeSpeedBaselineMilliseconds(InkTailMotion motion)
        {
            // 按速度的对数归一化：线性归一化会把 40~250px/s 整段压进 t<0.1，
            // 超低速因而贴死下限；人对笔速的感知本身也接近对数。
            var speedT = NormalizeSpeed(motion.Speed);
            return MinHorizonMilliseconds
                + (MaxHorizonMilliseconds - MinHorizonMilliseconds) * speedT;
        }

        /// <summary>
        /// 在（可能已平滑的）速度基线上施加拐弯/停滞抑制与距离约束。
        /// 这些抑制必须当帧全量生效，不能被平滑拖慢，否则拐弯时笔尾会甩到弯道外侧。
        /// </summary>
        private static double ApplyHorizonSuppression(
            double baselineMilliseconds,
            IReadOnlyList<RealInkPoint> realPoints,
            InkTailMotion motion)
        {
            var horizon = baselineMilliseconds;
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
            // 点数恒定，步长连续随视界变化：视界平滑变化时笔尾长度也连续变化，
            // 不会因为点数量化而整段跳变。
            pointCount = PredictionPointCount;
            stepMicroseconds = (long)(horizonMicroseconds / pointCount);
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

            // 指数加权最近若干段速度：单段差分会把 ±2ms 的报点间隔抖动放大成 20% 以上的
            // 速度抖动，进而让视界逐帧跳变，笔尾观感发抽。越靠笔尖的段权重越高。
            var sumX = 0.0;
            var sumY = 0.0;
            var weightSum = 0.0;
            var weight = 1.0;
            var oldest = Math.Max(1, realPoints.Count - VelocityWindowSegments);
            for (var i = realPoints.Count - 1; i >= oldest; i--, weight *= VelocityWindowDecay)
            {
                if (!TrySegmentVelocity(realPoints[i - 1], realPoints[i], out var vx, out var vy))
                    continue;
                sumX += weight * vx;
                sumY += weight * vy;
                weightSum += weight;
            }

            if (weightSum > 0)
            {
                velocityX = sumX / weightSum;
                velocityY = sumY / weightSum;
            }
            else
            {
                // 整个窗口的时间戳都异常时，按默认步长把弦长当作速度。
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
            if (!TrySegmentVelocity(secondLast, last, out var latestVx, out var latestVy))
                return;

            // 用最近两段速度差估计加速度，使转弯时的预测更跟手。
            var dt = (last.TimestampMicroseconds - secondLast.TimestampMicroseconds) / 1_000_000.0;
            if (dt <= 0.000001)
                dt = DefaultStepMicroseconds / 1_000_000.0;
            accelerationX = (latestVx - prevVx) / dt;
            accelerationY = (latestVy - prevVy) / dt;

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

        /// <summary>
        /// 用最近三个真实点估计有符号曲率 κ = ±1/R（左转为正，右转为负）。
        /// 用曲率而不是「圆心 + 半径」的两个理由：
        ///  1. 直线对应 κ→0（连续），而半径是 R→∞（跨帧平滑无意义，近直线时会在
        ///     600↔∞ 之间乱跳，笔尾左右乱甩）；
        ///  2. 外推只需要「每步转多少角」，不需要圆心——用圆心时弧线的切线方向与
        ///     当前速度方向不一致，笔尾根部每帧带一个抖动的折角。
        /// 公式：κ = 2·cross(a, b) / (|a|·|b|·|a+b|)，a = p1→p2，b = p2→p3。
        /// 三点近似共线或退化时返回 0。
        /// </summary>
        private static double EstimateSignedCurvature(IReadOnlyList<RealInkPoint> realPoints)
        {
            if (realPoints == null || realPoints.Count < 3)
                return 0.0;

            var p1 = realPoints[realPoints.Count - 3];
            var p2 = realPoints[realPoints.Count - 2];
            var p3 = realPoints[realPoints.Count - 1];

            var ax = p2.X - p1.X;
            var ay = p2.Y - p1.Y;
            var bx = p3.X - p2.X;
            var by = p3.Y - p2.Y;
            var cx = p3.X - p1.X;
            var cy = p3.Y - p1.Y;

            var la = Math.Sqrt(ax * ax + ay * ay);
            var lb = Math.Sqrt(bx * bx + by * by);
            var lc = Math.Sqrt(cx * cx + cy * cy);
            // 任一段过短说明是同点重复/纯噪声，此时叉积的方向不可信。
            if (la < 0.5 || lb < 0.5 || lc < 0.5)
                return 0.0;

            var cross = ax * by - ay * bx;
            var curvature = 2.0 * cross / (la * lb * lc);
            if (!IsFinite(curvature))
                return 0.0;

            // 钳到可用区间：|κ| 过大（R<80px）多为报点噪声放大，按上限截断；
            // 低于下限的留给调用方判定为直线（这里不置零，平滑器需要连续输入）。
            return Math.Clamp(curvature, -MaxUsableCurvature, MaxUsableCurvature);
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

    /// <summary>
    /// 单笔生命周期内的笔速平滑器。
    /// 视界基线、最大外推距离约束、外推积分全部由笔速派生，因此速度估计的残余抖动会
    /// 同时从多条路径把抖动带进笔尾长度（观感即“一抽一抽”）。在源头平滑速度可一次性
    /// 覆盖全部下游路径。按真实报点间隔做指数平滑，报点率变化时平滑强度保持一致。
    /// 方向、拐弯抑制、停滞抑制均不经过这里，急转时笔尾仍能当帧收回。
    /// </summary>
    internal sealed class InkTailPredictionSmoother
    {
        private bool _hasPrevious;
        private double _previousSpeed;
        private long _previousTimestampMicroseconds;

        // 视界平滑状态：速度平滑只覆盖速度一条路径，但视界还受 turnScale /
        // staleScale / 距离截断 / 曲率有效性切换影响，这些每帧都可能突变，
        // 表现为笔尾"卡、突变式伸缩"。这里对最终视界再做一次跨帧平滑，
        // 把所有上游突变都吸收成渐变。
        private bool _hasPreviousHorizon;
        private double _previousHorizonMicroseconds;
        private long _previousHorizonTimestampMicroseconds;

        // 曲率平滑状态：三点拟合的曲率在近直线时符号会随噪声硬翻转
        // （+κ↔−κ，笔尾瞬间从左弯变右弯 = "上下乱甩"）。平滑后穿过 0 连续。
        private bool _hasPreviousCurvature;
        private double _previousCurvature;
        private long _previousCurvatureTimestampMicroseconds;

        /// <summary>
        /// 对有符号曲率做跨帧指数平滑。用单一较短时间常数（不做加速/减速区分）：
        /// 真实拐弯必须当帧跟上，但要足以抹掉近直线时的符号翻转。
        /// </summary>
        public double SmoothCurvature(
            double targetCurvature,
            IReadOnlyList<RealInkPoint> realPoints)
        {
            var timestamp = realPoints[realPoints.Count - 1].TimestampMicroseconds;
            if (!_hasPreviousCurvature)
            {
                _hasPreviousCurvature = true;
                _previousCurvatureTimestampMicroseconds = timestamp;
                _previousCurvature = targetCurvature;
                return targetCurvature;
            }

            var deltaMilliseconds =
                (timestamp - _previousCurvatureTimestampMicroseconds) / 1000.0;
            _previousCurvatureTimestampMicroseconds = timestamp;
            if (deltaMilliseconds <= 0)
                return _previousCurvature;

            var alpha = 1.0 - Math.Exp(
                -deltaMilliseconds / InkTailPredictor.CurvatureTauMilliseconds);
            _previousCurvature += (targetCurvature - _previousCurvature) * alpha;
            return _previousCurvature;
        }

        /// <summary>
        /// 对最终视界做跨帧指数平滑。伸长用 AccelerationTau，收缩用 SlowdownTau，
        /// 与速度平滑同源，保证笔尾长度只能渐变。
        /// </summary>
        public double SmoothHorizon(
            double targetHorizonMicroseconds,
            IReadOnlyList<RealInkPoint> realPoints)
        {
            var timestamp = realPoints[realPoints.Count - 1].TimestampMicroseconds;
            if (!_hasPreviousHorizon)
            {
                _hasPreviousHorizon = true;
                _previousHorizonTimestampMicroseconds = timestamp;
                if (_previousHorizonMicroseconds <= 0)
                {
                    _previousHorizonMicroseconds = targetHorizonMicroseconds;
                    return targetHorizonMicroseconds;
                }
                // 中断恢复：从上次视界渐进，不瞬间跳到目标。
                _previousHorizonMicroseconds +=
                    (targetHorizonMicroseconds - _previousHorizonMicroseconds) * 0.15;
                return _previousHorizonMicroseconds;
            }

            var deltaMilliseconds =
                (timestamp - _previousHorizonTimestampMicroseconds) / 1000.0;
            _previousHorizonTimestampMicroseconds = timestamp;
            if (deltaMilliseconds <= 0)
                return _previousHorizonMicroseconds;

            var tau = targetHorizonMicroseconds < _previousHorizonMicroseconds
                ? InkTailPredictor.SlowdownTauMilliseconds
                : InkTailPredictor.AccelerationTauMilliseconds;
            var alpha = 1.0 - Math.Exp(-deltaMilliseconds / tau);
            _previousHorizonMicroseconds +=
                (targetHorizonMicroseconds - _previousHorizonMicroseconds) * alpha;
            return _previousHorizonMicroseconds;
        }

        public void Reset()
        {
            // 不归零速度/视界/曲率，只重置时间戳与首帧标记：避免预测中断后重新开始
            // 时首帧直接返回全速（瞬间拉满），保留渐进伸长。
            _hasPrevious = false;
            _previousTimestampMicroseconds = 0;
            _hasPreviousHorizon = false;
            _previousHorizonTimestampMicroseconds = 0;
            _hasPreviousCurvature = false;
            _previousCurvatureTimestampMicroseconds = 0;
        }

        public double SmoothSpeed(
            double targetSpeed,
            IReadOnlyList<RealInkPoint> realPoints)
        {
            var timestamp = realPoints[realPoints.Count - 1].TimestampMicroseconds;
            if (!_hasPrevious)
            {
                // 首帧：从上次保留的速度向 target 渐进（不直接跳到全速），
                // 避免预测中断后重新开始瞬间拉满。用固定小 alpha 渐进，
                // 不依赖时间差（Reset 后时间戳被归零）。
                _hasPrevious = true;
                _previousTimestampMicroseconds = timestamp;
                if (_previousSpeed <= 0)
                    return targetSpeed;
                _previousSpeed += (targetSpeed - _previousSpeed) * 0.15;
                return _previousSpeed;
            }

            var deltaMilliseconds = (timestamp - _previousTimestampMicroseconds) / 1000.0;
            _previousTimestampMicroseconds = timestamp;
            if (deltaMilliseconds <= 0)
                return _previousSpeed;

            // 不对称平滑：
            //  - 速度上升（或持平）：用 AccelerationTau（较长），让预测尾"伸长渐进"，
            //    不瞬间拉到全长（自然书写感）。
            //  - 速度下降：用 SlowdownTau（更长），让预测视界在减速时缓慢收缩，
            //    笔尾不会"突然缩短"。
            var tau = targetSpeed < _previousSpeed
                ? InkTailPredictor.SlowdownTauMilliseconds
                : InkTailPredictor.AccelerationTauMilliseconds;
            var alpha = 1.0 - Math.Exp(-deltaMilliseconds / tau);
            _previousSpeed += (targetSpeed - _previousSpeed) * alpha;
            return _previousSpeed;
        }
    }
}
