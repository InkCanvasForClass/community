using H.NotifyIcon;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    internal static class WindowsNotificationHelper
    {
        private const string APP_ID = "InkCanvasForClass.CE";

        public static void ShowNewVersionToast(string version)
        {
            ShowToast(new NotificationMessage
            {
                Type = NotificationMessageType.Update,
                Level = NotificationMessageLevel.Normal,
                Title = "InkCanvasForClass CE",
                Summary = string.Format(NotificationStrings.NewVersion, version),
                DisplaySeconds = 5
            });
        }

        public static void ShowToast(NotificationMessage message)
        {
            try
            {
                if (message == null) return;
                var os = Environment.OSVersion.Version;

                if (os.Major == 6 && os.Minor == 1)
                {
                    ShowBalloonForWin7(message);
                }
                else
                {
                    ShowToastForModernWindows(message);
                }
            }
            catch
            {
            }
        }

        private static void ShowBalloonForWin7(NotificationMessage message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    var taskbar = Application.Current.Resources["TaskbarTrayIcon"] as TaskbarIcon;
                    if (taskbar == null) return;

                    taskbar.Visibility = Visibility.Visible;
                    taskbar.ShowNotification(
                        string.IsNullOrWhiteSpace(message.Title) ? "InkCanvasForClass CE" : message.Title,
                        message.Summary ?? string.Empty);
                }
                catch
                {
                }
            });
        }

        private static void ShowToastForModernWindows(NotificationMessage message)
        {
            var builder = new ToastContentBuilder()
                .AddText(string.IsNullOrWhiteSpace(message.Title) ? "InkCanvasForClass CE" : message.Title);

            if (!string.IsNullOrWhiteSpace(message.Summary)) builder.AddText(message.Summary);
            else if (!string.IsNullOrWhiteSpace(message.Content)) builder.AddText(message.Content);

            builder.Show();
        }
    }
}
