using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.ActionHandlers
{
    /// <summary>
    /// 重置工具栏在PPT模式的位置的行动设置
    /// </summary>
    public class ResetPPTPositionActionSettings
    {
    }

    /// <summary>
    /// 重置工具栏在PPT模式位置的旧式注册（兼容 Actions 目录）
    /// </summary>
    public static class ResetPPTPositionAction
    {
        public const string ActionId = "inkcanvas.resetpptposition";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "重置PPT模式位置", "Presentation")
            {
                SettingsType = typeof(ResetPPTPositionActionSettings)
            };
            return info;
        }
    }

    /// <summary>
    /// 重置工具栏在PPT模式位置的 ActionHandler。
    /// 对齐 ClassIsland 的 ActionHandler 模式，通过 DI 注入 IActionService 注册处理程序。
    /// </summary>
    public class ResetPPTPositionActionHandler
    {
        public ResetPPTPositionActionHandler(IActionService actionService)
        {
            actionService.RegisterActionHandler("inkcanvas.resetpptposition", (settings, guid) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    // 清空PPT模式保存的坐标，让动画走默认位置分支
                    mw._userHasDraggedFloatingBar = false;
                    mw.pointPPT = new Point(-1, -1);

                    // 仅在非折叠且处于PPT模式下执行动画
                    if (!mw.isFloatingBarFolded && mw.IsInPPTPresentationMode)
                    {
                        mw.PureViewboxFloatingBarMarginAnimationInPPTMode();
                    }
                });
            });
        }
    }
}
