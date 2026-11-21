using System;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 全局动画速度倍率：数值 = 播放速度。10 最快，0.1 最慢，1 为原始速度。
    /// </summary>
    /// <remarks>
    /// 语义约定：数值就是"多少倍速"，与用户的直觉一致 ——
    /// 2 = 两倍速（时长减半），0.5 = 半速（时长翻倍）。
    ///
    /// 实现说明：WPF 没有"全局动画速度"的公开开关
    /// （Timeline.SpeedRatioProperty 元数据已注册，OverrideMetadata 会抛异常，实测验证），
    /// 因此所有动画时长统一经 <see cref="Scale"/> 换算，倍率一变全局生效。
    ///
    /// 时长 = 基准时长 / 倍率。倍率越大时长越短、动画越快。
    /// </remarks>
    internal static class AnimationSpeed
    {
        /// <summary>允许的最小倍率（最慢）。</summary>
        public const double MinMultiplier = 0.1;

        /// <summary>允许的最大倍率（最快）。</summary>
        public const double MaxMultiplier = 10.0;

        private static double _multiplier = 1.0;

        /// <summary>当前播放速度倍率。</summary>
        public static double Multiplier => _multiplier;

        /// <summary>
        /// 设置播放速度倍率，自动钳制到 [0.1, 10]。
        /// </summary>
        /// <returns>实际生效的倍率（钳制后的值）。</returns>
        public static double Set(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0)
            {
                multiplier = 1.0;
            }

            if (multiplier < MinMultiplier) multiplier = MinMultiplier;
            if (multiplier > MaxMultiplier) multiplier = MaxMultiplier;

            _multiplier = multiplier;

            LogHelper.WriteLogToFile(
                $"[AnimationSpeed] 全局动画播放速度 = {multiplier:0.##}x",
                LogHelper.LogType.Info);

            return multiplier;
        }

        /// <summary>
        /// 把基准秒数按当前倍率换算成实际时长：时长 = 基准 / 倍率。
        /// </summary>
        /// <param name="baseSeconds">倍率 1.0 时的时长（秒）。</param>
        public static double Scale(double baseSeconds)
        {
            if (baseSeconds <= 0 || _multiplier <= 0) return baseSeconds;
            return baseSeconds / _multiplier;
        }

        /// <summary>把基准秒数换算成 <see cref="TimeSpan"/>。</summary>
        public static TimeSpan ScaleToTimeSpan(double baseSeconds)
        {
            return TimeSpan.FromSeconds(Scale(baseSeconds));
        }

        /// <summary>把基准毫秒数按当前倍率换算成 <see cref="TimeSpan"/>。</summary>
        public static TimeSpan ScaleMilliseconds(double baseMilliseconds)
        {
            if (baseMilliseconds <= 0 || _multiplier <= 0) return TimeSpan.FromMilliseconds(baseMilliseconds);
            return TimeSpan.FromMilliseconds(baseMilliseconds / _multiplier);
        }
    }
}
