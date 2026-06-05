using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Diagnostics;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// PPT放映退出触发器设置
    /// </summary>
    public class PptSlideShowExitSettings
    {
        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 2000;
    }

    /// <summary>
    /// 退出PPT放映模式时触发的触发器。
    /// </summary>
    public class PptSlideShowExitTrigger : TriggerBase<PptSlideShowExitSettings>
    {
        private Timer? _timer;
        private bool _wasInSlideShow = false;

        public override void Loaded()
        {
            _timer = new Timer(Settings.CheckIntervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            _wasInSlideShow = IsPowerPointInSlideShow();
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
                bool isInSlideShow = IsPowerPointInSlideShow();

                if (!isInSlideShow && _wasInSlideShow)
                {
                    _wasInSlideShow = false;
                    Trigger();
                }
                else if (isInSlideShow)
                {
                    _wasInSlideShow = true;
                }
            }
            catch
            {
                // 忽略检测错误
            }
        }

        private static bool IsPowerPointInSlideShow()
        {
            var pptProcesses = Process.GetProcessesByName("POWERPNT");
            if (pptProcesses.Length == 0) return false;

            foreach (var proc in pptProcesses)
            {
                try
                {
                    if (proc.MainWindowTitle.Contains("幻灯片放映") || proc.MainWindowTitle.Contains("Slide Show"))
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
