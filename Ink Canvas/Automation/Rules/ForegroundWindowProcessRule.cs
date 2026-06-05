using Ink_Canvas.WorkflowAutomation.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 前台窗口进程名规则设置
    /// </summary>
    public class ForegroundWindowProcessRuleSettings
    {
        /// <summary>
        /// 要匹配的进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get; set; } = "";
    }

    /// <summary>
    /// 判断前台窗口的进程名是否匹配指定名称的规则。
    /// </summary>
    public static class ForegroundWindowProcessRule
    {
        public const string RuleId = "inkcanvas.foregroundwindowprocess";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "前台窗口进程名", "Window")
            {
                SettingsType = typeof(ForegroundWindowProcessRuleSettings)
            };

            info.Handle = (settings) =>
            {
                var s = settings as ForegroundWindowProcessRuleSettings;
                if (s == null || string.IsNullOrEmpty(s.ProcessName)) return false;

                try
                {
                    var handle = GetForegroundWindow();
                    if (handle == IntPtr.Zero) return false;

                    uint pid;
                    GetWindowThreadProcessId(handle, out pid);
                    if (pid == 0) return false;

                    var process = Process.GetProcessById((int)pid);
                    return string.Equals(process.ProcessName, s.ProcessName, StringComparison.OrdinalIgnoreCase);
                }
                catch (Win32Exception)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch
                {
                    return false;
                }
            };

            return info;
        }
    }
}
