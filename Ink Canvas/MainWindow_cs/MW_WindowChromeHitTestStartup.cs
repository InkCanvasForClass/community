using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        /// <summary>
        /// Keeps the Win32 transparent-window hit-test state synchronized with the visual
        /// annotation state.  This is intentionally callable after startup and after a
        /// deferred mode change because the HWND does not exist during construction.
        /// </summary>
        private void ApplyTransparentHitTestForCurrentMode(string reason)
        {
            var annotationVisible = GridTransparencyFakeBackground != null
                && GridTransparencyFakeBackground.Visibility == Visibility.Visible
                && GridTransparencyFakeBackground.Opacity > 0.01
                && GridTransparencyFakeBackground.Background != null
                && GridTransparencyFakeBackground.Background != Brushes.Transparent;

            if (annotationVisible || inkCanvas?.EditingMode == System.Windows.Controls.InkCanvasEditingMode.Ink)
                SetTransparentNotHitThrough();
            else
                SetTransparentHitThrough();
        }
    }
}
