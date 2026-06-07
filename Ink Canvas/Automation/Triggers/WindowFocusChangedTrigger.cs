using Ink_Canvas.WorkflowAutomation.Abstractions;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 前台窗口变化触发器设置
    /// </summary>
    public class WindowFocusChangedSettings
    {
    }

    /// <summary>
    /// 前台窗口焦点变化时触发的触发器。
    /// 通过 SystemEventMonitor 的 WinEvent 钩子驱动，无需轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.windowfocuschanged", "前台窗口变化", "Window")]
    public class WindowFocusChangedTrigger : TriggerBase<WindowFocusChangedSettings>
    {
        public override void Loaded()
        {
            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.ForegroundWindowChanged += OnForegroundWindowChanged;
            }
        }

        public override void UnLoaded()
        {
            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.ForegroundWindowChanged -= OnForegroundWindowChanged;
            }
        }

        private void OnForegroundWindowChanged(object sender, System.EventArgs e)
        {
            Trigger();
        }
    }
}
