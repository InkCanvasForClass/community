using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using System;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// PPT 放映检测触发器的设置
    /// </summary>
    public class PPTSlideShowSettings
    {
    }

    /// <summary>
    /// 当 PPT 进入放映模式时触发的触发器。
    /// 通过 SystemEventMonitor 的进程和窗口事件驱动，无需独立轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.pptslideshow", "PPT放映检测", "Presentation")]
    public class PPTSlideShowTrigger : TriggerBase<PPTSlideShowSettings>
    {
        private bool _wasInSlideShow = false;

        private static int _diagExceptionCount;

        private static void LogCallbackException(string what, Exception ex)
        {
            var n = System.Threading.Interlocked.Increment(ref _diagExceptionCount);
            if (n == 1 || n % 100 == 0)
                LogHelper.WriteLogToFile($"[Automation] {what} 异常（累计 {n} 次）: {ex.Message}", LogHelper.LogType.Info);
        }

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
                    catch (Exception ex)
                    {
                        LogCallbackException("读取 POWERPNT 进程主窗口标题判断是否处于放映", ex);
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                LogCallbackException("枚举 POWERPNT 进程判断是否处于放映", ex);
                return false;
            }
        }
    }
}
