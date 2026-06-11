using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.WorkflowAutomation.Models;
using System;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 显示通知行动的设置
    /// </summary>
    public class ShowNotificationActionSettings
    {
        /// <summary>
        /// 通知类型
        /// </summary>
        public NotificationMessageType Type { get; set; } = NotificationMessageType.Other;

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
                    ShowAutomationNotification(s);
                });
            };

            // 显示通知不支持恢复
            info.RevertHandle = null;

            return info;
        }

        public static void ShowAutomationNotification(ShowNotificationActionSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.Message)) return;

            NotificationCenterService.Enqueue(new NotificationMessage
            {
                Id = "automation-" + Guid.NewGuid().ToString("N"),
                Type = settings.Type,
                Level = GetLevel(settings.Type),
                Title = GetTitle(settings.Type),
                Summary = settings.Message,
                Icon = GetIcon(settings.Type),
                DisplaySeconds = 3,
                Priority = GetPriority(settings.Type),
                Source = "automation",
                ProviderId = "local"
            });
        }

        private static NotificationMessageLevel GetLevel(NotificationMessageType type)
        {
            return type switch
            {
                NotificationMessageType.Urgent => NotificationMessageLevel.Critical,
                NotificationMessageType.Important => NotificationMessageLevel.High,
                _ => NotificationMessageLevel.Normal
            };
        }

        private static int GetPriority(NotificationMessageType type)
        {
            return type switch
            {
                NotificationMessageType.Urgent => 300,
                NotificationMessageType.Important => 200,
                NotificationMessageType.Update => 100,
                NotificationMessageType.Reminder => 80,
                _ => 0
            };
        }

        private static string GetIcon(NotificationMessageType type)
        {
            return type switch
            {
                NotificationMessageType.Urgent => "Warning",
                NotificationMessageType.Important => "Important",
                NotificationMessageType.Update => "Update",
                NotificationMessageType.Reminder => "Info",
                _ => "Info"
            };
        }

        private static string GetTitle(NotificationMessageType type)
        {
            return type switch
            {
                NotificationMessageType.Update => NotificationStrings.Type_Update,
                NotificationMessageType.Urgent => NotificationStrings.Type_Urgent,
                NotificationMessageType.Important => NotificationStrings.Type_Important,
                NotificationMessageType.Reminder => NotificationStrings.Type_Reminder,
                _ => NotificationStrings.Type_Other
            };
        }
    }
}
