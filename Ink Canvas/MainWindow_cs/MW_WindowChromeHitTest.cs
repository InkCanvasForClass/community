using System;
using System.Windows;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        protected override bool ShouldHandleWindowChromeHitTest(Point windowPoint)
        {
            return ContainsPoint(ViewboxFloatingBar, windowPoint)
                   || ContainsPoint(LeftSidePanel, windowPoint)
                   || ContainsPoint(RightSidePanel, windowPoint)
                   || ContainsPoint(LeftUnFoldButtonQuickPanel, windowPoint)
                   || ContainsPoint(RightUnFoldButtonQuickPanel, windowPoint)
                   || ContainsPoint(LeftBottomPanelForPPTNavigation, windowPoint)
                   || ContainsPoint(RightBottomPanelForPPTNavigation, windowPoint)
                   || ContainsPoint(LeftSidePanelForPPTNavigation, windowPoint)
                   || ContainsPoint(RightSidePanelForPPTNavigation, windowPoint)
                   || ContainsPoint(ViewboxBlackboardLeftSide, windowPoint)
                   || ContainsPoint(BlackboardCenterSide, windowPoint)
                   || ContainsPoint(ViewboxBlackboardRightSide, windowPoint)
                   || ContainsPoint(BorderStrokeSelectionControl, windowPoint)
                   || ContainsPoint(BorderImageSelectionControl, windowPoint)
                   || ContainsPoint(BorderPdfPageSidebar, windowPoint)
                   || ContainsPoint(ImageSelectionOverlay, windowPoint)
                   || ContainsPoint(QuickDrawFloatingButton, windowPoint)
                   || ContainsPoint(BorderInkReplayToolBox, windowPoint)
                   || ContainsPoint(TimerContainer, windowPoint)
                   || ContainsPoint(MinimizedTimerContainer, windowPoint)
                   || ContainsPoint(PPTTimeCapsuleContainer, windowPoint)
                   || ContainsPoint(PPTQuickPanelContainer, windowPoint)
                   || ContainsPoint(VideoPresenterSidebar, windowPoint);
        }

        private bool ContainsPoint(FrameworkElement element, Point windowPoint)
        {
            if (element == null || !element.IsVisible || !element.IsHitTestVisible)
                return false;

            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
                return false;

            try
            {
                var topLeft = element.TransformToAncestor(this).Transform(new Point(0, 0));
                var bounds = new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
                return bounds.Contains(windowPoint);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
