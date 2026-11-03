using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Per-pixel transparent overlay. Transparent pixels must pass input to
    /// windows in other processes, while visible controls remain interactive.
    /// </summary>
    public partial class PerformanceTransparentWin : Window
    {
        // Preserve the public surface for existing callers/plugins. The former
        // DWM backend used constant HWND alpha with HTTRANSPARENT, which searches
        // only the same thread and therefore blocked the desktop. A persisted
        // preference must never reactivate that unsafe input model.
        public bool IsUsingWindowChromeRendering => false;

        public string TransparentHitTestState => "chrome=False perPixel=True";

        public PerformanceTransparentWin()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }

        // WPF owns both the per-pixel alpha and the hit-test surface. Changing
        // WS_EX_LAYERED or its constant alpha here breaks that contract.
        public void SetTransparentHitThrough() { }
        public void SetTransparentNotHitThrough() { }

        protected virtual bool ShouldHandleWindowChromeHitTest(Point windowPoint)
            => InputHitTest(windowPoint) != null;
    }
}
