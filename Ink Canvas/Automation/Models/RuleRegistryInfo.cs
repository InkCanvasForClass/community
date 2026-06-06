using System;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个规则的注册信息。
    /// </summary>
    public class RuleRegistryInfo
    {
        /// <summary>
        /// 规则 ID。
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// 规则显示图标。
        /// </summary>
        public string IconKind { get; internal set; }

        /// <summary>
        /// 规则显示名称。
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// 设置控件类型。
        /// </summary>
        public Type SettingsControlType { get; internal set; }

        /// <summary>
        /// 设置类型。
        /// </summary>
        public Type SettingsType { get; internal set; }

        public delegate bool HandleDelegate(object settings);

        /// <summary>
        /// 规则判断处理程序
        /// </summary>
        public HandleDelegate Handle;

        public RuleRegistryInfo(string id, string name = "", string iconKind = "CogOutline")
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            IconKind = iconKind;
        }
    }
}
