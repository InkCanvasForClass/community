using System;
using System.Windows.Media;
using Windows.UI.Input.Inking;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Immutable per-start configuration: the physical-pixel presenter size plus the
    /// InkDrawingAttributes for wet rendering. The WPF DrawingAttributes twin is kept in
    /// WinRTStrokeStyleSnapshot for the dry commit.
    /// </summary>
    internal sealed class WinRTInkConfig
    {
        public WinRTInkConfig(
            float widthPx,
            float heightPx,
            WinRTStrokeStyleSnapshot style,
            double dpiScaleX,
            double dpiScaleY,
            System.Windows.Point canvasOriginDip)
        {
            WidthPx = widthPx;
            HeightPx = heightPx;
            Style = style;
            DpiScaleX = dpiScaleX;
            DpiScaleY = dpiScaleY;
            CanvasOriginDip = canvasOriginDip;
        }

        public float WidthPx { get; }
        public float HeightPx { get; }

        /// <summary>WPF DIP snapshot used to rebuild the dry WPF Stroke.</summary>
        public WinRTStrokeStyleSnapshot Style { get; }

        public double DpiScaleX { get; }
        public double DpiScaleY { get; }
        public System.Windows.Point CanvasOriginDip { get; }

        public InkDrawingAttributes ToInkDrawingAttributes()
        {
            var style = Style;
            var attributes = new InkDrawingAttributes
            {
                Color = global::Windows.UI.Color.FromArgb(
                    style.Color.A,
                    style.Color.R,
                    style.Color.G,
                    style.Color.B),
                // InkPresenter sizes are physical pixels.
                Size = new global::Windows.Foundation.Size(
                    (float)(style.WidthDip * DpiScaleX),
                    (float)(style.HeightDip * DpiScaleY)),
                PenTip = style.TipRectangle ? PenTipShape.Rectangle : PenTipShape.Circle,
                DrawAsHighlighter = style.IsHighlighter,
                IgnorePressure = style.IgnorePressure,
                FitToCurve = false
            };
            return attributes;
        }
    }
}
