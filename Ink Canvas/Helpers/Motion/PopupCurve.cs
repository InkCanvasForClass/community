using System;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 菜单弹出/收起的进度曲线：把动画进度 progress 映射为缩放、位移与透明度权重。
    /// </summary>
    /// <remarks>
    /// 曲线形状移植自 Inkeys（Inkeys/Inkeys/UI/Bar/Bar.Animation.cppm:116 的 EaseOutBack），
    /// 而不是自己拿线性段拼一条"看起来像"的曲线。
    ///
    /// 缩放与位移由这一条曲线驱动；透明度另有独立的时间线，见 <see cref="OpacityWeight"/>。
    /// 打开与收起共用这条曲线，只是播放方向相反：
    ///
    ///   打开 = progress 从当前值推进到 1
    ///   收起 = progress 从当前值倒退到 0
    ///   打断 = 只翻转播放方向
    ///
    /// Inkeys 里打开用 EaseOutBack、收起用 EaseInBack，看似两条曲线，实际严格互为时间反演：
    ///
    ///   EaseInBack(t) ≡ 1 - EaseOutBack(1 - t)
    ///
    /// 所以"收起 = 同一条曲线倒放"与 Inkeys 的行为完全一致，
    /// "打断天然连续"这一性质也原样保留：曲线只与 progress 有关，与方向无关。
    ///
    /// 用 Back 而不是"分段线性 + 独立缓动重映射"的理由：
    /// 过冲量、峰值位置、回落段长度全部由 back 一个参数导出，不存在两段各自调参、
    /// 接缝处速度不连续的可能；峰值处速度为零是曲线自身的极值性质，
    /// 不是靠上升段动调零、回落段动起零拼出来的。
    /// 旧的实现要同时维护 RiseEaseExponent、PeakAt、SmoothStep 三处互相制约的参数，
    /// 改一个就得重算另外两个 —— 那正是"参数越多越难改对"的典型。
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

        /// <summary>
        /// Back 曲线的张力（Inkeys 里叫 back），决定过冲有多"过"。
        /// </summary>
        /// <remarks>
        /// Inkeys 原值 1.1 是配 0.40s 时长用的：行程过冲只有 4.47%，
        /// 换算到缩放上峰值仅 1.0125 —— 在 0.2s 这个量级的时长下，60fps 采样落不到顶点，
        /// 肉眼完全看不出"先放大再回落"（用户曾报"放大效果没了"）。
        ///
        /// 这里按目标峰值 <see cref="PeakScale"/> 反解。EaseOutBack 的行程峰值是
        ///   travel(c) = 1 + 4c³ / (27(c+1)²)
        /// 令 travel = (PeakScale - StartScale) / (1 - StartScale) = 33/28，得三次方程
        ///   112c³ - 135c² - 270c - 135 = 0
        /// 取唯一正根 2.4120065841218903，于是 Evaluate(PeakAt).Scale 与 PeakScale
        /// 只差 2.2e-16（浮点舍入），菜单留白按 PeakScale 算不会裁到峰值。
        /// </remarks>
        public const double BackTension = 2.4120065841218903;

        /// <summary>峰值在进度轴上的位置，由 <see cref="BackTension"/> 导出。</summary>
        /// <remarks>
        /// 对 EaseOutBack 求导置零得 p = 1 - 2c / (3(c+1))，c = 2.412… 时约 0.5287。
        /// 它不再是独立可调的常量：改 BackTension 会同时移动峰值位置与回落段长度。
        /// 0.5287 意味着"越过终点"这件事发生在时间轴中点之前 —— 点下去很快就有"到位"的观感，
        /// 剩下的一半时间用在回弹落位上。
        /// </remarks>
        public const double PeakAt = 1.0 - 2.0 * BackTension / (3.0 * (BackTension + 1.0));

        /// <summary>完全收起时相对打开位置的下滑距离（DIP）。</summary>
        public const double SlideDistance = 10.0;

        /// <summary>完全收起时的透明度权重（再乘以菜单自身的目标透明度）。</summary>
        public const double MinOpacityWeight = 0.35;

        /// <summary>
        /// 曲线行程：0 = 完全收起，1 = 落位，峰值处越过 1 形成回弹。
        /// </summary>
        /// <remarks>
        /// 首帧速度是全程平均速度的 3 + c ≈ 5.4 倍（g'(0) = 3(c+1) - 2c），
        /// 末帧速度为零（g'(1) = 0）—— "先快后慢"由曲线本身给出，不需要额外重映射。
        /// </remarks>
        public static double Travel(double progress)
        {
            return TravelWithBack(BackTension, progress);
        }

        /// <summary>按给定张力取行程。</summary>
        /// <remarks>
        /// 通用形态只为验收"移植是否忠实"服务（PopupCurveTests 用它对齐 Inkeys 既有期望值
        /// EaseOutBack(0.5) = 1.0125），生产路径一律走 <see cref="Travel"/>。
        ///
        /// 实现照抄 Inkeys：s = t - 1，返回 1 + (back+1)·s³ + back·s²。
        /// 用 s2*s 直接展开而不是 Math.Pow —— 这是每帧都跑的路径，
        /// Inkeys 同样手写展开（Bar.Animation.cppm:102 注释"固定二三幂直接展开，避免逐帧进 pow"）。
        /// </remarks>
        public static double TravelWithBack(double back, double progress)
        {
            double s = Clamp01(progress) - 1.0;
            double s2 = s * s;
            return 1.0 + (back + 1.0) * s2 * s + back * s2;
        }

        /// <summary>
        /// 透明度权重：独立的 EaseOutSine 时间线，与缩放同时长，但不共用行程。
        /// </summary>
        /// <remarks>
        /// 这里曾经写成 MinOpacityWeight + (1-MinOpacityWeight)·clamp01(travel)，即"透明度跟着行程走"。
        /// 它有个不易察觉的副作用：行程在 EaseOutBack 上越过 1.0 之后就恒为 1，
        /// 而那件事只发生在进度的 29%（0.40s 里的 117ms）——
        /// 剩下 283ms 的回落段里透明度一动不动，观感是"先迅速变实，然后空转着抖一下"。
        /// 更糟的是这个饱和时刻由 <see cref="BackTension"/> 决定：back 从 1.1 调到 3.0，
        /// 饱和点从 190ms 提前到 100ms，也就是调"弹多狠"会顺带改掉"淡入多快"，两个旋钮绞在一起。
        ///
        /// Inkeys 把两者分成两条时间线（Bar.EraserAttributeMotion.cppm:66-67：
        /// 几何用 EaseOutBack、透明度用 EaseOutSine，时长相同时长），透明度一路铺到进度 1 才满。
        /// 这里照做：权重只与 progress 有关，因此打断依旧天然连续（同一 progress 必得同一取值），
        /// 而"几何先到位、透明度慢慢收尾"正是原设计要的节奏。
        /// </remarks>
        public static double OpacityWeight(double progress)
        {
            // 每帧一次 Math.Sin，作用在单个 Popup 上。Inkeys 避开 pow 是因为它跑在逐图元的
            // 渲染循环里；这里没有同样的压力，用解析式换可读性是划算的。
            return MinOpacityWeight +
                   (1.0 - MinOpacityWeight) * Math.Sin(Clamp01(progress) * Math.PI / 2.0);
        }

        /// <summary>按进度取曲线上的缩放、位移与透明度权重。</summary>
        /// <remarks>
        /// 缩放与位移共用同一个行程，避免"缩放先快后慢、位移匀速"的错位感。
        /// 透明度不跟行程走，理由见 <see cref="OpacityWeight"/>。
        /// </remarks>
        public static PopupCurveSample Evaluate(double progress)
        {
            double travel = Travel(progress);

            double scale = StartScale + (1.0 - StartScale) * travel;

            // 负值表示回弹时略微越过静止位置再退回（Inkeys 的呼吸感，峰值处约 -1.79 DIP），
            // 这点位移落在 PopupOverflowRoom 的留白里，不会裁切。
            double slideY = SlideDistance * (1.0 - travel);

            return new PopupCurveSample(scale, slideY, OpacityWeight(progress));
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

        /// <summary>纵向位移（正=向下，负=回弹时越过静止位置）。</summary>
        public readonly double SlideY;

        /// <summary>透明度权重，0.35~1.0，走独立的 EaseOutSine 时间线（见 <see cref="PopupCurve.OpacityWeight"/>）。</summary>
        public readonly double OpacityWeight;

        public PopupCurveSample(double scale, double slideY, double opacityWeight)
        {
            Scale = scale;
            SlideY = slideY;
            OpacityWeight = opacityWeight;
        }
    }
}
