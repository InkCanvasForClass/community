using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// PPT放映退出触发器设置
    /// </summary>
    public class PptSlideShowExitSettings
    {
    }

    /// <summary>
    /// 退出PPT放映模式时触发的触发器。
    /// 通过 SystemEventMonitor 的进程和窗口事件驱动，无需独立轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.pptslideshowexit", "退出PPT放映", "Presentation")]
    public class PptSlideShowExitTrigger : TriggerBase<PptSlideShowExitSettings>
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
