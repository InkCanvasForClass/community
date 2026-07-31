using System;

namespace Ink_Canvas.Mathematics.Services
{
    public static class MathAppearanceService
    {
        public const string DarkStrokeColor = "#FF111827";
        public const string LightStrokeColor = "#FFF8FAFC";

        public static string GetContrastingStrokeColor(byte red, byte green, byte blue)
        {
            var luminance =
                0.2126 * ToLinear(red / 255d) +
                0.7152 * ToLinear(green / 255d) +
                0.0722 * ToLinear(blue / 255d);
            return luminance < 0.45
                ? LightStrokeColor
                : DarkStrokeColor;
        }

        private static double ToLinear(double component)
        {
            return component <= 0.04045
                ? component / 12.92
                : Math.Pow((component + 0.055) / 1.055, 2.4);
        }
    }
}
