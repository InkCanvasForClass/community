using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows;
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
        private bool _startupUnreadNotificationShown;

        public static void ShowNewMessage(string notice, bool isShowImmediately = true)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, 3);
        }

        public void ShowNotification(string notice, bool isShowImmediately = true)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, Math.Max(1, notificationShowTime / 1000));
        }

        public void ShowPPTModePromptNotification()
        {
            if (Settings?.PowerPointSettings?.ShowPPTModePrompt != true) return;

            NotificationCenterService.Enqueue(new NotificationMessage
            {
                Id = "ppt-mode-prompt-" + Guid.NewGuid().ToString("N"),
                Type = NotificationMessageType.Reminder,
                Level = NotificationMessageLevel.Normal,
                Title = PPTStrings.PPT_ModePrompt_Title,
                Summary = PPTStrings.PPT_ModePrompt_Message,
                Icon = "Info",
                DisplaySeconds = 4,
                Priority = 20,
                Source = "ppt-mode-prompt",
                ProviderId = "local"
            });
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

                AnnouncementService.UnreadCountChanged -= OnAnnouncementUnreadCountChanged;
                AnnouncementService.UnreadCountChanged += OnAnnouncementUnreadCountChanged;

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

        /// <summary>
        /// 拆除通知控件上挂的插件 Action 回调。插件热重载时由 <see cref="Ink_Canvas.Plugins.PluginManager"/>
        /// 调用：当前显示的通知若来自该插件，<c>currentMessage.Action</c> 直接指向插件 ALC，
        /// 留着会阻止热重载时被回收。
        /// </summary>
        internal void DetachPluginNotificationAction(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return;
            try
            {
                DynamicNotification?.DetachPluginActionIfMatches(pluginId);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"摘除插件通知 Action 失败: {ex.Message}", LogHelper.LogType.Warning);
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
                        DynamicNotification.RefreshTheme(IsCurrentThemeDark());
                        ApplyDynamicNotificationPlacement(message);
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

            if (notification.IsDictationDoNotDisturbInPPTEnabled && IsInPPTPresentationMode)
            {
                return true;
            }

            return notification.IsDictationDoNotDisturbInWhiteboardEnabled && currentMode == 1;
        }

        private void DynamicNotification_Closed(object sender, EventArgs e)
        {
            NotificationCenterService.NotifyCurrentClosed();
        }

        private void ApplyDynamicNotificationPlacement(NotificationMessage message = null)
        {
            if (DynamicNotification == null) return;

            DynamicNotification.HorizontalAlignment = HorizontalAlignment.Center;
            DynamicNotification.VerticalAlignment = VerticalAlignment.Top;
            DynamicNotification.Margin = new Thickness(0);

            if (message?.Source == "ppt-mode-prompt")
            {
                ApplyDynamicNotificationFloatingBarPlacement();
                return;
            }

            switch (Settings?.Notification?.Placement)
            {
                case "TopLeft":
                    DynamicNotification.HorizontalAlignment = HorizontalAlignment.Left;
                    DynamicNotification.Margin = new Thickness(16, 0, 0, 0);
                    break;
                case "TopRight":
                    DynamicNotification.HorizontalAlignment = HorizontalAlignment.Right;
                    DynamicNotification.Margin = new Thickness(0, 0, 16, 0);
                    break;
                case "FloatingBarAbove":
                    ApplyDynamicNotificationFloatingBarPlacement();
                    break;
            }
        }

        private void ApplyDynamicNotificationFloatingBarPlacement()
        {
            if (DynamicNotification == null || ViewboxFloatingBar == null || ViewboxFloatingBar.Visibility != Visibility.Visible)
            {
                return;
            }

            try
            {
                var position = ViewboxFloatingBar.TransformToAncestor(this).Transform(new Point(0, 0));
                double notificationWidth = DynamicNotification.ActualWidth > 0 ? DynamicNotification.ActualWidth : DynamicNotification.Width;
                double notificationHeight = DynamicNotification.ActualHeight > 0 ? DynamicNotification.ActualHeight : 72;
                double floatingBarWidth = ViewboxFloatingBar.ActualWidth;
                double left = position.X + floatingBarWidth / 2 - notificationWidth / 2;
                double top = position.Y - notificationHeight - 12;

                left = Math.Max(12, Math.Min(ActualWidth - notificationWidth - 12, left));
                top = Math.Max(12, top);

                DynamicNotification.HorizontalAlignment = HorizontalAlignment.Left;
                DynamicNotification.VerticalAlignment = VerticalAlignment.Top;
                DynamicNotification.Margin = new Thickness(left, top, 0, 0);
            }
            catch
            {
            }
        }

        private void OnAnnouncementUnreadCountChanged()
        {
            if (_startupUnreadNotificationShown) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_startupUnreadNotificationShown) return;
                ShowStartupUnreadNotification();
            }));
        }

        private void ShowStartupUnreadNotification()
        {
            try
            {
                if (_startupUnreadNotificationShown) return;
                _startupUnreadNotificationShown = true;

                var count = AnnouncementService.GetUnreadCount(Settings);
                if (count <= 0) return;

                NotificationCenterService.Enqueue(new NotificationMessage
                {
                    Id = "startup-unread-announcements",
                    Type = NotificationMessageType.Reminder,
                    Level = NotificationMessageLevel.Normal,
                    Title = AnnouncementStrings.StartupUnreadTitle,
                    Summary = string.Format(AnnouncementStrings.StartupUnreadSummary, count),
                    ActionText = AnnouncementStrings.StartupUnreadAction,
                    Icon = "Info",
                    DisplaySeconds = 8,
                    Priority = 50,
                    Source = "startup-unread",
                    ProviderId = "announcement",
                    Action = () =>
                    {
                        try
                        {
                            var window = new AnnouncementCenterWindow { Owner = this };
                            window.Show();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"打开公告中心失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动未读公告通知失败: {ex.Message}", LogHelper.LogType.Warning);
            }
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
