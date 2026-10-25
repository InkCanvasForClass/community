using System.Windows;

namespace Ink_Canvas.Helpers
{
    internal static class InputCaptureHelper
    {
        internal static void ReleaseOwnedCaptures(UIElement element)
        {
            if (element.IsMouseCaptured) element.ReleaseMouseCapture();
            // WPF ReleaseStylusCapture() can clear the thread's mouse capture even
            // when this element has no stylus capture. Never cancel another control's
            // button press, popup, or drag while cleaning up an inactive overlay.
            if (element.IsStylusCaptured) element.ReleaseStylusCapture();
            element.ReleaseAllTouchCaptures();
        }
    }
}
