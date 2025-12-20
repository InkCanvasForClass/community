using Hardcodet.Wpf.TaskbarNotification;
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
            try
            {
                var os = Environment.OSVersion.Version;

                if (os.Major == 6 && os.Minor == 1)
                {
                    ShowBalloonForWin7(version);
                }
                else
                {
                    ShowToastForModernWindows(version);
                }
            }
            catch
            {
            }
        }

        private static void ShowBalloonForWin7(string version)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    var taskbar = Application.Current.Resources["TaskbarTrayIcon"] as TaskbarIcon;
                    if (taskbar == null) return;

                    taskbar.Visibility = Visibility.Visible;

                    taskbar.ShowBalloonTip(
                        "InkCanvasForClass CE",
                        $"有新版本：{version}",
                        BalloonIcon.Info);
                }
                catch
                {
                }
            });
        }

        private static void ShowToastForModernWindows(string version)
        {
            new ToastContentBuilder()
                .AddText("InkCanvasForClass CE")
                .AddText($"有新版本：{version}")
                .Show();
        }
    }
}
