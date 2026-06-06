using System;

namespace Ink_Canvas.WorkflowAutomation.Abstractions
{
    /// <summary>
    /// 自动化触发器信息特性。
    /// 对齐 ClassIsland 的 TriggerInfo Attribute，标注在触发器类上。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TriggerInfoAttribute : Attribute
    {
        /// <summary>
        /// 触发器 ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 触发器名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 触发器图标（字符串标识）
        /// </summary>
        public string IconKind { get; }

        /// <summary>
        /// 触发器类型（由框架自动设置）
        /// </summary>
        public Type TriggerType { get; internal set; }

        /// <summary>
        /// 设置界面类型
        /// </summary>
        public Type SettingsControlType { get; set; }

        /// <summary>
        /// 设置数据类型
        /// </summary>
        public Type SettingsType { get; internal set; }

        public TriggerInfoAttribute(string id, string name, string iconKind = "ClockOutline")
        {
            Id = id;
            Name = name;
            IconKind = iconKind;
        }
    }
}
