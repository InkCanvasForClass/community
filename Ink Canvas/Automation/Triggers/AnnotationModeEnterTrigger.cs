using Ink_Canvas.Helpers;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using System;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 批注模式进入触发器设置
    /// </summary>
    public class AnnotationModeEnterSettings
    {
    }

    /// <summary>
    /// 浮动工具栏进入批注模式时触发的触发器。
    /// 通过订阅 inkCanvas.EditingModeChanged 事件驱动，无需轮询。
    /// </summary>
    [TriggerInfo("inkcanvas.annotationenter", "进入批注模式", "PenTool")]
    public class AnnotationModeEnterTrigger : TriggerBase<AnnotationModeEnterSettings>
    {
        private bool _wasInAnnotationMode = false;

        private static int _diagExceptionCount;

        private static void LogCallbackException(string what, Exception ex)
        {
            var n = System.Threading.Interlocked.Increment(ref _diagExceptionCount);
            if (n == 1 || n % 100 == 0)
                LogHelper.WriteLogToFile($"[Automation] {what} 异常（累计 {n} 次）: {ex.Message}", LogHelper.LogType.Info);
        }

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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Automation] 批注模式进入触发器订阅 inkCanvas.EditingModeChanged 失败: {ex.Message}", LogHelper.LogType.Info);
            }
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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Automation] 批注模式进入触发器退订 inkCanvas.EditingModeChanged 失败: {ex.Message}", LogHelper.LogType.Info);
            }
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

            if (isInAnnotationMode && !_wasInAnnotationMode)
            {
                _wasInAnnotationMode = true;
                Trigger();
            }
            else if (!isInAnnotationMode)
            {
                _wasInAnnotationMode = false;
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
            catch (Exception ex)
            {
                LogCallbackException("读取批注模式状态 IsInAnnotationMode", ex);
                return false;
            }
        }
    }
}
