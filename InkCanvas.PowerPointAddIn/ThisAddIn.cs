using System;
using InkCanvas.PowerPointAddIn.Core;
using InkCanvas.PowerPointAddIn.IPC;
using InkCanvasPptAgent.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Office = Microsoft.Office.Core;

namespace InkCanvas.PowerPointAddIn
{
    public partial class ThisAddIn
    {
        private PPTController _controller;
        private PipeHost _pipeHost;
        private PPTStatePublisher _publisher;

        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            try
            {
                _controller = new PPTController(Application);
                _publisher = new PPTStatePublisher(json =>
                {
                    // 当前架构：PipeHost 是请求-响应模式，publisher 的主动推送
                    // 通过 pipe 的 SendFrame 直接写入。
                    // 这里预留扩展点，后续可改为独立写入通道。
                });
                _pipeHost = new PipeHost(HandleIncomingMessage);
                _pipeHost.Start();

                // 订阅 PowerPoint 事件，主动推送状态
                Application.PresentationOpen += _ => _publisher.RaiseEvent(PptEvents.PresentationOpen, _controller.GetState());
                Application.PresentationClose += _ => _publisher.RaiseEvent(PptEvents.PresentationClose, _controller.GetState());
                Application.SlideShowBegin += _ => _publisher.RaiseEvent(PptEvents.SlideShowBegin, _controller.GetState());
                Application.SlideShowNextSlide += _ => _publisher.RaiseEvent(PptEvents.SlideShowNextSlide, _controller.GetState());
                Application.SlideShowEnd += _ => _publisher.RaiseEvent(PptEvents.SlideShowEnd, _controller.GetState());

                System.Diagnostics.Debug.WriteLine("ICC PPT Agent: startup complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ICC PPT Agent startup failed: {ex}");
            }
        }

        private string HandleIncomingMessage(string json)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<PptPipeMessage<object>>(json);
                if (envelope == null || envelope.Type != PptMessageTypes.Command)
                    return null;

                string command = envelope.Cmd;

                switch (command)
                {
                    case PptCommands.State:
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId);

                    case PptCommands.Next:
                        bool nextResult = _controller.Next();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, nextResult);

                    case PptCommands.Previous:
                        bool prevResult = _controller.Previous();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, prevResult);

                    case PptCommands.GotoSlide:
                        var gotoReq = envelope.Data != null ? ((JObject)envelope.Data).ToObject<GotoSlideRequest>() : null;
                        bool gotoResult = gotoReq != null && _controller.GotoSlide(gotoReq.SlideNumber);
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, gotoResult);

                    case PptCommands.StartSlideShow:
                        bool startResult = _controller.StartSlideShow();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, startResult);

                    case PptCommands.EndSlideShow:
                        bool endResult = _controller.EndSlideShow();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, endResult);

                    case PptCommands.ShowSlideNavigation:
                        bool navResult = _controller.ShowSlideNavigation();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, navResult);

                    case PptCommands.DisableAutoPlayTimings:
                        bool disableResult = _controller.DisableAutoPlayTimings();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, disableResult);

                    case PptCommands.UnhideHiddenSlides:
                        bool unhideResult = _controller.UnhideHiddenSlides();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, unhideResult);

                    default:
                        return _publisher.SendError(envelope.RequestId, $"Unknown command: {command}");
                }
            }
            catch (Exception ex)
            {
                return _publisher.SendError(null, ex.Message);
            }
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
            _pipeHost?.Dispose();
            _pipeHost = null;
            _controller = null;
            _publisher = null;
        }

        #region VSTO 生成的代码

        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }

        #endregion
    }
}
