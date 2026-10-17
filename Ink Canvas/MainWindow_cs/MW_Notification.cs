using Ink_Canvas.Helpers;
using Ink_Canvas.Models;

namespace Ink_Canvas
{
    /// <summary>
    /// 通知转发壳（M13）：实现已整体搬至 <see cref="Ink_Canvas.Services.NotificationService"/>，
    /// 本文件仅保留对外入口签名，方法体为一行转发。
    /// 通知队列与去重逻辑由 <see cref="NotificationCenterService"/>（纯逻辑静态类）承载。
    /// </summary>
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        public static void ShowNewMessage(string notice, bool isShowImmediately = true)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, 3);
        }

        public void ShowNotification(string notice, bool isShowImmediately = true)
        {
            _notificationService.ShowText(notice);
        }

        public void ShowPPTModePromptNotification()
        {
            _notificationService.ShowPptModePromptNotification();
        }

        private void InitializeNotificationProviders()
        {
            _notificationService.InitializeProviders();
        }

        internal void DetachPluginNotificationAction(string pluginId)
        {
            _notificationService.DetachPluginNotificationAction(pluginId);
        }
    }
}
