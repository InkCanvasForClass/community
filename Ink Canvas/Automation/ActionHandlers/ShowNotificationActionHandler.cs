using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class ShowNotificationActionHandler
    {
        public ShowNotificationActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.shownotification", (settings, guid) =>
            {
                var s = settings as ShowNotificationActionSettings;
                if (s == null || string.IsNullOrEmpty(s.Message)) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    mw?.ShowNotification(s.Message);
                });
            });
        }
    }
}
