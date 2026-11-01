using Ink_Canvas.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private void ToggleExternalPresentationPreview(PPTNavBar bar)
        {
            var target = ResolvePreviewTargetBar(bar) ?? bar;
            if (target == null || _presentationSourceService?.PreviewItems == null) return;
            if (target.IsPreviewExpanded) { target.IsPreviewExpanded = false; return; }
            target.PreviewItems = _presentationSourceService.PreviewItems;
            target.CurrentSlide = _presentationSourceService.CurrentPage;
            target.IsPreviewExpanded = true;
        }

        internal void CloseExternalPresentationPreviews()
        {
            foreach (var bar in new[] { LeftBottomPanelForPPTNavigation, RightBottomPanelForPPTNavigation,
                LeftSidePanelForPPTNavigation, RightSidePanelForPPTNavigation })
            {
                if (bar == null) continue;
                bar.IsPreviewExpanded = false; bar.PreviewItems = null;
            }
        }
    }
}
