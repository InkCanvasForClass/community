namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// PPT放映中规则设置
    /// </summary>
    public class IsPptSlideshowRuleSettings
    {
    }

    /// <summary>
    /// 判断当前是否处于PPT放映模式的规则。
    /// </summary>
    public static class IsPptSlideshowRule
    {
        public const string RuleId = "inkcanvas.ispptslideshow";

        public static bool Evaluate(object settings)
        {
            try
            {
                // 检测 PowerPoint 放映窗口
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("POWERPNT"))
                {
                    if (proc.MainWindowTitle.Contains("PowerPoint Slide Show") ||
                        proc.MainWindowTitle.Contains("幻灯片放映"))
                    {
                        return true;
                    }
                }
                // 也检测 WPS 演示放映
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("wpp"))
                {
                    if (proc.MainWindowTitle.Length > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
