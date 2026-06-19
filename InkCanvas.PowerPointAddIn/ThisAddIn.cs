using System;
using System.IO;
using InkCanvasPptAgent.Contracts;
using PptAgent.PowerPointAddIn.Core;
using PptAgent.PowerPointAddIn.IPC;
using Newtonsoft.Json;

namespace PptAgent.PowerPointAddIn
{
    public partial class ThisAddIn
    {
        private PptController _controller;
        private PipeHost _pipeHost;

        protected override object RequestComAddInAutomationObject()
        {
            return new ComAutomation Shim();
        }

        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
            try
            {
                _controller = new PptController(Application);
                var publisher = new PptStatePublisher(json => { SendToCurrentClient(json); return string.Empty; });
                _pipeHost = new PipeHost(HandleIncomingMessage);
                _pipeHost.Start();

                Application.PresentationOpen += _ => publisher.RaiseEvent(PptEvents.PresentationOpen, _controller.GetState());
                Application.PresentationClose += _ => publisher.RaiseEvent(PptEvents.PresentationClose, _controller.GetState());
                Application.SlideShowBegin += _ => publisher.RaiseEvent(PptEvents.SlideShowBegin, _controller.GetState());
                Application.SlideShowNextSlide += _ => publisher.RaiseEvent(PptEvents.SlideShowNextSlide, _controller.GetState());
                Application.SlideShowEnd += _ => publisher.RaiseEvent(PptEvents.SlideShowEnd, _controller.GetState());

                publisher.Publish(_controller.GetState());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ICC PPT Agent startup failed: {ex.Message}");
            }
        }

        private string HandleIncomingMessage(string json)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<PptPipeMessage<object>>(json);
                if (envelope == null || envelope.Type != PptMessageTypes.Command)
                    return null;

                bool handled = false;
                string command = envelope.Cmd;

                switch (command)
                {
                    case PptCommands.State:
                        handled = true;
                        break;
                    case PptCommands.Next:
                        handled = _controller.Next();
                        break;
                    case PptCommands.Previous:
                        handled = _controller.Previous();
                        break;
                    case PptCommands.GotoSlide:
                        var gotoRequest = envelope.Data != null ? ((JObject)envelope.Data).ToObject<GotoSlideRequest>() : null;
                        handled = gotoRequest != null && _controller.GotoSlide(gotoRequest.SlideNumber);
                        break;
                    case PptCommands.StartSlideShow:
                        handled = _controller.StartSlideShow();
                        break;
                    case PptCommands.EndSlideShow:
                        handled = _controller.EndSlideShow();
                        break;
                    case PptCommands.ShowSlideNavigation:
                        handled = _controller.ShowSlideNavigation();
                        break;
                    case PptCommands.DisableAutoPlayTimings:
                        handled = _controller.DisableAutoPlayTimings();
                        break;
                    case PptCommands.UnhideHiddenSlides:
                        handled = _controller.UnhideHiddenSlides();
                        break;
                }

                if (command == PptCommands.ExportSlideThumbnails)
                    return CreateErrorResponse(envelope.RequestId, "ExportSlideThumbnails is not supported in Agent mode yet.");

                var response = new PptPipeMessage<PptState>
                {
                    Type = PptMessageTypes.Response,
                    Cmd = command,
                    RequestId = envelope.RequestId,
                    Data = _controller.GetState(),
                    Success = handled
                };

                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(null, ex.Message);
            }
        }

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            _pipeHost?.Dispose();
            _pipeHost = null;
            _controller = null;
        }

        private static string CreateErrorResponse(string requestId, string error)
        {
            var response = new PptPipeMessage<object>
            {
                Type = PptMessageTypes.Error,
                RequestId = requestId,
                Success = false,
                Error = error
            };
            return JsonConvert.SerializeObject(response);
        }

        private void SendToCurrentClient(string json)
        {
            _pipeHost?.SendFrame(json);
        }

        #region VSTO generated code

        private void InternalStartup()
        {
            Startup += ThisAddIn_Startup;
            Shutdown += ThisAddIn_Shutdown;
        }

        #endregion

        private sealed class ComAutomation
        {
            public string Ping() => "OK";
        }
    }
}
