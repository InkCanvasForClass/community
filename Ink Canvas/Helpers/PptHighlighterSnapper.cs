using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT 文字行（已换算到墨迹画布坐标系）的矩形区域。
    /// </summary>
    public sealed class PptTextLineRect
    {
        /// <summary>文字行在墨迹画布坐标系下的外接矩形。</summary>
        public Rect Rect { get; set; }

        /// <summary>字形本身的估算纵向中心（用于修正基线漂移，非外接矩形中心）。</summary>
        public double GlyphCenterY { get; set; }

        /// <summary>
        /// 该行字形实际高度占行盒的比例；≤0 表示未知，用默认估值。
        /// </summary>
        /// <remarks>
        /// 逐行携带而不是全局一个值：同一张幻灯片里大小标题字号往往差一倍，
        /// 统一系数会让大字号那行明显偏细、小字号那行偏粗。
        ///
        /// 现已不参与条厚计算（条厚改由行盒实测高度推导），仅作诊断信息保留。
        /// </remarks>
        public double GlyphHeightRatio { get; set; }

        /// <summary>
        /// 由字号推算出的字形墨迹高度（画布单位）；≤0 表示未知。
        /// </summary>
        /// <remarks>
        /// 用来给行盒做交叉校验。PPT 的 <c>BoundHeight</c> 在不同版本、不同行距、
        /// 不同占位符类型下语义并不统一（有时是「字号」，有时是「字号 × 行距」），
        /// 单看它无法判断是否可信；而「字号 × 中文字形占 em 的比例」是稳定的物理量，
        /// 用它反推行盒是否偏小，比猜一个系数可靠。
        /// </remarks>
        public double GlyphInkHeight { get; set; }

        /// <summary>来源形状名称，仅用于日志定位。</summary>
        public string ShapeName { get; set; }
    }

    /// <summary>
    /// 「PPT 荧光笔墨迹纠正」的纯算法实现。
    /// </summary>
    /// <remarks>
    /// 职责边界：只负责几何计算与笔画构造，不访问 COM、不访问 UI 控件、不读写设置。
    /// 这样单元测试可以直接用假数据驱动，实机路径只负责把 PPT 文字行喂进来。
    ///
    /// 术语说明：
    /// - 本功能的「纠正」= 把随手画的斜线/歪线，规整成贴合文字行的水平高亮条；
    /// - 只有满足「横向主导 + 纵向漂移在容差内」的笔画才会被纠正，
    ///   否则视为用户在画圈/画竖线/画箭头，原样保留，避免误伤正常批注。
    /// </remarks>
    public static class PptHighlighterSnapper
    {
        /// <summary>
        /// 在笔画上附加的标记，表示这一笔已被纠正（用于撤销、诊断和避免重复处理）。
        /// </summary>
        public static readonly Guid SnappedGuid = new Guid("9C4E5B21-7A3D-4F68-8E11-2B6A0D55F3A7");

        /// <summary>判定为「横向笔画」的最小长宽比（包围盒宽 / 包围盒高）。</summary>
        private const double MinHorizontalAspect = 2.0;

        /// <summary>笔画包围盒中心到文字行纵向中心的最大偏移比例（相对行高）。超过即认为画的是别的东西。</summary>
        private const double MaxCenterDriftRatio = 0.85;

        /// <summary>纠正条与文字行的最小横向重叠比例，用于过滤「擦边而过」的笔画。</summary>
        private const double MinHorizontalOverlapRatio = 0.35;

        /// <summary>文字行最小/最大高宽比，用于过滤明显不是一行文字的异常矩形。</summary>
        private const double MinLineAspect = 0.8;
        private const double MaxLineAspect = 100.0;

        /// <summary>斜线在单行内的最大倾角（度）。超过说明用户在画引线，不纠正。</summary>
        private const double MaxSlantAngleDegrees = 45.0;

        /// <summary>
        /// 用字号推算的墨迹高度校验行盒，必要时把行盒高度修正到可信值。
        /// </summary>
        /// <remarks>
        /// 背景：PPT 的 <c>TextRange2.BoundHeight</c> 并非在所有情况下都等于「字号 × 行距」。
        /// 单倍行距时它常常就约等于字号，而 <c>BoundTop</c> 仍按含行距的排版盒给，
        /// 结果就是「盒顶偏上、盒高偏小」—— 高亮条压住字的上半截，下缘明显露出。
        ///
        /// 判据用稳定物理量：中文字形墨迹高度 ≈ 字号 × <see cref="CjkInkToEmRatio"/>。
        /// 若行盒明显比墨迹还矮，说明行盒没含行距，此时按「行距 1.2 倍」补足到 字号 × 1.2。
        /// 这个修正只在异常情况下触发，正常行盒原样保留。
        /// </remarks>
        private static double ResolveEffectiveLineHeight(PptTextLineRect line)
        {
            double boxHeight = line.Rect.Height;
            if (boxHeight <= 0) return boxHeight;

            double inkHeight = line.GlyphInkHeight;
            if (inkHeight <= 0 || double.IsNaN(inkHeight) || double.IsInfinity(inkHeight))
                return boxHeight;   // 读不到字号，不做修正

            // 行盒比墨迹还矮（或几乎等高）→ 未含行距，按 1.2 倍行距补齐。
            // 阈值取 1.08：留一点余量，避免单倍行距的正常盒子被误判。
            if (boxHeight < inkHeight * 1.08)
            {
                double expanded = inkHeight * NormalLineSpacing;
                // 仅当补足后仍落在合理区间才采纳，避免字号读数异常时把条撑爆。
                if (expanded > boxHeight && expanded <= boxHeight * 2.0)
                    return expanded;
            }

            return boxHeight;
        }

        /// <summary>
        /// 原始（未修正）行盒的几何中心，作为横向命中判定的参考点。
        /// </summary>
        /// <remarks>
        /// 命中判定必须用一个与「行盒是否被低报」无关的量，否则一行被修正后
        /// 参考中心会跟着下移，同一笔在修正前后可能得出不同的命中结论。
        /// </remarks>
        public static double RawBoxCenterY(PptTextLineRect line)
        {
            return line.Rect.Top + line.Rect.Height * GlyphCenterInLineBox;
        }

        /// <summary>
        /// 计算最终的纵向中心；行盒被修正过时，中心也要跟着落到修正后的位置。
        /// </summary>
        /// <remarks>
        /// 行盒被修正意味着「盒顶是可信的，但盒高被低报」。
        /// <para>
        /// 两个锚点必须对齐：横向命中判定用的是 <see cref="RawBoxCenterY"/>（原始行盒几何中心，
        /// 也就是 <see cref="GlyphCenterY"/> 的语义），因此修正后的中心必须与它同源 ——
        /// 同样取「盒顶 + 盒高 × <see cref="GlyphCenterInLineBox"/>」，只是换成修正后的盒高。
        /// </para>
        /// <para>
        /// 不能假设「盒顶代表字形顶部、盒子向下撑高」。实测（见用户截图：条高 68 已能覆盖
        /// 71 的字形，却整体偏上 18.5px）表明 BoundTop 是含上半个行距的排版盒顶，
        /// 不是墨迹顶。按墨迹顶对齐会让条整体再上移约半个行距，反而更糟。
        /// </para>
        /// </remarks>
        private static double ResolveEffectiveCenterY(PptTextLineRect line, double effectiveLineHeight, double thickness)
        {
            double boxHeight = line.Rect.Height;

            // 行盒未被修正：中心就是行盒几何中心。
            if (Math.Abs(effectiveLineHeight - boxHeight) < 0.01)
                return RawBoxCenterY(line);

            // 行盒被撑高：盒顶与本行的几何关系不变，中心随新盒高下移。
            return line.Rect.Top + effectiveLineHeight * GlyphCenterInLineBox;        }

        /// <summary>
        /// 中文字形墨迹高度占 em 字号的典型比例。
        /// </summary>
        /// <remarks>
        /// 实测（fontprobe，WPF BuildGeometry，100px 字号）墨迹高度占 em：
        /// 微软雅黑 0.728、黑体 0.809、宋体 0.802、等线 0.896、楷体 0.778。
        /// 注意这比「字形撑满 em 框」的直觉小不少 —— 汉字墨迹实际只占 em 的
        /// 七成到九成，其余是字身故意留的字面边距。
        /// 取 0.80 作代表值，用于判断行盒是否可信。
        /// </remarks>
        private const double CjkInkToEmRatio = 0.80;

        /// <summary>PPT 单倍行距下，行盒高度相对字号的倍数。</summary>
        private const double NormalLineSpacing = 1.2;

        /// <summary>
        /// 行盒里「字形墨迹中心」的相对位置（相对行盒高度的 0~1）。
        /// </summary>
        /// <remarks>
        /// 0.50 = 几何中心。依据实测（fontprobe，五款中文字体，行距 1.0）：
        /// 墨迹中线分别为 0.537 / 0.502 / 0.499 / 0.476 / 0.505，均值 0.504。
        /// 中文墨迹几乎撑满 em 框，不存在「ascent 以上大量内部留白」的情况，
        /// 因此不存在需要补偿的系统性上偏。
        /// </remarks>
        public const double GlyphCenterInLineBox = 0.50;

        /// <summary>
        /// 中文 PPT 常见行距（约 1.2 倍）下，字形墨迹占行盒的典型比例。
        /// </summary>
        /// <remarks>
        /// 0.80（墨迹高占 em 的比例，五款字体实测 0.73~0.90）× 1/1.2 ≈ 0.67，
        /// 实测 1.2 倍行盒下占比 0.74~0.80。此处取 0.78 作为参考值，
        /// 仅用于日志诊断，不参与条厚计算 —— 条厚改由行盒实测高度推导。
        /// </remarks>
        public const double GlyphTypicalRatio = 0.78;

        /// <summary>
        /// 中文行盒里「字形实际墨迹」的纵向占比的兜底估值。
        /// </summary>
        /// <remarks>
        /// 实测中文字体（WPF BuildGeometry，100px 字号）墨迹高度占 Em：
        /// 微软雅黑 0.983、黑体 0.934、宋体 0.930、等线 0.957、楷体 0.913。
        /// 而 PPT 的文字行 BoundHeight 通常等于「字号 × 行距」，自带上下留白，
        /// 因此字形在行盒内只占其中一部分。0.85 是取中偏保守的估值。
        ///
        /// 注意：条厚与纵坐标已不再依赖该值（改由行盒实测尺寸推导），
        /// 它只作为行距异常时的微调参考。
        /// </remarks>
        public const double DefaultGlyphHeightRatio = 0.85;

        /// <summary>
        /// 对单条笔画做「纠正到文字行」处理。
        /// </summary>
        /// <param name="stroke">待处理的笔画；必须已经处于目标墨迹画布坐标系。</param>
        /// <param name="lines">当前幻灯片可用的文字行矩形集合（同一坐标系）。</param>
        /// <param name="highlighterWidth">荧光笔宽度（与当前画笔设置一致，用于决定高亮条厚度）。</param>
        /// <param name="isHighlighter">该笔画是否为荧光笔笔迹。</param>
        /// <param name="reason">输出参数：未纠正时给出可读原因，便于日志排查。</param>
        /// <returns>纠正后的笔画；若不应纠正则返回 <c>null</c>。</returns>
        public static Stroke TrySnap(
            Stroke stroke,
            IReadOnlyList<PptTextLineRect> lines,
            double highlighterWidth,
            bool isHighlighter,
            out string reason)
        {
            return TrySnap(stroke, lines, highlighterWidth, isHighlighter, null, out reason);
        }

        /// <summary>
        /// 对单条笔画做「纠正到文字行」处理，可选择自动贴合文字大小。
        /// </summary>
        /// <param name="stroke">待处理的笔画；必须已经处于目标墨迹画布坐标系。</param>
        /// <param name="lines">当前幻灯片可用的文字行矩形集合（同一坐标系）。</param>
        /// <param name="highlighterWidth">荧光笔宽度，作为未启用自适应时的条厚度。</param>
        /// <param name="isHighlighter">该笔画是否为荧光笔笔迹。</param>
        /// <param name="fitOptions">
        /// 自动贴合文字大小的参数；传 <c>null</c> 表示关闭，条厚度回到跟随荧光笔宽度。
        /// </param>
        /// <param name="reason">输出参数：未纠正时给出可读原因，便于日志排查。</param>
        /// <returns>纠正后的笔画；若不应纠正则返回 <c>null</c>。</returns>
        public static Stroke TrySnap(
            Stroke stroke,
            IReadOnlyList<PptTextLineRect> lines,
            double highlighterWidth,
            bool isHighlighter,
            PptTextFitOptions fitOptions,
            out string reason)
        {
            reason = null;

            if (stroke == null)
            {
                reason = "笔画为空";
                return null;
            }

            if (!isHighlighter)
            {
                reason = "非荧光笔";
                return null;
            }

            if (stroke.ContainsPropertyData(SnappedGuid))
            {
                reason = "已纠正过";
                return null;
            }

            if (lines == null || lines.Count == 0)
            {
                reason = "无可对齐文字行";
                return null;
            }

            var points = stroke.StylusPoints;
            if (points == null || points.Count < 2)
            {
                reason = "笔画点数不足";
                return null;
            }

            Rect bounds;
            try
            {
                bounds = stroke.GetBounds();
            }
            catch (Exception)
            {
                reason = "读取笔画包围盒失败";
                return null;
            }

            if (bounds.IsEmpty || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height)
                || bounds.Width <= 1 || bounds.Height <= 0)
            {
                reason = "笔画包围盒无效";
                return null;
            }

            // 1) 横向主导判断：竖线、圈、箭头等一律放行，避免误伤。
            double horizontalLength = Math.Max(bounds.Width, HorizontalProjectionLength(points));
            double aspect = bounds.Height < 1 ? double.MaxValue : horizontalLength / bounds.Height;
            if (aspect < MinHorizontalAspect && bounds.Height >= 1)
            {
                reason = $"非横向笔画(长宽比 {aspect:F2})";
                return null;
            }

            // 2) 找到与笔画横向范围重叠最多的文字行。
            PptTextLineRect bestLine = null;
            double bestOverlap = 0;
            foreach (var line in lines)
            {
                if (line == null) continue;

                double overlap = HorizontalOverlap(bounds.Left, bounds.Right, line.Rect.Left, line.Rect.Right);
                if (overlap <= 0) continue;

                double overlapRatio = overlap / Math.Max(1.0, Math.Min(bounds.Width, line.Rect.Width));
                if (overlapRatio < MinHorizontalOverlapRatio) continue;

                double drift = Math.Abs(bounds.Top + bounds.Height / 2.0 - RawBoxCenterY(line));
                if (drift > Math.Max(1.0, line.Rect.Height) * MaxCenterDriftRatio) continue;

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestLine = line;
                }
            }

            if (bestLine == null)
            {
                reason = "未命中文字行";
                return null;
            }

            // 3) 斜线容差：单行内斜率过大的笔画视为引线/箭头，不纠正。
            double dx = points[points.Count - 1].X - points[0].X;
            double dy = points[points.Count - 1].Y - points[0].Y;
            if (Math.Abs(dx) > 1)
            {
                double slantAngle = Math.Abs(Math.Atan2(dy, dx)) * 180.0 / Math.PI;
                if (slantAngle > MaxSlantAngleDegrees && slantAngle < 180.0 - MaxSlantAngleDegrees)
                {
                    reason = $"斜率过大({slantAngle:F0}°)";
                    return null;
                }
            }

            // 4) 还原用户笔迹的中心线范围。
            //    Stroke.GetBounds() 返回的是含笔尖外扩的包围盒（笔宽 20 时左右各多约 10px），
            //    直接拿来当意图范围会让高亮条比实际划出的范围更宽。这里先内缩半个原始笔宽，
            //    再外扩半个新笔宽，得到的才是「用户看到的起止位置」。
            double originalNib = stroke.DrawingAttributes?.Width ?? 0;
            if (double.IsNaN(originalNib) || double.IsInfinity(originalNib)) originalNib = 0;
            double originalInset = Math.Max(0, originalNib) / 2.0;

            double gestureLeft = bounds.Left + originalInset;
            double gestureRight = bounds.Right - originalInset;
            if (gestureRight < gestureLeft)
            {
                gestureLeft = bounds.Left;
                gestureRight = bounds.Right;
            }

            // 5) 目标矩形：只取笔画实际覆盖的横向范围与文字行求交，
            //    这样用户只高亮半句时不会整行刷满。
            double left = Math.Max(gestureLeft, bestLine.Rect.Left);
            double right = Math.Min(gestureRight, bestLine.Rect.Right);
            if (right - left < 2)
            {
                reason = "横向有效长度过短";
                return null;
            }

            // 端点外扩半个笔宽，让两端看起来是自然覆盖，而不是被文字行硬切一刀。
            double extend = Math.Max(0, highlighterWidth) * 0.5;
            double lineLeft = bestLine.Rect.Left;
            double lineRight = bestLine.Rect.Right;
            left = Math.Max(lineLeft, left - extend);
            right = Math.Min(lineRight, right + extend);

            if (right - left < 2)
            {
                reason = "横向有效长度过短";
                return null;
            }

            // 6) 高亮条尺寸。
            //    自适应模式：按文字行自身的实际高度铺满，解决「字大条细、只包住一小条」的问题；
            //    固定模式：沿用荧光笔宽度，字号大时条会明显偏细。
            double lineHeight = bestLine.Rect.Height;
            double thickness;
            double gestureExtend;

            // 实际参与排版的行高。固定模式直接就是行盒高度；
            // 自适应模式下会被 ResolveEffectiveLineHeight 自校准（见下）。
            double effectiveLineHeight = lineHeight;

            if (fitOptions != null)
            {
                // 先用字号推算的墨迹高度校验行盒：PPT 的 BoundHeight 在部分版本/行距/占位符下
                // 只等于字号而不含行距，导致行盒偏小、中心偏上（条压住字的上半截）。
                // 墨迹高度是稳定物理量，用它反推才靠得住。
                effectiveLineHeight = ResolveEffectiveLineHeight(bestLine);

                // 条厚直接由「行盒高度 × 铺满系数」决定。
                //
                // 曾经这里算的是 lineHeight × glyphRatio，而 glyphRatio 又是
                // fontSize × 0.93 ÷ lineHeight —— 代入后 lineHeight 被约掉，
                // 结果等于 fontSize × 0.93，行盒尺寸完全没参与计算。
                // 一旦 Font.Size 读数偏小（主题字号、占位符缩放、混排都会导致），
                // 条就整体偏小且偏上。改用行盒这个「看得见、量得准」的量作基准。
                thickness = effectiveLineHeight * fitOptions.FillRatio;

                // 上下限兜底：至少能看见，也别超过行高的 1.5 倍。
                thickness = Math.Max(1.5, thickness);
                thickness = Math.Min(thickness, effectiveLineHeight * fitOptions.MaxThicknessRatio);

                // 横向外扩同样按行盒高走，中文标点右侧留白靠这一步才能覆盖到。
                gestureExtend = fitOptions.ExtendRatio * effectiveLineHeight;
            }
            else
            {
                thickness = Math.Max(1.0, highlighterWidth);
                if (lineHeight > 0)
                    thickness = Math.Min(thickness, lineHeight * 1.15);
                thickness = Math.Max(1.0, Math.Min(thickness, lineHeight * 1.5));
                gestureExtend = Math.Max(0, highlighterWidth) * 0.5;
            }

            // 自适应模式下用字形外扩重算左右边界：外扩量通常大于固定模式，
            // 但不允许越出文字行自身的左右边界太多（避免刷到相邻文本框上）。
            if (fitOptions != null)
            {
                double relaxedLeft = Math.Max(bestLine.Rect.Left - gestureExtend, gestureLeft - gestureExtend);
                double relaxedRight = Math.Min(bestLine.Rect.Right + gestureExtend, gestureRight + gestureExtend);
                if (relaxedRight - relaxedLeft >= 2)
                {
                    left = relaxedLeft;
                    right = relaxedRight;
                }
            }

            if (right - left < 2)
            {
                reason = "横向有效长度过短";
                return null;
            }

            double centerY = ResolveEffectiveCenterY(bestLine, effectiveLineHeight, thickness);
            double top = centerY - thickness / 2.0;

            var rect = new Rect(left, top, right - left, thickness);
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0
                || double.IsNaN(rect.Width) || double.IsNaN(rect.Height))
            {
                reason = "目标矩形无效";
                return null;
            }

            Stroke snapped;
            try
            {
                snapped = BuildBarStroke(stroke.DrawingAttributes, rect);
            }
            catch (Exception)
            {
                reason = "构造高亮条失败";
                return null;
            }

            if (snapped == null)
            {
                reason = "构造高亮条失败";
                return null;
            }

            reason = null;
            return snapped;
        }

        /// <summary>
        /// 用三条共线笔画拼出一个矩形高亮条。
        /// </summary>
        /// <remarks>
        /// 选三条重合笔画而不是单笔画 + 自定义形状，原因：
        /// - 纯 <see cref="StrokeCollection"/> 即可表达，撤销/重做、UInk 序列化、页面保存全部沿用既有链路；
        /// - 不依赖自定义 Stroke 子类，避免 InkFadeManager、形状识别等既有逻辑碰到不认识的类型；
        /// - 矩形笔尖（StylusTip.Rectangle）的荧光笔在这种折返路径下能干净填满一个方块。
        /// </remarks>
        private static Stroke BuildBarStroke(DrawingAttributes sourceAttributes, Rect rect)
        {
            if (sourceAttributes == null || rect.IsEmpty) return null;

            var attributes = sourceAttributes.Clone();

            // 矩形条必须是荧光笔渲染，否则会变成一条折返的实线。
            attributes.IsHighlighter = true;
            attributes.StylusTip = StylusTip.Rectangle;

            // Width 是笔尖横向尺寸，Height 是笔尖纵向（= 条厚度）。
            // 折返路径下横向尺寸会被描边两次，取极小值让左右边对齐目标矩形。
            const double nibWidth = 0.1;
            attributes.Width = nibWidth;
            attributes.Height = rect.Height;

            attributes.FitToCurve = false;
            attributes.IgnorePressure = true;

            // WPF 会给笔迹两端各加半个笔尖宽度的描边，实测条宽比目标宽约半个笔宽。
            // 两端各内缩半个笔尖，让最终渲染出来的宽度与目标矩形对齐。
            double inset = nibWidth / 2.0;
            double left = rect.Left + inset;
            double right = rect.Right - inset;
            if (right - left < 0.2)
            {
                left = rect.Left;
                right = rect.Right;
            }

            double midY = rect.Top + rect.Height / 2.0;

            var cornerPoints = new List<Point>
            {
                new Point(left, midY),
                new Point(right, midY),
                new Point(right, midY),
                new Point(left, midY)
            };

            var stylusPoints = new StylusPointCollection();
            foreach (var p in cornerPoints)
            {
                stylusPoints.Add(new StylusPoint(p.X, p.Y, 0.5f));
            }

            var stroke = new Stroke(stylusPoints, attributes);

            // 标记已纠正，避免被二次处理，也便于在日志与调试中识别。
            stroke.AddPropertyData(SnappedGuid, true);

            return stroke;
        }

        /// <summary>
        /// 把一组文字行矩形过滤成「像一行文字」的候选集。
        /// </summary>
        /// <remarks>
        /// 兜底策略：PPT 里取不到逐行边界时，会用整个文本框的矩形代替，
        /// 这种矩形通常高宽比异常（多行文本或空占位符），此过滤器会把它剔除，
        /// 宁可这次不纠正，也不要在错误位置刷出一条高亮。
        /// </remarks>
        public static List<PptTextLineRect> Sanitize(IEnumerable<PptTextLineRect> raw)
        {
            var result = new List<PptTextLineRect>();
            if (raw == null) return result;

            foreach (var line in raw)
            {
                if (line == null) continue;

                var rect = line.Rect;
                if (rect.IsEmpty) continue;
                if (double.IsNaN(rect.Width) || double.IsNaN(rect.Height)) continue;
                if (double.IsInfinity(rect.Width) || double.IsInfinity(rect.Height)) continue;
                if (rect.Width < 2 || rect.Height < 2) continue;

                double aspect = rect.Width / rect.Height;
                if (aspect < MinLineAspect) continue;
                if (aspect > MaxLineAspect) continue;

                if (double.IsNaN(line.GlyphCenterY) || double.IsInfinity(line.GlyphCenterY)) continue;
                if (line.GlyphCenterY < rect.Top - rect.Height || line.GlyphCenterY > rect.Bottom + rect.Height) continue;

                result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// 计算笔画折线在 X 轴上的累计投影长度，用来兜住「斜着横扫」这类包围盒宽高比失真的笔画。
        /// </summary>
        private static double HorizontalProjectionLength(StylusPointCollection points)
        {
            double total = 0;
            for (int i = 1; i < points.Count; i++)
            {
                total += Math.Abs(points[i].X - points[i - 1].X);
            }
            return total;
        }

        /// <summary>
        /// 两个一维区间（[a1,a2] 与 [b1,b2]）的重叠长度。
        /// </summary>
        private static double HorizontalOverlap(double a1, double a2, double b1, double b2)
        {
            double left = Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));
            double right = Math.Min(Math.Max(a1, a2), Math.Max(b1, b2));
            return right - left;
        }

        /// <summary>
        /// 把 PPT 磅值坐标下的文字行换算到墨迹画布坐标系。
        /// </summary>
        /// <param name="linesInPoints">PPT 磅值坐标系下的文字行。</param>
        /// <param name="slideWidth">幻灯片宽度（磅）。</param>
        /// <param name="slideHeight">幻灯片高度（磅）。</param>
        /// <param name="slideScreenRect">幻灯片内容在屏幕上的实际像素矩形（不含黑边）。</param>
        /// <param name="canvasPointFromScreen">屏幕像素点 → 墨迹画布坐标的转换委托。</param>
        /// <returns>换算后的文字行；若任一环节失败则返回空列表。</returns>
        public static List<PptTextLineRect> MapToCanvas(
            IEnumerable<PptTextLineRect> linesInPoints,
            double slideWidth,
            double slideHeight,
            Rect slideScreenRect,
            Func<Point, Point> canvasPointFromScreen)
        {
            var result = new List<PptTextLineRect>();
            if (linesInPoints == null || canvasPointFromScreen == null) return result;
            if (slideWidth <= 0 || slideHeight <= 0) return result;
            if (slideScreenRect.IsEmpty || slideScreenRect.Width <= 0 || slideScreenRect.Height <= 0) return result;

            double scaleX = slideScreenRect.Width / slideWidth;
            double scaleY = slideScreenRect.Height / slideHeight;

            foreach (var line in linesInPoints)
            {
                if (line == null) continue;

                try
                {
                    var tl = canvasPointFromScreen(new Point(
                        slideScreenRect.Left + line.Rect.Left * scaleX,
                        slideScreenRect.Top + line.Rect.Top * scaleY));
                    var br = canvasPointFromScreen(new Point(
                        slideScreenRect.Left + line.Rect.Right * scaleX,
                        slideScreenRect.Top + line.Rect.Bottom * scaleY));
                    var glyph = canvasPointFromScreen(new Point(
                        slideScreenRect.Left + line.Rect.Left * scaleX,
                        slideScreenRect.Top + line.GlyphCenterY * scaleY));

                    var rect = new Rect(tl, br);
                    if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;

                    result.Add(new PptTextLineRect
                    {
                        Rect = rect,
                        GlyphCenterY = glyph.Y,
                        // 逐形状字形占比必须跟着换算走，否则自适应模式下会退回默认值，
                        // 大字号行的条又会变细。
                        GlyphHeightRatio = line.GlyphHeightRatio,
                        // 墨迹高度是按磅给出的，换到画布坐标要走同样的纵向缩放。
                        GlyphInkHeight = line.GlyphInkHeight * (rect.Height / Math.Max(1e-6, line.Rect.Height)),
                        ShapeName = line.ShapeName
                    });
                }
                catch (Exception)
                {
                    // 单行换算失败不影响其余行，直接跳过。
                }
            }

            return result;
        }

        /// <summary>
        /// 从行盒推断「视觉上像一行字」的纵向中心。
        /// </summary>
        /// <remarks>
        /// 直接用行盒的几何中心 —— 这是量出来的，不是推的。
        ///
        /// 实测方法（fontprobe，WPF <c>FormattedText.BuildGeometry()</c>，100px 字号，
        /// 行距 1.0）：汉字墨迹在行盒内纵向占据的区间与中线如下
        /// <list type="bullet">
        /// <item>微软雅黑 0.173~0.901，中线 0.537</item>
        /// <item>黑体 0.097~0.906，中线 0.502</item>
        /// <item>宋体 0.098~0.900，中线 0.499</item>
        /// <item>等线 0.028~0.924，中线 0.476</item>
        /// <item>楷体 0.116~0.894，中线 0.505</item>
        /// </list>
        /// 五款字体中线均值 0.504，即墨迹中心与行盒几何中心重合，没有系统性偏移。
        ///
        /// 曾经这里写过 0.71，依据是「ascent 以上有内部留白所以重心偏下」的推断 ——
        /// 实测证明这个推断是错的：中文字体的墨迹几乎撑满 em 框，
        /// 上缘就在 ascent 附近，不存在显著的上部内部留白。
        /// 用推断值会把整条高亮推到字形上方，正是「条偏上、字从下面露出来」的成因。
        /// </remarks>
        /// <param name="lineTop">行盒上缘。</param>
        /// <param name="lineHeight">行盒高度。</param>
        /// <param name="glyphHeightRatio">保留参数以兼容旧调用；不再参与计算。</param>
        public static double EstimateGlyphCenterY(double lineTop, double lineHeight, double glyphHeightRatio = 0)
        {
            if (lineHeight <= 0) return lineTop;

            return lineTop + lineHeight * GlyphCenterInLineBox;
        }

        /// <summary>
        /// 把当前幻灯片内所有文字形状拆成逐行矩形（PPT 磅值坐标系）。
        /// </summary>
        /// <remarks>
        /// 仅处理带文本框的形状；单个文本框内通过 <c>TextRange2.Lines</c> 逐行取边界，
        /// 取不到时退化为整个文本框矩形（后续 <see cref="Sanitize"/> 会剔除可疑项）。
        /// 该方法只做数据整理，COM 访问由调用方负责，便于测试与职责分离。
        /// </remarks>
        public static List<PptTextLineRect> FlattenTextLines(IEnumerable<PptTextShapeBounds> shapes)
        {
            var result = new List<PptTextLineRect>();
            if (shapes == null) return result;

            foreach (var shape in shapes)
            {
                if (shape == null) continue;

                if (shape.Lines != null && shape.Lines.Count > 0)
                {
                    // 同一文本框内各行字号一致，用该形状的字体比例统一估算，避免逐行抖动。
                    double shapeRatio = shape.GlyphHeightRatio > 0
                        ? shape.GlyphHeightRatio
                        : DefaultGlyphHeightRatio;

                    foreach (var lineRect in shape.Lines)
                    {
                        if (lineRect.IsEmpty || lineRect.Width <= 0 || lineRect.Height <= 0) continue;
                        result.Add(new PptTextLineRect
                        {
                            Rect = lineRect,
                            GlyphCenterY = EstimateGlyphCenterY(lineRect.Top, lineRect.Height, shapeRatio),
                            GlyphHeightRatio = shape.GlyphHeightRatio,
                            GlyphInkHeight = shape.GlyphInkHeight,
                            ShapeName = shape.ShapeName
                        });
                    }
                }
                else if (!shape.ShapeRect.IsEmpty && shape.ShapeRect.Width > 0 && shape.ShapeRect.Height > 0)
                {
                    result.Add(new PptTextLineRect
                    {
                        Rect = shape.ShapeRect,
                        // 退化成整个文本框时行高已包含大量留白，字形中心不能再按行盒推算，
                        // 直接用几何中心更稳。
                        GlyphCenterY = shape.ShapeRect.Top + shape.ShapeRect.Height / 2.0,
                        GlyphHeightRatio = shape.GlyphHeightRatio,
                        GlyphInkHeight = shape.GlyphInkHeight,
                        ShapeName = shape.ShapeName
                    });
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 一个 PPT 文本形状的边界信息（PPT 磅值坐标系）。
    /// </summary>
    public sealed class PptTextShapeBounds
    {
        /// <summary>形状名称，用于日志。</summary>
        public string ShapeName { get; set; }

        /// <summary>形状外接矩形。</summary>
        public Rect ShapeRect { get; set; }

        /// <summary>逐行文字外接矩形；为空表示无法逐行获取，将退化为 <see cref="ShapeRect"/>。</summary>
        public List<Rect> Lines { get; set; }

        /// <summary>
        /// 该形状字形实际高度占行盒的比例；≤0 表示未知。
        /// </summary>
        /// <remarks>
        /// 由「字号 × 中文字形占 em 的比例」除以行盒高度得到。
        /// 现主要用于诊断，不参与条厚计算。
        /// </remarks>
        public double GlyphHeightRatio { get; set; }

        /// <summary>
        /// 由字号推算的字形墨迹高度（与 <see cref="Lines"/> 同一坐标系，单位同为磅）。
        /// </summary>
        /// <remarks>
        /// 用于交叉校验行盒高度是否可信：<c>BoundHeight</c> 在部分情况下只等于字号，
        /// 而墨迹高度 ≈ 字号 × 0.8 是稳定物理量，据此可判断盒高是否被低报。
        /// </remarks>
        public double GlyphInkHeight { get; set; }
    }

    /// <summary>
    /// 「自动贴合文字大小」的参数。
    /// </summary>
    /// <remarks>
    /// 关闭该选项时给算法传 <c>null</c>，高亮条厚度回到跟随荧光笔宽度。
    /// </remarks>
    public sealed class PptTextFitOptions
    {
        /// <summary>字形高度占行盒的比例；保留以兼容旧设置，不再参与条厚计算。</summary>
        public double GlyphHeightRatio { get; set; } = PptHighlighterSnapper.DefaultGlyphHeightRatio;

        /// <summary>
        /// 纵向铺满系数：条厚 = 行盒高 × 该系数。
        /// </summary>
        /// <remarks>
        /// 1.0 = 与行盒等高。中文行盒自带上下留白，字形在盒内只占约 0.78，
        /// 因此取略小于 1 就能饱满包住字形又不会糊到相邻行。
        /// </remarks>
        public double FillRatio { get; set; } = 0.95;

        /// <summary>条厚上限相对行盒高的倍数，防止异常行距把条撑到糊住邻行。</summary>
        public double MaxThicknessRatio { get; set; } = 1.5;

        /// <summary>横向外扩量，单位是「行盒高度」的倍数。</summary>
        public double ExtendRatio { get; set; } = 0.12;

        /// <summary>按设置值构造，并做区间夹紧，避免用户填出离谱数值。</summary>
        public static PptTextFitOptions FromSettings(double fillRatio, double extendRatio)
        {
            if (double.IsNaN(fillRatio) || double.IsInfinity(fillRatio)) fillRatio = 0.95;
            if (double.IsNaN(extendRatio) || double.IsInfinity(extendRatio)) extendRatio = 0.12;

            return new PptTextFitOptions
            {
                FillRatio = Math.Max(0.6, Math.Min(1.4, fillRatio)),
                ExtendRatio = Math.Max(0, Math.Min(1.5, extendRatio))
            };
        }
    }
}
