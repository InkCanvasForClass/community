using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 集中式系统事件监控服务。
    /// 使用 WinEvent 钩子监听前台窗口变化，使用单一计时器监控进程状态，
    /// 替代各触发器和 RulesetService 各自轮询的方式。
    /// </summary>
    public class SystemEventMonitor : IDisposable
    {
        #region WinEvent Hook P/Invoke

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        #endregion

        /// <summary>
        /// 前台窗口变化事件（窗口切换、焦点变化时触发）
        /// </summary>
        public event EventHandler ForegroundWindowChanged;

        /// <summary>
        /// 监控的进程状态变化事件（任何已注册进程启动或退出时触发）
        /// </summary>
        public event EventHandler ProcessChanged;

        /// <summary>
        /// 应用内部状态变化事件（批注模式、浮动栏折叠等变化时触发）
        /// </summary>
        public event EventHandler InternalStateChanged;

        // WinEvent 钩子
        private IntPtr _foregroundHook;
        private WinEventProc _foregroundProc; // 防止 GC 回收委托

        // 前台窗口钩子失败时的降级轮询
        private Timer _foregroundFallbackTimer;
        private IntPtr _lastForegroundWindow;

        // 进程监控
        private Timer _processTimer;
        private readonly object _processLock = new();
        private readonly Dictionary<string, int> _processRefCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _processStates = new(StringComparer.OrdinalIgnoreCase);

        private bool _disposed;

        public SystemEventMonitor()
        {
            // 设置前台窗口变化钩子
            _foregroundProc = OnForegroundWindowEvent;
            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundProc,
                0, 0, WINEVENT_OUTOFCONTEXT);

            // 如果钩子设置失败，降级为轮询
            if (_foregroundHook == IntPtr.Zero)
            {
                _foregroundFallbackTimer = new Timer(500);
                _foregroundFallbackTimer.Elapsed += OnForegroundFallbackElapsed;
                _foregroundFallbackTimer.AutoReset = true;
            }

            // 进程监控定时器（1s，仅在有注册进程时运行）
            _processTimer = new Timer(1000);
            _processTimer.Elapsed += OnProcessTimerElapsed;
            _processTimer.AutoReset = true;
        }

        /// <summary>
        /// 启动监控
        /// </summary>
        public void Start()
        {
            _foregroundFallbackTimer?.Start();
            UpdateProcessTimerState();
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void Stop()
        {
            _foregroundFallbackTimer?.Stop();
            _processTimer?.Stop();
        }

        #region 进程监控

        /// <summary>
        /// 注册需要监控的进程（引用计数，同一进程名可多次注册）
        /// </summary>
        public void RegisterProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return;

            lock (_processLock)
            {
                if (_processRefCounts.TryGetValue(processName, out var count))
                {
                    _processRefCounts[processName] = count + 1;
                }
                else
                {
                    _processRefCounts[processName] = 1;
                    _processStates[processName] = CheckProcessRunning(processName);
                    UpdateProcessTimerState();
                }
            }
        }

        /// <summary>
        /// 取消注册进程监控（引用计数归零时移除）
        /// </summary>
        public void UnregisterProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return;

            lock (_processLock)
            {
                if (_processRefCounts.TryGetValue(processName, out var count))
                {
                    if (count <= 1)
                    {
                        _processRefCounts.Remove(processName);
                        _processStates.Remove(processName);
                        UpdateProcessTimerState();
                    }
                    else
                    {
                        _processRefCounts[processName] = count - 1;
                    }
                }
            }
        }

        /// <summary>
        /// 查询指定进程是否正在运行
        /// </summary>
        public bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            lock (_processLock)
            {
                return _processStates.TryGetValue(processName, out var running) && running;
            }
        }

        private void UpdateProcessTimerState()
        {
            if (_disposed) return;
            lock (_processLock)
            {
                if (_processRefCounts.Count > 0)
                    _processTimer?.Start();
                else
                    _processTimer?.Stop();
            }
        }

        private void OnProcessTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed) return;

            bool anyChanged = false;
            lock (_processLock)
            {
                foreach (var name in _processRefCounts.Keys)
                {
                    var isRunning = CheckProcessRunning(name);
                    if (_processStates.TryGetValue(name, out var wasRunning) && wasRunning != isRunning)
                    {
                        anyChanged = true;
                    }
                    _processStates[name] = isRunning;
                }
            }

            if (anyChanged)
            {
                ProcessChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static bool CheckProcessRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 前台窗口监控

        private void OnForegroundWindowEvent(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        private void OnForegroundFallbackElapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed) return;
            try
            {
                var current = GetForegroundWindow();
                if (current != _lastForegroundWindow && current != IntPtr.Zero)
                {
                    _lastForegroundWindow = current;
                    ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch { }
        }

        #endregion

        #region 内部状态通知

        /// <summary>
        /// 通知应用内部状态已变化（批注模式、浮动栏折叠等）。
        /// 由 Action 在修改状态后调用。
        /// </summary>
        public void NotifyInternalStateChanged()
        {
            InternalStateChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_foregroundHook != IntPtr.Zero)
            {
                try { UnhookWinEvent(_foregroundHook); } catch { }
                _foregroundHook = IntPtr.Zero;
            }
            _foregroundProc = null;

            _foregroundFallbackTimer?.Stop();
            _foregroundFallbackTimer?.Dispose();
            _foregroundFallbackTimer = null;

            _processTimer?.Stop();
            _processTimer?.Dispose();
            _processTimer = null;
        }
    }
}
