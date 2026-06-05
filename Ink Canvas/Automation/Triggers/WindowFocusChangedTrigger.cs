using Ink_Canvas.WorkflowAutomation.Abstractions;
using System;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 前台窗口变化触发器设置
    /// </summary>
    public class WindowFocusChangedSettings
    {
        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 500;
    }

    /// <summary>
    /// 前台窗口焦点变化时触发的触发器。
    /// </summary>
    public class WindowFocusChangedTrigger : TriggerBase<WindowFocusChangedSettings>
    {
        private Timer? _timer;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        public override void Loaded()
        {
            _timer = new Timer(Settings.CheckIntervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            _lastForegroundWindow = GetForegroundWindow();
        }

        public override void UnLoaded()
        {
            if (_timer != null)
            {
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var currentForeground = GetForegroundWindow();
                if (currentForeground != _lastForegroundWindow)
                {
                    _lastForegroundWindow = currentForeground;
                    Trigger();
                }
            }
            catch
            {
                // 忽略 Win32 异常
            }
        }
    }
}
