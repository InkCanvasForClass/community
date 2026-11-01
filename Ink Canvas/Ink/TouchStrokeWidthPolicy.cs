using System;
using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    /// <summary>
    /// 触摸笔迹粗细的统一策略。
    /// 固定笔宽 / 接触原始尺寸 / 接触尺寸×倍率 / 速度模拟，四种来源统一在这里换算，
    /// 预览与提交共用同一套 DrawingAttributes，避免预览和落笔后粗细不一致。
    /// </summary>
    internal static class TouchStrokeWidthPolicy
    {
        public const int Fixed = 0;
        public const int Contact = 1;
        public const int ContactScaled = 2;
        public const int Simulated = 3;

        private const double MinWidthDip = 0.6;
        private const double MaxWidthDip = 64.0;

        public static int Mode => Math.Clamp(MainWindow.Settings.Advanced.TouchWidthMode, Fixed, Simulated);

        public static double Scale => Math.Clamp(MainWindow.Settings.Advanced.TouchWidthScale, 0.25, 4.0);

        public static double Influence => Math.Clamp(MainWindow.Settings.Advanced.TouchPressureInfluence, 0.0, 1.0);

        public static bool PressureEnabled =>
            Mode != Fixed && !MainWindow.Settings.Canvas.DisablePressure && Influence > 0.001;

        /// <summary>
        /// 按当前模式生成一根笔迹的 DrawingAttributes。
        /// contactWidthDip/contactHeightDip 来自原始触摸接触面积（WPF 用 TouchPoint.Bounds，
        /// HID 用接触宽高换算成 DIP）；为 0 时退回画笔默认宽度。
        /// </summary>
        public static DrawingAttributes CreateAttributes(
            DrawingAttributes source, double contactWidthDip, double contactHeightDip)
        {
            var attributes = source?.Clone() ?? new DrawingAttributes();
            double penWidth = Math.Max(0.1, attributes.Width);
            double penHeight = Math.Max(0.1, attributes.Height);
            double contactW = contactWidthDip > 0 ? contactWidthDip : penWidth;
            double contactH = contactHeightDip > 0 ? contactHeightDip : penHeight;

            double width = penWidth;
            double height = penHeight;
            switch (Mode)
            {
                case Contact:
                    width = contactW;
                    height = contactH;
                    break;
                case ContactScaled:
                    width = contactW * Scale;
                    height = contactH * Scale;
                    break;
                case Simulated:
                case Fixed:
                default:
                    break;
            }

            attributes.Width = ClampWidth(width);
            attributes.Height = ClampWidth(height);
            attributes.IgnorePressure = !PressureEnabled;
            return attributes;
        }

        /// <summary>把原始压感按“压感影响”缩放，0=恒定 0.5，1=完全保留。</summary>
        public static float AdjustPressure(float pressure)
        {
            if (!PressureEnabled) return 0.5f;
            double adjusted = 0.5 + (Math.Clamp(pressure, 0.01f, 1.0f) - 0.5) * Influence;
            return (float)Math.Clamp(adjusted, 0.01, 1.0);
        }

        /// <summary>速度模拟笔锋：越快越细，并用 70/30 平滑避免抖动。</summary>
        public static float SimulatedPressure(double speedDipPerMs, float previous)
        {
            double target = 1.0 - Math.Clamp(speedDipPerMs / 2.5, 0.0, 1.0);
            target = Math.Clamp(target, 0.15, 1.0);
            float next = (float)(previous * 0.7 + target * 0.3);
            return (float)Math.Clamp(next, 0.01, 1.0);
        }

        private static double ClampWidth(double value) => Math.Clamp(value, MinWidthDip, MaxWidthDip);
    }
}
