using Ink_Canvas.WorkflowAutomation.Models;
using System.Diagnostics;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 杀进程行动的设置
    /// </summary>
    public class KillProcessActionSettings
    {
        /// <summary>
        /// 要杀死的进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get; set; } = "";
    }

    /// <summary>
    /// 杀进程的行动注册。
    /// </summary>
    public static class KillProcessAction
    {
        public const string ActionId = "inkcanvas.killprocess";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "结束进程", "CloseCircleOutline")
            {
                SettingsType = typeof(KillProcessActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                var s = settings as KillProcessActionSettings;
                if (s == null || string.IsNullOrEmpty(s.ProcessName)) return;

                try
                {
                    foreach (var process in Process.GetProcessesByName(s.ProcessName))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                    }
                }
                catch { }
            };

            // 杀进程不支持恢复
            info.RevertHandle = null;

            return info;
        }
    }
}
