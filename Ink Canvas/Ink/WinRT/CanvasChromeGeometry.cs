using System;
using System.Windows;

namespace Ink_Canvas.Ink.WinRT
{
    internal static class CanvasChromeGeometry
    {
        internal static readonly string[] Names =
        {
            "ViewboxFloatingBar", "ViewboxBlackboardLeftSide", "ViewboxBlackboardCenterSide",
            "ViewboxBlackboardRightSide", "BlackboardLeftSide", "BlackboardCenterSide", "BlackboardRightSide",
            "BorderInkReplayToolBox", "IdleMiniBar", "EdgeExpandHint", "PPTControlsGrid", "GridPPTControlLeft",
            "GridPPTControlRight", "LeftBottomPanelForPPTNavigation", "RightBottomPanelForPPTNavigation",
            "LeftSidePanelForPPTNavigation", "RightSidePanelForPPTNavigation", "PPTQuickPanelContainer",
            "GridInkCanvasSelectionCover", "WordLectureControls", "RightBottomWordLectureControls"
        };

        // Disabled controls still occupy UI space. They must never become writable canvas,
        // even when a parent temporarily changes IsHitTestVisible during replay/selection.
        internal static bool TryGetBounds(FrameworkElement element, UIElement ancestor, out Rect bounds)
        {
            bounds = Rect.Empty;
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
            try
            {
                bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
                return !bounds.IsEmpty && double.IsFinite(bounds.X) && double.IsFinite(bounds.Y)
                    && double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height);
            }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
