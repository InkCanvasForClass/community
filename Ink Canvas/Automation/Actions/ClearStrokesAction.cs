using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 清空笔画行动的设置
    /// </summary>
    public class ClearStrokesActionSettings
    {
    }

    /// <summary>
    /// 清空画布上所有笔画的行动注册。
    /// </summary>
    public static class ClearStrokesAction
    {
        public const string ActionId = "inkcanvas.clearstrokes";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "清空笔画", "Eraser")
            {
                SettingsType = typeof(ClearStrokesActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;

                    mw.inkCanvas.Strokes.Clear();
                });
            };

            // 清空笔画不支持恢复
            info.RevertHandle = null;

            return info;
        }
    }
}
