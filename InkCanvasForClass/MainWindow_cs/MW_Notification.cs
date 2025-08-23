using Ink_Canvas.Helpers;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Ink_Canvas
{
    public partial class MainWindow : PerformanceTransparentWin
    {
        int lastNotificationShowTime = 0;
        int notificationShowTime = 2500;

        public static void ShowNewMessage(string notice) {
            (Application.Current?.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow)?.ShowNotification(notice);
        }

        public MW_Toast ShowNotification(string notice) {
            var notification = new MW_Toast(MW_Toast.ToastType.Informative, notice, (self) => {
                GridNotifications.Children.Remove(self);
            });
            GridNotifications.Children.Add(notification);
            notification.ShowAnimatedWithAutoDispose(3000 + notice.Length * 10);
            return notification;
        }

        public MW_Toast ShowNewToast(string notice, MW_Toast.ToastType type, int autoCloseMs) {
            var notification = new MW_Toast(type, notice, (self) => {
                GridNotifications.Children.Remove(self);
            });
            GridNotifications.Children.Add(notification);
            notification.ShowAnimatedWithAutoDispose(autoCloseMs + notice.Length * 10);
            return notification;
        }
    }
}