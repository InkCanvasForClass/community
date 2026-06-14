using Ink_Canvas.WorkflowAutomation.Abstractions;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 进入白板模式触发器设置
    /// </summary>
    public class WhiteboardEnterSettings
    {
    }

    /// <summary>
    /// 进入白板/黑板模式时触发的触发器。
    /// 通过订阅 SystemEventMonitor.InternalStateChanged 事件驱动，检测 currentMode 从 0 变为 1。
    /// </summary>
    [TriggerInfo("inkcanvas.whiteboardenter", "进入白板模式", "Drawing")]
    public class WhiteboardEnterTrigger : TriggerBase<WhiteboardEnterSettings>
    {
        private bool _wasInWhiteboardMode = false;

        public override void Loaded()
        {
            _wasInWhiteboardMode = IsInWhiteboardMode();

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
            bool isInWhiteboardMode = IsInWhiteboardMode();

            if (isInWhiteboardMode && !_wasInWhiteboardMode)
            {
                _wasInWhiteboardMode = true;
                Trigger();
            }
            else if (!isInWhiteboardMode)
            {
                _wasInWhiteboardMode = false;
            }
        }

        internal static bool IsInWhiteboardMode()
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    return mw.currentMode == 1;
                });
            }
            catch
            {
                return false;
            }
        }
    }
}
