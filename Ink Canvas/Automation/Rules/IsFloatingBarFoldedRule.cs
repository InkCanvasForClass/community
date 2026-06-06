using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 浮动栏已折叠规则设置
    /// </summary>
    public class IsFloatingBarFoldedRuleSettings
    {
    }

    /// <summary>
    /// 判断浮动工具栏是否已折叠的规则。
    /// </summary>
    public static class IsFloatingBarFoldedRule
    {
        public const string RuleId = "inkcanvas.isfloatingbarfolded";

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "工具栏已折叠", "ArrowCollapse")
            {
                SettingsType = typeof(IsFloatingBarFoldedRuleSettings)
            };

            info.Handle = (settings) =>
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mw = Application.Current.MainWindow as MainWindow;
                        if (mw == null) return false;
                        return mw.isFloatingBarFolded;
                    });
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
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    return mw.isFloatingBarFolded;
                });
            }
            catch
            {
                return false;
            }
        }
    }
}
