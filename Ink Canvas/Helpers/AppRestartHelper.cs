using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    public static class AppRestartHelper
    {
        public static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartApp(bool asAdmin)
        {
            try
            {
                App.IsAppExitByUser = true;

                (Application.Current as App)?.ReleaseMutexForRestart();

                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                if (asAdmin)
                {
                    var psi = new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" };
                    Process.Start(psi);
                }
                else
                {
                    // 当前已是管理员时，直接通过用户令牌降权启动，避免经由 explorer 中转的延迟
                    if (IsRunningAsAdmin() && UIAccessHelper.RestartAsNormalUser())
                    {
                        Application.Current.Shutdown();
                        return;
                    }

                    Process.Start("explorer.exe", "\"" + exePath + "\"");
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重启应用时出错: {ex.Message}");
            }
        }

        public static void RestartWithCurrentPrivileges()
        {
            RestartApp(IsRunningAsAdmin());
        }

        public static void RestartAsAdmin()
        {
            RestartApp(true);
        }

        public static void RestartAsNormal()
        {
            RestartApp(false);
        }

        public static void SwitchToUIATopMostAndRestart()
        {
            try
            {
                SettingsManager.Settings.Advanced.EnableUIAccessTopMost = true;

                if (!SettingsManager.Settings.Advanced.IsAlwaysOnTop)
                {
                    SettingsManager.Settings.Advanced.IsAlwaysOnTop = true;
                }

                SettingsManager.SaveSettingsToFile();

                App.IsUIAccessTopMostEnabled = true;
                App.IsAppExitByUser = true;
                (Application.Current as App)?.ReleaseMutexForRestart();

                bool started;
                if (IsRunningAsAdmin())
                {
                    started = UIAccessHelper.RestartAsNormalUserWithUIAccess();
                }
                else
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    var psi = new ProcessStartInfo(exePath)
                    {
                        Arguments = "--enable-uia-topmost-helper",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    started = true;
                }

                if (started)
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    App.IsAppExitByUser = false;
                    App.IsUIAccessTopMostEnabled = false;
                }
            }
            catch (Exception ex)
            {
                App.IsAppExitByUser = false;
                App.IsUIAccessTopMostEnabled = false;
                Debug.WriteLine($"切换到UIA置顶模式时出错: {ex.Message}");
            }
        }

        public static void SwitchToNormalTopMostAndRestart()
        {
            try
            {
                SettingsManager.Settings.Advanced.EnableUIAccessTopMost = false;
                SettingsManager.SaveSettingsToFile();

                App.IsUIAccessTopMostEnabled = false;
                RestartApp(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"切换到普通置顶模式时出错: {ex.Message}");
            }
        }
    }
}
