using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class FoldActionHandler
    {
        public FoldActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.fold", (settings, guid) =>
            {
                var s = settings as FoldActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    if (s.Fold && !mw.isFloatingBarFolded)
                    {
                        _ = mw.FoldFloatingBar(new object(), true);
                    }
                    else if (!s.Fold && mw.isFloatingBarFolded)
                    {
                        _ = mw.UnFoldFloatingBar(null);
                    }
                });

                AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
            });

            actionService.RegisterRevertHandler("inkcanvas.fold", (settings, guid) =>
            {
                var s = settings as FoldActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    if (s.Fold && mw.isFloatingBarFolded)
                    {
                        _ = mw.UnFoldFloatingBar(null);
                    }
                    else if (!s.Fold && !mw.isFloatingBarFolded)
                    {
                        _ = mw.FoldFloatingBar(new object(), true);
                    }
                });

                AutomationBootstrap.Monitor?.NotifyInternalStateChanged();
            });
        }
    }
}
