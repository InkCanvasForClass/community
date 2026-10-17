using H.NotifyIcon;
using Ink_Canvas.Services;
using System;
using System.Windows;
using Application = System.Windows.Application;

namespace Ink_Canvas
{
    public partial class App : Application
    {
        // 托盘逻辑已提取至 Services/Shell/TrayIconService.cs，本文件仅保留
        // XAML wired 事件处理器与插件托盘 API 的转发壳（壳删除试点）。
        // 服务的依赖装配在 App.xaml.cs 的 CreateTrayIconService，释放在 App_Exit。
        private TrayIconService _trayIconService;

        private TrayIconService TrayIcon => _trayIconService ??= CreateTrayIconService();

        internal event Action PluginTrayLeftClicked;
        internal event Action PluginTrayRightClicked;

        private void TaskbarTrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e) => TrayIcon.TrayLeftMouseDown(sender, e);
        private void TaskbarTrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e) => TrayIcon.TrayRightMouseDown(sender, e);
        private void SysTrayMenu_Opened(object sender, RoutedEventArgs e) => TrayIcon.SysTrayMenuOpened(sender, e);
        private void SysTrayMenu_Closed(object sender, RoutedEventArgs e) => TrayIcon.SysTrayMenuClosed(sender, e);
        private void TempShowMainWindowTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.TempShowMainWindowClicked(sender, e);
        private void OpenSettingsTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.OpenSettingsClicked(sender, e);
        private void DisableAllHotkeysMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.DisableAllHotkeysClicked(sender, e);
        private void ForceFullScreenTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.ForceFullScreenClicked(sender, e);
        private void FoldFloatingBarTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.FoldFloatingBarClicked(sender, e);
        private void ResetFloatingBarPositionTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.ResetFloatingBarPositionClicked(sender, e);
        private void RestartAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.RestartAppClicked(sender, e);
        private void CloseAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) => TrayIcon.CloseAppClicked(sender, e);
        private void HideICCMainWindowTrayIconMenuItem_Checked(object sender, RoutedEventArgs e) => TrayIcon.HideMainWindowChecked(sender, e);
        private void HideICCMainWindowTrayIconMenuItem_UnChecked(object sender, RoutedEventArgs e) => TrayIcon.HideMainWindowUnchecked(sender, e);

        internal void ShowTrayContextMenu() => TrayIcon.ShowTrayContextMenu();
        internal TaskbarIcon GetPluginTaskbarIcon() => TrayIcon.GetPluginTaskbarIcon();
        internal void SetPluginTrayIconVisibility(bool visible) => TrayIcon.SetPluginTrayIconVisibility(visible);
        internal bool AddPluginTrayMenuItem(string id, string text, Action onClicked) => TrayIcon.AddPluginTrayMenuItem(id, text, onClicked);
        internal bool RemovePluginTrayMenuItem(string id) => TrayIcon.RemovePluginTrayMenuItem(id);
        internal bool HasPluginTrayMenuItem(string id) => TrayIcon.HasPluginTrayMenuItem(id);
        internal int RemovePluginTrayMenuItemsByPrefix(string pluginId) => TrayIcon.RemovePluginTrayMenuItemsByPrefix(pluginId);
        internal void ShowPluginMainWindow() => TrayIcon.ShowPluginMainWindow();
        internal void HidePluginMainWindow() => TrayIcon.HidePluginMainWindow();
    }
}
