using Ink_Canvas.WorkflowAutomation.Models;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 进程运行规则设置
    /// </summary>
    public class ProcessRunningRuleSettings
    {
        /// <summary>
        /// 要检测的进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get; set; } = "";
    }

    /// <summary>
    /// 判断指定进程是否正在运行的规则。
    /// </summary>
    public static class ProcessRunningRule
    {
        public const string RuleId = "inkcanvas.processrunning";

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "进程正在运行", "ApplicationOutline")
            {
                SettingsType = typeof(ProcessRunningRuleSettings)
            };

            info.Handle = (settings) =>
            {
                var s = settings as ProcessRunningRuleSettings;
                if (s == null || string.IsNullOrEmpty(s.ProcessName)) return false;

                try
                {
                    return Process.GetProcessesByName(s.ProcessName).Length > 0;
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
            var s = settings as ProcessRunningRuleSettings;
            if (s == null || string.IsNullOrEmpty(s.ProcessName)) return false;
            try
            {
                return Process.GetProcessesByName(s.ProcessName).Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
