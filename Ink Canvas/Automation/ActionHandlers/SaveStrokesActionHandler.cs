using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class SaveStrokesActionHandler
    {
        public SaveStrokesActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.savestrokes", (settings, guid) =>
            {
                // TODO: 调用 MainWindow 的保存逻辑
            });
        }
    }
}
