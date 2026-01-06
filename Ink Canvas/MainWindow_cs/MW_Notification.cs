using Ink_Canvas.Helpers;
using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private int lastNotificationShowTime;
        private int notificationShowTime = 2500;

        public static void ShowNewMessage(string notice, bool isShowImmediately = true)
        {
            (Application.Current?.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow)
                ?.ShowNotification(notice, isShowImmediately);
        }

        public void ShowNotification(string notice, bool isShowImmediately = true)
        {
            try
            {
                lastNotificationShowTime = Environment.TickCount;

                TextBlockNotice.Text = notice;
                AnimationsHelper.ShowWithSlideFromBottomAndFade(GridNotifications);

                Task.Run(async () =>
                {
                    await Task.Delay(notificationShowTime + 300).ConfigureAwait(false);
                    if (Environment.TickCount - lastNotificationShowTime >= notificationShowTime)
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AnimationsHelper.HideWithSlideAndFade(GridNotifications);
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                });
            }
            catch { }
        }
    }
}