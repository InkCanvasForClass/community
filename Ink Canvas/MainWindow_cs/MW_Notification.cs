using Ink_Canvas.Helpers;
using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private int lastNotificationShowTime = 0;
        private int notificationShowTime = 2500;

        public static void ShowNewMessage(string notice, bool isShowImmediately = true) {
            (Application.Current?.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow)
                ?.ShowNotification(notice, isShowImmediately);
        }

        public void ShowNotification(string notice, bool isShowImmediately = true) {
            try {
                lastNotificationShowTime = Environment.TickCount;

                TextBlockNotice.Text = notice;
                AnimationsHelper.ShowWithSlideFromBottomAndFade(GridNotifications);

                new Thread(new ThreadStart(() => {
                    Thread.Sleep(notificationShowTime + 300);
                    if (Environment.TickCount - lastNotificationShowTime >= notificationShowTime)
                        Application.Current.Dispatcher.Invoke(() => {
                            AnimationsHelper.HideWithSlideAndFade(GridNotifications);
                        });
                })).Start();
            }
            catch { }
        }
    }
}