using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 窗口置顶中央管理器。
    /// 所有窗口的置顶状态由此类统一管理，子窗口不再自行调用 Win32 API 置顶。
    /// </summary>
    public static class WindowTopmostManager
    {
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMilliseconds(500);

        private static readonly List<ManagedWindow> ManagedWindows = new List<ManagedWindow>();
        private static readonly object SyncRoot = new object();
        private static DispatcherTimer _maintenanceTimer;
        private static Window _mainWindow;
        private static bool _isPaused;
        private static bool _mainWindowTopmostEnabled;
        private static bool _topmostMaintenanceEnabled;
        private static long _zOrderSeed;

        private sealed class ManagedWindow
        {
            public Window Window { get; set; }
            public IntPtr Handle { get; set; }
            public bool IsMainWindow { get; set; }
            public bool InitialTopmost { get; set; }
            public bool AppliedTopmost { get; set; }
            public long ZOrder { get; set; }
        }

        public static void Initialize(Window mainWindow, bool skipScan = false)
        {
            if (mainWindow == null || Application.Current == null) return;

            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    _mainWindow = mainWindow;
                    _mainWindowTopmostEnabled = SettingsManager.Settings.Advanced.IsAlwaysOnTop;
                    EnsureMaintenanceTimer();
                }

                RegisterWindow(mainWindow, true);
                if (!skipScan)
                {
                    ScanOpenWindows();
                }
                StartTimer();
            });
        }

        public static void Shutdown()
        {
            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    _maintenanceTimer?.Stop();
                    _topmostMaintenanceEnabled = false;
                    _mainWindowTopmostEnabled = false;
                    _isPaused = false;

                    foreach (var managedWindow in ManagedWindows.ToList())
                    {
                        DetachWindowEvents(managedWindow.Window);
                    }

                    ManagedWindows.Clear();
                    _mainWindow = null;
                }
            });
        }

        public static void ApplyMainWindowTopmost(Window mainWindow, bool isTopmost)
        {
            if (mainWindow == null) return;

            RunOnDispatcher(() =>
            {
                Initialize(mainWindow);

                lock (SyncRoot)
                {
                    _mainWindow = mainWindow;
                    _mainWindowTopmostEnabled = isTopmost;
                    RegisterWindowCore(mainWindow, true);
                    ApplyZOrderCore();
                }
            });
        }

        public static void StartTopmostMaintenance(Window mainWindow)
        {
            if (mainWindow == null) return;

            RunOnDispatcher(() =>
            {
                Initialize(mainWindow);

                lock (SyncRoot)
                {
                    _topmostMaintenanceEnabled = true;
                    _isPaused = false;
                    StartTimer();
                    ApplyZOrderCore();
                }
            });
        }

        public static void StopTopmostMaintenance()
        {
            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    _topmostMaintenanceEnabled = false;
                    _isPaused = false;
                    ApplyZOrderCore();
                }
            });
        }

        public static void PauseTopmostMaintenance()
        {
            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    _isPaused = true;
                }
            });
        }

        public static void ResumeTopmostMaintenance(Window mainWindow)
        {
            if (mainWindow == null) return;

            RunOnDispatcher(() =>
            {
                Initialize(mainWindow);

                lock (SyncRoot)
                {
                    _isPaused = false;
                    StartTimer();
                    ApplyZOrderCore();
                }
            });
        }

        public static void RegisterWindow(Window window, bool isMainWindow = false)
        {
            if (window == null) return;

            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    RegisterWindowCore(window, isMainWindow || window == _mainWindow || window == Application.Current?.MainWindow);
                    ApplyZOrderCore();
                }
            });
        }

        public static void UnregisterWindow(Window window)
        {
            if (window == null) return;

            RunOnDispatcher(() =>
            {
                lock (SyncRoot)
                {
                    UnregisterWindowCore(window);
                    ApplyZOrderCore();
                }
            });
        }

        private static void MaintenanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                lock (SyncRoot)
                {
                    if (_isPaused) return;

                    if (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled)
                    {
                        // 仅当有窗口丢失 TOPMOST 状态时才重新应用（避免无意义的 Win32 调用导致 Z 序抖动）
                        if (HasAnyWindowLostTopmost())
                        {
                            ApplyZOrderCore();
                            PopupManagerHelper.NotifyTopmostMaintained();
                        }
                    }
                    else
                    {
                        ReleaseManagedChildTopmostCore();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"窗口置顶管理出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查是否有已注册的窗口丢失了 TOPMOST 状态（被其他应用抢占）
        /// </summary>
        private static bool HasAnyWindowLostTopmost()
        {
            foreach (var w in ManagedWindows)
            {
                if (!NativeWindowHelper.IsWindowReady(w.Handle)) continue;

                if (w.IsMainWindow)
                {
                    if (_mainWindowTopmostEnabled && !IsTopmostApplied(w.Handle))
                        return true;
                }
                else if (w.AppliedTopmost && !IsTopmostApplied(w.Handle))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                RegisterWindow(window, window == _mainWindow || window == Application.Current?.MainWindow);
            }
        }

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                RegisterWindow(window, window == _mainWindow || window == Application.Current?.MainWindow);
            }
        }

        private static void Window_Activated(object sender, EventArgs e)
        {
            if (sender is not Window window) return;

            lock (SyncRoot)
            {
                var managedWindow = ManagedWindows.FirstOrDefault(w => w.Window == window);
                if (managedWindow != null)
                {
                    managedWindow.ZOrder = ++_zOrderSeed;
                    // 用户点击激活的窗口需要强制重新设置 Z 序（即使已处于 TOPMOST 状态）
                    managedWindow.AppliedTopmost = false;
                }

                if (!_isPaused && (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled))
                {
                    ApplyZOrderCore();
                }
            }
        }

        private static void Window_Closed(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                UnregisterWindow(window);
            }
        }

        private static void ScanOpenWindows()
        {
            lock (SyncRoot)
            {
                ScanOpenWindowsCore();
                ApplyZOrderCore();
            }
        }

        private static void ScanOpenWindowsCore()
        {
            if (Application.Current == null) return;

            foreach (Window window in Application.Current.Windows)
            {
                RegisterWindowCore(window, window == _mainWindow || window == Application.Current.MainWindow);
            }
        }

        private static void RegisterWindowCore(Window window, bool isMainWindow)
        {
            if (window == null) return;

            var helper = new WindowInteropHelper(window);
            var handle = helper.Handle;

            var managedWindow = ManagedWindows.FirstOrDefault(w => w.Window == window);
            if (managedWindow == null && handle != IntPtr.Zero)
            {
                managedWindow = ManagedWindows.FirstOrDefault(w => w.Handle == handle);
            }

            if (managedWindow == null)
            {
                managedWindow = new ManagedWindow
                {
                    Window = window,
                    Handle = handle,
                    InitialTopmost = window.Topmost,
                    ZOrder = ++_zOrderSeed
                };
                ManagedWindows.Add(managedWindow);
            }

            managedWindow.Window = window;
            managedWindow.Handle = handle;
            managedWindow.IsMainWindow = isMainWindow;
            if (isMainWindow)
            {
                _mainWindow = window;
            }

            AttachWindowEvents(window);
        }

        private static void UnregisterWindowCore(Window window)
        {
            var managedWindow = ManagedWindows.FirstOrDefault(w => w.Window == window);
            if (managedWindow == null) return;

            DetachWindowEvents(window);
            ManagedWindows.Remove(managedWindow);

            if (managedWindow.IsMainWindow)
            {
                _mainWindow = null;
                _mainWindowTopmostEnabled = false;
                _topmostMaintenanceEnabled = false;
            }
        }

        private static void ApplyZOrderCore()
        {
            CleanupInvalidWindowsCore();

            var mainWindow = ManagedWindows.FirstOrDefault(w => w.IsMainWindow);
            var childWindows = ManagedWindows
                .Where(w => !w.IsMainWindow && NativeWindowHelper.IsWindowReady(w.Handle))
                .OrderBy(w => w.ZOrder)
                .ToList();

            // Z序规范：主窗口先置顶，子窗口按打开顺序逐级覆盖（后打开的高于先打开的）
            if (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled)
            {
                // 1) 主窗口先设为 TOPMOST
                if (mainWindow != null && NativeWindowHelper.IsWindowReady(mainWindow.Handle))
                {
                    if (!mainWindow.AppliedTopmost || !IsTopmostApplied(mainWindow.Handle))
                    {
                        mainWindow.Window.Topmost = true;
                        NativeWindowHelper.SetTopmost(mainWindow.Handle);
                        mainWindow.AppliedTopmost = true;
                    }
                }

                // 2) 子窗口按 ZOrder 升序设为 TOPMOST（后打开的 ZOrder 更大，排在 TOPMOST 队列更高位）
                foreach (var childWindow in childWindows)
                {
                    if (!childWindow.AppliedTopmost || !IsTopmostApplied(childWindow.Handle))
                    {
                        childWindow.Window.Topmost = true;
                        NativeWindowHelper.SetTopmost(childWindow.Handle);
                        childWindow.AppliedTopmost = true;
                    }
                }

                // 3) 最后提升 Popup 窗口（如 ComboBox 下拉），确保它们在所有窗口之上
                BoostPopupWindowsAboveChildren();
            }
            else
            {
                if (mainWindow != null && NativeWindowHelper.IsWindowReady(mainWindow.Handle))
                {
                    if (mainWindow.AppliedTopmost || mainWindow.Window.Topmost)
                    {
                        mainWindow.Window.Topmost = false;
                        NativeWindowHelper.SetNotTopmost(mainWindow.Handle);
                        mainWindow.AppliedTopmost = false;
                    }
                }

                ReleaseManagedChildTopmostCore();
            }
        }

        /// <summary>
        /// 检查窗口当前是否已处于 TOPMOST 状态（避免重复调用 Win32 API 导致 Z 序抖动）
        /// </summary>
        private static bool IsTopmostApplied(IntPtr handle)
        {
            int exStyle = PInvoke.GetWindowLong(new HWND(handle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            return (exStyle & NativeWindowHelper.WS_EX_TOPMOST) != 0;
        }

        /// <summary>
        /// 提升同线程中所有非 managed Window 的 HWND（如 WPF Popup/ComboBox 下拉）到 TOPMOST 最顶层。
        /// </summary>
        private static void BoostPopupWindowsAboveChildren()
        {
            try
            {
                var currentThreadId = PInvoke.GetCurrentThreadId();
                var popupHandles = new List<IntPtr>();

                PInvoke.EnumThreadWindows(currentThreadId, (hWnd, _) =>
                {
                    if (!NativeWindowHelper.IsWindowReady(hWnd)) return true;

                    var isManaged = ManagedWindows.Any(w => w.Handle == hWnd);
                    if (!isManaged)
                    {
                        popupHandles.Add(hWnd);
                    }

                    return true;
                }, IntPtr.Zero);

                foreach (var hwnd in popupHandles)
                {
                    PInvoke.SetWindowPos(new HWND(hwnd), new HWND(NativeWindowHelper.HWND_TOPMOST), 0, 0, 0, 0,
                        SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"提升 Popup Z 序失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void ReleaseManagedChildTopmostCore()
        {
            foreach (var childWindow in ManagedWindows.Where(w => !w.IsMainWindow && w.AppliedTopmost && !w.InitialTopmost).ToList())
            {
                if (NativeWindowHelper.IsWindowReady(childWindow.Handle))
                {
                    childWindow.Window.Topmost = false;
                    NativeWindowHelper.SetNotTopmost(childWindow.Handle);
                }

                childWindow.AppliedTopmost = false;
            }
        }

        private static void CleanupInvalidWindowsCore()
        {
            foreach (var managedWindow in ManagedWindows.Where(w => w.Handle != IntPtr.Zero && !PInvoke.IsWindow(new HWND(w.Handle))).ToList())
            {
                DetachWindowEvents(managedWindow.Window);
                ManagedWindows.Remove(managedWindow);
            }
        }

        private static void AttachWindowEvents(Window window)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Loaded -= Window_Loaded;
            window.Activated -= Window_Activated;
            window.Closed -= Window_Closed;

            window.SourceInitialized += Window_SourceInitialized;
            window.Loaded += Window_Loaded;
            window.Activated += Window_Activated;
            window.Closed += Window_Closed;
        }

        private static void DetachWindowEvents(Window window)
        {
            if (window == null) return;

            window.SourceInitialized -= Window_SourceInitialized;
            window.Loaded -= Window_Loaded;
            window.Activated -= Window_Activated;
            window.Closed -= Window_Closed;
        }

        private static void EnsureMaintenanceTimer()
        {
            if (_maintenanceTimer != null) return;

            _maintenanceTimer = new DispatcherTimer
            {
                Interval = MaintenanceInterval
            };
            _maintenanceTimer.Tick += MaintenanceTimer_Tick;
        }

        private static void StartTimer()
        {
            EnsureMaintenanceTimer();
            if (!_maintenanceTimer.IsEnabled)
            {
                _maintenanceTimer.Start();
            }
        }

        private static void RunOnDispatcher(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }
    }
}
