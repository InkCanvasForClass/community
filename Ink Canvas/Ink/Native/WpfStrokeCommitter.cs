using System;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Ink.Native
{
    internal static class WpfStrokeCommitter
    {
        public static readonly Guid RealtimeVelocityBrushTipAppliedGuid =
            new Guid("74E57D95-945F-4A8C-B52A-7D3EF2D4FD5B");

        /// <summary>
        /// Marks a dry Stroke that was produced by the native wet-ink pipeline.
        /// Dry post-process must not rewrite PressureFactor for these strokes —
        /// wet preview already baked the final pressure, and rewriting it at
        /// pen-up causes a wet→dry flash.
        /// Must stay identical to MainWindow.NativeWetInkCommittedGuid.
        /// </summary>
        public static readonly Guid NativeWetInkCommittedGuid =
            new Guid("B1F0A3C7-2D64-49B0-9E1A-7A4F8C2D5E60");

        /// <summary>
        /// Marks a dry Stroke that should be rendered by the laser neon fade path.
        /// WPF-collected laser strokes and native wet-ink committed laser strokes
        /// must stay visually aligned during wet→dry handoff and fade.
        /// </summary>
        public static readonly Guid LaserRenderModeGuid =
            new Guid("A69B0C9D-9A3A-4F91-8BE3-8E2DBB9AD4F7");

        public static Stroke CreateStroke(NativeStrokeCommitPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Points == null || payload.Points.Count == 0)
                throw new ArgumentException("Commit payload must contain real points.", nameof(payload));

            var style = payload.Style;

            var points = new StylusPointCollection(payload.Points.Count);
            for (var i = 0; i < payload.Points.Count; i++)
            {
                var point = payload.Points[i];
                var pressure = ClampPressure(point.Pressure);
                points.Add(new StylusPoint(point.X, point.Y, pressure));
            }
            // 笔锋写入的 PressureFactor 必须被 WPF 采纳；点集/速率/实时笔锋都强制开启压感渲染。
            var honorPressure = payload.VelocityBrushTipApplied
                                || payload.FinalBrushTipApplied
                                || style.UseVelocityBrushTip;
            var attributes = new DrawingAttributes
            {
                Color = ColorFromArgb(style.ColorArgb),
                Width = Math.Max(0.1, style.Width),
                Height = Math.Max(0.1, style.Height),
                IsHighlighter = style.IsHighlighter,
                IgnorePressure = honorPressure ? false : style.IgnorePressure,
                FitToCurve = false,
                StylusTip = style.StylusTipShape == InkStylusTipShape.Rectangle
                    ? StylusTip.Rectangle
                    : StylusTip.Ellipse
            };

            var stroke = new Stroke(points, attributes);
            if (!stroke.ContainsPropertyData(NativeWetInkCommittedGuid))
                stroke.AddPropertyData(NativeWetInkCommittedGuid, true);
            if (style.RenderMode == InkRenderMode.Laser
                && !stroke.ContainsPropertyData(LaserRenderModeGuid))
            {
                stroke.AddPropertyData(LaserRenderModeGuid, true);
            }
            if (payload.VelocityBrushTipApplied
                && !stroke.ContainsPropertyData(RealtimeVelocityBrushTipAppliedGuid))
            {
                stroke.AddPropertyData(RealtimeVelocityBrushTipAppliedGuid, true);
            }

            return stroke;
        }

        private static float ClampPressure(float pressure)
        {
            if (float.IsNaN(pressure) || float.IsInfinity(pressure))
                return 0.5f;
            if (pressure < 0f)
                return 0f;
            if (pressure > 1f)
                return 1f;
            return pressure;
        }

        private static Color ColorFromArgb(uint colorArgb)
        {
            return Color.FromArgb(
                (byte)((colorArgb >> 24) & 0xFF),
                (byte)((colorArgb >> 16) & 0xFF),
                (byte)((colorArgb >> 8) & 0xFF),
                (byte)(colorArgb & 0xFF));
        }
    }
}
