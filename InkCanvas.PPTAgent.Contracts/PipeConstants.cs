namespace InkCanvasPPTAgent.Contracts
{
    public static class PipeConstants
    {
        public const string PipeName = "ICC_PPT_PIPE";
        public const int ProtocolVersion = 1;
        public const int MaxFrameSize = 1024 * 1024;
        public const int ConnectTimeoutMilliseconds = 1000;
        public const int RequestTimeoutMilliseconds = 4000;
    }
}
