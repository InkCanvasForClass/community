using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class SaveStrokesActionHandler
    {
        public SaveStrokesActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.savestrokes", (settings, guid) =>
            {
                var s = settings as SaveStrokesActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;
                    mw.SaveInkCanvasStrokes(newNotice: false, saveByUser: true);
                });
            });
        }
    }
}