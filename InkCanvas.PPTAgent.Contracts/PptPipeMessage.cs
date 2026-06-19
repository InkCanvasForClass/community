namespace InkCanvasPptAgent.Contracts
{
    public static class PptMessageTypes
    {
        public const string Command = "cmd";
        public const string State = "state";
        public const string Event = "event";
        public const string Response = "response";
        public const string Error = "error";
    }

    public sealed class PptPipeMessage<T>
    {
        public int Version { get; set; } = PipeConstants.ProtocolVersion;
        public string Type { get; set; }
        public string Cmd { get; set; }
        public T Data { get; set; }
        public string RequestId { get; set; }
        public bool Success { get; set; } = true;
        public string Error { get; set; }
    }
}
