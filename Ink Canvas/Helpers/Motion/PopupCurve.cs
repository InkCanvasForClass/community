using System;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 菜单弹出/收起的进度曲线：把动画进度 progress 映射为缩放、位移与透明度权重。
    /// </summary>
    /// <remarks>
    /// 整个菜单动画只有这一条曲线，打开与收起共用它、只是播放方向相反：
    ///
    ///   打开 = progress 从当前值推进到 1
    ///   收起 = progress 从当前值倒退到 0
    ///   打断 = 只翻转播放方向
    ///
    /// "打开与收起互为倒放""打断天然连续"因此由结构本身保证，而不是靠两段动画对齐。
    ///
    /// 曲线分两层：
    ///
    /// 1) 时间重映射 <see cref="Remap"/>：progress → u，u 才是在形状上取值的参数。
    ///    上升段用 EaseOut 幂函数（先快后慢），回落段用 smoothstep（两端速度为零）。
    ///    用户说的"分段 用曲线 先快 后慢"指的就是这一层：
    ///    前 20% 的时间走完 50% 的行程，最后一段则是减速落位。
    ///
    /// 2) 形状曲线 u → 缩放，分段线性：
    ///      u ∈ [0, PeakAt]   0.72 → 1.05（由小长到略微过大）
    ///      u ∈ [PeakAt, 1]   1.05 → 1.00（回落到正常大小）
    ///
    /// PeakAt = 8/11 来自用户给出的观感序列：
    ///   打开 1-2-3-4-5-6-7-8-9-8-7-6  => 上升 8 步、回落 3 步、共 11 步
    ///   收起 6-7-8-9-8-7-6-5-4-3-2-1  => 先涨 3 步、再降 8 步
    /// 曲线只有一条，收起就是同一函数倒放，所以 8/11 自动让两个方向都符合该序列。
    /// 曾按"峰值位置 = 上升行程/总行程"算得 0.896：收起时"先涨"只剩 10.4%（约 1.4 帧），
    /// 过冲完全看不见——那正是"放大效果没了"的原因。
    ///
    /// 本类是纯数学、无 WPF 依赖，因此被 InkCanvas.NativeInk.Tests 直接链接做单测（同 SpringSolver）。
    /// 曲线形状的回归必须由 PopupCurveTests 兜住，不要靠肉眼目测。
    /// </remarks>
    public static class PopupCurve
    {
        /// <summary>收起状态的缩放（曲线起点）。</summary>
        public const double StartScale = 0.72;

        /// <summary>
        /// 缩放过冲峰值（&gt;1 才会出现"变大一点再回落"）。
        /// 必须与菜单留白计算使用同一个常量，否则峰值时四边会被裁掉。
        /// </summary>
        public const double PeakScale = 1.05;

        /// <summary>峰值在进度轴上的位置。</summary>
        public const double PeakAt = 8.0 / 11.0;

        /// <summary>完全收起时相对打开位置的下滑距离（DIP）。</summary>
        public const double SlideDistance = 10.0;

        /// <summary>完全收起时的透明度权重（再乘以菜单自身的目标透明度）。</summary>
        public const double MinOpacityWeight = 0.35;

        /// <summary>上升段 EaseOut 的幂次：越大越"先快后慢"。</summary>
        /// <remarks>
        /// 取 2（EaseOutQuad）：半程行程落在上升段的 29.5%（约 32ms），
        /// 首帧就有接近 1/4 的行程，点下去立刻有反馈；再大就要靠加速掩盖"跳"。
        /// </remarks>
        public const double RiseEaseExponent = 2.0;

        /// <summary>
        /// 时间重映射：把线性进度换成"先快后慢"的进度。
        /// </summary>
        /// <remarks>
        /// 上升段用 EaseOut：起点速度最大、到峰值时速度归零，于是过冲顶点是一个
        /// 自然的"停一下"的节拍（用户序列里那个 9）。
        /// 回落段用 smoothstep：两端速度都为零，峰值处不会出现折角，
        /// 落位时也不会突然刹停。
        /// </remarks>
        public static double Remap(double progress)
        {
            progress = Clamp01(progress);

            if (progress <= PeakAt)
            {
                double local = PeakAt > 0.0001 ? progress / PeakAt : 1.0;
                return PeakAt * (1.0 - Math.Pow(1.0 - local, RiseEaseExponent));
            }

            double fall = (progress - PeakAt) / Math.Max(0.0001, 1.0 - PeakAt);
            return PeakAt + (1.0 - PeakAt) * SmoothStep(fall);
        }

        /// <summary>按进度取曲线上的缩放、位移与透明度权重。</summary>
        public static PopupCurveSample Evaluate(double progress)
        {
            double u = Remap(progress);

            double scale;
            if (u <= PeakAt)
            {
                double local = PeakAt > 0.0001 ? u / PeakAt : 1.0;
                scale = StartScale + (PeakScale - StartScale) * local;
            }
            else
            {
                double local = (u - PeakAt) / Math.Max(0.0001, 1.0 - PeakAt);
                scale = PeakScale + (1.0 - PeakScale) * local;
            }

            double slideY = SlideDistance * (1.0 - u);
            double opacityWeight = MinOpacityWeight + (1.0 - MinOpacityWeight) * u;
            return new PopupCurveSample(scale, slideY, opacityWeight);
        }

        private static double SmoothStep(double t)
        {
            t = Clamp01(t);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value)) return 0.0;
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }
    }

    /// <summary>曲线在某一进度上的取值。</summary>
    public readonly struct PopupCurveSample
    {
        /// <summary>缩放倍率。</summary>
        public readonly double Scale;

        /// <summary>纵向位移（正=向下）。</summary>
        public readonly double SlideY;

        /// <summary>透明度权重，0.35~1.0。</summary>
        public readonly double OpacityWeight;

        public PopupCurveSample(double scale, double slideY, double opacityWeight)
        {
            Scale = scale;
            SlideY = slideY;
            OpacityWeight = opacityWeight;
        }
    }
}
