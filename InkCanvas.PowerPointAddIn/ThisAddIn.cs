using System;
using InkCanvas.PowerPointAddIn.Core;
using InkCanvas.PowerPointAddIn.IPC;
using InkCanvasPPTAgent.Contracts;
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
                _pipeHost = new PipeHost(HandleIncomingMessage);
                _publisher = new PPTStatePublisher(json => _pipeHost.SendFrame(json));
                _pipeHost.Start();

                // 订阅 PowerPoint 事件，主动推送状态
                Application.PresentationOpen += _ => _publisher.RaiseEvent(PPTEvents.PresentationOpen, _controller.GetState());
                Application.PresentationClose += _ => _publisher.RaiseEvent(PPTEvents.PresentationClose, _controller.GetState());
                Application.SlideShowBegin += _ => _publisher.RaiseEvent(PPTEvents.SlideShowBegin, _controller.GetState());
                Application.SlideShowNextSlide += _ => _publisher.RaiseEvent(PPTEvents.SlideShowNextSlide, _controller.GetState());
                Application.SlideShowEnd += _ => _publisher.RaiseEvent(PPTEvents.SlideShowEnd, _controller.GetState());

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
                var envelope = JsonConvert.DeserializeObject<PPTPipeMessage<object>>(json);
                if (envelope == null || envelope.Type != PPTMessageTypes.Command)
                    return null;

                string command = envelope.Cmd;

                switch (command)
                {
                    case PPTCommands.State:
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId);

                    case PPTCommands.Next:
                        bool nextResult = _controller.Next();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, nextResult);

                    case PPTCommands.Previous:
                        bool prevResult = _controller.Previous();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, prevResult);

                    case PPTCommands.GotoSlide:
                        var gotoReq = envelope.Data != null ? ((JObject)envelope.Data).ToObject<GotoSlideRequest>() : null;
                        bool gotoResult = gotoReq != null && _controller.GotoSlide(gotoReq.SlideNumber);
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, gotoResult);

                    case PPTCommands.StartSlideShow:
                        bool startResult = _controller.StartSlideShow();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, startResult);

                    case PPTCommands.EndSlideShow:
                        bool endResult = _controller.EndSlideShow();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, endResult);

                    case PPTCommands.ShowSlideNavigation:
                        bool navResult = _controller.ShowSlideNavigation();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, navResult);

                    case PPTCommands.DisableAutoPlayTimings:
                        bool disableResult = _controller.DisableAutoPlayTimings();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, disableResult);

                    case PPTCommands.UnhideHiddenSlides:
                        bool unhideResult = _controller.UnhideHiddenSlides();
                        return _publisher.SendResponse(command, _controller.GetState(), envelope.RequestId, unhideResult);

                    case PPTCommands.ExportSlideThumbnails:
                        // Agent 模式下暂不支持缩略图导出，返回空列表
                        return _publisher.SendResponse(command, new ExportSlideThumbnailsResponse(), envelope.RequestId);

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
