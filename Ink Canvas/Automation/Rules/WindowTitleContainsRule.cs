using System;
using System.Diagnostics;
using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.Rules
{
    /// <summary>
    /// 窗口标题包含规则设置
    /// </summary>
    public class WindowTitleContainsRuleSettings
    {
        /// <summary>
        /// 要匹配的窗口标题文本
        /// </summary>
        public string TitleContains { get; set; } = "";

        /// <summary>
        /// 是否忽略大小写
        /// </summary>
        public bool IgnoreCase { get; set; } = true;
    }

    /// <summary>
    /// 判断前台窗口标题是否包含指定文本的规则。
    /// </summary>
    public static class WindowTitleContainsRule
    {
        public const string RuleId = "inkcanvas.windowtitlecontains";

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        public static RuleRegistryInfo Register()
        {
            var info = new RuleRegistryInfo(RuleId, "窗口标题包含", "Window")
            {
                SettingsType = typeof(WindowTitleContainsRuleSettings)
            };

            info.Handle = (settings) =>
            {
                var s = settings as WindowTitleContainsRuleSettings;
                if (s == null || string.IsNullOrEmpty(s.TitleContains)) return false;

                try
                {
                    var handle = GetForegroundWindow();
                    var sb = new System.Text.StringBuilder(256);
                    GetWindowText(handle, sb, sb.Capacity);
                    string windowTitle = sb.ToString();

                    if (s.IgnoreCase)
                    {
                        return windowTitle.IndexOf(s.TitleContains, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else
                    {
                        return windowTitle.Contains(s.TitleContains);
                    }
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
