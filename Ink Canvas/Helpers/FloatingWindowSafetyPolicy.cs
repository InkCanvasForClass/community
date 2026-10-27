using System;

namespace Ink_Canvas.Helpers
{
    /// <summary>Positive identification for EasiNote floating windows; ambiguous windows are left alone.</summary>
    internal static class FloatingWindowSafetyPolicy
    {
        internal static bool IsSafeSeewoTarget(uint style, uint extendedStyle, string title,
            int width, int height, uint dpi, int monitorWidth, int monitorHeight)
        {
            const uint Child = 0x40000000, Caption = 0x00C00000, ThickFrame = 0x00040000;
            const uint Minimized = 0x20000000, Maximized = 0x01000000, AppWindow = 0x00040000;
            if ((style & (Child | Caption | ThickFrame | Minimized | Maximized)) != 0
                || (extendedStyle & AppWindow) != 0 || !string.IsNullOrWhiteSpace(title)) return false;
            if (width <= 0 || height <= 0 || monitorWidth <= 0 || monitorHeight <= 0 || dpi < 96 || dpi > 768)
                return false;
            // Never treat a full-screen/near-full-screen teaching surface as a popup,
            // even when its WPF class name and frameless style match the floating bar.
            if ((long)width * 100 >= (long)monitorWidth * 85
                && (long)height * 100 >= (long)monitorHeight * 85) return false;
            return MatchesSize(width, height, 550, 200)
                || MatchesSize(width, height, 550 * dpi / 96.0, 200 * dpi / 96.0);
        }

        private static bool MatchesSize(int width, int height, double expectedWidth, double expectedHeight)
            => Math.Abs(width - Math.Round(expectedWidth)) <= 2
                && Math.Abs(height - Math.Round(expectedHeight)) <= 2;
    }
}
