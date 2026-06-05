using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 显示通知行动的设置
    /// </summary>
    public class ShowNotificationActionSettings
    {
        /// <summary>
        /// 通知内容
        /// </summary>
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 显示通知的行动注册。
    /// </summary>
    public static class ShowNotificationAction
    {
        public const string ActionId = "inkcanvas.shownotification";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "显示通知", "Message")
            {
                SettingsType = typeof(ShowNotificationActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                var s = settings as ShowNotificationActionSettings;
                if (s == null || string.IsNullOrEmpty(s.Message)) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    mw?.ShowNotification(s.Message);
                });
            };

            // 显示通知不支持恢复
            info.RevertHandle = null;

            return info;
        }
    }
}
