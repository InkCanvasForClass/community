using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 定时触发器设置
    /// </summary>
    public class TimerTriggerSettings
    {
        /// <summary>
        /// 触发间隔（秒）
        /// </summary>
        public double IntervalSeconds { get; set; } = 60;

        /// <summary>
        /// 是否只触发一次
        /// </summary>
        public bool TriggerOnce { get; set; } = false;
    }

    /// <summary>
    /// 定时触发器。
    /// </summary>
    public class TimerTrigger : TriggerBase<TimerTriggerSettings>
    {
        private Timer? _timer;
        private bool _hasTriggered = false;

        public override void Loaded()
        {
            _timer = new Timer(Settings.IntervalSeconds * 1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = !Settings.TriggerOnce;
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
            _hasTriggered = false;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (Settings.TriggerOnce && _hasTriggered) return;
            _hasTriggered = true;
            Trigger();
        }
    }
}
