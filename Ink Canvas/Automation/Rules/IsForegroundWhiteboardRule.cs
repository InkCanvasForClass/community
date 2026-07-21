using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 前台窗口是 ICC-CE 白板规则设置
    /// </summary>
    public class IsForegroundWhiteboardRuleSettings
    {
    }

    /// <summary>
    /// 判断前台窗口是否为 ICC-CE 白板的规则。
    /// 当 ICC-CE 处于白板模式（currentMode == 1）且主窗口可见时返回真。
    /// 此规则不依赖 GetForegroundWindow()，因此在无焦点模式（WS_EX_NOACTIVATE）下也能正常工作。
    /// </summary>
    public static class IsForegroundWhiteboardRule
    {
        public const string RuleId = "inkcanvas.isforegroundwhiteboard";

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "前台窗口是 ICC-CE 白板", "Whiteboard")
            {
                SettingsType = typeof(IsForegroundWhiteboardRuleSettings)
            };

            info.Handle = (settings) =>
            {
                try
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher != null && dispatcher.CheckAccess())
                    {
                        var mw = Application.Current.MainWindow as MainWindow;
                        if (mw == null) return false;
                        if (mw.currentMode != 1) return false;
                        return mw.IsVisible;
                    }

                    return dispatcher?.Invoke(() =>
                    {
                        var mw = Application.Current.MainWindow as MainWindow;
                        if (mw == null) return false;
                        if (mw.currentMode != 1) return false;
                        return mw.IsVisible;
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
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && dispatcher.CheckAccess())
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    if (mw.currentMode != 1) return false;
                    return mw.IsVisible;
                }

                return dispatcher?.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw == null) return false;
                    if (mw.currentMode != 1) return false;
                    return mw.IsVisible;
                }) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
