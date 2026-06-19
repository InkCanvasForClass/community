namespace InkCanvasPPTAgent.Contracts
{
    public sealed class PPTState
    {
        public int SlideIndex { get; set; }
        public int TotalSlides { get; set; }
        public bool IsRunning { get; set; }
        public string PresentationName { get; set; }
        public string PresentationFullName { get; set; }
        public bool HasHiddenSlides { get; set; }
        public bool HasAutoPlayTimings { get; set; }
    }
}
