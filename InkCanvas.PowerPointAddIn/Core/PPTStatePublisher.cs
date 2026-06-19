using System;
using InkCanvasPptAgent.Contracts;

namespace PptAgent.PowerPointAddIn.Core
{
    public sealed class PptStatePublisher
    {
        private readonly Func<string, string> _send;

        public PptStatePublisher(Func<string, string> send)
        {
            _send = send;
        }

        public void Publish(PptState state)
        {
            var message = new PptPipeMessage<PptState>
            {
                Type = PptMessageTypes.State,
                Data = state
            };

            _send.Invoke(Newtonsoft.Json.JsonConvert.SerializeObject(message));
        }

        public void RaiseEvent(string eventName, PptState state)
        {
            var message = new PptPipeMessage<PptState>
            {
                Type = PptMessageTypes.Event,
                Cmd = eventName,
                Data = state
            };

            _send.Invoke(Newtonsoft.Json.JsonConvert.SerializeObject(message));
        }
    }
}
