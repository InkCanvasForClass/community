using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class WindowSettingsHelper
    {
        #region Timer Callbacks

        public static Action OnStopKillProcessTimer { get; set; }
        public static Action OnStartKillProcessTimer { get; set; }

        #endregion

        #region PPT Only Mode

        private static DispatcherTimer _pptOnlyVisibilityProbeTimer;
        private static Window _pptModeWindow;
        private const int PPTOnlyVisibilityProbeIntervalMs = 800;

        public static Action<bool> OnPPTOnlyModeChanged { get; set; }

        public static void ApplyPPTOnlyMode(Window window, bool isEnabled)
        {
            try
            {
                SettingsManager.Settings.ModeSettings.IsPPTOnlyMode = isEnabled;
                SettingsManager.SaveSettingsToFile();

                if (isEnabled)
                {
                    window.Hide();
                    LogHelper.WriteLogToFile("已切换到仅PPT模式，主窗口已隐藏", LogHelper.LogType.Event);
                    EnsurePPTOnlyVisibilityProbeTimer(window);
                }
                else
                {
                    StopPPTOnlyVisibilityProbeTimer();
                    window.Show();
                    LogHelper.WriteLogToFile("已切换到正常模式，主窗口已显示", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void EnsurePPTOnlyVisibilityProbeTimer(Window window)
        {
            try
            {
                if (!SettingsManager.Settings.ModeSettings.IsPPTOnlyMode)
                {
                    StopPPTOnlyVisibilityProbeTimer();
                    return;
                }

                _pptModeWindow = window;

                if (_pptOnlyVisibilityProbeTimer == null)
                {
                    _pptOnlyVisibilityProbeTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(PPTOnlyVisibilityProbeIntervalMs)
                    };
                    _pptOnlyVisibilityProbeTimer.Tick += PPTOnlyVisibilityProbeTimer_Tick;
                }

                if (!_pptOnlyVisibilityProbeTimer.IsEnabled)
                    _pptOnlyVisibilityProbeTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"仅PPT可见性探测计时器启动失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static void StopPPTOnlyVisibilityProbeTimer()
        {
            try
            {
                _pptOnlyVisibilityProbeTimer?.Stop();
            }
            catch { }
        }

        private static void PPTOnlyVisibilityProbeTimer_Tick(object sender, EventArgs e)
        {
            OnPPTOnlyModeChanged?.Invoke(true);
        }

        #endregion

        #region Window Settings Methods

        public static bool IsTemporarilyDisablingNoFocusMode { get; set; }

        public static void ApplyNoFocusMode(Window window)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            int exStyle = PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            bool shouldBeNoFocus = !IsTemporarilyDisablingNoFocusMode && SettingsManager.Settings.Advanced.IsNoFocusMode;

            if (shouldBeNoFocus)
            {
                PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | NativeWindowHelper.WS_EX_NOACTIVATE);
            }
            else
            {
                PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle & ~NativeWindowHelper.WS_EX_NOACTIVATE);
            }
        }

        public static void SetWindowMode(Window window)
        {
            if (SettingsManager.Settings.Advanced.WindowMode)
            {
                window.WindowState = WindowState.Normal;
                window.Left = 0.0;
                window.Top = 0.0;
                window.Height = SystemParameters.PrimaryScreenHeight;
                window.Width = SystemParameters.PrimaryScreenWidth;
            }
            else
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        public static void ApplyAlwaysOnTop(Window window)
        {
            try
            {
                WindowTopmostManager.ApplyMainWindowTopmost(window, SettingsManager.Settings.Advanced.IsAlwaysOnTop);

                if (SettingsManager.Settings.Advanced.IsAlwaysOnTop &&
                    SettingsManager.Settings.Advanced.IsNoFocusMode &&
                    !SettingsManager.Settings.Advanced.EnableUIAccessTopMost)
                {
                    StartTopmostMaintenance(window);
                }
                else
                {
                    StopTopmostMaintenance();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用窗口置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void ApplyUIAccessTopMost(Window window)
        {
            bool runtimePaused = false;

            try
            {
                if (SettingsManager.Settings.Advanced.EnableUIAccessTopMost && SettingsManager.Settings.Advanced.IsAlwaysOnTop)
                {
                    // helper 失败后启动的普通用户实例只执行一次回退，不再重新申请 UIA。
                    if (App.IsUIAccessFallbackLaunch)
                    {
                        FallbackToNormalTopMost(window, "UIA helper 已请求普通置顶回退");
                        return;
                    }

                    var identity = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);

                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        try
                        {
                            // 已具有 UIAccess 时无需重启
                            if (UIAccessHelper.HasUIAccess())
                            {
                                LogHelper.WriteLogToFile("UIAccess | 当前进程已具有 UIAccess 权限");
                                App.IsUIAccessTopMostEnabled = true;
                                return;
                            }

                            runtimePaused = true;
                            OnStopKillProcessTimer?.Invoke();

                            if (App.watchdogProcess != null && !App.watchdogProcess.HasExited)
                            {
                                App.watchdogProcess.Kill();
                                App.watchdogProcess = null;
                            }

                            App.IsUIAccessTopMostEnabled = true;
                            App.IsAppExitByUser = true;
                            (Application.Current as App)?.ReleaseMutexForRestart();

                            bool useProcessToken = SettingsManager.Settings.Advanced.UIAMode == UIAMode.ProcessToken;
                            bool started;

                            if (useProcessToken)
                            {
                                started = UIAccessHelper.RestartAsNormalUserWithUIAccess_ProcessToken(sourcePid: (uint)Process.GetCurrentProcess().Id);
                            }
                            else
                            {
                                started = UIAccessHelper.RestartAsNormalUserWithUIAccess();
                            }
                            if (started)
                            {
                                Application.Current.Shutdown();
                            }
                            else
                            {
                                FallbackToNormalTopMost(window, "UIAccess 子进程在启动观察期内退出");
                                RestoreAfterUIAccessFailure();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"启用UIA置顶功能时出错: {ex.Message}", LogHelper.LogType.Error);
                            FallbackToNormalTopMost(window, "启用 UIA 置顶时发生异常");
                            if (runtimePaused)
                            {
                                RestoreAfterUIAccessFailure();
                            }
                        }
                    }
                    else if (UIAccessHelper.HasUIAccess())
                    {
                        LogHelper.WriteLogToFile("UIAccess | 当前普通用户进程已具有 UIAccess 权限");
                        App.IsUIAccessTopMostEnabled = true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("UIA置顶功能需要管理员权限，正在申请管理员权限重启");
                        runtimePaused = true;
                        OnStopKillProcessTimer?.Invoke();

                        if (App.watchdogProcess != null && !App.watchdogProcess.HasExited)
                        {
                            App.watchdogProcess.Kill();
                            App.watchdogProcess = null;
                        }

                        bool started = AppRestartHelper.TrySwitchToUIATopMostAndRestart();
                        if (!started)
                        {
                            RestoreAfterUIAccessFailure();
                        }
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("UIA置顶功能已禁用", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用UIA置顶功能时出错: {ex.Message}", LogHelper.LogType.Error);
                FallbackToNormalTopMost(window, "应用 UIA 置顶时发生异常");
                if (runtimePaused)
                {
                    RestoreAfterUIAccessFailure();
                }
            }
        }

        /// <summary>
        /// 关闭失败的 UIAccess 置顶方案并立即恢复普通置顶。
        /// </summary>
        public static void FallbackToNormalTopMost(Window window, string reason)
        {
            App.IsUIAccessTopMostEnabled = false;
            App.IsAppExitByUser = false;

            try
            {
                if (SettingsManager.Settings?.Advanced != null)
                {
                    bool wasEnabled = SettingsManager.Settings.Advanced.EnableUIAccessTopMost;
                    SettingsManager.Settings.Advanced.EnableUIAccessTopMost = false;
                    if (wasEnabled)
                    {
                        SettingsManager.SaveSettingsToFile();
                    }
                }

                if (window != null)
                {
                    ApplyAlwaysOnTop(window);
                }

                string detail = string.IsNullOrWhiteSpace(reason) ? "UIA 启动失败" : reason;
                LogHelper.WriteLogToFile($"UIAccess | {detail}，已回退到普通置顶", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | 回退到普通置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void RestoreAfterUIAccessFailure()
        {
            try
            {
                App.StartWatchdogIfNeeded();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | 恢复看门狗失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            try
            {
                OnStartKillProcessTimer?.Invoke();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | 恢复进程计时器失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public static void SetTopmostBasedOnSettings(Window window, bool shouldBeTopmost)
        {
            if (SettingsManager.Settings.Advanced.IsAlwaysOnTop)
            {
                WindowTopmostManager.ApplyMainWindowTopmost(window, true);
                ApplyAlwaysOnTop(window);
            }
            else
            {
                WindowTopmostManager.ApplyMainWindowTopmost(window, shouldBeTopmost);
                if (!shouldBeTopmost)
                {
                    ApplyAlwaysOnTop(window);
                }
            }
        }

        public static void PauseTopmostMaintenance()
        {
            WindowTopmostManager.PauseTopmostMaintenance();
        }

        public static void ResumeTopmostMaintenance(Window window)
        {
            if (SettingsManager.Settings.Advanced.IsAlwaysOnTop &&
                SettingsManager.Settings.Advanced.IsNoFocusMode &&
                !SettingsManager.Settings.Advanced.EnableUIAccessTopMost)
            {
                WindowTopmostManager.ResumeTopmostMaintenance(window);
            }
        }

        private static void StartTopmostMaintenance(Window window)
        {
            if (SettingsManager.Settings.Advanced.EnableUIAccessTopMost) return;

            WindowTopmostManager.StartTopmostMaintenance(window);
            LogHelper.WriteLogToFile("启动置顶维护定时器", LogHelper.LogType.Trace);
        }

        private static void StopTopmostMaintenance()
        {
            WindowTopmostManager.StopTopmostMaintenance();
            LogHelper.WriteLogToFile("停止置顶维护定时器", LogHelper.LogType.Trace);
        }

        #endregion
    }
}
