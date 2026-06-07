using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class ClearStrokesActionHandler
    {
        public ClearStrokesActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.clearstrokes", (settings, guid) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;
                    mw.inkCanvas.Strokes.Clear();
                });
            });
        }
    }
}
