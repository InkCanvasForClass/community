using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Serializes a WinRT InkStroke (physical-pixel points produced by the OS InkPresenter)
    /// into a WPF Stroke that can be added to the app's InkCanvas.Strokes, so all downstream
    /// features (undo, eraser, selection, save/load, plugin SDK) work unchanged.
    /// </summary>
    internal static class WinRTStrokeConverter
    {
        internal static readonly Guid RealtimeVelocityBrushTipAppliedGuid =
            new Guid("74E57D95-945F-4A8C-B52A-7D3EF2D4FD5B");

        /// <summary>
        /// Marks a dry Stroke produced by the WinRT wet-ink pipeline. Dry post-process must
        /// not rewrite PressureFactor for these strokes (wet preview already baked it).
        /// </summary>
        internal static readonly Guid NativeWetInkCommittedGuid =
            new Guid("B1F0A3C7-2D64-49B0-9E1A-7A4F8C2D5E60");

        /// <summary>
        /// Marks a dry Stroke that should be rendered by the laser neon fade path.
        /// </summary>
        internal static readonly Guid LaserRenderModeGuid =
            new Guid("A69B0C9D-9A3A-4F91-8BE3-8E2DBB9AD4F7");

        public static Stroke CreateStroke(
            IReadOnlyList<global::Windows.UI.Input.Inking.InkPoint> inkPoints,
            WinRTStrokeStyleSnapshot style,
            double dpiScaleX,
            double dpiScaleY,
            System.Windows.Point canvasOriginDip)
        {
            if (inkPoints == null || inkPoints.Count == 0)
                throw new ArgumentException("Stroke must contain points.", nameof(inkPoints));

            var points = new StylusPointCollection(inkPoints.Count);
            for (var i = 0; i < inkPoints.Count; i++)
            {
                var ip = inkPoints[i];
                var position = ip.Position;
                var x = position.X / dpiScaleX - canvasOriginDip.X;
                var y = position.Y / dpiScaleY - canvasOriginDip.Y;
                var pressure = ClampPressure(ip.Pressure);
                points.Add(new StylusPoint(x, y, pressure));
            }

            // 笔锋写入的 PressureFactor 必须被 WPF 采纳；速度笔锋强制开启压感渲染。
            var honorPressure = style.UseVelocityBrushTip;
            var attributes = new DrawingAttributes
            {
                Color = style.Color,
                Width = Math.Max(0.1, style.WidthDip),
                Height = Math.Max(0.1, style.HeightDip),
                IsHighlighter = style.IsHighlighter,
                IgnorePressure = honorPressure ? false : style.IgnorePressure,
                FitToCurve = false,
                StylusTip = style.TipRectangle ? StylusTip.Rectangle : StylusTip.Ellipse
            };

            var stroke = new Stroke(points, attributes);
            if (!stroke.ContainsPropertyData(NativeWetInkCommittedGuid))
                stroke.AddPropertyData(NativeWetInkCommittedGuid, true);
            if (style.IsLaser && !stroke.ContainsPropertyData(LaserRenderModeGuid))
                stroke.AddPropertyData(LaserRenderModeGuid, true);
            if (style.UseVelocityBrushTip
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
    }

    /// <summary>
    /// Immutable snapshot of the stroke appearance used to (a) push InkDrawingAttributes to
    /// the OS presenter for wet rendering and (b) rebuild WPF DrawingAttributes at pen-up.
    /// </summary>
    internal readonly struct WinRTStrokeStyleSnapshot
    {
        public WinRTStrokeStyleSnapshot(
            Color color,
            double widthDip,
            double heightDip,
            bool isHighlighter,
            bool ignorePressure,
            bool tipRectangle,
            bool useVelocityBrushTip,
            bool isLaser)
        {
            Color = color;
            WidthDip = widthDip;
            HeightDip = heightDip;
            IsHighlighter = isHighlighter;
            IgnorePressure = ignorePressure;
            TipRectangle = tipRectangle;
            UseVelocityBrushTip = useVelocityBrushTip;
            IsLaser = isLaser;
        }

        public Color Color { get; }
        public double WidthDip { get; }
        public double HeightDip { get; }
        public bool IsHighlighter { get; }
        public bool IgnorePressure { get; }
        public bool TipRectangle { get; }
        public bool UseVelocityBrushTip { get; }
        public bool IsLaser { get; }
    }
}
