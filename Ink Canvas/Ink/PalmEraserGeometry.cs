using System;

namespace Ink_Canvas.Ink
{
    /// <summary>
    /// 手掌擦接触尺寸计算。
    /// 上下红外等触摸框可能把接触矩形某一轴异常放大（例如接近全屏宽度的长条），
    /// 这里把异常细长接触折算为几何平均宽度，并限制最终手掌橡皮大小。
    /// </summary>
    internal static class PalmEraserGeometry
    {
        /// <summary>
        /// 手掌橡皮最大宽度（DIP）。用于防止上下红外等设备上报的异常接触矩形
        /// 把橡皮放大到远超真实手掌的程度。
        /// </summary>
        internal const double MaxPalmEraserWidthDip = 200;

        /// <summary>
        /// 接触矩形长宽比超过该值时，认为某一轴可能是红外框的异常放大结果，
        /// 改用几何平均 sqrt(width*height) 来估算有效接触宽度。
        /// </summary>
        private const double ElongatedAspectRatioThreshold = 3.0;

        internal static double GetEffectiveContactWidthDip(
            double contactWidthDip,
            double contactHeightDip,
            bool isQuadIr)
        {
            if (contactWidthDip <= 0)
                return 0;

            // 部分设备不提供高度，仍回退到宽度，避免破坏原有行为。
            if (contactHeightDip <= 0)
                return contactWidthDip;

            var min = Math.Min(contactWidthDip, contactHeightDip);
            var max = Math.Max(contactWidthDip, contactHeightDip);
            var elongated = min <= 0 || max / min > ElongatedAspectRatioThreshold;

            if (isQuadIr || elongated)
                return Math.Sqrt(Math.Max(0, contactWidthDip * contactHeightDip));

            return contactWidthDip;
        }

        internal static double ApplyPalmEraserSize(
            double effectiveContactWidthDip,
            double eraserSizeFactor,
            bool isSpecialScreen,
            double touchMultiplier)
        {
            var widthDip = effectiveContactWidthDip * eraserSizeFactor;
            if (isSpecialScreen)
                widthDip *= touchMultiplier;

            return Math.Min(widthDip, MaxPalmEraserWidthDip);
        }
    }
}
