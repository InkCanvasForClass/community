using System;
using System.Windows.Ink;
using System.Windows.Input;

namespace Ink_Canvas.Ink.WinRT
{
    internal static class PenToolStylePolicy
    {
        internal static WinRTStrokeStyleSnapshot Capture(DrawingAttributes attributes, int penType,
            double highlighterWidth, bool overlapEnabled, bool ignorePressure, bool velocityTip)
        {
            bool highlighter = penType == 1;
            double width = attributes.Width, height = attributes.Height;
            if (highlighter && double.IsFinite(highlighterWidth) && highlighterWidth >= 0.2)
            { width = highlighterWidth / 2; height = highlighterWidth; }
            return new(attributes.Color, Math.Max(0.1, width), Math.Max(0.1, height),
                isHighlighter: highlighter && !overlapEnabled, ignorePressure: ignorePressure,
                tipRectangle: highlighter || (penType != 2 && attributes.StylusTip == StylusTip.Rectangle),
                useVelocityBrushTip: velocityTip && !highlighter, isLaser: penType == 2,
                isHighlighterTool: highlighter);
        }
    }
}
