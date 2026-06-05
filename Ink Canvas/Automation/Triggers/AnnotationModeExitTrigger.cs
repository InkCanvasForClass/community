using Ink_Canvas.WorkflowAutomation.Abstractions;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 批注模式退出触发器设置
    /// </summary>
    public class AnnotationModeExitSettings
    {
        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 200;
    }

    /// <summary>
    /// 浮动工具栏退出批注模式时触发的触发器。
    /// </summary>
    public class AnnotationModeExitTrigger : TriggerBase<AnnotationModeExitSettings>
    {
        private Timer? _timer;
        private bool _wasInAnnotationMode = false;

        public override void Loaded()
        {
            _timer = new Timer(Settings.CheckIntervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            _wasInAnnotationMode = IsInAnnotationMode();
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
            catch
            {
                // 忽略检测错误
            }
        }

        private static bool IsInAnnotationMode()
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                if (mw == null) return false;
                return mw.inkCanvas?.EditingMode == System.Windows.Controls.InkCanvasEditingMode.Ink;
            });
        }
    }
}
