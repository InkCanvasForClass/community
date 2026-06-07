using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class ToggleAnnotationModeActionHandler
    {
        public ToggleAnnotationModeActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.toggleannotationmode", (settings, guid) =>
            {
                var s = settings as ToggleAnnotationModeActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;

                    if (s.EnterAnnotation)
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    else
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.None;
                });

                AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
            });

            actionService.RegisterRevertHandler("inkcanvas.toggleannotationmode", (settings, guid) =>
            {
                var s = settings as ToggleAnnotationModeActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;

                    if (s.EnterAnnotation)
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    else
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                });

                AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
            });
        }
    }
}
