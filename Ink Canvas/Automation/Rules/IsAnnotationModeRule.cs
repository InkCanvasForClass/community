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

        public static bool Evaluate(object settings)
        {
            try
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    return mw.inkCanvas?.EditingMode == System.Windows.Controls.InkCanvasEditingMode.Ink;
                });
            }
            catch
            {
                return false;
            }
        }
    }
}
