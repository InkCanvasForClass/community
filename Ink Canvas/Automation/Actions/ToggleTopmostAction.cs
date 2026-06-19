using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 切换窗口置顶行动的设置
    /// </summary>
    public class ToggleTopmostActionSettings
    {
        /// <summary>
        /// true = 置顶，false = 取消置顶
        /// </summary>
        public bool Topmost { get; set; } = true;
    }

    /// <summary>
    /// 切换窗口置顶的行动注册。
    /// </summary>
    public static class ToggleTopmostAction
    {
        public const string ActionId = "inkcanvas.toggletopmost";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "切换窗口置顶", "Pin")
            {
                SettingsType = typeof(ToggleTopmostActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                var s = settings as ToggleTopmostActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;
                    WindowTopmostManager.ApplyMainWindowTopmost(mw, s.Topmost);
                });
            };

            info.RevertHandle = (settings, guid) =>
            {
                var s = settings as ToggleTopmostActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;
                    WindowTopmostManager.ApplyMainWindowTopmost(mw, !s.Topmost);
                });
            };

            return info;
        }
    }
}
