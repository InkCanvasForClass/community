using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Services
{
    /// <summary>
    /// 通知服务（M13 寄生提取）。承载从 MW_Notification.cs 搬出的通知 UI 编排逻辑：
    /// 提供者初始化、通知请求分发（Windows Toast / 灵动通知 / 旧版动画通知）、位置计算、
    /// 听写勿扰抑制、启动未读公告提示与插件回调摘除。
    /// 通知队列、去重与历史由既有 <see cref="NotificationCenterService"/>（纯逻辑静态类，
    /// 不接触任何 UI 线程对象）承载，本服务只做 UI 呈现编排。
    /// 禁止引用主窗口类型：主窗口私有状态经 <see cref="Hooks"/> 委托注入，x:Name 控件经
    /// 构造函数以强类型引用注入（GLOBAL-RULES 第六条 1），禁止按名查找视觉树。
    /// UI 线程切换经注入的 <c>UiInvoke</c> 委托完成（M30 统一替换为 ISyncService 前的过渡形态）。
    /// </summary>
    internal sealed class NotificationService
    {
        /// <summary>
        /// 通知服务的外部依赖委托集合。由主窗口在 <c>CreateNotificationService</c> 中装配。
        /// </summary>
        internal sealed class Hooks
        {
            /// <summary>读取应用设置（SettingsManager.Settings）。</summary>
            public Func<Settings> GetSettings { get; set; }
            /// <summary>判断当前主题是否为深色。</summary>
            public Func<bool> IsCurrentThemeDark { get; set; }
            /// <summary>读取是否处于 PPT 放映模式。</summary>
            public Func<bool> IsInPptPresentationMode { get; set; }
            /// <summary>读取当前模式（0=批注，1=白板）。</summary>
            public Func<int> GetCurrentMode { get; set; }
            /// <summary>读取浮动栏是否处于收纳模式。</summary>
            public Func<bool> IsFloatingBarFolded { get; set; }
            /// <summary>读取浮动栏是否正在切换隐藏模式动画。</summary>
            public Func<bool> IsFloatingBarChangingHideMode { get; set; }
            /// <summary>获取宿主窗口（以 Window 基类形态返回，服务不接触主窗口类型）。</summary>
            public Func<Window> GetHostWindow { get; set; }
            /// <summary>在 UI 线程上异步执行委托（默认优先级；过渡形态，M30 收敛为 ISyncService）。</summary>
            public Action<Action> UiInvoke { get; set; }
            /// <summary>在 UI 线程空闲优先级异步执行委托（用于公告服务延迟启动）。</summary>
            public Action<Action> UiInvokeContextIdle { get; set; }
        }

        private readonly DynamicNotificationControl _dynamicNotification;
        private readonly TextBlock _textBlockNotice;
        private readonly Grid _gridNotifications;
        private readonly Viewbox _viewboxFloatingBar;
        private readonly Hooks _hooks;

        private int _lastNotificationShowTime;
        private int _notificationShowTime = 2500;
        private bool _startupUnreadNotificationShown;
        private readonly CancellationTokenSource _providerCancellation = new CancellationTokenSource();
        private AnnouncementService _announcementService;
        private Timer _legacyHideTimer;

        public NotificationService(
            DynamicNotificationControl dynamicNotification,
            TextBlock textBlockNotice,
            Grid gridNotifications,
            Viewbox viewboxFloatingBar,
            Hooks hooks)
        {
            _dynamicNotification = dynamicNotification;
            _textBlockNotice = textBlockNotice;
            _gridNotifications = gridNotifications;
            _viewboxFloatingBar = viewboxFloatingBar;
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        }

        /// <summary>旧版通知显示时长换算（秒 → 毫秒，下限 1 秒），纯逻辑。</summary>
        private static int DisplayMillisecondsFromSeconds(int displaySeconds) => Math.Max(1, displaySeconds) * 1000;

        /// <summary>
        /// 显示一条普通文本通知（原主窗口 ShowNotification 方法的本体）。
        /// </summary>
        public void ShowText(string notice)
        {
            NotificationCenterService.EnqueueText(notice, NotificationMessageLevel.Normal, Math.Max(1, _notificationShowTime / 1000));
        }

        /// <summary>
        /// 显示 PPT 放映模式提示通知（受设置项 ShowPPTModePrompt 控制）。
        /// </summary>
        public void ShowPptModePromptNotification()
        {
            if (_hooks.GetSettings()?.PowerPointSettings?.ShowPPTModePrompt != true) return;

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

        /// <summary>
        /// 初始化通知提供者：挂接灵动通知关闭事件、订阅通知中心请求，
        /// 并在启用公告时创建公告服务（UI 线程空闲优先级延迟 3 秒启动）。
        /// </summary>
        public void InitializeProviders()
        {
            if (_dynamicNotification != null)
            {
                _dynamicNotification.Closed -= DynamicNotification_Closed;
                _dynamicNotification.Closed += DynamicNotification_Closed;
            }

            NotificationCenterService.NotificationRequested -= NotificationCenterService_NotificationRequested;
            NotificationCenterService.NotificationRequested += NotificationCenterService_NotificationRequested;

            if (_announcementService == null && _hooks.GetSettings()?.Notification?.IsAnnouncementEnabled == true)
            {
                _announcementService = new AnnouncementService(_hooks.GetSettings());

                AnnouncementService.UnreadCountChanged -= OnAnnouncementUnreadCountChanged;
                AnnouncementService.UnreadCountChanged += OnAnnouncementUnreadCountChanged;

                _hooks.UiInvokeContextIdle(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), _providerCancellation.Token);
                        await _announcementService.StartAsync(_providerCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"公告通知提供商启动失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                });
            }

            LogHelper.WriteLogToFile(
                $"[Notification] 通知提供商已初始化: 公告已启用={_hooks.GetSettings()?.Notification?.IsAnnouncementEnabled == true}, 公告服务实例={_announcementService != null}",
                LogHelper.LogType.Info);
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
                _dynamicNotification?.DetachPluginActionIfMatches(pluginId);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"摘除插件通知 Action 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void NotificationCenterService_NotificationRequested(NotificationMessage message)
        {
            _hooks.UiInvoke(() =>
            {
                try
                {
                    if (IsNotificationSuppressedByDictationDoNotDisturb())
                    {
                        NotificationCenterService.NotifyCurrentClosed();
                        return;
                    }

                    if (_hooks.GetSettings()?.Notification?.IsWindowsToastEnabled == true)
                    {
                        WindowsNotificationHelper.ShowToast(message);
                    }

                    if (_hooks.GetSettings()?.Notification?.IsDynamicNotificationEnabled == true && _dynamicNotification != null)
                    {
                        _dynamicNotification.RefreshTheme(_hooks.IsCurrentThemeDark());
                        ApplyDynamicNotificationPlacement(message);
                        _dynamicNotification.Show(message);
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
            });
        }

        private bool IsNotificationSuppressedByDictationDoNotDisturb()
        {
            var notification = _hooks.GetSettings()?.Notification;
            if (notification?.IsDictationDoNotDisturbEnabled != true) return false;

            if (notification.IsDictationDoNotDisturbInPPTEnabled && _hooks.IsInPptPresentationMode())
            {
                return true;
            }

            return notification.IsDictationDoNotDisturbInWhiteboardEnabled && _hooks.GetCurrentMode() == 1;
        }

        private void DynamicNotification_Closed(object sender, EventArgs e)
        {
            NotificationCenterService.NotifyCurrentClosed();
        }

        private void ApplyDynamicNotificationPlacement(NotificationMessage message = null)
        {
            if (_dynamicNotification == null) return;

            _dynamicNotification.HorizontalAlignment = HorizontalAlignment.Center;
            _dynamicNotification.VerticalAlignment = VerticalAlignment.Top;
            _dynamicNotification.Margin = new Thickness(0);

            if (message?.Source == "ppt-mode-prompt")
            {
                ApplyDynamicNotificationFloatingBarPlacement();
                return;
            }

            switch (_hooks.GetSettings()?.Notification?.Placement)
            {
                case "TopLeft":
                    _dynamicNotification.HorizontalAlignment = HorizontalAlignment.Left;
                    _dynamicNotification.Margin = new Thickness(16, 0, 0, 0);
                    break;
                case "TopRight":
                    _dynamicNotification.HorizontalAlignment = HorizontalAlignment.Right;
                    _dynamicNotification.Margin = new Thickness(0, 0, 16, 0);
                    break;
                case "FloatingBarAbove":
                    ApplyDynamicNotificationFloatingBarPlacement();
                    break;
            }
        }

        private void ApplyDynamicNotificationFloatingBarPlacement()
        {
            // 收纳或收纳动画期间浮动栏已移出可用区域，保留默认的顶部居中位置，避免通知跟随到屏幕外。
            if (_dynamicNotification == null || _viewboxFloatingBar == null ||
                _viewboxFloatingBar.Visibility != Visibility.Visible ||
                _hooks.IsFloatingBarFolded() || _hooks.IsFloatingBarChangingHideMode())
            {
                return;
            }

            try
            {
                var hostWindow = _hooks.GetHostWindow();
                var position = _viewboxFloatingBar.TransformToAncestor(hostWindow).Transform(new Point(0, 0));
                double notificationWidth = _dynamicNotification.ActualWidth > 0 ? _dynamicNotification.ActualWidth : _dynamicNotification.Width;
                double notificationHeight = _dynamicNotification.ActualHeight > 0 ? _dynamicNotification.ActualHeight : 72;
                double floatingBarWidth = _viewboxFloatingBar.ActualWidth;
                double left = position.X + floatingBarWidth / 2 - notificationWidth / 2;
                double top = position.Y - notificationHeight - 12;

                left = Math.Max(12, Math.Min(hostWindow.ActualWidth - notificationWidth - 12, left));
                top = Math.Max(12, top);

                _dynamicNotification.HorizontalAlignment = HorizontalAlignment.Left;
                _dynamicNotification.VerticalAlignment = VerticalAlignment.Top;
                _dynamicNotification.Margin = new Thickness(left, top, 0, 0);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Notification] 计算通知悬浮栏位置失败: {ex.Message}", LogHelper.LogType.Info);
            }
        }

        private void OnAnnouncementUnreadCountChanged()
        {
            if (_startupUnreadNotificationShown) return;

            _hooks.UiInvoke(() =>
            {
                if (_startupUnreadNotificationShown) return;
                ShowStartupUnreadNotification();
            });
        }

        private void ShowStartupUnreadNotification()
        {
            try
            {
                if (_startupUnreadNotificationShown) return;
                _startupUnreadNotificationShown = true;

                var count = AnnouncementService.GetUnreadCount(_hooks.GetSettings());
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
                            var window = new AnnouncementCenterWindow { Owner = _hooks.GetHostWindow() };
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
                if (_textBlockNotice == null || _gridNotifications == null)
                {
                    NotificationCenterService.NotifyCurrentClosed();
                    return;
                }

                _lastNotificationShowTime = Environment.TickCount;
                _notificationShowTime = DisplayMillisecondsFromSeconds(displaySeconds);
                _textBlockNotice.Text = notice;
                AnimationsHelper.ShowWithSlideFromBottomAndFade(_gridNotifications);

                // 原实现为独立线程 Sleep 后经 UI 线程调度器隐藏；等价改写为单次计时器 +
                // UiInvoke 回 UI 线程（新类不得创建 Thread 访问 UI 线程对象，同 M12 过渡形态）。
                StopLegacyHideTimer();
                _legacyHideTimer = new Timer(_ =>
                {
                    if (Environment.TickCount - _lastNotificationShowTime >= _notificationShowTime)
                    {
                        _hooks.UiInvoke(() =>
                        {
                            AnimationsHelper.HideWithSlideAndFade(_gridNotifications);
                            NotificationCenterService.NotifyCurrentClosed();
                        });
                    }
                }, null, _notificationShowTime + 300, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ShowNotification 异常: {ex.Message}", LogHelper.LogType.Error);
                NotificationCenterService.NotifyCurrentClosed();
            }
        }

        private void StopLegacyHideTimer()
        {
            _legacyHideTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _legacyHideTimer?.Dispose();
            _legacyHideTimer = null;
        }
    }
}
