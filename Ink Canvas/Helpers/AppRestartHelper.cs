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
            Process successor = null;
            try
            {
                App.IsAppExitByUser = true;

                (Application.Current as App)?.ReleaseMutexForRestart();

                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                if (asAdmin)
                {
                    var psi = new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" };
                    successor = Process.Start(psi);
                }
                else if (IsRunningAsAdmin())
                {
                    // 优先通过用户令牌直接降权启动，避免经由 explorer 中转的延迟
                    if (UIAccessHelper.RestartAsNormalUser())
                    {
                        Application.Current.Shutdown();
                        return;
                    }

                    // 降权回退：explorer 以普通用户运行，中转启动可继承普通权限。
                    // explorer 中转拿不到目标进程句柄，无法做存活观察，只能信任启动结果。
                    Process.Start("explorer.exe", "\"" + exePath + "\"");
                }
                else
                {
                    // 普通权限直接启动：explorer.exe 中转只会增加延迟与失败面
                    // （explorer 忙/重启中/被安全软件拦截时新实例根本起不来，表现为"点了重启软件消失"）。
                    successor = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }

                if (!WaitForSuccessorToSurvive(successor))
                {
                    // 新实例启动后立刻退出：宁可保留当前实例，也不能变成"旧进程退了、新进程没起来"。
                    throw new InvalidOperationException("重启的新实例在启动观察期内已退出");
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                // 启动失败（UAC 被取消、新实例秒退等）：回滚退出状态并保留当前实例，
                // 避免"已标记退出但进程未退"的半退出残留，同时让用户能感知失败原因。
                App.IsAppExitByUser = false;
                LogHelper.WriteLogToFile($"重启应用失败，已保留当前实例: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                successor?.Dispose();
            }
        }

        /// <summary>
        /// 重启就绪等待：以"新实例进入消息循环（界面就绪）"为完成条件，而不是固定时长。
        /// 快设备通常几百毫秒内返回，慢设备可一直等到真正就绪；
        /// 启动即崩（0xC000007b、缺 DLL 等）会在等待期间返回 false，
        /// 此时保留旧进程，避免出现"旧进程退出、新进程未起来"的重启失败。
        /// </summary>
        private static bool WaitForSuccessorToSurvive(Process successor)
        {
            if (successor == null) return true; // 无句柄（令牌降权/explorer 中转路径）时无法观察

            try
            {
                if (successor.HasExited) return false;

                // WaitForInputIdle 在新实例创建首个窗口、进入消息循环时返回：
                // 与设备加载速度自适应，替代"固定 1.5 秒观察期"。
                if (successor.WaitForInputIdle(15000))
                {
                    return !successor.HasExited;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重启 | 等待新实例就绪失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            // 无 UI 消息循环或等待超时：退回轻量确认（启动即崩通常在数百毫秒内暴露）
            try
            {
                System.Threading.Thread.Sleep(300);
                return !successor.HasExited;
            }
            catch
            {
                return true;
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

        /// <summary>
        /// 启动阶段专用的轻量 UIA 交接入口。
        /// 不读写 SettingsManager、不主动关闭当前进程，由 App_Startup 统一处理交接与回退。
        /// </summary>
        public static bool TryLaunchUIAFromStartup(bool useProcessToken)
        {
            try
            {
                if (UIAccessHelper.HasUIAccess())
                {
                    return true;
                }

                if (IsRunningAsAdmin())
                {
                    return useProcessToken
                        ? UIAccessHelper.RestartAsNormalUserWithUIAccess_ProcessToken(
                            sourcePid: (uint)Process.GetCurrentProcess().Id)
                        : UIAccessHelper.RestartAsNormalUserWithUIAccess();
                }

                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string forwardArgs = UIAccessHelper.BuildForwardArgsArgument();
                ProcessStartInfo psi;
                Process helperProcess;

                if (useProcessToken)
                {
                    int currentPid = Process.GetCurrentProcess().Id;
                    psi = new ProcessStartInfo(exePath)
                    {
                        Arguments = $"{UIAccessHelper.UIA_HELPER_SWITCH} --uia-source-pid {currentPid}{forwardArgs}",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    helperProcess = Process.Start(psi);

                    // helper 需要复制当前普通进程的用户令牌；当前进程必须保持存活。
                    System.Threading.Thread.Sleep(2000);
                }
                else
                {
                    psi = new ProcessStartInfo(exePath)
                    {
                        Arguments = $"{UIAccessHelper.UIA_HELPER_SWITCH}{forwardArgs}",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    helperProcess = Process.Start(psi);
                }

                return WaitForUIAHelperExit(helperProcess);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | 启动阶段 UIA 交接失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
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

                    // 提权重启链会经过一个管理员 helper 进程，helper 自身只带 UIA 开关，
                    // 必须把当前进程的启动参数（icc:// 深链接、文件路径等）显式转发给它，
                    // 否则 UIA 子进程重建命令行时会丢掉这些参数，用户启动意图消失。
                    string forwardArgs = UIAccessHelper.BuildForwardArgsArgument();

                    if (useProcessToken)
                    {
                        int currentPid = Process.GetCurrentProcess().Id;
                        psi = new ProcessStartInfo(exePath)
                        {
                            Arguments = $"{UIAccessHelper.UIA_HELPER_SWITCH} --uia-source-pid {currentPid}{forwardArgs}",
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
                            Arguments = $"{UIAccessHelper.UIA_HELPER_SWITCH}{forwardArgs}",
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
