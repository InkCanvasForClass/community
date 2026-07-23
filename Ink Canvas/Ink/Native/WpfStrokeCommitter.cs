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

        public static Stroke CreateStroke(NativeStrokeCommitPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Points == null || payload.Points.Count == 0)
                throw new ArgumentException("Commit payload must contain real points.", nameof(payload));

            var points = new StylusPointCollection(payload.Points.Count);
            for (var i = 0; i < payload.Points.Count; i++)
            {
                var point = payload.Points[i];
                var pressure = ClampPressure(point.Pressure);
                points.Add(new StylusPoint(point.X, point.Y, pressure));
            }

            var style = payload.Style;
            var attributes = new DrawingAttributes
            {
                Color = ColorFromArgb(style.ColorArgb),
                Width = Math.Max(0.1, style.Width),
                Height = Math.Max(0.1, style.Height),
                IsHighlighter = style.IsHighlighter,
                IgnorePressure = style.IgnorePressure,
                FitToCurve = false,
                StylusTip = style.StylusTipShape == InkStylusTipShape.Rectangle
                    ? StylusTip.Rectangle
                    : StylusTip.Ellipse
            };

            var stroke = new Stroke(points, attributes);
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
