using Ink_Canvas.WorkflowAutomation.Abstractions;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 退出白板模式触发器设置
    /// </summary>
    public class WhiteboardExitSettings
    {
    }

    /// <summary>
    /// 退出白板/黑板模式时触发的触发器。
    /// 通过订阅 SystemEventMonitor.InternalStateChanged 事件驱动，检测 currentMode 从 1 变为 0。
    /// </summary>
    [TriggerInfo("inkcanvas.whiteboardexit", "退出白板模式", "Drawing")]
    public class WhiteboardExitTrigger : TriggerBase<WhiteboardExitSettings>
    {
        private bool _wasInWhiteboardMode = false;

        public override void Loaded()
        {
            _wasInWhiteboardMode = WhiteboardEnterTrigger.IsInWhiteboardMode();

            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.InternalStateChanged += OnInternalStateChanged;
            }
        }

        public override void UnLoaded()
        {
            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.InternalStateChanged -= OnInternalStateChanged;
            }
        }

        private void OnInternalStateChanged(object sender, System.EventArgs e)
        {
            CheckWhiteboardMode();
        }

        private void CheckWhiteboardMode()
        {
            bool isInWhiteboardMode = WhiteboardEnterTrigger.IsInWhiteboardMode();

            if (!isInWhiteboardMode && _wasInWhiteboardMode)
            {
                _wasInWhiteboardMode = false;
                Trigger();
            }
            else if (isInWhiteboardMode)
            {
                _wasInWhiteboardMode = true;
            }
        }
    }
}
