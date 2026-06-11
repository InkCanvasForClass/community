using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class ToggleTopmostActionHandler
    {
        public ToggleTopmostActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.toggletopmost", (settings, guid) =>
            {
                var s = settings as ToggleTopmostActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;
                    WindowTopmostManager.ApplyMainWindowTopmost(mw, s.Topmost);
                });
            });

            actionService.RegisterRevertHandler("inkcanvas.toggletopmost", (settings, guid) =>
            {
                var s = settings as ToggleTopmostActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;
                    WindowTopmostManager.ApplyMainWindowTopmost(mw, !s.Topmost);
                });
            });
        }
    }
}
