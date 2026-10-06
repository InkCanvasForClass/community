using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        internal bool TryGetPluginCanvasScreenPixels(out Rect screenBounds)
        {
            screenBounds = Rect.Empty;
            if (inkCanvas == null || !inkCanvas.IsLoaded
                || inkCanvas.ActualWidth <= 0 || inkCanvas.ActualHeight <= 0)
                return false;

            try
            {
                var topLeftDip = inkCanvas.PointToScreen(new Point(0, 0));
                var bottomRightDip = inkCanvas.PointToScreen(
                    new Point(inkCanvas.ActualWidth, inkCanvas.ActualHeight));
                var transform = GetPluginDeviceTransform();
                var topLeft = DipScreenToPixel(topLeftDip, transform);
                var bottomRight = DipScreenToPixel(bottomRightDip, transform);
                screenBounds = new Rect(topLeft, bottomRight);
                return !screenBounds.IsEmpty && screenBounds.Width > 0 && screenBounds.Height > 0;
            }
            catch
            {
                screenBounds = Rect.Empty;
                return false;
            }
        }

        internal bool TryConvertPluginCanvasToScreenPixels(Point canvasPoint, out Point screenPixelPoint)
        {
            screenPixelPoint = default;
            if (inkCanvas == null || !inkCanvas.IsLoaded) return false;

            try
            {
                var dipScreen = inkCanvas.PointToScreen(canvasPoint);
                screenPixelPoint = DipScreenToPixel(dipScreen, GetPluginDeviceTransform());
                return IsFinite(screenPixelPoint);
            }
            catch
            {
                return false;
            }
        }

        internal bool TryConvertPluginScreenPixelsToCanvas(Point screenPixelPoint, out Point canvasPoint)
        {
            canvasPoint = default;
            if (inkCanvas == null || !inkCanvas.IsLoaded) return false;
            if (!IsFinite(screenPixelPoint)) return false;

            try
            {
                var dipScreen = PixelScreenToDip(screenPixelPoint, GetPluginDeviceTransform());
                canvasPoint = inkCanvas.PointFromScreen(dipScreen);
                return IsFinite(canvasPoint);
            }
            catch
            {
                return false;
            }
        }

        private static Matrix GetPluginDeviceTransform()
        {
            var visual = System.Windows.Application.Current?.MainWindow as Visual;
            var source = visual == null ? null : PresentationSource.FromVisual(visual);
            return source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        }

        private static Point DipScreenToPixel(Point dipPoint, Matrix transform)
            => new Point(dipPoint.X * (transform.M11 == 0 ? 1 : transform.M11),
                         dipPoint.Y * (transform.M22 == 0 ? 1 : transform.M22));

        private static Point PixelScreenToDip(Point pixelPoint, Matrix transform)
            => new Point(pixelPoint.X / (transform.M11 == 0 ? 1 : transform.M11),
                         pixelPoint.Y / (transform.M22 == 0 ? 1 : transform.M22));

        private static bool IsFinite(Point point)
            => !double.IsNaN(point.X) && !double.IsNaN(point.Y)
                && !double.IsInfinity(point.X) && !double.IsInfinity(point.Y);
    }
}
