using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 批注模式规则设置
    /// </summary>
    public class IsAnnotationModeRuleSettings
    {
    }

    /// <summary>
    /// 判断浮动工具栏是否处于批注模式的规则。
    /// </summary>
    public static class IsAnnotationModeRule
    {
        public const string RuleId = "inkcanvas.isannotationmode";

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "批注模式", "PenTool")
            {
                SettingsType = typeof(IsAnnotationModeRuleSettings)
            };

            info.Handle = (settings) =>
            {
                try
                {
                    // Automation 引擎可能在 UI 线程上同步评估规则；
                    // 在 UI 线程上直接读取 MainWindow 状态，避免 Dispatcher.Invoke 自锁。
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
            };

            return info;
        }

        public static bool Evaluate(object settings)
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
