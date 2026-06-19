using System;
using InkCanvasPptAgent.Contracts;
using Newtonsoft.Json;

namespace InkCanvas.PowerPointAddIn.Core
{
    public sealed class PPTStatePublisher
    {
        private readonly Action<string> _send;

        public PPTStatePublisher(Action<string> send)
        {
            _send = send;
        }

        public void PublishState(PptState state)
        {
            var message = new PptPipeMessage<PptState>
            {
                Type = PptMessageTypes.State,
                Data = state
            };
            _send.Invoke(JsonConvert.SerializeObject(message));
        }

        public void RaiseEvent(string eventName, PptState state)
        {
            var message = new PptPipeMessage<PptState>
            {
                Type = PptMessageTypes.Event,
                Cmd = eventName,
                Data = state
            };
            _send.Invoke(JsonConvert.SerializeObject(message));
        }

        public string SendResponse(string command, object data, string requestId, bool success = true)
        {
            var message = new PptPipeMessage<object>
            {
                Type = PptMessageTypes.Response,
                Cmd = command,
                Data = data,
                RequestId = requestId,
                Success = success
            };
            return JsonConvert.SerializeObject(message);
        }

        public string SendError(string requestId, string error)
        {
            var message = new PptPipeMessage<object>
            {
                Type = PptMessageTypes.Error,
                RequestId = requestId,
                Success = false,
                Error = error
            };
            return JsonConvert.SerializeObject(message);
        }
    }
}
