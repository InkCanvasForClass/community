using System.Collections.Generic;

namespace InkCanvasPPTAgent.Contracts
{
    public static class PPTCommands
    {
        public const string State = "state";
        public const string Next = "next";
        public const string Previous = "prev";
        public const string GotoSlide = "gotoSlide";
        public const string StartSlideShow = "startSlideShow";
        public const string EndSlideShow = "endSlideShow";
        public const string ShowSlideNavigation = "showSlideNavigation";
        public const string ExportSlideThumbnails = "exportSlideThumbnails";
        public const string DisableAutoPlayTimings = "disableAutoPlayTimings";
        public const string UnhideHiddenSlides = "unhideHiddenSlides";
    }

    public static class PPTEvents
    {
        public const string PresentationOpen = "presentationOpen";
        public const string PresentationClose = "presentationClose";
        public const string SlideShowBegin = "slideShowBegin";
        public const string SlideShowNextSlide = "slideShowNextSlide";
        public const string SlideShowEnd = "slideShowEnd";
    }

    public sealed class GotoSlideRequest
    {
        public int SlideNumber { get; set; }
    }

    public sealed class ExportSlideThumbnailsRequest
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class PPTSlideThumbnailData
    {
        public int SlideNumber { get; set; }
        public byte[] PngBytes { get; set; }
    }

    public sealed class ExportSlideThumbnailsResponse
    {
        public List<PPTSlideThumbnailData> Slides { get; set; } = new List<PPTSlideThumbnailData>();
    }
}
