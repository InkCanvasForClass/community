using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using System;

namespace Ink_Canvas.Plugins
{
    internal class NotificationService : INotificationService
    {
        private readonly MainWindow _mainWindow;

        public NotificationService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Show(string title, string message, NotificationLevel level = NotificationLevel.Info)
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    MainWindow.ShowNewMessage(message);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"NotificationService.Show failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public void Show(string title, string message, NotificationLevel level, Action onClicked)
        {
            if (onClicked == null)
            {
                Show(title, message, level);
                return;
            }

            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    // 带点击回调的通知走完整的 NotificationMessage 队列：
                    // 灵动通知会显示"查看详情"操作按钮，点击后触发 onClicked
                    NotificationCenterService.Enqueue(new NotificationMessage
                    {
                        Type = MapType(level),
                        Level = MapLevel(level),
                        Title = title ?? "",
                        Summary = message ?? "",
                        Icon = level >= NotificationLevel.Warning ? "Warning" : "Info",
                        DisplaySeconds = 4,
                        Priority = 100,
                        Source = "plugin",
                        ProviderId = "plugin",
                        Action = onClicked,
                    });
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"NotificationService.Show (with callback) failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static NotificationMessageLevel MapLevel(NotificationLevel level) => level switch
        {
            NotificationLevel.Warning => NotificationMessageLevel.High,
            NotificationLevel.Error => NotificationMessageLevel.Critical,
            NotificationLevel.Success => NotificationMessageLevel.Normal,
            _ => NotificationMessageLevel.Normal,
        };

        private static NotificationMessageType MapType(NotificationLevel level) => level switch
        {
            NotificationLevel.Warning => NotificationMessageType.Important,
            NotificationLevel.Error => NotificationMessageType.Urgent,
            NotificationLevel.Success => NotificationMessageType.Reminder,
            _ => NotificationMessageType.Other,
        };
    }
}
