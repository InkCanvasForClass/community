using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = WpfUiCompat.Controls.MessageBox;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 统一弹窗辅助类。
    /// 自动为弹窗定位最适合的 Owner 窗口（优先关联当前 Page/Control 所在的设置窗口或活动窗口），
    /// 防止在设置窗口置顶或最大化时，无 Owner 的弹窗被遮挡在设置窗口后方导致界面死锁。
    /// </summary>
    public static class MessageBoxHelper
    {
        /// <summary>
        /// 解析最适合作为弹窗 Owner 的 Window 实例。
        /// </summary>
        public static Window GetDefaultOwner(DependencyObject context = null)
        {
            if (context != null)
            {
                try
                {
                    var window = Window.GetWindow(context);
                    if (window != null && window.IsLoaded && window.IsVisible)
                    {
                        return window;
                    }
                }
                catch
                {
                    // VisualTree 遍历异常时降级使用全局查找
                }
            }

            var app = Application.Current;
            if (app == null) return null;

            try
            {
                // 1. 优先获取当前处于活动状态且可见的窗口
                var activeWindow = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible);
                if (activeWindow != null) return activeWindow;

                // 2. 其次获取当前可见的 SettingsWindow（设置窗口）
                var settingsWindow = app.Windows.OfType<Window>().FirstOrDefault(w => w.GetType().Name == "SettingsWindow" && w.IsVisible);
                if (settingsWindow != null) return settingsWindow;

                // 3. 再次获取主窗口（若可见）
                if (app.MainWindow != null && app.MainWindow.IsVisible) return app.MainWindow;

                // 4. 最后获取列表中最后一个可见窗口
                return app.Windows.OfType<Window>().LastOrDefault(w => w.IsVisible);
            }
            catch
            {
                return app.MainWindow;
            }
        }

        #region 同步 Show

        public static MessageBoxResult Show(
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return Show(null as DependencyObject, messageBoxText, caption, button, icon, defaultResult);
        }

        public static MessageBoxResult Show(
            DependencyObject context,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var app = Application.Current;
            var dispatcher = app?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => Show(context, messageBoxText, caption, button, icon, defaultResult));
            }

            var owner = GetDefaultOwner(context);
            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                return MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult);
            }

            return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
        }

        public static MessageBoxResult Show(
            Window owner,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return Show(owner as DependencyObject, messageBoxText, caption, button, icon, defaultResult);
        }

        #endregion

        #region 异步 ShowAsync

        public static Task<MessageBoxResult> ShowAsync(
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return ShowAsync(null as DependencyObject, messageBoxText, caption, button, icon, defaultResult);
        }

        public static async Task<MessageBoxResult> ShowAsync(
            DependencyObject context,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var app = Application.Current;
            var dispatcher = app?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                var task = await dispatcher.InvokeAsync(() => ShowAsync(context, messageBoxText, caption, button, icon, defaultResult));
                return await task;
            }

            var owner = GetDefaultOwner(context);
            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                return await MessageBox.ShowAsync(owner, messageBoxText, caption, button, icon, defaultResult);
            }

            return await MessageBox.ShowAsync(messageBoxText, caption, button, icon, defaultResult);
        }

        public static Task<MessageBoxResult> ShowAsync(
            Window owner,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return ShowAsync(owner as DependencyObject, messageBoxText, caption, button, icon, defaultResult);
        }

        #endregion
    }
}
