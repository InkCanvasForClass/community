using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Ink_Canvas.Windows.SettingsViews.Helpers;

namespace Ink_Canvas.Helpers
{
    public static class WindowTopmostManager
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMilliseconds(500);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

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
                .Where(w => !w.IsMainWindow && IsWindowReady(w.Handle))
                .OrderBy(w => w.ZOrder)
                .ToList();

            if (mainWindow != null && IsWindowReady(mainWindow.Handle))
            {
                if (_mainWindowTopmostEnabled)
                {
                    mainWindow.Window.Topmost = true;
                    SetTopmost(mainWindow.Handle);
                    mainWindow.AppliedTopmost = true;
                }
                else
                {
                    mainWindow.Window.Topmost = false;
                    SetNotTopmost(mainWindow.Handle);
                    mainWindow.AppliedTopmost = false;
                }
            }

            if (_mainWindowTopmostEnabled || _topmostMaintenanceEnabled)
            {
                foreach (var childWindow in childWindows)
                {
                    childWindow.Window.Topmost = true;
                    SetTopmost(childWindow.Handle);
                    childWindow.AppliedTopmost = true;
                }
            }
            else
            {
                ReleaseManagedChildTopmostCore();
            }
        }

        private static void ReleaseManagedChildTopmostCore()
        {
            foreach (var childWindow in ManagedWindows.Where(w => !w.IsMainWindow && w.AppliedTopmost && !w.InitialTopmost).ToList())
            {
                if (IsWindowReady(childWindow.Handle))
                {
                    childWindow.Window.Topmost = false;
                    SetNotTopmost(childWindow.Handle);
                }

                childWindow.AppliedTopmost = false;
            }
        }

        private static void CleanupInvalidWindowsCore()
        {
            foreach (var managedWindow in ManagedWindows.Where(w => w.Handle != IntPtr.Zero && !IsWindow(w.Handle)).ToList())
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

        private static bool IsWindowReady(IntPtr handle)
        {
            return handle != IntPtr.Zero && IsWindow(handle) && IsWindowVisible(handle) && !IsIconic(handle);
        }

        private static void SetTopmost(IntPtr handle)
        {
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) == 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
            }
        }

        private static void SetNotTopmost(IntPtr handle)
        {
            SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) != 0)
            {
                SetWindowLong(handle, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
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
