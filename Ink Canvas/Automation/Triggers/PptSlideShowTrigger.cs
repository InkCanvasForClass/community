using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// PPT 放映检测触发器的设置
    /// </summary>
    public class PptSlideShowSettings
    {
        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 2000;
    }

    /// <summary>
    /// 当 PPT 进入放映模式时触发的触发器。
    /// </summary>
    public class PptSlideShowTrigger : TriggerBase<PptSlideShowSettings>
    {
        private Timer? _timer;
        private bool _wasInSlideShow = false;

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
            try
            {
                var pptProcesses = System.Diagnostics.Process.GetProcessesByName("POWERPNT");
                bool isInSlideShow = false;

                if (pptProcesses.Length > 0)
                {
                    // 检查是否有放映窗口（窗口标题通常包含 "PowerPoint 幻灯片放映" 或 "PowerPoint Slide Show"）
                    foreach (var proc in pptProcesses)
                    {
                        try
                        {
                            if (proc.MainWindowTitle.Contains("幻灯片放映") || proc.MainWindowTitle.Contains("Slide Show"))
                            {
                                isInSlideShow = true;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (isInSlideShow && !_wasInSlideShow)
                {
                    Trigger();
                }
                else if (!isInSlideShow && _wasInSlideShow)
                {
                    TriggerRevert();
                }

                _wasInSlideShow = isInSlideShow;
            }
            catch
            {
                // 忽略检测错误
            }
        }
    }
}
