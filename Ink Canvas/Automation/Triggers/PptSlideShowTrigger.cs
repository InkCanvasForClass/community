using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// PPT 放映检测触发器的设置
    /// </summary>
    public class PptSlideShowSettings
    {
    }

    /// <summary>
    /// 当 PPT 进入放映模式时触发的触发器。
    /// 通过 SystemEventMonitor 的进程和窗口事件驱动，无需独立轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.pptslideshow", "PPT放映检测", "Presentation")]
    public class PptSlideShowTrigger : TriggerBase<PptSlideShowSettings>
    {
        private bool _wasInSlideShow = false;

        public override void Loaded()
        {
            var monitor = AutomationBootstrap.Monitor;
            if (monitor == null) return;

            monitor.RegisterProcess("POWERPNT");
            _wasInSlideShow = IsPowerPointInSlideShow();

            monitor.ProcessChanged += OnStateChanged;
            monitor.ForegroundWindowChanged += OnStateChanged;
        }

        public override void UnLoaded()
        {
            var monitor = AutomationBootstrap.Monitor;
            if (monitor == null) return;

            monitor.ProcessChanged -= OnStateChanged;
            monitor.ForegroundWindowChanged -= OnStateChanged;
            monitor.UnregisterProcess("POWERPNT");
        }

        private void OnStateChanged(object sender, System.EventArgs e)
        {
            var isInSlideShow = IsPowerPointInSlideShow();

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

        private static bool IsPowerPointInSlideShow()
        {
            try
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
            catch
            {
                return false;
            }
        }
    }
}
