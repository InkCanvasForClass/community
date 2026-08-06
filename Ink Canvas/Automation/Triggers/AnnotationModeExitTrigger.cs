using Ink_Canvas.WorkflowAutomation.Abstractions;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 批注模式退出触发器设置
    /// </summary>
    public class AnnotationModeExitSettings
    {
    }

    /// <summary>
    /// 浮动工具栏退出批注模式时触发的触发器。
    /// 通过订阅 inkCanvas.EditingModeChanged 事件驱动，无需轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.annotationexit", "退出批注模式", "PenTool")]
    public class AnnotationModeExitTrigger : TriggerBase<AnnotationModeExitSettings>
    {
        private bool _wasInAnnotationMode = false;

        public override void Loaded()
        {
            _wasInAnnotationMode = IsInAnnotationMode();

            // 订阅 inkCanvas 事件
            TrySubscribeInkCanvas();

            // 同时订阅 Monitor 的内部状态变化事件作为兜底
            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.InternalStateChanged += OnInternalStateChanged;
            }
        }

        public override void UnLoaded()
        {
            TryUnsubscribeInkCanvas();

            var monitor = AutomationBootstrap.Monitor;
            if (monitor != null)
            {
                monitor.InternalStateChanged -= OnInternalStateChanged;
            }
        }

        private void TrySubscribeInkCanvas()
        {
            try
            {
                var mw = System.Windows.Application.Current?.MainWindow as MainWindow;
                if (mw?.inkCanvas != null)
                {
                    mw.inkCanvas.EditingModeChanged += OnEditingModeChanged;
                }
            }
            catch { }
        }

        private void TryUnsubscribeInkCanvas()
        {
            try
            {
                var mw = System.Windows.Application.Current?.MainWindow as MainWindow;
                if (mw?.inkCanvas != null)
                {
                    mw.inkCanvas.EditingModeChanged -= OnEditingModeChanged;
                }
            }
            catch { }
        }

        private void OnEditingModeChanged(object sender, System.EventArgs e)
        {
            CheckAnnotationMode();
        }

        private void OnInternalStateChanged(object sender, System.EventArgs e)
        {
            CheckAnnotationMode();
        }

        private void CheckAnnotationMode()
        {
            bool isInAnnotationMode = IsInAnnotationMode();

            if (!isInAnnotationMode && _wasInAnnotationMode)
            {
                _wasInAnnotationMode = false;
                Trigger();
            }
            else if (isInAnnotationMode)
            {
                _wasInAnnotationMode = true;
            }
        }

        private static bool IsInAnnotationMode()
        {
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && dispatcher.CheckAccess())
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    return mw.IsAnnotationModeActive();
                }

                return dispatcher?.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    return mw.IsAnnotationModeActive();
                }) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
