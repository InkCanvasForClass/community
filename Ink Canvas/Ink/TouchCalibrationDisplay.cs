using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ink_Canvas.Ink
{
    internal static class TouchCalibrationDisplay
    {
        public static TouchCalibrationEnvironment Capture(Window window, string source)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);
            var dpi = VisualTreeHelper.GetDpi(window);
            return new TouchCalibrationEnvironment
            {
                Source = source, Display = screen.DeviceName,
                PixelWidth = screen.Bounds.Width, PixelHeight = screen.Bounds.Height,
                DpiX = dpi.DpiScaleX, DpiY = dpi.DpiScaleY
            };
        }
    }
}
