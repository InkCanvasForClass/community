using Ink_Canvas.Helpers;
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
            Show(title, message, level);
            // 点击回调暂不支持，ShowNewMessage 是静态方法无回调
            onClicked?.Invoke();
        }
    }
}
