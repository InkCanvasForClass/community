using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class KillProcessActionHandler
    {
        public KillProcessActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.killprocess", (settings, guid) =>
            {
                var s = settings as KillProcessActionSettings;
                if (s == null || string.IsNullOrEmpty(s.ProcessName)) return;

                try
                {
                    foreach (var process in Process.GetProcessesByName(s.ProcessName))
                    {
                        try { process.Kill(); } catch (System.Exception ex)
                        {
                            Helpers.LogService.LogException(ex);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Helpers.LogService.LogException(ex);
                }
            });
        }
    }
}
