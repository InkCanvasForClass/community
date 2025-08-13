using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Point = System.Windows.Point;

namespace Ink_Canvas.Helpers
{
    internal class IsOutsideOfScreenHelper
    {
        public static bool IsOutsideOfScreen(FrameworkElement target)
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(target);
            if (hwndSource is null)
            {
                return true;
            }

            var hWnd = hwndSource.Handle;
            var targetBounds = GetPixelBoundsToScreen(target);

            var screens = Screen.AllScreens;
            return !screens.Any(x => x.Bounds.IntersectsWith(targetBounds));

            Rectangle GetPixelBoundsToScreen(FrameworkElement visual)
            {
                var pixelBoundsToScreen = Rect.Empty;
                pixelBoundsToScreen.Union(visual.PointToScreen(new Point(0, 0)));
                pixelBoundsToScreen.Union(visual.PointToScreen(new Point(visual.ActualWidth, 0)));
                pixelBoundsToScreen.Union(visual.PointToScreen(new Point(0, visual.ActualHeight)));
                pixelBoundsToScreen.Union(visual.PointToScreen(new Point(visual.ActualWidth, visual.ActualHeight)));
                return new Rectangle(
                    (int)pixelBoundsToScreen.X, (int)pixelBoundsToScreen.Y,
                    (int)pixelBoundsToScreen.Width, (int)pixelBoundsToScreen.Height);
            }
        }
    }
}