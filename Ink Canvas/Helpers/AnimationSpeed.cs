using System;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 全局动画速度倍率。
    /// </summary>
    /// <remarks>
    /// 用途：调试时统一放慢/加快全部界面动画，便于观察细节或验证打断行为。
    ///
    /// 实现方式说明：WPF 没有"全局动画速度"的公开开关。
    /// <c>Timeline.SpeedRatioProperty</c> 的元数据已注册，二次 OverrideMetadata 会抛
    /// ArgumentException（实测验证），所以无法靠改全局默认值实现。
    /// 因此这里提供统一的时长换算入口：所有动画时长都经 <see cref="Scale"/>
    /// 计算，倍率一变，全局同步生效。
    ///
    /// 倍率语义：
    /// - 1.0 为原始速度；
    /// - 大于 1 更**慢**（时长变长），例如 2.0 表示慢放一倍；
    /// - 小于 1 更**快**，例如 0.5 表示耗时减半。
    ///
    /// 之所以用"速度倍率"而不是直接给时长，是因为调试时更关心
    /// "整体快慢"，而不是逐个动画的毫秒数。
    /// </remarks>
    internal static class AnimationSpeed
    {
        /// <summary>允许的最小倍率。</summary>
        public const double MinMultiplier = 0.1;

        /// <summary>允许的最大倍率。</summary>
        public const double MaxMultiplier = 10.0;

        private static double _multiplier = 1.0;

        /// <summary>
        /// 当前速度倍率。<see cref="Set"/> 之外不要直接写。
        /// </summary>
        public static double Multiplier => _multiplier;

        /// <summary>
        /// 设置速度倍率，自动钳制到 [0.1, 10]。
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
                $"[AnimationSpeed] 全局动画速度倍率 = {multiplier:0.##}x",
                LogHelper.LogType.Info);

            return multiplier;
        }

        /// <summary>
        /// 把基准秒数按当前倍率换算成实际时长。
        /// </summary>
        /// <param name="baseSeconds">倍率 1.0 时的时长（秒）。</param>
        public static double Scale(double baseSeconds)
        {
            if (baseSeconds <= 0) return baseSeconds;
            return baseSeconds * _multiplier;
        }

        /// <summary>把基准秒数换算成 <see cref="TimeSpan"/>。</summary>
        public static TimeSpan ScaleToTimeSpan(double baseSeconds)
        {
            return TimeSpan.FromSeconds(Scale(baseSeconds));
        }

        /// <summary>把基准毫秒数按当前倍率换算成 <see cref="TimeSpan"/>。</summary>
        public static TimeSpan ScaleMilliseconds(double baseMilliseconds)
        {
            if (baseMilliseconds <= 0) return TimeSpan.FromMilliseconds(baseMilliseconds);
            return TimeSpan.FromMilliseconds(baseMilliseconds * _multiplier);
        }
    }
}
