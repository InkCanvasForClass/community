using System;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 菜单弹出/收起的进度曲线：把动画进度 progress 映射为缩放与位移。
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
    /// 起点缩放不是全局常量，而是按菜单自身宽度导出的（见 <see cref="CompactWidth"/>）：
    /// 收起态应当"就是一个工具栏按钮那么大"，而这个视觉尺寸与菜单本身多大无关。
    /// 于是每条曲线实例由 <see cref="PopupCurveShape"/> 承载，它把起点、张力、
    /// 位移三个互相牵制的量一次算好，调用方只面对 Evaluate。
    ///
    /// 本类是纯数学、无 WPF 依赖，因此被 InkCanvas.NativeInk.Tests 直接链接做单测（同 SpringSolver）。
    /// 曲线形状的回归必须由 PopupCurveTests 兜住，不要靠肉眼目测。
    /// </remarks>
    public static class PopupCurve
    {
        /// <summary>
        /// 收起态的视觉宽度（DIP）：菜单"从按钮里长出来"时，起始大小就取这么宽。
        /// </summary>
        /// <remarks>
        /// 数值对齐工具栏按钮：浮动栏的按钮是 58×58（MainWindow.xaml 的 BorderFloatingBarMoveControls）、
        /// 闲置态迷你栏高 44（IdleMiniBar），Inkeys 那边取的是 60
        /// （Bar.Main.cpp:67 的 BarDrawAttributeCompactWidth = 60.0）。
        /// 取 58 是因为本工程的按钮就是这个尺寸，起点大小与按钮一致时，
        /// 观感才是"按钮撑开成菜单"，而不是"凭空冒出一个色块"。
        ///
        /// 注意这个常量是**宽度**，所以起点缩放 = 本值 / 菜单实际宽度，
        /// 每条菜单各不相同：大菜单起点更小、小菜单起点更大，
        /// 但收起来时它们在屏幕上占的宽度是同一个数。
        /// </remarks>
        public const double CompactWidth = 58.0;

        /// <summary>
        /// 起点缩放的上下限。
        /// </summary>
        /// <remarks>
        /// 上限 0.45：太小的菜单（比如只有 120 宽）若按 58 算会得到 0.48，
        /// 那已经不是"长出来"而是"闪一下"。夹到 0.45 让它至少还有一段可见的生长。
        ///
        /// 下限 0.05：极宽的菜单（1000 宽以上）算出来不足 0.06，夹住避免除以接近零的数，
        /// 也避免起点小到只剩几个像素、完全看不出形状。
        /// </remarks>
        public const double MinStartScale = 0.05;

        /// <inheritdoc cref="MinStartScale"/>
        public const double MaxStartScale = 0.45;

        /// <summary>
        /// 缩放过冲峰值（&gt;1 才会出现"变大一点再回落"）。
        /// 必须与菜单留白计算使用同一个常量，否则峰值时四边会被裁掉。
        /// </summary>
        /// <remarks>
        /// 这个量是**绝对**的：不管菜单起点多小，峰值都落在静止尺寸的 1.05 倍。
        /// 于是张力必须随起点反解（见 <see cref="TensionForPeak"/>）——
        /// 若张力固定，起点越小、行程越长，峰值反而越大，大小菜单的"弹"的力度就不一致了。
        /// </remarks>
        public const double PeakScale = 1.05;

        /// <summary>
        /// 缩放中心到菜单底边的距离（DIP）：按钮中心到底边的距离 = 按钮半高 24.5 + 弹窗间距 8。
        /// </summary>
        /// <remarks>
        /// 菜单的 <c>RenderTransformOrigin</c> 被夹在 [0,1]，纵向最多只能取到底边（1.0），
        /// 也就是说纯靠原点做不出「绕按钮中心缩放」——中心跑到了菜单外面。
        /// 但绕某个点缩放与绕底边缩放再叠一段位移是等价的：
        /// 绕底边下方 b 处缩放时，底边上升 b·(1 - scale)，而底边本身不动、位移量正好是这个差。
        /// 所以只要位移取 <see cref="PopupCurveShape.SlideDistance"/> = b·(1 - 起点缩放)，
        /// 合成结果就与「绕按钮中心缩放」逐点相同。
        ///
        /// 这个常量就是那个 b。Inkeys 没有独立位移量（grep slide/offsetY/translateY 无命中），
        /// 它的纵向位移完全是「中心点从锚点插值到面板中心」的副产品
        /// （Bar.EraserAttributeMotion.cppm:24-29 的 ResolveEraserSurfacePose），
        /// 锚点取的是触发按钮矩形中心（Bar.EraserAttribute.cpp:194）。
        /// 这里沿用了它「绕按钮中心长出来」的几何，只是换成 WPF 能表达的等价写法。
        /// </remarks>
        public const double AnchorOffset = 24.5 + 8.0;

        /// <summary>
        /// 完全收起时的透明度权重：0，也就是一开始完全不可见。
        /// </summary>
        /// <remarks>
        /// 这里曾经是 0.35，理由是"菜单一出现就该是有形的，免得像在半空中浮出来"。
        /// 但 Inkeys 的面板透明度初值就是 0（Bar.EraserAttributeMotion.cppm:95 的 opacity_{0.0}、
        /// Bar.RenderLoop.cpp:710 的 morePanelOpacity{0.0}），全程 0 → 1 淡入，
        /// 静止展开态再统一乘 BarDrawAttributeSurfaceOpacity = 0.95（Bar.Main.cpp:75）。
        /// 也就是说 Inkeys 从来没有"起点就露三成"这回事。
        ///
        /// 旧值还有个副作用：进度 0 时菜单已经 35% 可见，于是"弹出"这件事的开头
        /// 是半透明色块凭空胀出来，而不是从无到有地显现；缩放本身还有个不小的起点，
        /// 两者叠加就抵消掉了"从小长大"该有的开场感。
        /// </remarks>
        public const double MinOpacityWeight = 0.0;

        /// <summary>按菜单宽度取收起态的缩放：收起来时在屏幕上占 <see cref="CompactWidth"/> 那么宽。</summary>
        /// <remarks>
        /// 宽度无效（0、NaN、无穷）时退回 <see cref="MaxStartScale"/>：
        /// 这是"菜单尺寸未知"时唯一不会出错的答案——起点偏大无非是生长段短一点，
        /// 而除以零得到的 NaN 缩放会让 WPF 直接不绘制，整段动画凭空消失。
        /// </remarks>
        public static double StartScaleFor(double contentWidth)
        {
            if (double.IsNaN(contentWidth) || double.IsInfinity(contentWidth) || contentWidth <= 0.0)
            {
                return MaxStartScale;
            }

            return Clamp(CompactWidth / contentWidth, MinStartScale, MaxStartScale);
        }

        /// <summary>EaseOutBack 的行程峰值：<c>1 + 4c³ / (27(c+1)²)</c>。</summary>
        public static double TravelPeakFor(double tension)
        {
            var c = tension;
            return 1.0 + 4.0 * c * c * c / (27.0 * (c + 1.0) * (c + 1.0));
        }

        /// <summary>
        /// 反解张力：让这条曲线在起点缩放下恰好把缩放推到 <see cref="PeakScale"/>。
        /// </summary>
        /// <remarks>
        /// 行程峰值要取 t = (PeakScale - s) / (1 - s)，代入 <see cref="TravelPeakFor"/> 得
        ///   4c³ = 27(t - 1)(c + 1)²
        /// 等价于 g(c) = 4c³ / (c + 1)² 等于常数 27k。g 在正半轴上严格单调递增
        /// （g'(c) = 4c²(c + 3) / (c + 1)³ &gt; 0），所以正根唯一。
        ///
        /// 求根用"倍增定界 + 二分"，不用牛顿：行程短（起点大、k 小）时牛顿会从 1.3
        /// 一步跨到负方向——起点 0.72 处导数 12c² - 54k(c + 1) 在 c = 1.3 已为负，迭代立刻失效，
        /// 返回偏小的张力（实测峰值只弹到 1.0615，而不是设计上的 1.05）。
        /// 二分对这个单调问题不需要好初值，也不会跑飞；
        /// 60 次迭代后区间已收窄到 1e-18，残差是纯浮点噪音。
        /// 每次打开菜单只算一次，代价可以忽略，换来的是"峰值恒定、不用查表"。
        /// </remarks>
        public static double TensionForPeak(double startScale)
        {
            var travelPeak = (PeakScale - startScale) / (1.0 - startScale);
            var k = travelPeak - 1.0;
            if (!(k > 0.0)) return 0.0;

            var low = 0.0;
            var high = 1.0;
            while (Residual(high, k) < 0.0) high *= 2.0;

            for (var i = 0; i < 60; i++)
            {
                var middle = 0.5 * (low + high);
                if (Residual(middle, k) < 0.0) low = middle;
                else high = middle;
            }

            return 0.5 * (low + high);
        }

        /// <summary>反解残差，零点即所求张力。</summary>
        private static double Residual(double tension, double k)
        {
            var shifted = tension + 1.0;
            return 4.0 * tension * tension * tension - 27.0 * k * shifted * shifted;
        }

        /// <summary>峰值在进度轴上的位置，由张力导出：<c>p = 1 - 2c / (3(c+1))</c>。</summary>
        public static double PeakAtFor(double tension)
        {
            return 1.0 - 2.0 * tension / (3.0 * (tension + 1.0));
        }

        /// <summary>按给定张力取行程。</summary>
        /// <remarks>
        /// 通用形态只为验收"移植是否忠实"服务（PopupCurveTests 用它对齐 Inkeys 既有期望值
        /// EaseOutBack(0.5) = 1.0125），生产路径一律走 <see cref="PopupCurveShape.Evaluate"/>。
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
        /// 而那件事只发生在进度的四成左右（0.40s 里的 176ms）——
        /// 剩下大半段回落里透明度一动不动，观感是"先迅速变实，然后空转着抖一下"。
        /// 更糟的是这个饱和时刻由张力决定：张力越大饱和越早，
        /// 也就是调"弹多狠"会顺带改掉"淡入多快"，两个旋钮绞在一起。
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

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value)) return 0.0;
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return max;
            return value < min ? min : value > max ? max : value;
        }
    }

    /// <summary>
    /// 一条具体的菜单弹出曲线：起点缩放由菜单宽度决定，其余参数全部由它导出。
    /// </summary>
    /// <remarks>
    /// 之所以要这样一个值类型，是因为"起点多小"和"张力取多少""位移多长"三者互相牵制：
    /// 起点越小行程越长、峰值就越容易超过 PeakScale，于是张力要跟着变、位移也要跟着变。
    /// 把它算一次存下来，调用方（动画时钟、留白计算、诊断工程）拿到的就是同一组自洽的数，
    /// 不会出现"动画按 0.7 播、留白按 0.3 留"这种对不上的情况。
    /// </remarks>
    public readonly struct PopupCurveShape
    {
        /// <summary>收起态的缩放。由菜单宽度与 <see cref="PopupCurve.CompactWidth"/> 导出。</summary>
        public readonly double StartScale;

        /// <summary>Back 曲线张力，由 <see cref="PopupCurve.PeakScale"/> 与起点缩放反解。</summary>
        public readonly double Tension;

        /// <summary>峰值在进度轴上的位置。</summary>
        public readonly double PeakAt;

        /// <summary>峰值缩放，恒等于 <see cref="PopupCurve.PeakScale"/>（张力就是按它反解的）。</summary>
        public readonly double PeakScale;

        /// <summary>完全收起时相对打开位置的下滑距离（DIP）：<see cref="PopupCurve.AnchorOffset"/>·(1 - 起点缩放)。</summary>
        public readonly double SlideDistance;

        private PopupCurveShape(double startScale, double tension, double peakAt, double slideDistance)
        {
            StartScale = startScale;
            Tension = tension;
            PeakAt = peakAt;
            PeakScale = PopupCurve.PeakScale;
            SlideDistance = slideDistance;
        }

        /// <summary>按菜单自然宽度构造。</summary>
        public static PopupCurveShape ForContentWidth(double contentWidth)
        {
            return ForStartScale(PopupCurve.StartScaleFor(contentWidth));
        }

        /// <summary>按给定起点缩放构造（诊断工程与单测直接用这个，跳过宽度换算）。</summary>
        public static PopupCurveShape ForStartScale(double startScale)
        {
            var tension = PopupCurve.TensionForPeak(startScale);
            return new PopupCurveShape(
                startScale,
                tension,
                PopupCurve.PeakAtFor(tension),
                PopupCurve.AnchorOffset * (1.0 - startScale));
        }

        /// <summary>曲线行程：0 = 完全收起，1 = 落位，峰值处越过 1 形成回弹。</summary>
        public double Travel(double progress)
        {
            return PopupCurve.TravelWithBack(Tension, progress);
        }

        /// <summary>
        /// 峰值处额外向上探出的距离（DIP，正数表示向上）。
        /// </summary>
        /// <remarks>
        /// 峰值时行程越过 1.0，位移量 = 位移行程·(1 - 1.0588…) 为负，即内容整体上移。
        /// 留白计算要把它和"缩放本身向两边撑出去的量"一起算进去，否则顶部仍会被裁。
        /// </remarks>
        public double PeakRise => Math.Abs(SlideDistance * (1.0 - Travel(PeakAt)));

        /// <summary>按进度取曲线上的缩放、位移与透明度权重。</summary>
        /// <remarks>
        /// 缩放与位移共用同一个行程，避免"缩放先快后慢、位移匀速"的错位感。
        /// 透明度不跟行程走，理由见 <see cref="PopupCurve.OpacityWeight"/>。
        /// </remarks>
        public PopupCurveSample Evaluate(double progress)
        {
            double travel = Travel(progress);

            double scale = StartScale + (1.0 - StartScale) * travel;

            // 负值表示回弹时略微越过静止位置再退回（Inkeys 的呼吸感），
            // 这点位移落在 PopupOverflowRoom 的留白里，不会裁切。
            double slideY = SlideDistance * (1.0 - travel);

            return new PopupCurveSample(scale, slideY, PopupCurve.OpacityWeight(progress));
        }
    }

    /// <summary>曲线在某一进度上的取值。</summary>
    public readonly struct PopupCurveSample
    {
        /// <summary>缩放倍率。</summary>
        public readonly double Scale;

        /// <summary>纵向位移（正=向下，负=回弹时越过静止位置）。</summary>
        public readonly double SlideY;

        /// <summary>透明度权重，0~1，走独立的 EaseOutSine 时间线（见 <see cref="PopupCurve.OpacityWeight"/>）。</summary>
        public readonly double OpacityWeight;

        public PopupCurveSample(double scale, double slideY, double opacityWeight)
        {
            Scale = scale;
            SlideY = slideY;
            OpacityWeight = opacityWeight;
        }
    }
}
