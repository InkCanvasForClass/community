using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    public static class AppRestartHelper
    {
        private const int UIA_HELPER_WAIT_TIMEOUT_MS = 30000;
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
            TrySwitchToUIATopMostAndRestart();
        }

        /// <summary>
        /// 尝试切换到 UIAccess 置顶并重启。等待提升 helper 完成 UIA 子进程启动观察期，
        /// 只有 helper 成功退出时才关闭当前进程；失败时保留当前进程并回退普通置顶。
        /// </summary>
        public static bool TrySwitchToUIATopMostAndRestart()
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
                bool useProcessToken = SettingsManager.Settings.Advanced.UIAMode == UIAMode.ProcessToken;
                Process helperProcess = null;

                if (IsRunningAsAdmin())
                {
                    if (useProcessToken)
                    {
                        started = UIAccessHelper.RestartAsNormalUserWithUIAccess_ProcessToken(sourcePid: (uint)Process.GetCurrentProcess().Id);
                    }
                    else
                    {
                        started = UIAccessHelper.RestartAsNormalUserWithUIAccess();
                    }
                }
                else
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    ProcessStartInfo psi;

                    if (useProcessToken)
                    {
                        int currentPid = Process.GetCurrentProcess().Id;
                        psi = new ProcessStartInfo(exePath)
                        {
                            Arguments = $"--enable-uia-topmost-helper --uia-source-pid {currentPid}",
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        helperProcess = Process.Start(psi);
                        // 保持原进程短暂存活，确保提升的 helper 可以复制当前进程令牌。
                        System.Threading.Thread.Sleep(2000);
                    }
                    else
                    {
                        psi = new ProcessStartInfo(exePath)
                        {
                            Arguments = "--enable-uia-topmost-helper",
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        helperProcess = Process.Start(psi);
                    }

                    started = WaitForUIAHelperExit(helperProcess);
                }

                if (started)
                {
                    Application.Current?.Shutdown();
                    return true;
                }

                FallbackToNormalTopMost("UIA 置顶启动失败");
                return false;
            }
            catch (Exception ex)
            {
                FallbackToNormalTopMost($"切换到 UIA 置顶时出错: {ex.Message}");
                return false;
            }
        }

        private static bool WaitForUIAHelperExit(Process helperProcess)
        {
            if (helperProcess == null)
            {
                LogHelper.WriteLogToFile("UIAccess | 未取得 UIA helper 进程句柄", LogHelper.LogType.Error);
                return false;
            }

            try
            {
                if (!helperProcess.WaitForExit(UIA_HELPER_WAIT_TIMEOUT_MS))
                {
                    LogHelper.WriteLogToFile($"UIAccess | 等待 UIA helper 超时 ({UIA_HELPER_WAIT_TIMEOUT_MS}ms)", LogHelper.LogType.Error);
                    try
                    {
                        if (!helperProcess.HasExited)
                        {
                            helperProcess.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception killEx)
                    {
                        LogHelper.WriteLogToFile($"UIAccess | 结束超时 UIA helper 失败: {killEx.Message}", LogHelper.LogType.Warning);
                    }

                    return false;
                }

                int exitCode = helperProcess.ExitCode;
                LogHelper.WriteLogToFile($"UIAccess | UIA helper 已退出 (ExitCode={exitCode})");
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | 等待 UIA helper 失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                helperProcess.Dispose();
            }
        }

        private static void FallbackToNormalTopMost(string reason)
        {
            App.IsAppExitByUser = false;
            App.IsUIAccessTopMostEnabled = false;

            try
            {
                WindowSettingsHelper.FallbackToNormalTopMost(Application.Current?.MainWindow, reason);
            }
            catch (Exception fallbackEx)
            {
                Debug.WriteLine($"回退到普通置顶时出错: {fallbackEx.Message}");
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
