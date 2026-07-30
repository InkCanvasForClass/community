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
    /// 重置工具栏在桌面模式位置的旧式注册（兼容 Actions 目录）
    /// </summary>
    public static class ResetDesktopPositionAction
    {
        public const string ActionId = "inkcanvas.resetdesktopposition";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "重置桌面模式位置", "DockBottom")
            {
                SettingsType = typeof(ResetDesktopPositionActionSettings)
            };
            return info;
        }
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

                    // 清空桌面模式保存的坐标，让动画走默认位置分支
                    mw._userHasDraggedFloatingBar = false;
                    mw.pointDesktop = new Point(-1, -1);

                    // 仅在非折叠且非PPT模式下执行动画
                    if (!mw.isFloatingBarFolded && !mw.IsInPPTPresentationMode)
                    {
                        mw.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    }
                });
            });
        }
    }
}
