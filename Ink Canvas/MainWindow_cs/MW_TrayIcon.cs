using H.NotifyIcon;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Ink_Canvas
{
    public partial class App : Application
    {
        private const int TrayTemporaryShowMinutes = 2;

        private DispatcherTimer _trayTemporaryShowTimer;

        private bool _trayTemporaryShowRestoreHideChecked;

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
        private void SysTrayMenu_Opened(object sender, RoutedEventArgs e)
        {
            var s = (ContextMenu)sender;
            var FoldFloatingBarTrayIconMenuItemIconEyeOff = (Image)((Grid)((MenuItem)s.Items[s.Items.Count - 5]).Icon).Children[0];
            var FoldFloatingBarTrayIconMenuItemIconEyeOn = (Image)((Grid)((MenuItem)s.Items[s.Items.Count - 5]).Icon).Children[1];
            var FoldFloatingBarTrayIconMenuItemHeaderText = (TextBlock)((SimpleStackPanel)((MenuItem)s.Items[s.Items.Count - 5]).Header).Children[0];
            var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
            var HideICCMainWindowTrayIconMenuItem = s.Items.OfType<MenuItem>()
                .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
            if (HideICCMainWindowTrayIconMenuItem == null) return;
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin.IsLoaded)
            {
                // 在无焦点模式下，暂时取消主窗口置顶，让系统菜单能够正常显示
                if (Ink_Canvas.MainWindow.Settings.Advanced.IsAlwaysOnTop && Ink_Canvas.MainWindow.Settings.Advanced.IsNoFocusMode)
                {
                    mainWin.Topmost = false;
                }

                // 判斷是否在收納模式中
                if (mainWin.isFloatingBarFolded)
                {
                    FoldFloatingBarTrayIconMenuItemIconEyeOff.Visibility = Visibility.Hidden;
                    FoldFloatingBarTrayIconMenuItemIconEyeOn.Visibility = Visibility.Visible;
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = "退出收纳模式";
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
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = "切换为收纳模式";
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
        private bool EnsureMainWindowReadyForSettings(MainWindow mainWin)
        {
            if (mainWin?.IsLoaded != true)
            {
                return false;
            }

            var trayMenu = ((TaskbarIcon)Current.Resources["TaskbarTrayIcon"]).ContextMenu;
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

        private bool IsLegacySettingsVisible(MainWindow mainWin)
        {
            try
            {
                var borderSettingsField = typeof(MainWindow).GetField("BorderSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var borderSettings = borderSettingsField?.GetValue(mainWin) as FrameworkElement;
                return borderSettings?.Visibility == Visibility.Visible;
            }
            catch
            {
                return false;
            }
        }

        private void TempShowMainWindowTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = Current.MainWindow as MainWindow;
            if (mainWin?.IsLoaded != true)
                return;

            MenuItem hideItem = null;
            try
            {
                var trayMenu = ((TaskbarIcon)Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                hideItem = trayMenu?.Items.OfType<MenuItem>()
                    .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
            }
            catch
            {
            }

            _trayTemporaryShowRestoreHideChecked = hideItem?.IsChecked == true;

            EnsureMainWindowReadyForSettings(mainWin);

            global::Ink_Canvas.MainWindow.TrayTemporaryShowUntilUtc = DateTime.UtcNow.AddMinutes(TrayTemporaryShowMinutes);

            _trayTemporaryShowTimer?.Stop();
            if (_trayTemporaryShowTimer == null)
            {
                _trayTemporaryShowTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(TrayTemporaryShowMinutes)
                };
                _trayTemporaryShowTimer.Tick += TrayTemporaryShowTimer_OnTick;
            }
            else
            {
                _trayTemporaryShowTimer.Interval = TimeSpan.FromMinutes(TrayTemporaryShowMinutes);
            }

            _trayTemporaryShowTimer.Start();
        }

        private void TrayTemporaryShowTimer_OnTick(object sender, EventArgs e)
        {
            _trayTemporaryShowTimer?.Stop();
            global::Ink_Canvas.MainWindow.TrayTemporaryShowUntilUtc = null;

            var mainWin = Current.MainWindow as MainWindow;
            if (mainWin?.IsLoaded != true)
            {
                _trayTemporaryShowRestoreHideChecked = false;
                return;
            }

            try
            {
                if (_trayTemporaryShowRestoreHideChecked)
                {
                    var trayMenu = ((TaskbarIcon)Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                    var hideItem = trayMenu?.Items.OfType<MenuItem>()
                        .FirstOrDefault(mi => mi.Name == "HideICCMainWindowTrayIconMenuItem");
                    if (hideItem != null)
                        hideItem.IsChecked = true;
                    else
                        mainWin.Hide();
                }
                else
                {
                    mainWin.CheckMainWindowVisibility();
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

        private void OpenSettingsTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = Current.MainWindow as MainWindow;
            if (!EnsureMainWindowReadyForSettings(mainWin))
            {
                return;
            }

            if (IsLegacySettingsVisible(mainWin))
            {
                return;
            }

            try
            {
                var method = typeof(MainWindow).GetMethod("BtnSettings_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                method?.Invoke(mainWin, new object[] { null, null });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Open settings from tray failed: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SysTrayMenu_Closed(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                // 菜单关闭后，恢复主窗口的置顶状态
                if (Ink_Canvas.MainWindow.Settings.Advanced.IsAlwaysOnTop && Ink_Canvas.MainWindow.Settings.Advanced.IsNoFocusMode)
                {
                    mainWin.Topmost = true;
                }
            }
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
        private void CloseAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                IsAppExitByUser = true;
                mainWin.BtnExit_Click(null, null);
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
        private void RestartAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                IsAppExitByUser = true;

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
                Current.Shutdown();
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
        private void ForceFullScreenTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                Ink_Canvas.MainWindow.MoveWindow(new WindowInteropHelper(mainWin).Handle, 0, 0,
                    Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, true);
                Ink_Canvas.MainWindow.ShowNewMessage($"已强制全屏化：{Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height}（缩放比例为{Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth}x{Screen.PrimaryScreen.Bounds.Height / SystemParameters.PrimaryScreenHeight}）");
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
        private void FoldFloatingBarTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
                if (mainWin.isFloatingBarFolded) mainWin.UnFoldFloatingBar_MouseUp(new object(), null);
                else mainWin.FoldFloatingBar_MouseUp(new object(), null);
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
        ///    - 检查是否处于PPT演示模式
        ///    - 如果浮动栏当前未处于收纳模式：
        ///       - 如果不处于PPT演示模式，调用PureViewboxFloatingBarMarginAnimationInDesktopMode方法重置浮动栏位置
        ///       - 否则，调用PureViewboxFloatingBarMarginAnimationInPPTMode方法重置浮动栏位置
        /// </remarks>
        private void ResetFloatingBarPositionTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                var isInPPTPresentationMode = false;
                Dispatcher.Invoke(() =>
                {
                    isInPPTPresentationMode = mainWin.BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                });
                if (!mainWin.isFloatingBarFolded)
                {
                    if (!isInPPTPresentationMode) mainWin.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    else mainWin.PureViewboxFloatingBarMarginAnimationInPPTMode();
                }
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
        private void HideICCMainWindowTrayIconMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            _trayTemporaryShowTimer?.Stop();
            global::Ink_Canvas.MainWindow.TrayTemporaryShowUntilUtc = null;
            _trayTemporaryShowRestoreHideChecked = false;

            var mi = (MenuItem)sender;
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                mainWin.Hide();
                var s = ((TaskbarIcon)Current.Resources["TaskbarTrayIcon"]).ContextMenu;
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
        private void HideICCMainWindowTrayIconMenuItem_UnChecked(object sender, RoutedEventArgs e)
        {
            var mi = (MenuItem)sender;
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                mainWin.Show();
                var s = ((TaskbarIcon)Current.Resources["TaskbarTrayIcon"]).ContextMenu;
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
        private void DisableAllHotkeysMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Current.MainWindow;
            if (mainWin != null && mainWin.IsLoaded)
            {
                try
                {
                    // 获取全局快捷键管理器
                    var hotkeyManagerField = typeof(MainWindow).GetField("_globalHotkeyManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var hotkeyManager = hotkeyManagerField?.GetValue(mainWin) as GlobalHotkeyManager;

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
                                    if (textBlock.Text == "禁用所有快捷键")
                                    {
                                        textBlock.Text = "启用所有快捷键";
                                        LogHelper.WriteLogToFile("已禁用所有快捷键", LogHelper.LogType.Event);
                                    }
                                    else
                                    {
                                        textBlock.Text = "禁用所有快捷键";
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

    }
}
