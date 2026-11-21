using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Actions;
using System;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    public class SaveStrokesActionHandler
    {
        public SaveStrokesActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.savestrokes", (settings, guid) =>
            {
                var actionSettings = settings as SaveStrokesActionSettings;
                if (actionSettings == null)
                {
                    LogHelper.WriteLogToFile("[Automation] 保存笔画失败：行动配置无效。", LogHelper.LogType.Warning);
                    return;
                }

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                dispatcher.Invoke(() =>
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null)
                    {
                        LogHelper.WriteLogToFile("[Automation] 保存笔画失败：主窗口不可用。", LogHelper.LogType.Warning);
                        return;
                    }

                    mainWindow.SaveStrokesForAutomation(actionSettings.SavePath, actionSettings.SaveAsXml);
                });
            });
        }
    }
}
