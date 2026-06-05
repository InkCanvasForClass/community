using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Diagnostics;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 进程检测触发器的设置
    /// </summary>
    public class ProcessDetectedSettings
    {
        /// <summary>
        /// 要检测的进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get; set; } = "";

        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 1000;
    }

    /// <summary>
    /// 当指定进程启动时触发的触发器。
    /// </summary>
    public class ProcessDetectedTrigger : TriggerBase<ProcessDetectedSettings>
    {
        private Timer? _timer;
        private bool _wasRunning = false;

        public override void Loaded()
        {
            _timer = new Timer(Settings.CheckIntervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
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
            var isRunning = IsProcessRunning(Settings.ProcessName);

            if (isRunning && !_wasRunning)
            {
                // 进程刚启动
                Trigger();
            }
            else if (!isRunning && _wasRunning)
            {
                // 进程刚退出
                TriggerRevert();
            }

            _wasRunning = isRunning;
        }

        private static bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
