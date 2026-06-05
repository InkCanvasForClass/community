using Ink_Canvas.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class WindowSettingsHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WH_KEYBOARD_LL = 13;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Keyboard Hook

        private static LowLevelKeyboardProc _keyboardProc;
        private static IntPtr _keyboardHookId = IntPtr.Zero;

        private static IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        public static void InstallKeyboardHook()
        {
            if (_keyboardHookId == IntPtr.Zero)
            {
                _keyboardProc = KeyboardHookProc;
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                    GetModuleHandle(null), 0);
                if (_keyboardHookId == IntPtr.Zero)
                {
                    LogHelper.WriteLogToFile("安装低级键盘钩子失败", LogHelper.LogType.Error);
                }
            }
        }

        public static void UninstallKeyboardHook()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
                _keyboardProc = null;
            }
        }

        #endregion

        #region Timer Callbacks

        public static Action OnStopKillProcessTimer { get; set; }
        public static Action OnStartKillProcessTimer { get; set; }

        #endregion

        #region Topmost Maintenance Timer

        #endregion

        #region PPT Only Mode

        private static DispatcherTimer _pptOnlyVisibilityProbeTimer;
        private static Window _pptModeWindow;
        private const int PptOnlyVisibilityProbeIntervalMs = 800;

        public static Action<bool> OnPptOnlyModeChanged { get; set; }

        public static void ApplyPptOnlyMode(Window window, bool isEnabled)
        {
            try
            {
                SettingsManager.Settings.ModeSettings.IsPPTOnlyMode = isEnabled;
                SettingsManager.SaveSettingsToFile();

                if (isEnabled)
                {
                    window.Hide();
                    LogHelper.WriteLogToFile("已切换到仅PPT模式，主窗口已隐藏", LogHelper.LogType.Event);
                    EnsurePptOnlyVisibilityProbeTimer(window);
                }
                else
                {
                    StopPptOnlyVisibilityProbeTimer();
                    window.Show();
                    LogHelper.WriteLogToFile("已切换到正常模式，主窗口已显示", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void EnsurePptOnlyVisibilityProbeTimer(Window window)
        {
            try
            {
                if (!SettingsManager.Settings.ModeSettings.IsPPTOnlyMode)
                {
                    StopPptOnlyVisibilityProbeTimer();
                    return;
                }

                _pptModeWindow = window;

                if (_pptOnlyVisibilityProbeTimer == null)
                {
                    _pptOnlyVisibilityProbeTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(PptOnlyVisibilityProbeIntervalMs)
                    };
                    _pptOnlyVisibilityProbeTimer.Tick += PptOnlyVisibilityProbeTimer_Tick;
                }

                if (!_pptOnlyVisibilityProbeTimer.IsEnabled)
                    _pptOnlyVisibilityProbeTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"仅PPT可见性探测计时器启动失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static void StopPptOnlyVisibilityProbeTimer()
        {
            try
            {
                _pptOnlyVisibilityProbeTimer?.Stop();
            }
            catch { }
        }

        private static void PptOnlyVisibilityProbeTimer_Tick(object sender, EventArgs e)
        {
            OnPptOnlyModeChanged?.Invoke(true);
        }

        #endregion

        #region Window Settings Methods

        public static bool IsTemporarilyDisablingNoFocusMode { get; set; }

        public static void ApplyNoFocusMode(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            bool shouldBeNoFocus = !IsTemporarilyDisablingNoFocusMode && SettingsManager.Settings.Advanced.IsNoFocusMode;

            if (shouldBeNoFocus)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
                InstallKeyboardHook();
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
                UninstallKeyboardHook();
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
            try
            {
                if (SettingsManager.Settings.Advanced.EnableUIAccessTopMost && SettingsManager.Settings.Advanced.IsAlwaysOnTop)
                {
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

                            OnStopKillProcessTimer?.Invoke();

                            if (App.watchdogProcess != null && !App.watchdogProcess.HasExited)
                            {
                                App.watchdogProcess.Kill();
                                App.watchdogProcess = null;
                            }

                            App.IsUIAccessTopMostEnabled = true;
                            App.IsAppExitByUser = true;
                            (Application.Current as App)?.ReleaseMutexForRestart();

                            bool started = UIAccessHelper.RestartAsNormalUserWithUIAccess();
                            if (started)
                            {
                                Application.Current.Shutdown();
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("UIAccess | 启动失败，回退到普通管理员模式", LogHelper.LogType.Warning);
                                App.IsUIAccessTopMostEnabled = false;
                                App.IsAppExitByUser = false;
                                App.StartWatchdogIfNeeded();
                                OnStartKillProcessTimer?.Invoke();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"启用UIA置顶功能时出错: {ex.Message}", LogHelper.LogType.Error);
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
                        OnStopKillProcessTimer?.Invoke();

                        if (App.watchdogProcess != null && !App.watchdogProcess.HasExited)
                        {
                            App.watchdogProcess.Kill();
                            App.watchdogProcess = null;
                        }

                        AppRestartHelper.SwitchToUIATopMostAndRestart();
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

        private static void TopmostMaintenanceTimer_Tick(object sender, EventArgs e)
        {
        }

        #endregion
    }
}
