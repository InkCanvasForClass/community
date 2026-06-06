using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Ink_Canvas.Windows.SettingsViews.Helpers;

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

        public static void Initialize(Window mainWindow)
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
                ScanOpenWindows();
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
                    ScanOpenWindowsCore();

                    if (_isPaused) return;

                    if (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled)
                    {
                        ApplyZOrderCore();
                        PopupManagerHelper.NotifyTopmostMaintained();
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

            if (mainWindow != null && NativeWindowHelper.IsWindowReady(mainWindow.Handle))
            {
                if (_mainWindowTopmostEnabled)
                {
                    mainWindow.Window.Topmost = true;
                    NativeWindowHelper.SetTopmost(mainWindow.Handle);
                    mainWindow.AppliedTopmost = true;
                }
                else
                {
                    mainWindow.Window.Topmost = false;
                    NativeWindowHelper.SetNotTopmost(mainWindow.Handle);
                    mainWindow.AppliedTopmost = false;
                }
            }

            if (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled)
            {
                foreach (var childWindow in childWindows)
                {
                    childWindow.Window.Topmost = true;
                    NativeWindowHelper.SetTopmost(childWindow.Handle);
                    childWindow.AppliedTopmost = true;
                }

                BoostPopupWindowsAboveChildren();
            }
            else
            {
                ReleaseManagedChildTopmostCore();
            }
        }

        /// <summary>
        /// 提升同线程中所有非 managed Window 的 HWND（如 WPF Popup/ComboBox 下拉）到 TOPMOST 最顶层。
        /// </summary>
        private static void BoostPopupWindowsAboveChildren()
        {
            try
            {
                var currentThreadId = NativeWindowHelper.GetCurrentThreadId();
                var popupHandles = new List<IntPtr>();

                NativeWindowHelper.EnumThreadWindows(currentThreadId, (hWnd, _) =>
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
                    NativeWindowHelper.SetWindowPos(hwnd, NativeWindowHelper.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOACTIVATE | NativeWindowHelper.SWP_SHOWWINDOW | NativeWindowHelper.SWP_NOOWNERZORDER);
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
            foreach (var managedWindow in ManagedWindows.Where(w => w.Handle != IntPtr.Zero && !NativeWindowHelper.IsWindow(w.Handle)).ToList())
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
