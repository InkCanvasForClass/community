using iNKORE.UI.WPF.Modern.Common;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

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

        #region 支持自定义显示位置（ShowAt / ShowAtAsync / ShowAtNonBlocking）

        /// <summary>
        /// 将可视元素上的坐标点换算为屏幕坐标（DIP，即 Window.Left/Top 使用的单位），
        /// 供 ShowAt / ShowAtAsync / ShowAtNonBlocking 定位使用。
        /// 假定目标点与该可视元素位于同一显示器（DPI 按该元素当前所在显示器换算）。
        /// 失败时返回 NaN 点。
        /// </summary>
        public static Point TranslateToScreen(Visual visual, Point point)
        {
            try
            {
                var source = PresentationSource.FromVisual(visual);
                if (source?.CompositionTarget == null) return new Point(double.NaN, double.NaN);
                var fromDevice = source.CompositionTarget.TransformToDevice;
                var screenPx = visual.PointToScreen(point);
                return new Point(screenPx.X / fromDevice.M11, screenPx.Y / fromDevice.M22);
            }
            catch
            {
                return new Point(double.NaN, double.NaN);
            }
        }

        /// <summary>
        /// 以指定屏幕位置（DIP）显示同步模态弹窗，返回点击结果。
        /// 位置无效（NaN/∞）时回退为居中显示。
        /// </summary>
        public static MessageBoxResult ShowAt(
            DependencyObject context,
            double screenX, double screenY,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            Action<MessageBox> configure = null)
        {
            var app = Application.Current;
            var dispatcher = app?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => ShowAt(context, screenX, screenY, messageBoxText, caption, button, icon, configure));
            }

            var owner = GetDefaultOwner(context);
            var box = CreatePositionedMessageBox(owner, screenX, screenY, messageBoxText, caption, button, icon);
            configure?.Invoke(box);
            return box.ShowDialog();
        }

        /// <summary>
        /// 以指定屏幕位置（DIP）显示异步弹窗（非阻塞调用线程，等待用户点击后返回结果）。
        /// </summary>
        public static Task<MessageBoxResult> ShowAtAsync(
            DependencyObject context,
            double screenX, double screenY,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            Action<MessageBox> configure = null)
        {
            var tcs = new TaskCompletionSource<MessageBoxResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            ShowAtNonBlocking(context, screenX, screenY, messageBoxText, caption, button, icon,
                onClosed: r => tcs.TrySetResult(r),
                autoCloseSeconds: null,
                showActivated: true,
                configure: configure);
            return tcs.Task;
        }

        /// <summary>
        /// 以指定屏幕位置（DIP）显示非模态弹窗：不阻塞调用方、默认不抢焦点。
        /// 可通过 <paramref name="autoCloseSeconds"/> 设置自动关闭；
        /// 弹窗关闭时（含自动关闭、用户点击、外部关闭）以最终结果回调 <paramref name="onClosed"/>，
        /// 未点击按钮时结果为 <see cref="MessageBoxResult.None"/>。
        /// 返回弹窗实例，可通过其 <c>Close(MessageBoxResult)</c> 主动关闭。
        /// </summary>
        public static MessageBox ShowAtNonBlocking(
            DependencyObject context,
            double screenX, double screenY,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            Action<MessageBoxResult> onClosed = null,
            double? autoCloseSeconds = null,
            bool showActivated = false,
            Action<MessageBox> configure = null)
        {
            var app = Application.Current;
            var dispatcher = app?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => ShowAtNonBlocking(context, screenX, screenY, messageBoxText, caption, button, icon, onClosed, autoCloseSeconds, showActivated, configure));
            }

            var owner = GetDefaultOwner(context);
            var box = CreatePositionedMessageBox(owner, screenX, screenY, messageBoxText, caption, button, icon, showActivated);
            configure?.Invoke(box);

            var completed = false;

            DispatcherTimer autoCloseTimer = null;
            if (autoCloseSeconds.HasValue && autoCloseSeconds.Value > 0)
            {
                autoCloseTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher ?? Dispatcher.CurrentDispatcher)
                {
                    Interval = TimeSpan.FromSeconds(autoCloseSeconds.Value)
                };
                autoCloseTimer.Tick += (s, e) =>
                {
                    try { box.Close(MessageBoxResult.None); } catch { }
                };
                autoCloseTimer.Start();
            }

            void Complete()
            {
                if (completed) return;
                completed = true;
                autoCloseTimer?.Stop();
                // 库的 Close(result) 先写入 _result 再调用 Close()（此时 Window.Closed 同步触发），
                // 库自身的 Closed 事件要等 Close() 返回后才触发（晚于 Window.Closed），
                // 因此必须直接读 Result 属性：按钮点击路径能取到点击结果，外部关闭路径为 None。
                onClosed?.Invoke(box.Result);
            }

            // Window.Closed 覆盖所有关闭路径（含 Alt+F4 等外部关闭）
            ((Window)box).Closed += (s, e) => Complete();

            try
            {
                box.Show();
            }
            catch
            {
                Complete();
                throw;
            }

            return box;
        }

        /// <summary>
        /// 构建以 Manual 方式定位到屏幕指定位置（DIP）的 MessageBox 实例。
        /// 镜像 Owner 的置顶状态（置顶窗口的弹窗需同置顶才能保持可见），
        /// 并在显示后钳制到所在显示器工作区内，避免弹窗超出屏幕。
        /// </summary>
        private static MessageBox CreatePositionedMessageBox(
            Window owner,
            double screenX, double screenY,
            string messageBoxText, string caption,
            MessageBoxButton button, MessageBoxImage icon,
            bool showActivated = true)
        {
            bool manualPosition = IsFinite(screenX) && IsFinite(screenY);

            var box = new MessageBox
            {
                Owner = owner,
                Content = messageBoxText,
                Caption = caption ?? string.Empty,
                MessageBoxButtons = button,
                IconSource = CreateIconSource(icon),
                ShowInTaskbar = false,
                ShowActivated = showActivated,
                Topmost = owner != null && owner.Topmost,
                WindowStartupLocation = manualPosition
                    ? WindowStartupLocation.Manual
                    : (owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen),
                Left = manualPosition ? screenX : 0,
                Top = manualPosition ? screenY : 0,
            };

            if (MessageBox.MakeSound)
            {
                box.SystemSoundOnLoaded = CreateSystemSound(icon);
            }

            box.ContentRendered += (s, e) => ClampToWorkArea(box);
            return box;
        }

        /// <summary>将 <see cref="MessageBoxImage"/> 映射为库内图标（与库静态 Show 的映射一致）。</summary>
        private static IconSource CreateIconSource(MessageBoxImage icon)
        {
            FontIconData symbol;
            switch (icon)
            {
                case MessageBoxImage.Error: symbol = SegoeFluentIcons.ErrorBadge; break;
                case MessageBoxImage.Information: symbol = SegoeFluentIcons.Info; break;
                case MessageBoxImage.Warning: symbol = SegoeFluentIcons.Warning; break;
                case MessageBoxImage.Question: symbol = SegoeFluentIcons.Unknown; break;
                default: return null;
            }
            return new FontIconSource { Icon = symbol, FontSize = 30 };
        }

        /// <summary>将 <see cref="MessageBoxImage"/> 映射为系统提示音（与库静态 Show 的映射一致）。</summary>
        private static SystemSound CreateSystemSound(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error: return SystemSounds.Hand;
                case MessageBoxImage.Information: return SystemSounds.Asterisk;
                case MessageBoxImage.Warning: return SystemSounds.Exclamation;
                case MessageBoxImage.Question: return SystemSounds.Question;
                default: return null;
            }
        }

        /// <summary>显示后按实际渲染尺寸将弹窗钳制回其所在显示器的工作区，防止溢出屏幕。</summary>
        private static void ClampToWorkArea(MessageBox box)
        {
            try
            {
                if (box.ActualWidth <= 0 || box.ActualHeight <= 0) return;
                if (!IsFinite(box.Left) || !IsFinite(box.Top)) return;

                var hwnd = new WindowInteropHelper(box).Handle;
                if (hwnd == IntPtr.Zero) return;
                var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                if (screen == null) return;

                var dpi = VisualTreeHelper.GetDpi(box);
                double waLeft = screen.WorkingArea.Left / dpi.PixelsPerDip;
                double waTop = screen.WorkingArea.Top / dpi.PixelsPerDip;
                double waRight = screen.WorkingArea.Right / dpi.PixelsPerDip;
                double waBottom = screen.WorkingArea.Bottom / dpi.PixelsPerDip;

                double left = box.Left;
                double top = box.Top;
                if (left + box.ActualWidth > waRight) left = waRight - box.ActualWidth;
                if (top + box.ActualHeight > waBottom) top = waBottom - box.ActualHeight;
                if (left < waLeft) left = waLeft;
                if (top < waTop) top = waTop;

                const double epsilon = 0.5;
                if (Math.Abs(left - box.Left) > epsilon) box.Left = left;
                if (Math.Abs(top - box.Top) > epsilon) box.Top = top;
            }
            catch
            {
                // 钳制失败不影响弹窗本身
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        #endregion
    }
}
