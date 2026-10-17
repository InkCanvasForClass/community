using H.NotifyIcon;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Ink_Canvas.Services
{
    /// <summary>
    /// 系统托盘服务（M12 寄生提取）。承载从 App（原 MW_TrayIcon.cs）搬出的托盘图标与右键菜单逻辑。
    /// 禁止引用 MainWindow 类型：所有对 MainWindow/App 私有成员的调用经 <see cref="Hooks"/> 委托注入，
    /// 事件出抛方向唯一（Service → App）。
    /// 计时器使用 <see cref="System.Threading.Timer"/> + 注入的 <c>UiInvoke</c> 回到 UI 线程
    ///（M30 统一替换为 ISyncService 前的过渡形态）。
    /// </summary>
    internal sealed class TrayIconService : IDisposable
    {
        /// <summary>
        /// 托盘服务的外部依赖委托集合。由 App 在 <c>CreateTrayIconService</c> 中装配。
        /// </summary>
        internal sealed class Hooks
        {
            /// <summary>获取主窗口（以 Window 基类形态返回，服务不接触 MainWindow 类型）。</summary>
            public Func<Window> GetMainWindow { get; set; }
            /// <summary>读取托盘左键单击动作设置。</summary>
            public Func<TrayClickAction> GetTrayLeftClickAction { get; set; }
            /// <summary>读取托盘右键单击动作设置。</summary>
            public Func<TrayClickAction> GetTrayRightClickAction { get; set; }
            /// <summary>读取"始终置顶"设置。</summary>
            public Func<bool> IsAlwaysOnTop { get; set; }
            /// <summary>读取"无焦点模式"设置。</summary>
            public Func<bool> IsNoFocusMode { get; set; }
            /// <summary>读取浮动栏是否处于收纳模式。</summary>
            public Func<bool> IsFloatingBarFolded { get; set; }
            /// <summary>进入收纳模式（调用 MainWindow 的 UnFold/Fold 反向操作由服务侧判断）。</summary>
            public Action FoldFloatingBar { get; set; }
            /// <summary>退出收纳模式。</summary>
            public Action UnfoldFloatingBar { get; set; }
            /// <summary>重置浮动栏位置（清空拖动标志与坐标后按 PPT/桌面模式播放归位动画）。</summary>
            public Action ResetFloatingBarPosition { get; set; }
            /// <summary>写入托盘临时显示的截止时间（MainWindow 静态字段）。</summary>
            public Action<DateTime?> SetTrayTemporaryShowUntilUtc { get; set; }
            /// <summary>在 UI 线程上执行委托（过渡形态，M30 收敛为 ISyncService）。</summary>
            public Action<Action> UiInvoke { get; set; }
            /// <summary>打开设置窗口（反射调用 MainWindow.BtnSettings_Click）。</summary>
            public Action OpenSettings { get; set; }
            /// <summary>标记本次退出为用户主动退出。</summary>
            public Action MarkExitByUser { get; set; }
            /// <summary>标记用户主动退出并调用 MainWindow.ExitApplication。</summary>
            public Action ExitApplicationByUser { get; set; }
            /// <summary>关闭当前应用实例（Application.Shutdown）。</summary>
            public Action Shutdown { get; set; }
            /// <summary>强制全屏化主窗口并显示提示消息。</summary>
            public Action ForceFullScreen { get; set; }
            /// <summary>触发 MainWindow.CheckMainWindowVisibility。</summary>
            public Action CheckMainWindowVisibility { get; set; }
            /// <summary>反射获取 MainWindow 的全局快捷键管理器。</summary>
            public Func<GlobalHotkeyManager> GetGlobalHotkeyManager { get; set; }
        }

        private const int TrayTemporaryShowMinutes = 2;

        private readonly Hooks _hooks;

        private Timer _trayTemporaryShowTimer;

        private bool _trayTemporaryShowRestoreHideChecked;

        public TrayIconService(Hooks hooks)
        {
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        }

        /// <summary>
        /// 停止并释放托盘临时显示计时器。
        /// </summary>
        public void Dispose() => StopTemporaryShowTimer();

        /// <summary>
        /// 插件托盘左键单击事件（App 转发为 PluginTrayLeftClicked 供插件订阅）。
        /// </summary>
        internal event Action TrayLeftClicked;

        /// <summary>
        /// 插件托盘右键单击事件（App 转发为 PluginTrayRightClicked 供插件订阅）。
        /// </summary>
        internal event Action TrayRightClicked;

        public void TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            var action = _hooks.GetTrayLeftClickAction();
            ExecuteTrayClickAction(action);

            try { TrayLeftClicked?.Invoke(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"转发插件托盘左键事件失败: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void TrayRightMouseDown(object sender, RoutedEventArgs e)
        {
            var action = _hooks.GetTrayRightClickAction();
            ExecuteTrayClickAction(action);

            try { TrayRightClicked?.Invoke(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"转发插件托盘右键事件失败: {ex.Message}", LogHelper.LogType.Warning); }
        }

        private void ExecuteTrayClickAction(TrayClickAction action)
        {
            switch (action)
            {
                case TrayClickAction.ShowMenu:
                    ShowTrayContextMenu();
                    break;
                case TrayClickAction.HideShowMainWindow:
                    ToggleMainWindowVisibility();
                    break;
                case TrayClickAction.TempShowMainWindow:
                    TempShowMainWindowClicked(null, null);
                    break;
                case TrayClickAction.OpenSettings:
                    OpenSettingsClicked(null, null);
                    break;
                case TrayClickAction.DisableAllHotkeys:
                    DisableAllHotkeysClicked(FindTrayMenuItem("DisableAllHotkeysMenuItem"), null);
                    break;
                case TrayClickAction.ForceFullScreen:
                    ForceFullScreenClicked(null, null);
                    break;
                case TrayClickAction.ToggleFoldFloatingBar:
                    FoldFloatingBarClicked(null, null);
                    break;
                case TrayClickAction.ResetFloatingBarPosition:
                    ResetFloatingBarPositionClicked(null, null);
                    break;
                case TrayClickAction.RestartApp:
                    RestartAppClicked(null, null);
                    break;
                case TrayClickAction.CloseApp:
                    CloseAppClicked(null, null);
                    break;
                case TrayClickAction.NoAction:
                    break;
            }
        }

        private MenuItem FindTrayMenuItem(string name)
        {
            try
            {
                var trayMenu = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                return trayMenu?.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == name);
            }
            catch
            {
                return null;
            }
        }

        public void ShowTrayContextMenu()
        {
            try
            {
                var taskbarIcon = (TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"];
                if (taskbarIcon?.ContextMenu != null)
                {
                    taskbarIcon.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "显示托盘右键菜单失败", LogHelper.LogType.Warning);
            }
        }

        private void ToggleMainWindowVisibility()
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin?.IsLoaded != true) return;

            var hideItem = FindTrayMenuItem("HideICCMainWindowTrayIconMenuItem");
            if (hideItem != null)
            {
                if (hideItem.IsChecked)
                {
                    hideItem.IsChecked = false;
                    HideMainWindowUnchecked(hideItem, null);
                }
                else
                {
                    hideItem.IsChecked = true;
                    HideMainWindowChecked(hideItem, null);
                }
            }
            else
            {
                if (mainWin.IsVisible)
                    mainWin.Hide();
                else
                    mainWin.Show();
            }
        }

        /// <summary>
        /// 系统托盘菜单打开时的事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理系统托盘菜单打开时的逻辑，包括以下步骤：
        /// 1. 获取系统托盘菜单及其相关菜单项和图标
        /// 2. 获取主窗口实例
        /// 3. 如果主窗口已加载：
        ///    - 在无焦点模式下，暂时取消主窗口置顶，让系统菜单能够正常显示
        ///    - 根据浮动栏是否处于收纳模式，更新菜单项图标和文本
        ///    - 根据浮动栏状态和主窗口是否隐藏，更新重置浮动栏位置菜单项的启用状态
        /// </remarks>
        public void SysTrayMenuOpened(object sender, RoutedEventArgs e)
        {
            var s = (ContextMenu)sender;
            var FoldFloatingBarTrayIconMenuItemIconEyeOff = (Image)((Grid)((MenuItem)s.Items[s.Items.Count - 5]).Icon).Children[0];
            var FoldFloatingBarTrayIconMenuItemIconEyeOn = (Image)((Grid)((MenuItem)s.Items[s.Items.Count - 5]).Icon).Children[1];
            var FoldFloatingBarTrayIconMenuItemHeaderText = (TextBlock)((SimpleStackPanel)((MenuItem)s.Items[s.Items.Count - 5]).Header).Children[0];
            var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
            var HideICCMainWindowTrayIconMenuItem = s.Items.OfType<MenuItem>()
                .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
            if (HideICCMainWindowTrayIconMenuItem == null) return;
            var mainWin = _hooks.GetMainWindow();
            if (mainWin.IsLoaded)
            {
                // 在无焦点模式下，暂时取消主窗口置顶，让系统菜单能够正常显示
                if (_hooks.IsAlwaysOnTop() && _hooks.IsNoFocusMode())
                {
                    mainWin.Topmost = false;
                }

                // 判斷是否在收納模式中
                if (_hooks.IsFloatingBarFolded())
                {
                    FoldFloatingBarTrayIconMenuItemIconEyeOff.Visibility = Visibility.Hidden;
                    FoldFloatingBarTrayIconMenuItemIconEyeOn.Visibility = Visibility.Visible;
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = MainWindowStrings.Main_Tray_ExitFoldMode;
                    if (!HideICCMainWindowTrayIconMenuItem.IsChecked)
                    {
                        ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = false;
                        ResetFloatingBarPositionTrayIconMenuItem.Opacity = 0.5;
                    }
                }
                else
                {
                    FoldFloatingBarTrayIconMenuItemIconEyeOff.Visibility = Visibility.Visible;
                    FoldFloatingBarTrayIconMenuItemIconEyeOn.Visibility = Visibility.Hidden;
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = MainWindowStrings.Main_Tray_EnterFoldMode;
                    if (!HideICCMainWindowTrayIconMenuItem.IsChecked)
                    {
                        ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = true;
                        ResetFloatingBarPositionTrayIconMenuItem.Opacity = 1;
                    }

                }
            }
        }

        /// <summary>
        /// 系统托盘菜单关闭时的事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理系统托盘菜单关闭时的逻辑，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载，且在无焦点模式下启用了始终置顶，则恢复主窗口的置顶状态
        /// </remarks>
        public void SysTrayMenuClosed(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                // 菜单关闭后，恢复主窗口的置顶状态
                if (_hooks.IsAlwaysOnTop() && _hooks.IsNoFocusMode())
                {
                    mainWin.Topmost = true;
                }
            }
        }

        private bool EnsureMainWindowReadyForSettings(Window mainWin)
        {
            if (mainWin?.IsLoaded != true)
            {
                return false;
            }

            var trayMenu = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
            var hideMainWindowMenuItem = trayMenu?.Items.OfType<MenuItem>()
                .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");

            if (hideMainWindowMenuItem != null && hideMainWindowMenuItem.IsChecked)
            {
                hideMainWindowMenuItem.IsChecked = false;
            }
            else if (!mainWin.IsVisible)
            {
                mainWin.Show();
            }

            if (mainWin.WindowState == WindowState.Minimized)
            {
                mainWin.WindowState = WindowState.Normal;
            }

            mainWin.Activate();
            return true;
        }

        public void TempShowMainWindowClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin?.IsLoaded != true)
                return;

            MenuItem hideItem = null;
            try
            {
                var trayMenu = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                hideItem = trayMenu?.Items.OfType<MenuItem>()
                    .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Tray] 查找托盘「隐藏主窗口」菜单项失败: {ex.Message}", LogHelper.LogType.Info);
            }

            _trayTemporaryShowRestoreHideChecked = hideItem?.IsChecked == true;

            EnsureMainWindowReadyForSettings(mainWin);

            _hooks.SetTrayTemporaryShowUntilUtc(DateTime.UtcNow.AddMinutes(TrayTemporaryShowMinutes));

            StopTemporaryShowTimer();
            _trayTemporaryShowTimer = new Timer(
                _ => _hooks.UiInvoke(TemporaryShowTimerTick), null,
                TimeSpan.FromMinutes(TrayTemporaryShowMinutes), Timeout.InfiniteTimeSpan);
        }

        private void TemporaryShowTimerTick()
        {
            StopTemporaryShowTimer();
            _hooks.SetTrayTemporaryShowUntilUtc(null);

            var mainWin = _hooks.GetMainWindow();
            if (mainWin?.IsLoaded != true)
            {
                _trayTemporaryShowRestoreHideChecked = false;
                return;
            }

            try
            {
                if (_trayTemporaryShowRestoreHideChecked)
                {
                    var trayMenu = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                    var hideItem = trayMenu?.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
                    if (hideItem != null)
                        hideItem.IsChecked = true;
                    else
                        mainWin.Hide();
                }
                else
                {
                    _hooks.CheckMainWindowVisibility();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"托盘临时显示计时结束处理失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                _trayTemporaryShowRestoreHideChecked = false;
            }
        }

        private void StopTemporaryShowTimer()
        {
            _trayTemporaryShowTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _trayTemporaryShowTimer?.Dispose();
            _trayTemporaryShowTimer = null;
        }

        public void OpenSettingsClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (!EnsureMainWindowReadyForSettings(mainWin))
            {
                return;
            }

            _hooks.OpenSettings();
        }

        /// <summary>
        /// 关闭应用程序托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理关闭应用程序托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 设置IsAppExitByUser为true，表示用户主动退出
        ///    - 关闭应用程序
        /// </remarks>
        public void CloseAppClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                _hooks.ExitApplicationByUser();
            }
        }

        /// <summary>
        /// 重启应用程序托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理重启应用程序托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 设置IsAppExitByUser为true，表示用户主动退出
        ///    - 尝试启动应用程序的新实例，带延迟参数
        ///    - 捕获并记录启动新实例时可能出现的异常
        ///    - 关闭当前应用程序实例
        /// </remarks>
        public void RestartAppClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                _hooks.MarkExitByUser();

                try
                {
                    // 启动新实例
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = exePath;
                    startInfo.UseShellExecute = true;

                    // 启动进程但不等待
                    Process.Start(new ProcessStartInfo(exePath, "-delay 2000") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog($"重启程序时出错: {ex.Message}");
                }

                // 退出当前实例
                _hooks.Shutdown();
            }
        }

        /// <summary>
        /// 强制全屏化托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理强制全屏化托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 调用MoveWindow方法将主窗口移动到屏幕左上角并设置为全屏大小
        ///    - 显示强制全屏化的消息，包含屏幕分辨率和缩放比例信息
        /// </remarks>
        public void ForceFullScreenClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                _hooks.ForceFullScreen();
            }
        }

        /// <summary>
        /// 切换浮动栏收纳模式托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理切换浮动栏收纳模式托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 如果浮动栏当前处于收纳模式，则调用UnFoldFloatingBar_MouseUp方法退出收纳模式
        ///    - 否则，调用FoldFloatingBar_MouseUp方法进入收纳模式
        /// </remarks>
        public void FoldFloatingBarClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
                if (_hooks.IsFloatingBarFolded()) _hooks.UnfoldFloatingBar();
                else _hooks.FoldFloatingBar();
        }

        /// <summary>
        /// 重置浮动栏位置托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理重置浮动栏位置托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 清空用户拖动标志和已保存的位置坐标，让动画走默认位置分支
        ///    - 如果浮动栏当前未处于收纳模式：
        ///       - 如果不处于PPT演示模式，调用PureViewboxFloatingBarMarginAnimationInDesktopMode方法重置浮动栏位置
        ///       - 否则，调用PureViewboxFloatingBarMarginAnimationInPPTMode方法重置浮动栏位置
        /// </remarks>
        public void ResetFloatingBarPositionClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                _hooks.ResetFloatingBarPosition();
            }
        }

        /// <summary>
        /// 隐藏主窗口托盘菜单项选中事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理隐藏主窗口托盘菜单项的选中事件，包括以下步骤：
        /// 1. 获取菜单项和主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 隐藏主窗口
        ///    - 获取系统托盘菜单
        ///    - 禁用并设置半透明效果给以下菜单项：
        ///       - 重置浮动栏位置
        ///       - 切换浮动栏收纳模式
        ///       - 强制全屏化
        /// 3. 否则，取消菜单项的选中状态
        /// </remarks>
        public void HideMainWindowChecked(object sender, RoutedEventArgs e)
        {
            StopTemporaryShowTimer();
            _hooks.SetTrayTemporaryShowUntilUtc(null);
            _trayTemporaryShowRestoreHideChecked = false;

            var mi = (MenuItem)sender;
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                mainWin.Hide();
                var s = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                if (s != null)
                {
                    var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
                    var FoldFloatingBarTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 5];
                    var ForceFullScreenTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 6];
                    ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = false;
                    FoldFloatingBarTrayIconMenuItem.IsEnabled = false;
                    ForceFullScreenTrayIconMenuItem.IsEnabled = false;
                    ResetFloatingBarPositionTrayIconMenuItem.Opacity = 0.5;
                    FoldFloatingBarTrayIconMenuItem.Opacity = 0.5;
                    ForceFullScreenTrayIconMenuItem.Opacity = 0.5;
                }
            }
            else
            {
                mi.IsChecked = false;
            }

        }

        /// <summary>
        /// 显示主窗口托盘菜单项取消选中事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理显示主窗口托盘菜单项的取消选中事件，包括以下步骤：
        /// 1. 获取菜单项和主窗口实例
        /// 2. 如果主窗口已加载：
        ///    - 显示主窗口
        ///    - 获取系统托盘菜单
        ///    - 启用并设置正常透明度给以下菜单项：
        ///       - 重置浮动栏位置
        ///       - 切换浮动栏收纳模式
        ///       - 强制全屏化
        /// 3. 否则，取消菜单项的选中状态
        /// </remarks>
        public void HideMainWindowUnchecked(object sender, RoutedEventArgs e)
        {
            var mi = (MenuItem)sender;
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                mainWin.Show();
                var s = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                if (s != null)
                {
                    var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
                    var FoldFloatingBarTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 5];
                    var ForceFullScreenTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 6];
                    ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = true;
                    FoldFloatingBarTrayIconMenuItem.IsEnabled = true;
                    ForceFullScreenTrayIconMenuItem.IsEnabled = true;
                    ResetFloatingBarPositionTrayIconMenuItem.Opacity = 1;
                    FoldFloatingBarTrayIconMenuItem.Opacity = 1;
                    ForceFullScreenTrayIconMenuItem.Opacity = 1;
                }
            }
            else
            {
                mi.IsChecked = false;
            }
        }

        /// <summary>
        /// 禁用/启用所有快捷键托盘菜单项点击事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 处理禁用/启用所有快捷键托盘菜单项的点击事件，包括以下步骤：
        /// 1. 获取主窗口实例
        /// 2. 如果主窗口已加载，尝试：
        ///    - 通过反射获取全局快捷键管理器
        ///    - 如果获取成功：
        ///       - 禁用快捷键注册
        ///       - 更新菜单项文本和状态：
        ///          - 如果当前文本是"禁用所有快捷键"，则更改为"启用所有快捷键"并记录日志
        ///          - 否则，更改为"禁用所有快捷键"，重新启用快捷键注册并记录日志
        ///    - 如果获取失败，记录错误日志
        /// 3. 捕获并记录可能出现的异常
        /// </remarks>
        public void DisableAllHotkeysClicked(object sender, RoutedEventArgs e)
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin != null && mainWin.IsLoaded)
            {
                try
                {
                    // 获取全局快捷键管理器
                    var hotkeyManager = _hooks.GetGlobalHotkeyManager();

                    if (hotkeyManager != null)
                    {
                        // 禁用所有快捷键
                        hotkeyManager.DisableHotkeyRegistration();

                        // 更新菜单项文本和状态
                        var menuItem = sender as MenuItem;
                        if (menuItem != null)
                        {
                            var headerPanel = menuItem.Header as SimpleStackPanel;
                            if (headerPanel != null)
                            {
                                var textBlock = headerPanel.Children[0] as TextBlock;
                                if (textBlock != null)
                                {
                                    if (textBlock.Text == MainWindowStrings.Main_Tray_DisableHotkeys)
                                    {
                                        textBlock.Text = MainWindowStrings.Main_Tray_EnableHotkeys;
                                        LogHelper.WriteLogToFile("已禁用所有快捷键", LogHelper.LogType.Event);
                                    }
                                    else
                                    {
                                        textBlock.Text = MainWindowStrings.Main_Tray_DisableHotkeys;
                                        // 重新启用快捷键
                                        hotkeyManager.EnableHotkeyRegistration();
                                        LogHelper.WriteLogToFile("已启用所有快捷键", LogHelper.LogType.Event);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("无法获取全局快捷键管理器", LogHelper.LogType.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"禁用/启用快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        #region 插件托盘服务（ITrayService 宿主支持）

        public TaskbarIcon GetPluginTaskbarIcon()
        {
            try { return Application.Current.Resources["TaskbarTrayIcon"] as TaskbarIcon; }
            catch { return null; }
        }

        public void SetPluginTrayIconVisibility(bool visible)
        {
            var icon = GetPluginTaskbarIcon();
            if (icon != null) icon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 向托盘右键菜单注入一个插件菜单项，插入到「重启程序」之前，
        /// 保持 SysTrayMenuOpened / HideMainWindowChecked 等
        /// 用倒数索引访问固定菜单项的兼容性。
        /// </summary>
        public bool AddPluginTrayMenuItem(string id, string text, Action onClicked)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text) || onClicked == null) return false;
            try
            {
                var trayMenu = GetPluginTaskbarIcon()?.ContextMenu;
                if (trayMenu == null) return false;

                string menuName = "PluginTray." + id;
                if (trayMenu.Items.OfType<MenuItem>().Any(mi => mi.Name == menuName)) return false;

                var item = new MenuItem
                {
                    Name = menuName,
                    Header = new TextBlock
                    {
                        Text = text,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1b))
                    }
                };
                item.Click += (s, e) => onClicked();

                var restartItem = trayMenu.Items.OfType<MenuItem>()
                    .FirstOrDefault(mi => mi.Name == "RestartAppTrayIconMenuItem");
                int insertIndex = restartItem != null ? trayMenu.Items.IndexOf(restartItem) : trayMenu.Items.Count;
                trayMenu.Items.Insert(insertIndex, item);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加插件托盘菜单项失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool RemovePluginTrayMenuItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            try
            {
                var trayMenu = GetPluginTaskbarIcon()?.ContextMenu;
                if (trayMenu == null) return false;

                var item = trayMenu.Items.OfType<MenuItem>()
                    .FirstOrDefault(mi => mi.Name == "PluginTray." + id);
                if (item == null) return false;

                trayMenu.Items.Remove(item);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"移除插件托盘菜单项失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool HasPluginTrayMenuItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            try
            {
                var trayMenu = GetPluginTaskbarIcon()?.ContextMenu;
                return trayMenu?.Items.OfType<MenuItem>().Any(mi => mi.Name == "PluginTray." + id) == true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 移除某个插件注册的所有托盘菜单项。插件热重载时调用：菜单项的 Click 处理器
        /// 闭包持有插件程序集中的回调，留着会阻止插件 AssemblyLoadContext 卸载。
        /// 按 "PluginTray.&lt;pluginId&gt;" 前缀匹配，覆盖插件注册多个菜单项的情况。
        /// </summary>
        public int RemovePluginTrayMenuItemsByPrefix(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return 0;
            try
            {
                var trayMenu = GetPluginTaskbarIcon()?.ContextMenu;
                if (trayMenu == null) return 0;

                var prefix = "PluginTray." + pluginId;
                var doomed = trayMenu.Items.OfType<MenuItem>()
                    .Where(mi => mi.Name != null
                                 && (mi.Name == prefix || mi.Name.StartsWith(prefix + ".", StringComparison.Ordinal)))
                    .ToList();

                foreach (var item in doomed) trayMenu.Items.Remove(item);
                return doomed.Count;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"按插件移除托盘菜单项失败: {ex.Message}", LogHelper.LogType.Warning);
                return 0;
            }
        }

        public void ShowPluginMainWindow()
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin?.IsLoaded != true) return;

            var hideItem = FindTrayMenuItem("HideICCMainWindowTrayIconMenuItem");
            if (hideItem != null) hideItem.IsChecked = false; // 触发 Unchecked → Show + 启用相关菜单项
            else if (!mainWin.IsVisible) mainWin.Show();
        }

        public void HidePluginMainWindow()
        {
            var mainWin = _hooks.GetMainWindow();
            if (mainWin?.IsLoaded != true) return;

            var hideItem = FindTrayMenuItem("HideICCMainWindowTrayIconMenuItem");
            if (hideItem != null) hideItem.IsChecked = true; // 触发 Checked → Hide + 禁用相关菜单项
            else if (mainWin.IsVisible) mainWin.Hide();
        }

        #endregion
    }
}
