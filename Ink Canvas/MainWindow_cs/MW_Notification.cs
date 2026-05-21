using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        private int lastNotificationShowTime;
        private int notificationShowTime = 2500;

        public static void ShowNewMessage(string notice, bool isShowImmediately = true)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, 3);
        }

        public void ShowNotification(string notice, bool isShowImmediately = true)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, Math.Max(1, notificationShowTime / 1000));
        }

        private void InitializeNotificationProviders()
        {
            if (DynamicNotification != null)
            {
                DynamicNotification.Closed -= DynamicNotification_Closed;
                DynamicNotification.Closed += DynamicNotification_Closed;
            }

            NotificationCenterService.NotificationRequested -= NotificationCenterService_NotificationRequested;
            NotificationCenterService.NotificationRequested += NotificationCenterService_NotificationRequested;

            if (_announcementService == null && Settings?.Notification?.IsAnnouncementEnabled == true)
            {
                _announcementService = new AnnouncementService(Settings);
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), _notificationProviderCancellation.Token);
                        await _announcementService.StartAsync(_notificationProviderCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"公告通知提供商启动失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }), DispatcherPriority.ContextIdle);
            }
        }

        private void NotificationCenterService_NotificationRequested(NotificationMessage message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (IsNotificationSuppressedByDictationDoNotDisturb())
                    {
                        NotificationCenterService.NotifyCurrentClosed();
                        return;
                    }

                    if (Settings?.Notification?.IsWindowsToastEnabled == true)
                    {
                        WindowsNotificationHelper.ShowToast(message);
                    }

                    if (Settings?.Notification?.IsDynamicNotificationEnabled == true && DynamicNotification != null)
                    {
                        DynamicNotification.Show(message);
                    }
                    else
                    {
                        ShowLegacyNotification(message.Title, message.DisplaySeconds);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"灵动通知显示失败: {ex.Message}", LogHelper.LogType.Error);
                    NotificationCenterService.NotifyCurrentClosed();
                }
            }));
        }

        private bool IsNotificationSuppressedByDictationDoNotDisturb()
        {
            var notification = Settings?.Notification;
            if (notification?.IsDictationDoNotDisturbEnabled != true) return false;

            if (notification.IsDictationDoNotDisturbInPptEnabled && IsInPptPresentationMode)
            {
                return true;
            }

            return notification.IsDictationDoNotDisturbInWhiteboardEnabled && currentMode == 1;
        }

        private void DynamicNotification_Closed(object sender, EventArgs e)
        {
            NotificationCenterService.NotifyCurrentClosed();
        }

        private void ShowLegacyNotification(string notice, int displaySeconds)
        {
            try
            {
                if (TextBlockNotice == null || GridNotifications == null)
                {
                    NotificationCenterService.NotifyCurrentClosed();
                    return;
                }

                lastNotificationShowTime = Environment.TickCount;
                notificationShowTime = Math.Max(1, displaySeconds) * 1000;
                TextBlockNotice.Text = notice;
                AnimationsHelper.ShowWithSlideFromBottomAndFade(GridNotifications);

                new Thread(() =>
                {
                    Thread.Sleep(notificationShowTime + 300);
                    if (Environment.TickCount - lastNotificationShowTime >= notificationShowTime)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AnimationsHelper.HideWithSlideAndFade(GridNotifications);
                            NotificationCenterService.NotifyCurrentClosed();
                        });
                    }
                }).Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ShowNotification 异常: {ex.Message}", LogHelper.LogType.Error);
                NotificationCenterService.NotifyCurrentClosed();
            }
        }
    }
}
