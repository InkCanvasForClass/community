using Ink_Canvas.WorkflowAutomation.Models;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);

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
                    if (handle == IntPtr.Zero) return false;

                    var sb = new StringBuilder(512);
                    int length = GetWindowTextW(handle, sb, sb.Capacity);
                    if (length <= 0) return false;

                    string windowTitle = sb.ToString(0, length);

                    if (s.IgnoreCase)
                    {
                        return windowTitle.IndexOf(s.TitleContains, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else
                    {
                        return windowTitle.Contains(s.TitleContains);
                    }
                }
                catch (Win32Exception)
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
