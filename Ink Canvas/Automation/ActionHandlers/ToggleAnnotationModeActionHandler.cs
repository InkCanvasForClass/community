using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;

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
                    if (mw == null) return;

                    // 统一走主程序工具切换序列，保证逻辑工具与原生湿墨迹管线同步
                    mw.SetAnnotationModeFromAutomation(s.EnterAnnotation);
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
                    if (mw == null) return;

                    mw.SetAnnotationModeFromAutomation(s.EnterAnnotation);
                });

                AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
            });
        }
    }
}
