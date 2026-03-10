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

        /// <summary>
        /// 静态方法，用于在主窗口中显示通知
        /// </summary>
        /// <param name="notice">要显示的通知文本</param>
        /// <param name="isShowImmediately">指示是否应立即显示通知</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 获取应用程序中的主窗口实例
        /// 2. 调用主窗口的ShowNotification方法显示通知
        /// </remarks>
        public static void ShowNewMessage(string notice, bool isShowImmediately = true)
        {
            (Application.Current?.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow)
                ?.ShowNotification(notice, isShowImmediately);
        }

        /// <summary>
        /// 在窗口中显示带从底部滑入并淡入的通知文本，并在配置的时长后自动隐藏（若未被新通知覆盖）。
        /// </summary>
        /// <param name="notice">要显示的通知文本。</param>
        /// <param name="isShowImmediately">指示是否应立即显示通知；当前实现默认立即显示。</param>
        public void ShowNotification(string notice, bool isShowImmediately = true)
        {
            try
            {
                if (TextBlockNotice == null || GridNotifications == null)
                {
                    return;
                }
                lastNotificationShowTime = Environment.TickCount;

                TextBlockNotice.Text = notice;
                AnimationsHelper.ShowWithSlideFromBottomAndFade(GridNotifications);

                new Thread(() =>
                {
                    Thread.Sleep(notificationShowTime + 300);
                    if (Environment.TickCount - lastNotificationShowTime >= notificationShowTime)
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AnimationsHelper.HideWithSlideAndFade(GridNotifications);
                        });
                }).Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ShowNotification 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}