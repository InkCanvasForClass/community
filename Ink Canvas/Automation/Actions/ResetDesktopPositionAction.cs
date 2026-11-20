using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    /// <summary>
    /// 重置工具栏在桌面模式的位置的行动设置
    /// </summary>
    public class ResetDesktopPositionActionSettings
    {
    }


    /// <summary>
    /// 重置工具栏在桌面模式位置的 ActionHandler，通过 DI 注入 IActionService 注册处理程序。
    /// </summary>
    public class ResetDesktopPositionActionHandler
    {
        public ResetDesktopPositionActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.resetdesktopposition", (settings, guid) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    // 仅在非折叠且非PPT模式下重置；折叠态由状态机保持折叠位
                    if (!mw.isFloatingBarFolded && !mw.IsInPPTPresentationMode)
                    {
                        mw.ResetFloatingBarToDefaultPlacement(FloatingBarPlacement.DesktopExpanded);
                    }
                });
            });
        }
    }
}
