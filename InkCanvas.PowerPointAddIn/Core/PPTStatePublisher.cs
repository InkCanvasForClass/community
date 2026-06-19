using System;
using InkCanvasPPTAgent.Contracts;
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

        public void PublishState(PPTState state)
        {
            var message = new PPTPipeMessage<PPTState>
            {
                Type = PPTMessageTypes.State,
                Data = state
            };
            _send.Invoke(JsonConvert.SerializeObject(message));
        }

        public void RaiseEvent(string eventName, PPTState state)
        {
            var message = new PPTPipeMessage<PPTState>
            {
                Type = PPTMessageTypes.Event,
                Cmd = eventName,
                Data = state
            };
            _send.Invoke(JsonConvert.SerializeObject(message));
        }

        public string SendResponse(string command, object data, string requestId, bool success = true)
        {
            var message = new PPTPipeMessage<object>
            {
                Type = PPTMessageTypes.Response,
                Cmd = command,
                Data = data,
                RequestId = requestId,
                Success = success
            };
            return JsonConvert.SerializeObject(message);
        }

        public string SendError(string requestId, string error)
        {
            var message = new PPTPipeMessage<object>
            {
                Type = PPTMessageTypes.Error,
                RequestId = requestId,
                Success = false,
                Error = error
            };
            return JsonConvert.SerializeObject(message);
        }
    }
}
